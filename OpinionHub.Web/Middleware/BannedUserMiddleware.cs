using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Middleware;

/// <summary>
/// Гарантирует, что забаненный (LockoutEnd в будущем) или soft-удалённый (IsDeleted) пользователь
/// не может выполнять действия на сайте, даже если у него ещё валидная auth-cookie.
///
/// Стандартный Identity ставит LockoutEnd только при логине — уже залогиненная сессия продолжает работать
/// до истечения cookie. Этот middleware на каждом запросе авторизованного пользователя проверяет состояние
/// в БД и при необходимости делает SignOut + редирект.
///
/// Кеш на 15 секунд: для большинства подряд идущих запросов проверка идёт без обращения к БД.
/// </summary>
public class BannedUserMiddleware
{
    private const int CacheSeconds = 15;

    private readonly RequestDelegate _next;

    public BannedUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        IMemoryCache cache)
    {
        if (ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userId = users.GetUserId(context.User);
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var status = await cache.GetOrCreateAsync($"banstatus:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheSeconds);
            var u = await users.FindByIdAsync(userId);
            if (u is null) return new UserStatus(MissingOrDeleted: true, Banned: false, UserName: null);
            if (u.IsDeleted) return new UserStatus(MissingOrDeleted: true, Banned: false, UserName: null);

            var banned = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;
            return new UserStatus(MissingOrDeleted: false, Banned: banned, UserName: u.UserName);
        });

        if (status is null)
        {
            await _next(context);
            return;
        }

        if (status.MissingOrDeleted)
        {
            await signIn.SignOutAsync();
            cache.Remove($"banstatus:{userId}");
            context.Response.Redirect("/");
            return;
        }

        if (status.Banned)
        {
            await signIn.SignOutAsync();
            cache.Remove($"banstatus:{userId}");
            var name = Uri.EscapeDataString(status.UserName ?? string.Empty);
            context.Response.Redirect($"/Identity/Account/Lockout?userName={name}");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Пропускаем запросы, для которых редирект бесполезен или приведёт к петле:
    /// SignalR-хабы (WebSocket-апгрейд не редиректится), сама страница Lockout и Logout-эндпоинт.
    /// </summary>
    private static bool ShouldSkip(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value;
        if (string.IsNullOrEmpty(path)) return true;
        if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/Identity/Account/Lockout", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private sealed record UserStatus(bool MissingOrDeleted, bool Banned, string? UserName);
}
