using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Models;
using OpinionHub.Web.Models.Admin;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// Простая админка для управления пользователями и ролями.
/// Доступ только для роли Admin.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;

    public AdminController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var query = _users.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var qq = q.Trim();
            query = query.Where(u =>
                (u.UserName != null && EF.Functions.ILike(u.UserName, $"%{qq}%")) ||
                (u.Email != null && EF.Functions.ILike(u.Email, $"%{qq}%")));
        }

        var userList = await query.OrderBy(u => u.UserName).ToListAsync();
        var rows = new List<AdminUserRowVm>();

        foreach (var u in userList)
        {
            var roles = await _users.GetRolesAsync(u);
            var lockoutEnd = u.LockoutEnd;
            var locked = lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow;
            rows.Add(new AdminUserRowVm
            {
                Id = u.Id,
                UserName = u.UserName ?? "(no username)",
                Email = u.Email,
                Roles = roles.ToList(),
                IsAdmin = roles.Contains("Admin"),
                LockoutEndUtc = lockoutEnd,
                IsLockedOut = locked
            });
        }

        return View(new AdminIndexVm { Users = rows, Query = q });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminUserCreateVm
        {
            AvailableRoles = await _roles.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateVm vm)
    {
        vm.AvailableRoles = await _roles.Roles
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync();

        if (!ModelState.IsValid)
            return View(vm);

        var user = new ApplicationUser
        {
            UserName = vm.UserName.Trim(),
            Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
            EmailConfirmed = vm.EmailConfirmed,
            PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim(),
            LockoutEnabled = true
        };

        var createRes = await _users.CreateAsync(user, vm.Password);
        if (!createRes.Succeeded)
        {
            foreach (var e in createRes.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(vm);
        }

        if (vm.SelectedRoles?.Count > 0)
        {
            foreach (var r in vm.SelectedRoles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await _roles.RoleExistsAsync(r))
                    await _users.AddToRoleAsync(user, r);
            }
        }

        TempData["AdminMessage"] = $"Пользователь '{user.UserName}' создан.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var userRoles = await _users.GetRolesAsync(user);
        var allRoles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();

        var vm = new AdminUserEditVm
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEndUtc = user.LockoutEnd,
            AvailableRoles = allRoles,
            SelectedRoles = userRoles.ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditVm vm)
    {
        var user = await _users.FindByIdAsync(vm.Id);
        if (user is null) return NotFound();

        vm.AvailableRoles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        if (!ModelState.IsValid)
            return View(vm);

        user.UserName = vm.UserName.Trim();
        user.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();
        user.EmailConfirmed = vm.EmailConfirmed;

        user.LockoutEnabled = true;
        user.LockoutEnd = vm.LockoutEndUtc;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
        {
            foreach (var e in upd.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(vm);
        }

        var current = await _users.GetRolesAsync(user);
        var desired = (vm.SelectedRoles ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var toRemove = current.Where(r => !desired.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        var toAdd = desired.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();

        if (toRemove.Count > 0)
            await _users.RemoveFromRolesAsync(user, toRemove);

        foreach (var r in toAdd)
        {
            if (await _roles.RoleExistsAsync(r))
                await _users.AddToRoleAsync(user, r);
        }

        TempData["AdminMessage"] = "Пользователь обновлён.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new AdminResetPasswordVm { Id = user.Id, UserName = user.UserName ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordVm vm)
    {
        var user = await _users.FindByIdAsync(vm.Id);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            vm.UserName = user.UserName ?? string.Empty;
            return View(vm);
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var res = await _users.ResetPasswordAsync(user, token, vm.NewPassword);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
            vm.UserName = user.UserName ?? string.Empty;
            return View(vm);
        }

        TempData["AdminMessage"] = "Пароль изменён.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Lockout(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new AdminLockoutVm { Id = user.Id, UserName = user.UserName ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lockout(AdminLockoutVm vm)
    {
        var user = await _users.FindByIdAsync(vm.Id);
        if (user is null) return NotFound();

        if (user.Id == _users.GetUserId(User))
            ModelState.AddModelError(string.Empty, "Нельзя заблокировать самого себя.");

        if (!ModelState.IsValid)
        {
            vm.UserName = user.UserName ?? string.Empty;
            return View(vm);
        }

        user.LockoutEnabled = true;
        if (vm.Permanent)
        {
            user.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            var minutes = vm.Minutes <= 0 ? 60 : vm.Minutes;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(minutes);
        }

        await _users.UpdateAsync(user);
        TempData["AdminMessage"] = "Пользователь заблокирован.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        user.LockoutEnabled = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await _users.UpdateAsync(user);

        TempData["AdminMessage"] = "Блокировка снята.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == _users.GetUserId(User))
        {
            TempData["AdminError"] = "Нельзя удалить самого себя.";
            return RedirectToAction(nameof(Index));
        }

        var res = await _users.DeleteAsync(user);
        if (!res.Succeeded)
        {
            TempData["AdminError"] = string.Join("\n", res.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["AdminMessage"] = $"Пользователь '{user.UserName}' удалён.";
        return RedirectToAction(nameof(Index));
    }
}
