using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

public class LockoutModel : PageModel
{
    [TempData]
    public string? LockoutLogin { get; set; }

    [TempData]
    public string? LockoutUntilUtc { get; set; }

    public string? LockoutUntilText { get; private set; }

    public void OnGet()
    {
        if (string.IsNullOrWhiteSpace(LockoutUntilUtc))
            return;

        // В TempData кладём ISO-8601 строку (UTC), например: 2026-03-20T15:30:00.0000000Z
        if (!DateTimeOffset.TryParse(LockoutUntilUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var utc))
            return;

        // Если блокировка выставлена «бессрочно» — обычно это DateTimeOffset.MaxValue.
        if (utc.Year >= 9999)
        {
            LockoutUntilText = "бессрочно";
            return;
        }

        var local = utc.ToLocalTime();
        LockoutUntilText = $"до {local:dd.MM.yyyy HH:mm} (местное время)";
    }
}