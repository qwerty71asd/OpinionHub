using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

public class ConfirmEmailCodeModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public ConfirmEmailCodeModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? UserId { get; private set; }

    public string ReturnUrl { get; private set; } = "~/";

    public string EmailMasked { get; private set; } = "";

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Код должен состоять из 6 цифр")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен состоять из 6 цифр")]
        public string Code { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync(string? userId, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToPage("./Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return RedirectToPage("./Login");

        if (user.EmailConfirmed)
            return Redirect(returnUrl ?? Url.Content("~/"));

        UserId = user.Id;
        ReturnUrl = returnUrl ?? Url.Content("~/");
        EmailMasked = MaskEmail(user.Email);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? userId, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToPage("./Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return RedirectToPage("./Login");

        UserId = user.Id;
        ReturnUrl = returnUrl;
        EmailMasked = MaskEmail(user.Email);

        if (!ModelState.IsValid)
            return Page();

        var (ok, error) = await EmailConfirmationCode.ValidateAsync(_userManager, user, Input.Code);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Неверный код.");
            return Page();
        }

        user.EmailConfirmed = true;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await EmailConfirmationCode.ClearAsync(_userManager, user);

        // Логиним пользователя после подтверждения
        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl);
    }

    public async Task<IActionResult> OnPostResendAsync(string? userId, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToPage("./Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return RedirectToPage("./Login");

        if (user.EmailConfirmed)
            return Redirect(returnUrl);

        var code = EmailConfirmationCode.Generate6Digits();
        var expiresUtc = DateTime.UtcNow.AddMinutes(15);
        await EmailConfirmationCode.SetAsync(_userManager, user, code, expiresUtc);
        var expiresLocal = expiresUtc.ToLocalTime();
        await _emailSender.SendConfirmationLinkAsync(
            user,
            user.Email!,
            $"Ваш код подтверждения OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут)." );

        StatusMessage = "Мы отправили новый код на вашу почту.";
        return RedirectToPage("./ConfirmEmailCode", new { userId = user.Id, returnUrl });
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(email не указан)";

        var at = email.IndexOf('@');
        if (at <= 1) return email;

        var name = email[..at];
        var domain = email[(at + 1)..];

        var maskedName = name.Length switch
        {
            0 => "",
            1 => "*",
            2 => $"{name[0]}*",
            _ => $"{name[0]}***{name[^1]}"
        };

        return $"{maskedName}@{domain}";
    }
}
