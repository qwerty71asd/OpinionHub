using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class GenerateRecoveryCodesModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GenerateRecoveryCodesModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToPage("./TwoFactorAuthentication");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToPage("./TwoFactorAuthentication");

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        TempData["RecoveryCodes"] = recoveryCodes?.ToArray();
        StatusMessage = "Сгенерированы новые recovery-коды. Старые больше не работают.";

        return RedirectToPage("./ShowRecoveryCodes");
    }
}
