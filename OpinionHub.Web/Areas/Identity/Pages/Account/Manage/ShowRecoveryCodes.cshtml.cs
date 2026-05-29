using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ShowRecoveryCodesModel : PageModel
{
    public string[] RecoveryCodes { get; private set; } = Array.Empty<string>();

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
    {
        if (TempData["RecoveryCodes"] is string[] codes && codes.Length > 0)
        {
            RecoveryCodes = codes;
            TempData.Keep("RecoveryCodes");
            return Page();
        }

        return RedirectToPage("./TwoFactorAuthentication");
    }
}
