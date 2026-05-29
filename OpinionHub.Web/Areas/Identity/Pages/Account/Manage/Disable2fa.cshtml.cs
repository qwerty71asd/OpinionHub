using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class Disable2faModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public Disable2faModel(UserManager<ApplicationUser> userManager)
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

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        StatusMessage = "2FA отключена.";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
