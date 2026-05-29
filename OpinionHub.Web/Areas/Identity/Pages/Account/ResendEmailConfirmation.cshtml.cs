using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResendEmailConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public ResendEmailConfirmationModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Не раскрываем наличие email — всегда возвращаемся на ConfirmEmailCode или показываем общий статус.
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
        {
            StatusMessage = "Если такая почта существует и не подтверждена — мы отправили код.";
            return RedirectToPage("./ResendEmailConfirmation");
        }

        var code = EmailConfirmationCode.Generate6Digits();
        var expiresUtc = DateTime.UtcNow.AddMinutes(15);
        await EmailConfirmationCode.SetAsync(_userManager, user, code, expiresUtc);
        var expiresLocal = expiresUtc.ToLocalTime();
        await _emailSender.SendConfirmationLinkAsync(
            user,
            user.Email!,
            $"Ваш код подтверждения OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут).");

        return RedirectToPage("./ConfirmEmailCode", new { userId = user.Id, returnUrl = Url.Content("~/") });
    }
}
