using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly UserManager<ApplicationUser> _users;

    public ReportsController(IReportService reports, UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _users = users;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReportCreateVm vm)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        // Требуем подтверждённый email — тот же порог, что и для голосования.
        if (!await _users.IsEmailConfirmedAsync(user))
        {
            TempData["ReportError"] = "Подтвердите почту, чтобы подавать жалобы.";
            return SafeRedirect(vm.ReturnUrl);
        }

        if (!ModelState.IsValid)
        {
            TempData["ReportError"] = "Не удалось отправить жалобу: проверьте форму.";
            return SafeRedirect(vm.ReturnUrl);
        }

        var result = await _reports.CreateAsync(user.Id, vm.TargetType, vm.TargetKey, vm.Reason, vm.Comment);
        if (!result.Success)
        {
            TempData["ReportError"] = result.ErrorMessage ?? "Не удалось отправить жалобу.";
            return SafeRedirect(vm.ReturnUrl);
        }

        TempData["ReportMessage"] = "Спасибо, жалоба отправлена модераторам.";
        return SafeRedirect(vm.ReturnUrl);
    }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}
