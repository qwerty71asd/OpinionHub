using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class TwoFactorAuthenticationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public TwoFactorAuthenticationModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public bool HasAuthenticator { get; private set; }
    public bool Is2faEnabled { get; private set; }
    public int RecoveryCodesLeft { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        HasAuthenticator = !string.IsNullOrWhiteSpace(await _userManager.GetAuthenticatorKeyAsync(user));
        Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

        return Page();
    }
}
