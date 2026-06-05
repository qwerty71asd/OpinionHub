using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// Подача апелляций на бан. Намеренно НЕ требует входа: забаненный пользователь логиниться
/// не может, поэтому форма принимает userName+password и проверяет credentials
/// без создания cookie через UserManager.CheckPasswordAsync.
/// </summary>
[AllowAnonymous]
public class AppealsController : Controller
{
    private readonly IAppealService _appeals;
    private readonly UserManager<ApplicationUser> _users;

    public AppealsController(IAppealService appeals, UserManager<ApplicationUser> users)
    {
        _appeals = appeals;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? userName)
    {
        // Pre-fill логина из query — пользователь пришёл сюда со страницы Lockout.
        var vm = new AppealCreateVm { UserName = userName?.Trim() ?? string.Empty };

        // Если userName известен и действительно забанен — покажем форму. Иначе тоже покажем,
        // но сервис при сабмите сам отобьёт «аккаунт не заблокирован».
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var user = await _users.FindByNameAsync(userName.Trim());
            if (user is not null && !user.IsDeleted && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                ViewBag.BanReason = user.BanReason;
                ViewBag.HasPending = await _appeals.HasPendingAsync(user.Id);
            }
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppealCreateVm vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _appeals.CreateAsync(vm.UserName, vm.Password, vm.Message);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Не удалось отправить апелляцию.");
            return View(vm);
        }

        // Возвращаемся на Lockout с сообщением.
        TempData["AppealMessage"] = "Апелляция отправлена. Администратор рассмотрит её в ближайшее время.";
        return RedirectToPage("/Account/Lockout", new { area = "Identity", userName = vm.UserName });
    }
}
