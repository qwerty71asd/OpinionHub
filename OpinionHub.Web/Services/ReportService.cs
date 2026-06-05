using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ReportResult> CreateAsync(string reporterId, ReportTargetType targetType, string targetKey, ReportReason reason, string? comment)
    {
        if (string.IsNullOrWhiteSpace(reporterId))
            return ReportResult.Fail("Не удалось определить отправителя жалобы.");
        if (string.IsNullOrWhiteSpace(targetKey))
            return ReportResult.Fail("Цель жалобы не указана.");

        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment?.Length > 500)
            return ReportResult.Fail("Комментарий слишком длинный (максимум 500 символов).");

        // Проверка цели: существует и принадлежит не репортеру.
        switch (targetType)
        {
            case ReportTargetType.Poll:
                if (!Guid.TryParse(targetKey, out var pollId))
                    return ReportResult.Fail("Неверный идентификатор опроса.");
                var poll = await _db.Polls.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pollId);
                if (poll is null || poll.IsDeleted)
                    return ReportResult.Fail("Опрос не найден.");
                if (poll.AuthorId == reporterId)
                    return ReportResult.Fail("Нельзя пожаловаться на свой опрос.");
                break;

            case ReportTargetType.Comment:
                if (!Guid.TryParse(targetKey, out var commentId))
                    return ReportResult.Fail("Неверный идентификатор комментария.");
                var commentEntity = await _db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId);
                if (commentEntity is null || commentEntity.IsDeleted)
                    return ReportResult.Fail("Комментарий не найден.");
                if (commentEntity.AuthorId == reporterId)
                    return ReportResult.Fail("Нельзя пожаловаться на свой комментарий.");
                break;

            case ReportTargetType.User:
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetKey);
                if (user is null || user.IsDeleted)
                    return ReportResult.Fail("Пользователь не найден.");
                if (user.Id == reporterId)
                    return ReportResult.Fail("Нельзя пожаловаться на свой профиль.");
                break;

            default:
                return ReportResult.Fail("Неизвестный тип цели.");
        }

        // Дубль: одна Pending-жалоба от одного юзера на одну цель.
        var duplicate = await _db.Reports.AnyAsync(r =>
            r.ReporterId == reporterId &&
            r.TargetType == targetType &&
            r.TargetKey == targetKey &&
            r.Status == ReportStatus.Pending);
        if (duplicate)
            return ReportResult.Fail("Вы уже отправили жалобу на эту цель. Она ожидает рассмотрения.");

        var report = new Report
        {
            ReporterId = reporterId,
            TargetType = targetType,
            TargetKey = targetKey,
            Reason = reason,
            Comment = comment,
            Status = ReportStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();
        return ReportResult.Ok(report.Id);
    }

    public async Task<bool> MarkReviewedAsync(Guid reportId, string adminId, string? adminNote)
    {
        var r = await _db.Reports.FirstOrDefaultAsync(x => x.Id == reportId);
        if (r is null) return false;

        adminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
        if (adminNote?.Length > 1000) adminNote = adminNote.Substring(0, 1000);

        r.Status = ReportStatus.Reviewed;
        r.ReviewedAtUtc = DateTime.UtcNow;
        r.ReviewedById = adminId;
        r.AdminNote = adminNote;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> ResolveByTargetAsync(ReportTargetType targetType, string targetKey, string adminId, string note)
    {
        if (string.IsNullOrWhiteSpace(targetKey)) return 0;

        note = string.IsNullOrWhiteSpace(note) ? "Цель удалена администратором" : note.Trim();
        if (note.Length > 1000) note = note.Substring(0, 1000);

        var now = DateTime.UtcNow;
        return await _db.Reports
            .Where(r => r.TargetType == targetType && r.TargetKey == targetKey && r.Status == ReportStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReportStatus.Reviewed)
                .SetProperty(r => r.ReviewedAtUtc, now)
                .SetProperty(r => r.ReviewedById, adminId)
                .SetProperty(r => r.AdminNote, note));
    }
}
