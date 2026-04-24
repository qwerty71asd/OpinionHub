using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Background;
using OpinionHub.Web.Data;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

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

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, RuValidationAttributeAdapterProvider>();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddScoped<IPollService, PollService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddHostedService<PollLifecycleHostedService>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, ConfigurableEmailSender>();

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<PollHub>("/hubs/polls");

app.Run();
