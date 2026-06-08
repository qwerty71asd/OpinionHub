using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public interface IPollService
{
    Task<Poll> CreateDraftAsync(CreatePollViewModel model, string authorId);
    Task PublishAsync(Guid pollId, string authorId);
    /// <summary>
    /// Единая точка SignalR-рассылки «новый опрос в ленте». При указанном
    /// signalrConnectionId — шлёт всем кроме инициатора (избегаем дубля
    /// карточки во вкладке автора). Иначе — Clients.All.
    /// </summary>
    Task PublishBroadcastAsync(Guid pollId, string? signalrConnectionId);
    Task VoteAsync(Guid pollId, string userId, IReadOnlyCollection<Guid> optionIds);
    Task<Poll?> GetPollDetailsAsync(Guid pollId, string? viewerUserId);
    Task<IReadOnlyCollection<Poll>> GetFeedAsync(string? viewerUserId);
    /// <summary>
    /// Базовый IQueryable&lt;Poll&gt; с применёнными правилами видимости (soft-delete,
    /// expired, Completed/Archived, гость vs. авторизованный, AllowedUsers). Caller
    /// добавляет свои Include/Where/Select. Используется лентой и глобальным поиском —
    /// одна точка истины для «что вообще можно показать этому зрителю».
    /// </summary>
    IQueryable<Poll> BuildVisiblePollsQuery(string? viewerUserId);
    Task<byte[]> ExportCsvAsync(Guid pollId, string userId);
    Task<byte[]> ExportXlsxAsync(Guid pollId, string userId);
    Task<int> CompleteExpiredPollsAsync();
    Task<int> ArchiveOldPollsAsync(int archiveAfterDays);
    Task DeleteAsync(Guid pollId, string userId);
    /// <summary>Soft-delete опроса админом — не требует, чтобы admin был автором. Логируется в AuditLog.</summary>
    Task AdminSoftDeleteAsync(Guid pollId, string adminId);
    Task<List<Poll>> GetUserPollsAsync(string userId);
    /// <summary>
    /// Опросы, в которых пользователь голосовал. Для собственного профиля передавать
    /// <paramref name="includeAnonymous"/> = true; для публичного профиля чужого
    /// пользователя — false, иначе раскрывается участие в анонимных опросах.
    /// </summary>
    Task<List<Poll>> GetVotedPollsAsync(string userId, bool includeAnonymous = true);
}
