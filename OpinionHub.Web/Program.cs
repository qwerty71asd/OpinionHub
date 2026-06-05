using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Background;
using OpinionHub.Web.Data;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Middleware;
using OpinionHub.Web.Models;
using System.Net;
using OpinionHub.Web.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure());

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// Identity UI (страницы /Identity/Account/Login, /Register и т.д.) появляется только с AddDefaultIdentity.
// Включаем подтверждение почты через ввод кода (см. /Identity/Account/ConfirmEmailCode).
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedAccount = true;

        // Пароль: для учебного проекта делаем простые правила (только длина).
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // И email, и UserName должны быть уникальными (по UserName мы ищем пользователей при создании закрытых опросов).
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<RuIdentityErrorDescriber>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<OpinionHub.Web.Filters.DomainExceptionFilter>();
});
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, RuValidationAttributeAdapterProvider>();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPollService, PollService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IUserDeletionService, UserDeletionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAppealService, AppealService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IPartialViewRenderer, PartialViewRenderer>();
builder.Services.AddHostedService<PollLifecycleHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, ConfigurableEmailSender>();
var botToken = builder.Configuration["TelegramBot:Token"];
if (!string.IsNullOrEmpty(botToken))
{
    builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));
    builder.Services.AddTransient<ITelegramNotificationService, TelegramNotificationService>();
    builder.Services.AddSingleton<ITelegramLinkTokenService, TelegramLinkTokenService>();
    builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();

    // Режим работы бота: "polling" (по умолчанию, работает локально без публичного URL)
    // или "webhook" (используется TelegramWebhookController, polling не запускается).
    var mode = builder.Configuration["TelegramBot:Mode"] ?? "polling";
    if (mode.Equals("polling", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddHostedService<TelegramBotPollingHostedService>();
    }
}
builder.Services.AddHttpClient<AiAnalyticsService>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();

        var proxyAddress = config["Proxy:Address"];
        var proxyUser = config["Proxy:Username"];
        var proxyPass = config["Proxy:Password"];

        // 1. ЗАЩИТА ОТ КРАША: Если прокси не указан, работаем напрямую
        if (string.IsNullOrEmpty(proxyAddress))
        {
            return new HttpClientHandler { UseProxy = false };
        }

        // 2. Создаем объект прокси только если адрес существует
        var proxy = new WebProxy
        {
            Address = new Uri(proxyAddress),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };

        // 3. Добавляем авторизацию, только если логин/пароль реально заданы
        if (!string.IsNullOrEmpty(proxyUser) && !string.IsNullOrEmpty(proxyPass))
        {
            proxy.Credentials = new NetworkCredential(proxyUser, proxyPass);
        }

        return new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true
        };
    });
var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Logger, app.Configuration, app.Environment);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
// Между Authentication и Authorization — чтобы User.Identity уже был восстановлен из cookie,
// но мы могли разлогинить забаненного раньше, чем доступ дойдёт до контроллеров.
app.UseMiddleware<BannedUserMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<PollHub>("/hubs/polls");

app.Run();
