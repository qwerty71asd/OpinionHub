using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpinionHub.Web.Models;
using System.Net.Sockets;
using System.Linq;

namespace OpinionHub.Web.Data;

/// <summary>
/// Инициализация БД при старте приложения.
///
/// Почему это нужно:
/// - В PostgreSQL EF Core не может создать саму базу данных, если она не существует.
/// - В репозитории могут отсутствовать EF migrations (MVP/учебный кейс) — тогда Migrate() ничего не делает.
///
/// Поведение:
/// 1) (опционально) пытается создать базу, если её нет
/// 2) если migrations существуют — накатывает их, иначе делает EnsureCreated()
/// 3) сидит роли
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, IConfiguration configuration, IHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Берём строку подключения максимально надёжно.
        var connectionString = db.Database.GetConnectionString() ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Не найдена строка подключения DefaultConnection.");

        // По умолчанию в Development пытаемся создать базу, если её нет.
        // В production это лучше контролировать явно через конфиг.
        var autoCreateDb = configuration.GetValue<bool?>("AutoCreateDatabase") ?? environment.IsDevelopment();
        if (autoCreateDb)
        {
            try
            {
                await EnsureDatabaseExistsAsync(connectionString, logger);
            }
            catch (Exception ex)
            {
                // Если прав на CREATE DATABASE нет — даём понятный лог и продолжаем пробовать подключиться.
                logger.LogWarning(ex, "Не удалось автоматически создать базу данных. " +
                                     "Если база ещё не создана — создайте её вручную и проверьте права/строку подключения.");
            }
        }

        try
        {
                await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var hint = BuildDbHint(ex, connectionString);
            logger.LogError(ex, "Ошибка при инициализации БД (Migrate/EnsureCreated). {Hint}", hint);
            throw new InvalidOperationException($"Ошибка инициализации БД. {hint}", ex);
        }

        // Seed ролей
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Participant", "Author", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed admin-пользователя (удобно для первого входа в админку).
        // Управляется через appsettings: SeedAdmin:Enabled/UserName/Email/Password.
        await SeedAdminUserAsync(scope.ServiceProvider, logger, configuration, environment);
    }

    private static async Task SeedAdminUserAsync(IServiceProvider sp, ILogger logger, IConfiguration configuration, IHostEnvironment environment)
    {
        // По умолчанию включаем сидинг только в Development.
        var enabled = configuration.GetValue<bool?>("SeedAdmin:Enabled") ?? environment.IsDevelopment();
        if (!enabled)
            return;

        var userName = configuration["SeedAdmin:UserName"] ?? "admin";
        var email = configuration["SeedAdmin:Email"] ?? "admin@local";
        var password = configuration["SeedAdmin:Password"] ?? "Admin12345";

        try
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var create = await userManager.CreateAsync(user, password);
                if (!create.Succeeded)
                {
                    var errors = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    logger.LogWarning("SeedAdmin: не удалось создать пользователя '{UserName}'. {Errors}", userName, errors);
                    return;
                }

                logger.LogInformation("SeedAdmin: создан пользователь '{UserName}'.", userName);
            }

            // На всякий случай выдаём роль Admin.
            if (!await userManager.IsInRoleAsync(user, "Admin"))
            {
                var addRole = await userManager.AddToRoleAsync(user, "Admin");
                if (!addRole.Succeeded)
                {
                    var errors = string.Join("; ", addRole.Errors.Select(e => $"{e.Code}: {e.Description}"));
                    logger.LogWarning("SeedAdmin: не удалось выдать роль Admin пользователю '{UserName}'. {Errors}", userName, errors);
                }
                else
                {
                    logger.LogInformation("SeedAdmin: пользователю '{UserName}' выдана роль Admin.", userName);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SeedAdmin: ошибка при создании/настройке admin-пользователя.");
        }
    }
    private static string BuildDbHint(Exception ex, string connectionString)
    {
        // Достаём Host/Port из строки подключения, чтобы подсказка была максимально конкретной.
        string endpointHint;
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            endpointHint = $"{csb.Host}:{csb.Port}";
        }
        catch
        {
            endpointHint = "host:port";
        }

        // Вытаскиваем наиболее частые причины из SQLSTATE.
        var pg = ex as PostgresException
                 ?? ex.InnerException as PostgresException
                 ?? ex.GetBaseException() as PostgresException;

        if (pg is null)
        {
            // Когда PostgreSQL недоступен вообще (например, Connection refused), PostgresException не будет.
            // Тогда ориентируемся на сетевые ошибки.
            var baseEx = ex.GetBaseException();

            if (baseEx is SocketException se)
            {
                return se.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => $"Не удалось подключиться к {endpointHint}. Соединение отклонено. Запустите PostgreSQL и проверьте Host/Port в строке подключения.",
                    SocketError.HostNotFound => $"Не удалось подключиться к {endpointHint}. Хост не найден. Проверьте параметр Host в строке подключения.",
                    SocketError.TimedOut => $"Не удалось подключиться к {endpointHint}. Таймаут подключения. Проверьте доступность PostgreSQL и открытость порта.",
                    _ => $"Не удалось подключиться к {endpointHint}. Сетевая ошибка: {se.SocketErrorCode}. Проверьте, что PostgreSQL запущен и параметры Host/Port корректны."
                };
            }

            if (baseEx is NpgsqlException)
            {
                return $"Не удалось подключиться к {endpointHint}. Проверьте, что PostgreSQL запущен и параметры Host/Port в строке подключения корректны.";
            }

            return $"Ошибка подключения к БД. Проверьте, что PostgreSQL запущен и строка подключения корректна (Host/Port/Database/Username/Password).";
        }

        return pg.SqlState switch
        {
            "3D000" => "База данных из строки подключения не существует. Создайте её вручную или включите AutoCreateDatabase=true (и проверьте права на CREATE DATABASE).",
            "28P01" => "Ошибка аутентификации (неверный логин/пароль). Проверьте Username/Password в строке подключения.",
            "28000" => "Ошибка аутентификации/доступа. Проверьте пользователя и настройки pg_hba.conf.",
            "42501" => "Не хватает прав на создание таблиц/схемы. Выдайте права пользователю БД (или используйте другого пользователя).",
            _ => $"PostgreSQL error {pg.SqlState}: {pg.MessageText}"
        };
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var dbName = csb.Database;
        if (string.IsNullOrWhiteSpace(dbName))
            return;

        // Создать базу можно, только подключившись к системной БД.
        // Обычно это "postgres".
        var adminCsb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };

        await using var conn = new NpgsqlConnection(adminCsb.ConnectionString);
        await conn.OpenAsync();

        await using (var check = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @db", conn))
        {
            check.Parameters.AddWithValue("db", dbName);
            var exists = await check.ExecuteScalarAsync();
            if (exists is not null)
                return;
        }

        logger.LogWarning("Database '{DbName}' not found. Attempting to create it...", dbName);

        // Важно: имя БД экранируем как идентификатор.
        var safeDbName = dbName.Replace("\"", "\"\"");
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{safeDbName}\"", conn);
        await create.ExecuteNonQueryAsync();
        logger.LogInformation("Database '{DbName}' created successfully.", dbName);
    }
}
    