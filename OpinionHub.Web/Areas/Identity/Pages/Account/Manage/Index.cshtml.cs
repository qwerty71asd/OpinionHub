using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsEmailConfirmed { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        UserName = user.UserName ?? string.Empty;
        Email = user.Email ?? string.Empty;
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        return Page();
    }
}
