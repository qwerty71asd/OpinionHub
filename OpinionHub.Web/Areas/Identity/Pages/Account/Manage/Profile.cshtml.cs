using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ProfileModel : PageModel
{
    private const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 МБ — согласовано с пользователем

    // Разрешённые MIME — JPG/PNG/WebP. Без GIF, чтобы избежать анимаций по требованию.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorage;

    public ProfileModel(UserManager<ApplicationUser> userManager, IFileStorageService fileStorage)
    {
        _userManager = userManager;
        _fileStorage = fileStorage;
    }

    public string UserName { get; private set; } = string.Empty;
    public string? CurrentAvatarPath { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [StringLength(500, ErrorMessage = "Описание не должно быть длиннее 500 символов")]
        [Display(Name = "О себе")]
        public string? Bio { get; set; }

        [Display(Name = "Аватарка")]
        public IFormFile? AvatarFile { get; set; }

        /// <summary>Удалить текущую аватарку (сбросить на fallback).</summary>
        public bool RemoveAvatar { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        UserName = user.UserName ?? string.Empty;
        CurrentAvatarPath = user.AvatarPath;
        Input.Bio = user.Bio;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        UserName = user.UserName ?? string.Empty;
        CurrentAvatarPath = user.AvatarPath;

        if (!ModelState.IsValid) return Page();

        var hasFile = Input.AvatarFile is { Length: > 0 };
        if (hasFile)
        {
            if (Input.AvatarFile!.Length > MaxAvatarBytes)
            {
                ModelState.AddModelError(nameof(Input.AvatarFile), "Аватарка должна быть не больше 2 МБ.");
                return Page();
            }

            if (string.IsNullOrEmpty(Input.AvatarFile.ContentType)
                || !AllowedContentTypes.Contains(Input.AvatarFile.ContentType))
            {
                ModelState.AddModelError(nameof(Input.AvatarFile), "Допустимы только JPG, PNG или WebP.");
                return Page();
            }
        }

        // Bio: trim, пустую строку трактуем как null (чтобы профиль не показывал пустой <p>).
        var bio = string.IsNullOrWhiteSpace(Input.Bio) ? null : Input.Bio.Trim();
        user.Bio = bio;

        // Сначала обрабатываем новый файл — на случай отказа сохранения старая ссылка остаётся живой.
        if (hasFile)
        {
            var newPath = await _fileStorage.SaveFileAsync(Input.AvatarFile!, "avatars");
            var oldPath = user.AvatarPath;
            user.AvatarPath = newPath;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _fileStorage.DeleteFile(newPath); // откатить только что сохранённый файл
                foreach (var err in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return Page();
            }

            _fileStorage.DeleteFile(oldPath);
            StatusMessage = "Профиль обновлён.";
            return RedirectToPage();
        }

        if (Input.RemoveAvatar && !string.IsNullOrEmpty(user.AvatarPath))
        {
            var oldPath = user.AvatarPath;
            user.AvatarPath = null;
            var r = await _userManager.UpdateAsync(user);
            if (!r.Succeeded)
            {
                foreach (var err in r.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return Page();
            }
            _fileStorage.DeleteFile(oldPath);
            StatusMessage = "Профиль обновлён.";
            return RedirectToPage();
        }

        var bioOnly = await _userManager.UpdateAsync(user);
        if (!bioOnly.Succeeded)
        {
            foreach (var err in bioOnly.Errors)
                ModelState.AddModelError(string.Empty, err.Description);
            return Page();
        }
        StatusMessage = "Профиль обновлён.";
        return RedirectToPage();
    }
}
