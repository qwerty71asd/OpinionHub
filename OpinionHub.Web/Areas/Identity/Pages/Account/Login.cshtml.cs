using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;

    // Передаём данные о блокировке на страницу /Identity/Account/Lockout
    [TempData]
    public string? LockoutLogin { get; set; }

    [TempData]
    public string? LockoutUntilUtc { get; set; }

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Это обязательное поле")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
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

        // Пытаемся войти по имени пользователя.
        ApplicationUser? resolvedUser = null;
        var result = await _signInManager.PasswordSignInAsync(Input.Login, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.IsLockedOut)
        {
            resolvedUser = await _userManager.FindByNameAsync(Input.Login);
        }

        // Если ввели email — пробуем найти пользователя по email и войти по UserName.
        if (!result.Succeeded && Input.Login.Contains('@'))
        {
            var user = await _userManager.FindByEmailAsync(Input.Login);
            if (user is not null)
            {
                resolvedUser = user;
                result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            }
        }

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? Url.Content("~/"));
        }

        // Если аккаунт заблокирован (админом или по lockout-политике) — показываем понятную страницу.
        // Если аккаунт заблокирован — передаем данные и уходим на страницу Lockout
        if (result.IsLockedOut)
        {
            // Мы уже пытались найти пользователя выше (resolvedUser). 
            // Если он все еще null, пробуем найти его по Login (это может быть и ник, и почта).
            resolvedUser ??= await _userManager.FindByNameAsync(Input.Login)
                             ?? await _userManager.FindByEmailAsync(Input.Login);

            if (resolvedUser?.LockoutEnd != null)
            {
                // Прямое присваивание свойствам [TempData] — самый надежный способ.
                // Превращаем дату в строку СРАЗУ здесь, чтобы избежать InvalidCastException.
                LockoutUntilUtc = resolvedUser.LockoutEnd.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                LockoutLogin = resolvedUser.UserName;
            }

            return RedirectToPage("./Lockout");
        }
        // Если email не подтверждён — не даём войти и перекидываем на страницу ввода кода.
        if (result.IsNotAllowed)
        {
            resolvedUser ??= await _userManager.FindByNameAsync(Input.Login);
            if (resolvedUser is null && Input.Login.Contains('@'))
                resolvedUser = await _userManager.FindByEmailAsync(Input.Login);

            if (resolvedUser is not null && !await _userManager.IsEmailConfirmedAsync(resolvedUser))
            {
                var code = EmailConfirmationCode.Generate6Digits();
                var expiresUtc = DateTime.UtcNow.AddMinutes(15);
                await EmailConfirmationCode.SetAsync(_userManager, resolvedUser, code, expiresUtc);
                var expiresLocal = expiresUtc.ToLocalTime();
                await _emailSender.SendConfirmationLinkAsync(
                    resolvedUser,
                    resolvedUser.Email!,
            $"Ваш код подтверждения OpinionHub: {code}. Действует до {expiresLocal:HH:mm} (15 минут).");

                return RedirectToPage("./ConfirmEmailCode", new { userId = resolvedUser.Id, returnUrl = returnUrl ?? Url.Content("~/") });
            }

            ModelState.AddModelError(string.Empty, "Вход сейчас невозможен. Подтвердите почту.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Неверный логин или пароль.");
        return Page();
    }
}
