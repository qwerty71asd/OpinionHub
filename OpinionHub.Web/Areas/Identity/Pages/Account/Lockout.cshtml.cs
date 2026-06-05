using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using System.Globalization;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

public class LockoutModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAppealService _appeals;

    public LockoutModel(UserManager<ApplicationUser> users, IAppealService appeals)
    {
        _users = users;
        _appeals = appeals;
    }

    [TempData]
    public string? LockoutLogin { get; set; }

    [TempData]
    public string? LockoutUntilUtc { get; set; }

    public string? LockoutUntilText { get; private set; }

    public string? BanReason { get; private set; }

    public string? BannedAtLocalText { get; private set; }

    /// <summary>UserName забаненного — для ссылки «Подать апелляцию». null, если не знаем юзера.</summary>
    public string? AppealForUserName { get; private set; }

    /// <summary>У юзера уже есть Pending-апелляция — показываем «уже отправлена» вместо ссылки.</summary>
    public bool HasPendingAppeal { get; private set; }

    [TempData]
    public string? AppealMessage { get; set; }

    public async Task OnGet(string? userName)
    {
        // Сценарий 1: TempData (классический Identity flow — забанили при попытке логина).
        if (!string.IsNullOrWhiteSpace(LockoutUntilUtc) &&
            DateTimeOffset.TryParse(LockoutUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc))
        {
            LockoutUntilText = FormatLockoutEnd(utc);
        }

        // Сценарий 2: middleware выкинул активную сессию забаненного, передал ?userName=.
        // Если по нему находим живого забаненного юзера — обогащаем страницу деталями из БД.
        if (string.IsNullOrWhiteSpace(userName))
            return;

        var user = await _users.FindByNameAsync(userName);
        if (user is null || user.IsDeleted)
            return;

        if (!user.LockoutEnd.HasValue || user.LockoutEnd.Value <= DateTimeOffset.UtcNow)
            return;

        if (string.IsNullOrWhiteSpace(LockoutLogin))
            LockoutLogin = user.UserName;

        LockoutUntilText ??= FormatLockoutEnd(user.LockoutEnd.Value);
        BanReason = user.BanReason;

        if (user.BannedAt.HasValue)
            BannedAtLocalText = user.BannedAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        AppealForUserName = user.UserName;
        HasPendingAppeal = await _appeals.HasPendingAsync(user.Id);
    }

    private static string FormatLockoutEnd(DateTimeOffset utc)
    {
        // LockoutEnd «бессрочно» — обычно DateTimeOffset.MaxValue (Year == 9999).
        if (utc.Year >= 9999)
            return "бессрочно";

        var local = utc.ToLocalTime();
        return $"до {local:dd.MM.yyyy HH:mm} (местное время)";
    }
}
