using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class PrivacyModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PrivacyModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public bool ShowLikedPolls { get; set; }
        public bool ShowVotedPolls { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        Input.ShowLikedPolls = user.ShowLikedPolls;
        Input.ShowVotedPolls = user.ShowVotedPolls;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        user.ShowLikedPolls = Input.ShowLikedPolls;
        user.ShowVotedPolls = Input.ShowVotedPolls;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return Page();
        }

        StatusMessage = "Настройки приватности сохранены.";
        return RedirectToPage();
    }
}
