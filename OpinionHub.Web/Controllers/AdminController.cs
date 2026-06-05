using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.Models.Admin;
using OpinionHub.Web.Services;

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
    private readonly IUserDeletionService _userDeletion;
    private readonly ApplicationDbContext _db;

    public AdminController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        IUserDeletionService userDeletion,
        ApplicationDbContext db)
    {
        _users = users;
        _roles = roles;
        _userDeletion = userDeletion;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, AdminUserFilter status = AdminUserFilter.All, AdminUserSort sort = AdminUserSort.UserNameAsc)
    {
        var now = DateTimeOffset.UtcNow;

        // Счётчики считаем по неотфильтрованному множеству — sidebar должен показывать реальные размеры категорий
        // вне зависимости от текущего поиска/выбранной вкладки.
        var allUsersQuery = _users.Users.AsNoTracking();
        var countAll = await allUsersQuery.CountAsync();
        var countDeleted = await allUsersQuery.CountAsync(u => u.IsDeleted);
        var countBanned = await allUsersQuery.CountAsync(u => !u.IsDeleted && u.LockoutEnd != null && u.LockoutEnd > now);
        var countActive = await allUsersQuery.CountAsync(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now));
        var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
        var countAdmin = admins.Count;

        // Основная выборка с фильтром по статусу и поиском.
        var query = _users.Users.AsNoTracking();
        switch (status)
        {
            case AdminUserFilter.Active:
                query = query.Where(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now));
                break;
            case AdminUserFilter.Banned:
                query = query.Where(u => !u.IsDeleted && u.LockoutEnd != null && u.LockoutEnd > now);
                break;
            case AdminUserFilter.Deleted:
                query = query.Where(u => u.IsDeleted);
                break;
            case AdminUserFilter.Admin:
                var adminIds = admins.Select(a => a.Id).ToList();
                query = query.Where(u => adminIds.Contains(u.Id));
                break;
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var qq = q.Trim();
            query = query.Where(u =>
                (u.UserName != null && EF.Functions.ILike(u.UserName, $"%{qq}%")) ||
                (u.Email != null && EF.Functions.ILike(u.Email, $"%{qq}%")));
        }

        query = sort switch
        {
            AdminUserSort.UserNameDesc => query.OrderByDescending(u => u.UserName),
            AdminUserSort.EmailAsc => query.OrderBy(u => u.Email),
            AdminUserSort.EmailDesc => query.OrderByDescending(u => u.Email),
            _ => query.OrderBy(u => u.UserName),
        };

        var userList = await query.ToListAsync();
        var rows = new List<AdminUserRowVm>();
        var adminIdSet = admins.Select(a => a.Id).ToHashSet();

        foreach (var u in userList)
        {
            var roles = await _users.GetRolesAsync(u);
            var lockoutEnd = u.LockoutEnd;
            var locked = lockoutEnd.HasValue && lockoutEnd.Value > now;
            rows.Add(new AdminUserRowVm
            {
                Id = u.Id,
                UserName = u.UserName ?? "(no username)",
                Email = u.Email,
                Roles = roles.ToList(),
                IsAdmin = adminIdSet.Contains(u.Id),
                LockoutEndUtc = lockoutEnd,
                IsLockedOut = locked,
                IsDeleted = u.IsDeleted,
                DeletedAt = u.DeletedAt,
                BanReason = u.BanReason
            });
        }

        return View(new AdminIndexVm
        {
            Users = rows,
            Query = q,
            Status = status,
            Sort = sort,
            CountAll = countAll,
            CountActive = countActive,
            CountBanned = countBanned,
            CountDeleted = countDeleted,
            CountAdmin = countAdmin,
            PendingReports = await GetPendingReportsCountAsync(),
            PendingAppeals = await GetPendingAppealsCountAsync()
        });
    }

    private Task<int> GetPendingReportsCountAsync() => _db.Reports.CountAsync(r => r.Status == ReportStatus.Pending);
    private Task<int> GetPendingAppealsCountAsync() => _db.Appeals.CountAsync(a => a.Status == AppealStatus.Pending);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new AdminUserCreateVm
        {
            AvailableRoles = await GetActiveRoleNamesAsync(),
            SelectedRole = Roles.User
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateVm vm)
    {
        vm.AvailableRoles = await GetActiveRoleNamesAsync();

        if (string.IsNullOrWhiteSpace(vm.SelectedRole) || !vm.AvailableRoles.Contains(vm.SelectedRole))
            ModelState.AddModelError(nameof(vm.SelectedRole), "Выберите одну из доступных ролей.");

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

        await _users.AddToRoleAsync(user, vm.SelectedRole!);

        TempData["AdminMessage"] = $"Пользователь '{user.UserName}' создан.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var userRoles = await _users.GetRolesAsync(user);

        var vm = new AdminUserEditVm
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            LockoutEndUtc = user.LockoutEnd,
            AvailableRoles = await GetActiveRoleNamesAsync(),
            SelectedRole = userRoles.FirstOrDefault() ?? Roles.User
        };

        // Подробная админка: список неанонимных опросов и комментариев пользователя.
        // Анонимные опросы и комментарии под анонимными опросами скрыты — иначе
        // через эту карточку админ деанонимизировал бы автора.
        vm.RecentPolls = await _db.Polls
            .AsNoTracking()
            .Where(p => p.AuthorId == user.Id && !p.IsAnonymousAuthor)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(50)
            .Select(p => new AdminUserPollVm
            {
                Id = p.Id,
                Title = p.Title,
                CreatedAtUtc = p.CreatedAtUtc,
                IsDeleted = p.IsDeleted,
                Status = p.Status.ToString()
            })
            .ToListAsync();

        vm.RecentComments = await _db.Comments
            .AsNoTracking()
            .Where(c => c.AuthorId == user.Id && !c.Poll.IsAnonymousAuthor)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .Select(c => new AdminUserCommentVm
            {
                Id = c.Id,
                Text = c.Text,
                CreatedAtUtc = c.CreatedAtUtc,
                IsDeleted = c.IsDeleted,
                PollId = c.PollId,
                // Defence-in-depth: Comment.Poll помечен как required FK, но при ручной правке БД
                // или race с hard-delete коммента и опроса можем словить NRE — явный null-fallback дешевле.
                PollTitle = c.Poll != null ? c.Poll.Title : "(удалён)"
            })
            .ToListAsync();

        vm.ReportsAgainstCount = await _db.Reports.CountAsync(r =>
            r.TargetType == ReportTargetType.User && r.TargetKey == user.Id);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditVm vm)
    {
        var user = await _users.FindByIdAsync(vm.Id);
        if (user is null) return NotFound();

        vm.AvailableRoles = await GetActiveRoleNamesAsync();

        if (string.IsNullOrWhiteSpace(vm.SelectedRole) || !vm.AvailableRoles.Contains(vm.SelectedRole))
            ModelState.AddModelError(nameof(vm.SelectedRole), "Выберите одну из доступных ролей.");

        // Защита: админ не должен случайно снять с себя Admin и потерять доступ к панели.
        var currentUserId = _users.GetUserId(User);
        if (user.Id == currentUserId && vm.SelectedRole != Roles.Admin)
            ModelState.AddModelError(nameof(vm.SelectedRole), "Нельзя снять с самого себя роль Admin.");

        // Защита: нельзя оставить систему без единого админа — если этот единственный, держим роль Admin.
        if (vm.SelectedRole != Roles.Admin)
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            var stillHasAdmin = admins.Any(a => a.Id != user.Id);
            if (!stillHasAdmin && admins.Any(a => a.Id == user.Id))
                ModelState.AddModelError(nameof(vm.SelectedRole), "Это последний пользователь с ролью Admin — нельзя её снять.");
        }

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

        // Одна роль одновременно: снимаем всё лишнее, оставляем только выбранную.
        var current = await _users.GetRolesAsync(user);
        var toRemove = current.Where(r => !string.Equals(r, vm.SelectedRole, StringComparison.OrdinalIgnoreCase)).ToList();
        if (toRemove.Count > 0)
            await _users.RemoveFromRolesAsync(user, toRemove);

        if (!current.Contains(vm.SelectedRole!, StringComparer.OrdinalIgnoreCase))
            await _users.AddToRoleAsync(user, vm.SelectedRole!);

        TempData["AdminMessage"] = "Пользователь обновлён.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Возвращает имена ролей, которые реально показываем в UI. Совпадает со списком <see cref="Roles.All"/>;
    /// фильтруем по существованию в БД на случай, если миграция legacy-ролей ещё не отработала.
    /// </summary>
    private async Task<List<string>> GetActiveRoleNamesAsync()
    {
        var existing = await _roles.Roles.Select(r => r.Name!).ToListAsync();
        return Roles.All.Where(r => existing.Contains(r)).ToList();
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

        if (user.IsDeleted)
            ModelState.AddModelError(string.Empty, "Этот аккаунт уже удалён.");

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

        user.BanReason = string.IsNullOrWhiteSpace(vm.Reason) ? null : vm.Reason.Trim();
        user.BannedAt = DateTimeOffset.UtcNow;
        user.BannedById = _users.GetUserId(User);

        await _users.UpdateAsync(user);

        // Инвалидируем активные cookie — BannedUserMiddleware на следующем запросе
        // увидит LockoutEnd и разлогинит, но обновление SecurityStamp — страховка,
        // если middleware зачем-то будет отключен.
        await _users.UpdateSecurityStampAsync(user);

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
        user.BanReason = null;
        user.BannedAt = null;
        user.BannedById = null;
        await _users.UpdateAsync(user);

        TempData["AdminMessage"] = "Блокировка снята.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, string? returnUrl, [FromServices] IReportService reports)
    {
        // Защита от self-delete оставлена здесь: специфично именно для админ-панели,
        // в сервисе её нет (на /Manage/DeletePersonalData self-delete как раз разрешён).
        if (id == _users.GetUserId(User))
        {
            TempData["AdminError"] = "Нельзя удалить самого себя.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userDeletion.SoftDeleteAsync(id);
        if (!result.Success)
        {
            TempData["AdminError"] = result.ErrorMessage ?? "Не удалось удалить пользователя.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(nameof(Index));
        }

        var adminId = _users.GetUserId(User)!;
        await reports.ResolveByTargetAsync(ReportTargetType.User, id, adminId, "Пользователь удалён администратором");

        TempData["AdminMessage"] = $"Пользователь '{result.OriginalName}' удалён.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // === Жалобы ===

    [HttpGet]
    public async Task<IActionResult> Reports()
    {
        var allReports = await _db.Reports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(500)
            .ToListAsync();

        var pending = allReports.Where(r => r.Status == ReportStatus.Pending).ToList();
        var reviewed = allReports.Where(r => r.Status == ReportStatus.Reviewed).ToList();

        // Превью целей и репортеров — батчем по типам.
        var pollIds = allReports.Where(r => r.TargetType == ReportTargetType.Poll)
            .Select(r => Guid.TryParse(r.TargetKey, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var commentIds = allReports.Where(r => r.TargetType == ReportTargetType.Comment)
            .Select(r => Guid.TryParse(r.TargetKey, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var userKeyIds = allReports.Where(r => r.TargetType == ReportTargetType.User)
            .Select(r => r.TargetKey).Distinct().ToList();

        var polls = await _db.Polls.AsNoTracking()
            .Where(p => pollIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.AuthorId })
            .ToDictionaryAsync(x => x.Id);

        var comments = await _db.Comments.AsNoTracking()
            .Where(c => commentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Text, c.AuthorId, c.PollId })
            .ToDictionaryAsync(x => x.Id);

        var reporterIds = allReports.Select(r => r.ReporterId).Distinct().ToList();
        var authorIds = polls.Values.Select(p => p.AuthorId)
            .Concat(comments.Values.Select(c => c.AuthorId))
            .Concat(userKeyIds)
            .Concat(allReports.Where(r => r.ReviewedById != null).Select(r => r.ReviewedById!))
            .Distinct().ToList();
        var allUserIds = reporterIds.Concat(authorIds).Distinct().ToList();

        var userNames = await _db.Users.AsNoTracking()
            .Where(u => allUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName ?? "(нет имени)");

        AdminReportRowVm BuildRow(Report r)
        {
            string? targetLabel = null;
            string? targetUrl = null;
            string? targetAuthorId = null;
            string? targetAuthorUserName = null;

            switch (r.TargetType)
            {
                case ReportTargetType.Poll:
                    if (Guid.TryParse(r.TargetKey, out var pid) && polls.TryGetValue(pid, out var pollInfo))
                    {
                        targetLabel = pollInfo.Title;
                        targetUrl = Url.Action("Details", "Polls", new { id = pid });
                        targetAuthorId = pollInfo.AuthorId;
                        userNames.TryGetValue(pollInfo.AuthorId, out targetAuthorUserName);
                    }
                    break;
                case ReportTargetType.Comment:
                    if (Guid.TryParse(r.TargetKey, out var cid) && comments.TryGetValue(cid, out var commentInfo))
                    {
                        var text = commentInfo.Text;
                        targetLabel = text.Length > 80 ? text.Substring(0, 80) + "…" : text;
                        targetUrl = Url.Action("Details", "Polls", new { id = commentInfo.PollId }) + "#comment-" + cid;
                        targetAuthorId = commentInfo.AuthorId;
                        userNames.TryGetValue(commentInfo.AuthorId, out targetAuthorUserName);
                    }
                    break;
                case ReportTargetType.User:
                    targetAuthorId = r.TargetKey;
                    if (userNames.TryGetValue(r.TargetKey, out var un))
                    {
                        targetLabel = un;
                        targetAuthorUserName = un;
                        targetUrl = Url.Action("Index", "Profile", new { userName = un });
                    }
                    break;
            }

            return new AdminReportRowVm
            {
                Id = r.Id,
                TargetType = r.TargetType,
                TargetKey = r.TargetKey,
                TargetLabel = targetLabel ?? "(удалено)",
                TargetUrl = targetUrl,
                TargetAuthorId = targetAuthorId,
                TargetAuthorUserName = targetAuthorUserName,
                ReporterId = r.ReporterId,
                ReporterUserName = userNames.TryGetValue(r.ReporterId, out var rn) ? rn : null,
                Reason = r.Reason,
                Comment = r.Comment,
                Status = r.Status,
                CreatedAtUtc = r.CreatedAtUtc,
                ReviewedAtUtc = r.ReviewedAtUtc,
                ReviewedByUserName = r.ReviewedById != null && userNames.TryGetValue(r.ReviewedById, out var rb) ? rb : null,
                AdminNote = r.AdminNote
            };
        }

        var vm = new AdminReportsVm
        {
            Pending = pending.Select(BuildRow).ToList(),
            Reviewed = reviewed.Select(BuildRow).ToList(),
            PendingReports = await GetPendingReportsCountAsync(),
            PendingAppeals = await GetPendingAppealsCountAsync()
        };

        // Sidebar нужно знать общие счётчики пользователей.
        var now = DateTimeOffset.UtcNow;
        vm.CountAll = await _users.Users.CountAsync();
        vm.CountDeleted = await _users.Users.CountAsync(u => u.IsDeleted);
        vm.CountBanned = await _users.Users.CountAsync(u => !u.IsDeleted && u.LockoutEnd != null && u.LockoutEnd > now);
        vm.CountActive = await _users.Users.CountAsync(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now));
        var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
        vm.CountAdmin = admins.Count;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReportReviewed(Guid id, string? adminNote, [FromServices] IReportService reports)
    {
        var ok = await reports.MarkReviewedAsync(id, _users.GetUserId(User)!, adminNote);
        TempData[ok ? "AdminMessage" : "AdminError"] = ok ? "Жалоба помечена как рассмотренная." : "Не удалось обновить жалобу.";
        return RedirectToAction(nameof(Reports));
    }

    // === Апелляции ===

    [HttpGet]
    public async Task<IActionResult> Appeals(
        AdminAppealStatusFilter status = AdminAppealStatusFilter.All,
        string? user = null,
        AdminAppealSort sort = AdminAppealSort.DateDesc)
    {
        var now = DateTimeOffset.UtcNow;

        var query = _db.Appeals.AsNoTracking().AsQueryable();
        switch (status)
        {
            case AdminAppealStatusFilter.Pending:
                query = query.Where(a => a.Status == AppealStatus.Pending);
                break;
            case AdminAppealStatusFilter.Reviewed:
                query = query.Where(a => a.Status != AppealStatus.Pending);
                break;
            case AdminAppealStatusFilter.Approved:
                query = query.Where(a => a.Status == AppealStatus.Approved);
                break;
            case AdminAppealStatusFilter.Rejected:
                query = query.Where(a => a.Status == AppealStatus.Rejected);
                break;
        }

        var userQuery = (user ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(userQuery))
        {
            // Фильтр по UserName (частичное совпадение, без учёта регистра) или по точному userId.
            var pattern = $"%{userQuery}%";
            query = query.Where(a =>
                a.UserId == userQuery ||
                _db.Users.Any(u => u.Id == a.UserId && EF.Functions.ILike(u.UserName!, pattern)));
        }

        query = sort == AdminAppealSort.DateAsc
            ? query.OrderBy(a => a.CreatedAtUtc)
            : query.OrderByDescending(a => a.CreatedAtUtc);

        var appeals = await query.Take(500).ToListAsync();

        var userIds = appeals.Select(a => a.UserId)
            .Concat(appeals.Where(a => a.ReviewedById != null).Select(a => a.ReviewedById!))
            .Distinct()
            .ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.LockoutEnd, u.BanReason })
            .ToDictionaryAsync(x => x.Id);

        AdminAppealRowVm Build(Appeal a)
        {
            users.TryGetValue(a.UserId, out var u);
            string? reviewedByName = null;
            if (a.ReviewedById is not null && users.TryGetValue(a.ReviewedById, out var rb))
                reviewedByName = rb.UserName;

            var stillBanned = u is not null && u.LockoutEnd.HasValue && u.LockoutEnd.Value > now;

            return new AdminAppealRowVm
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = u?.UserName,
                BanReasonSnapshot = a.BanReasonSnapshot,
                BanCreatedAtSnapshot = a.BanCreatedAtSnapshot,
                Message = a.Message,
                Status = a.Status,
                CreatedAtUtc = a.CreatedAtUtc,
                ReviewedAtUtc = a.ReviewedAtUtc,
                ReviewedByUserName = reviewedByName,
                AdminResponse = a.AdminResponse,
                StillBanned = stillBanned,
                CurrentBanReason = u?.BanReason
            };
        }

        // История по каждому уникальному юзеру в списке — без учёта самой текущей карточки.
        var history = new Dictionary<string, AppealHistorySummary>();
        var uniqueUserIds = appeals.Select(a => a.UserId).Distinct().ToList();
        if (uniqueUserIds.Count > 0)
        {
            var shownIds = appeals.Select(a => a.Id).ToHashSet();
            var allUserAppeals = await _db.Appeals.AsNoTracking()
                .Where(a => uniqueUserIds.Contains(a.UserId))
                .OrderByDescending(a => a.CreatedAtUtc)
                .ToListAsync();

            foreach (var grp in allUserAppeals.GroupBy(a => a.UserId))
            {
                var others = grp.Where(a => !shownIds.Contains(a.Id)).ToList();
                history[grp.Key] = new AppealHistorySummary
                {
                    Total = grp.Count(),
                    Approved = grp.Count(a => a.Status == AppealStatus.Approved),
                    Rejected = grp.Count(a => a.Status == AppealStatus.Rejected),
                    Pending = grp.Count(a => a.Status == AppealStatus.Pending),
                    Items = others.Select(a => new AppealHistoryItem
                    {
                        Id = a.Id,
                        Status = a.Status,
                        CreatedAtUtc = a.CreatedAtUtc,
                        ReviewedAtUtc = a.ReviewedAtUtc,
                        AdminResponse = a.AdminResponse,
                    }).ToList()
                };
            }
        }

        var vm = new AdminAppealsVm
        {
            Pending = appeals.Where(a => a.Status == AppealStatus.Pending).Select(Build).ToList(),
            Reviewed = appeals.Where(a => a.Status != AppealStatus.Pending).Select(Build).ToList(),
            History = history,
            PendingReports = await GetPendingReportsCountAsync(),
            PendingAppeals = await GetPendingAppealsCountAsync(),
            StatusFilter = status,
            UserQuery = userQuery,
            Sort = sort,
        };

        vm.CountAll = await _users.Users.CountAsync();
        vm.CountDeleted = await _users.Users.CountAsync(u => u.IsDeleted);
        vm.CountBanned = await _users.Users.CountAsync(u => !u.IsDeleted && u.LockoutEnd != null && u.LockoutEnd > now);
        vm.CountActive = await _users.Users.CountAsync(u => !u.IsDeleted && (u.LockoutEnd == null || u.LockoutEnd <= now));
        var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
        vm.CountAdmin = admins.Count;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAppeal(Guid id, string? response, [FromServices] IAppealService appeals)
    {
        var ok = await appeals.ApproveAsync(id, _users.GetUserId(User)!, response);
        TempData[ok ? "AdminMessage" : "AdminError"] = ok ? "Апелляция одобрена, пользователь разблокирован." : "Не удалось одобрить апелляцию.";
        return RedirectToAction(nameof(Appeals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAppeal(Guid id, string? response, [FromServices] IAppealService appeals)
    {
        var ok = await appeals.RejectAsync(id, _users.GetUserId(User)!, response);
        TempData[ok ? "AdminMessage" : "AdminError"] = ok ? "Апелляция отклонена." : "Не удалось отклонить апелляцию.";
        return RedirectToAction(nameof(Appeals));
    }
}
