using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class EmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public EmailModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    public string CurrentEmail { get; private set; } = string.Empty;
    public bool IsEmailConfirmed { get; private set; }
    public string? PendingNewEmail { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Новый email")]
        public string NewEmail { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostChangeAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        await LoadAsync(user);

        if (!ModelState.IsValid)
            return Page();

        if (string.Equals(Input.NewEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Новый email совпадает с текущим.");
            return Page();
        }

        var existing = await _userManager.FindByEmailAsync(Input.NewEmail);
        if (existing is not null && existing.Id != user.Id)
        {
            ModelState.AddModelError(string.Empty, "Этот email уже занят.");
            return Page();
        }

        await SendCodeAsync(user, Input.NewEmail);

        StatusMessage = $"Мы отправили 6-значный код на {Input.NewEmail}. Введите его ниже.";
        return RedirectToPage("./ConfirmEmailChange");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var pendingEmail = await EmailChangeCode.PeekNewEmailAsync(_userManager, user);
        if (string.IsNullOrWhiteSpace(pendingEmail))
        {
            await LoadAsync(user);
            ModelState.AddModelError(string.Empty, "Нет активного запроса на смену email.");
            return Page();
        }

        await SendCodeAsync(user, pendingEmail);

        StatusMessage = $"Код повторно отправлен на {pendingEmail}.";
        return RedirectToPage("./ConfirmEmailChange");
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        CurrentEmail = user.Email ?? string.Empty;
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        PendingNewEmail = await EmailChangeCode.PeekNewEmailAsync(_userManager, user);
    }

    private async Task SendCodeAsync(ApplicationUser user, string newEmail)
    {
        var code = EmailChangeCode.Generate6Digits();
        var expiresUtc = DateTime.UtcNow.AddMinutes(15);
        await EmailChangeCode.SetAsync(_userManager, user, code, newEmail, expiresUtc);

        var expiresLocal = expiresUtc.ToLocalTime();
        await _emailSender.SendConfirmationLinkAsync(
            user,
            newEmail,
            $"Ваш код подтверждения смены email в OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут).");
    }
}
