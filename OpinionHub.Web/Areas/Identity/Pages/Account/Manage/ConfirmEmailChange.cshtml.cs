using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ConfirmEmailChangeModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public ConfirmEmailChangeModel(
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

    public string PendingEmail { get; private set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Код должен состоять из 6 цифр")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Код должен состоять из 6 цифр")]
        public string Code { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var pending = await EmailChangeCode.PeekNewEmailAsync(_userManager, user);
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToPage("./Email");

        PendingEmail = pending;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var pending = await EmailChangeCode.PeekNewEmailAsync(_userManager, user);
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToPage("./Email");

        PendingEmail = pending;

        if (!ModelState.IsValid)
            return Page();

        var (ok, error, newEmail) = await EmailChangeCode.ValidateAsync(_userManager, user, Input.Code);
        if (!ok || newEmail is null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Неверный код.");
            return Page();
        }

        var setResult = await _userManager.SetEmailAsync(user, newEmail);
        if (!setResult.Succeeded)
        {
            foreach (var e in setResult.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
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

        await EmailChangeCode.ClearAsync(_userManager, user);
        await _signInManager.RefreshSignInAsync(user);

        StatusMessage = $"Email обновлён на {newEmail}.";
        return RedirectToPage("./Email");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var pending = await EmailChangeCode.PeekNewEmailAsync(_userManager, user);
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToPage("./Email");

        var code = EmailChangeCode.Generate6Digits();
        var expiresUtc = DateTime.UtcNow.AddMinutes(15);
        await EmailChangeCode.SetAsync(_userManager, user, code, pending, expiresUtc);

        var expiresLocal = expiresUtc.ToLocalTime();
        await _emailSender.SendConfirmationLinkAsync(
            user,
            pending,
            $"Ваш код подтверждения смены email в OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут).");

        StatusMessage = $"Новый код отправлен на {pending}.";
        return RedirectToPage();
    }
}
