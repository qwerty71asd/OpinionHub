using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ExternalLoginsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ExternalLoginsModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public IList<UserLoginInfo> CurrentLogins { get; set; } = new List<UserLoginInfo>();

    public IList<AuthenticationScheme> OtherLogins { get; set; } = new List<AuthenticationScheme>();

    public bool ShowRemoveButton { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        CurrentLogins = await _userManager.GetLoginsAsync(user);
        OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
            .ToList();

        ShowRemoveButton = await _userManager.HasPasswordAsync(user) || CurrentLogins.Count > 1;
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
        if (!result.Succeeded)
        {
            StatusMessage = "Не удалось отвязать внешний вход.";
            return RedirectToPage();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Внешний вход отвязан.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            provider, redirectUrl, _userManager.GetUserId(User));
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var userId = await _userManager.GetUserIdAsync(user);
        var info = await _signInManager.GetExternalLoginInfoAsync(userId);
        if (info is null)
        {
            StatusMessage = "Не удалось получить данные внешнего входа.";
            return RedirectToPage();
        }

        var result = await _userManager.AddLoginAsync(user, info);
        if (!result.Succeeded)
        {
            StatusMessage = "Не удалось привязать внешний вход.";
            return RedirectToPage();
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        StatusMessage = "Внешний вход привязан.";
        return RedirectToPage();
    }
}
