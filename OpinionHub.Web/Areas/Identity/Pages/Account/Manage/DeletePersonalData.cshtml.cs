using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class DeletePersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<DeletePersonalDataModel> _logger;

    public DeletePersonalDataModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<DeletePersonalDataModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool RequirePassword { get; private set; }

    public class InputModel
    {
        [DataType(DataType.Password)]
        [Display(Name = "Текущий пароль")]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        RequirePassword = await _userManager.HasPasswordAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        RequirePassword = await _userManager.HasPasswordAsync(user);

        if (RequirePassword)
        {
            if (string.IsNullOrEmpty(Input.Password))
            {
                ModelState.AddModelError(nameof(Input.Password), "Введите текущий пароль.");
                return Page();
            }
            if (!await _userManager.CheckPasswordAsync(user, Input.Password))
            {
                ModelState.AddModelError(nameof(Input.Password), "Пароль неверный.");
                return Page();
            }
        }

        var userId = await _userManager.GetUserIdAsync(user);
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await _signInManager.SignOutAsync();
        _logger.LogInformation("Пользователь {UserId} удалил свой аккаунт.", userId);

        TempData["StatusMessage"] = "Ваш аккаунт удалён.";
        return Redirect("~/");
    }
}
