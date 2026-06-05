using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class AppealService : IAppealService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public AppealService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<AppealResult> CreateAsync(string userName, string password, string message)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return AppealResult.Fail("Введите логин и пароль.");

        message = (message ?? string.Empty).Trim();
        if (message.Length < 50)
            return AppealResult.Fail("Опишите ситуацию подробнее (минимум 50 символов).");
        if (message.Length > 2000)
            return AppealResult.Fail("Сообщение слишком длинное (максимум 2000 символов).");

        var user = await _users.FindByNameAsync(userName.Trim());
        if (user is null || user.IsDeleted)
            return AppealResult.Fail("Неверный логин или пароль.");

        // Проверяем пароль без логина в систему (BannedUserMiddleware выкинул бы нас сразу).
        var passwordOk = await _users.CheckPasswordAsync(user, password);
        if (!passwordOk)
            return AppealResult.Fail("Неверный логин или пароль.");

        var now = DateTimeOffset.UtcNow;
        if (!user.LockoutEnd.HasValue || user.LockoutEnd.Value <= now)
            return AppealResult.Fail("Ваш аккаунт не заблокирован — апелляция не требуется.");

        var hasPending = await _db.Appeals.AnyAsync(a => a.UserId == user.Id && a.Status == AppealStatus.Pending);
        if (hasPending)
            return AppealResult.Fail("Ваша предыдущая апелляция ещё рассматривается.");

        var appeal = new Appeal
        {
            UserId = user.Id,
            BanReasonSnapshot = user.BanReason,
            BanCreatedAtSnapshot = user.BannedAt,
            Message = message,
            Status = AppealStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Appeals.Add(appeal);
        await _db.SaveChangesAsync();
        return AppealResult.Ok(appeal.Id);
    }

    public async Task<bool> ApproveAsync(Guid appealId, string adminId, string? response)
    {
        var appeal = await _db.Appeals.FirstOrDefaultAsync(a => a.Id == appealId);
        if (appeal is null) return false;
        if (appeal.Status != AppealStatus.Pending) return false;

        var user = await _users.FindByIdAsync(appeal.UserId);
        if (user is null) return false;

        // Тот же путь, что и в AdminController.Unlock.
        user.LockoutEnabled = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.BanReason = null;
        user.BannedAt = null;
        user.BannedById = null;
        await _users.UpdateAsync(user);

        appeal.Status = AppealStatus.Approved;
        appeal.ReviewedAtUtc = DateTime.UtcNow;
        appeal.ReviewedById = adminId;
        appeal.AdminResponse = string.IsNullOrWhiteSpace(response) ? null : response.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(Guid appealId, string adminId, string? response)
    {
        var appeal = await _db.Appeals.FirstOrDefaultAsync(a => a.Id == appealId);
        if (appeal is null) return false;
        if (appeal.Status != AppealStatus.Pending) return false;

        appeal.Status = AppealStatus.Rejected;
        appeal.ReviewedAtUtc = DateTime.UtcNow;
        appeal.ReviewedById = adminId;
        appeal.AdminResponse = string.IsNullOrWhiteSpace(response) ? null : response.Trim();
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> HasPendingAsync(string userId) =>
        _db.Appeals.AnyAsync(a => a.UserId == userId && a.Status == AppealStatus.Pending);

    public async Task<IReadOnlyList<Appeal>> GetUserHistoryAsync(string userId, Guid? excludeAppealId = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<Appeal>();

        var query = _db.Appeals.AsNoTracking().Where(a => a.UserId == userId);
        if (excludeAppealId.HasValue)
            query = query.Where(a => a.Id != excludeAppealId.Value);

        return await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync();
    }
}
