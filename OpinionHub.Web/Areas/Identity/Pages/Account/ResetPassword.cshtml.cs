using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Это обязательное поле")]
        [EmailAddress(ErrorMessage = "Некорректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Это обязательное поле")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Это обязательное поле")]
        [DataType(DataType.Password)]
        [Display(Name = "Повтор пароля")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? code = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(string.Empty, "Не указан код сброса. Попробуйте запросить новую ссылку.");
            return Page();
        }

        Input = new InputModel
        {
            Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
            Email = email ?? string.Empty
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Не раскрываем, что такого email нет.
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
        if (result.Succeeded)
            return RedirectToPage("./ResetPasswordConfirmation");

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}
