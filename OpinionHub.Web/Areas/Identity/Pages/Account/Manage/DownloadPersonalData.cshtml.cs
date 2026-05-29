using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class DownloadPersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DownloadPersonalDataModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public IActionResult OnGet() => RedirectToPage("./PersonalData");

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var personalData = new Dictionary<string, object?>();

        var personalDataProps = typeof(ApplicationUser).GetProperties()
            .Where(p => p.GetCustomAttribute<PersonalDataAttribute>() != null);
        foreach (var p in personalDataProps)
            personalData.Add(p.Name, p.GetValue(user));

        var logins = await _userManager.GetLoginsAsync(user);
        personalData.Add("ExternalLogins",
            logins.Select(l => new { l.LoginProvider, l.ProviderKey }).ToArray());

        var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (!string.IsNullOrEmpty(authenticatorKey))
            personalData.Add("AuthenticatorKey", authenticatorKey);

        Response.Headers["Content-Disposition"] = "attachment; filename=PersonalData.json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            personalData,
            new JsonSerializerOptions { WriteIndented = true });
        return new FileContentResult(bytes, "application/json");
    }
}
