using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    public RegisterModel(
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

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(64, MinimumLength = 3, ErrorMessage = "Имя пользователя должно быть от {2} до {1} символов.")]
        [Display(Name = "Имя пользователя")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Это обязательное поле")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;
        [Required(ErrorMessage = "Это обязательное поле")]
        [DataType(DataType.Password)]
        [Display(Name = "Повтор пароля")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser
        {
            UserName = Input.UserName.Trim(),
            Email = Input.Email.Trim(),
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            // По умолчанию все — участники.
            await _userManager.AddToRoleAsync(user, "Participant");

            // Отправляем код подтверждения на почту
            var code = EmailConfirmationCode.Generate6Digits();
            var expiresUtc = DateTime.UtcNow.AddMinutes(15);
            await EmailConfirmationCode.SetAsync(_userManager, user, code, expiresUtc);
            var expiresLocal = expiresUtc.ToLocalTime();
            await _emailSender.SendConfirmationLinkAsync(
                user,
                user.Email!,
            $"Ваш код подтверждения OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут).");

            // Не логиним пользователя до подтверждения
            return RedirectToPage("./ConfirmEmailCode", new { userId = user.Id, returnUrl = returnUrl ?? Url.Content("~") });
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}
