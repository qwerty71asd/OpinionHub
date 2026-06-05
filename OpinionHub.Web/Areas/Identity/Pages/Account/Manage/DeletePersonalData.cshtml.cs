using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class DeletePersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserDeletionService _userDeletion;
    private readonly ILogger<DeletePersonalDataModel> _logger;

    public DeletePersonalDataModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserDeletionService userDeletion,
        ILogger<DeletePersonalDataModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userDeletion = userDeletion;
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
        var result = await _userDeletion.SoftDeleteAsync(userId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Не удалось удалить аккаунт.");
            return Page();
        }

        // SoftDeleteAsync уже обновил SecurityStamp, но SignOut явно подчищает текущую cookie.
        await _signInManager.SignOutAsync();

        TempData["StatusMessage"] = "Ваш аккаунт удалён. Ваши опросы и комментарии остались как «Удалённый аккаунт».";
        return Redirect("~/");
    }
}
