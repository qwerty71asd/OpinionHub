using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class EnableAuthenticatorModel : PageModel
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UrlEncoder _urlEncoder;

    public EnableAuthenticatorModel(UserManager<ApplicationUser> userManager, UrlEncoder urlEncoder)
    {
        _userManager = userManager;
        _urlEncoder = urlEncoder;
    }

    public string SharedKey { get; private set; } = string.Empty;
    public string AuthenticatorUri { get; private set; } = string.Empty;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "Код должен быть 6–7 цифр")]
        [DataType(DataType.Text)]
        [Display(Name = "Код из приложения")]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await LoadSharedKeyAndUriAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndUriAsync(user);
            return Page();
        }

        var verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            ModelState.AddModelError(nameof(Input.Code), "Неверный код. Проверьте время на устройстве и повторите.");
            await LoadSharedKeyAndUriAsync(user);
            return Page();
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        StatusMessage = "Authenticator настроен. 2FA включена.";

        if (await _userManager.CountRecoveryCodesAsync(user) == 0)
        {
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["RecoveryCodes"] = recoveryCodes?.ToArray();
            return RedirectToPage("./ShowRecoveryCodes");
        }

        return RedirectToPage("./TwoFactorAuthentication");
    }

    private async Task LoadSharedKeyAndUriAsync(ApplicationUser user)
    {
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey ?? string.Empty);

        var email = await _userManager.GetEmailAsync(user) ?? string.Empty;
        AuthenticatorUri = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            _urlEncoder.Encode("OpinionHub"),
            _urlEncoder.Encode(email),
            unformattedKey);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new System.Text.StringBuilder();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
            result.Append(unformattedKey.AsSpan(currentPosition));

        return result.ToString().ToLowerInvariant();
    }
}
