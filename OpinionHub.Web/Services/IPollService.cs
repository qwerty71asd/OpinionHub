using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public interface IPollService
{
    Task<Poll> CreateDraftAsync(CreatePollViewModel model, string authorId);
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

    /// <summary>Опубликованные опросы автора (Status != Draft). Soft-удалённые остаются — их рисует профиль с бейджем «Удалён».</summary>
    Task<List<Poll>> GetUserPublishedPollsAsync(string userId);

    /// <summary>Черновики автора (Status == Draft, !IsDeleted). Для вкладки «Черновики» в собственном профиле.</summary>
    Task<List<Poll>> GetUserDraftsAsync(string userId);

    /// <summary>
    /// Подтягивает черновик со всеми навигациями (Options/Attachments/AllowedUsers) для построения EditPollViewModel.
    /// Бросает EntityNotFoundException, ForbiddenAccessException (не автор), InvalidOperationException (не Draft).
    /// </summary>
    Task<Poll> GetDraftForEditAsync(Guid pollId, string authorId);

    /// <summary>
    /// Обновляет черновик и (опционально) публикует его. Заменяет старые Create+Publish-flow:
    /// если publishNow == true — Status переводится в Active внутри той же транзакции, и метод
    /// сам ставит Telegram-задачу в очередь (так же, как CreateDraftAsync для PublishNow=true).
    /// </summary>
    Task<Poll> UpdateDraftAsync(Guid pollId, EditPollViewModel model, string authorId, bool publishNow);

    /// <summary>
    /// Опросы, в которых пользователь голосовал. Для собственного профиля передавать
    /// <paramref name="includeAnonymous"/> = true; для публичного профиля чужого
    /// пользователя — false, иначе раскрывается участие в анонимных опросах.
    /// </summary>
    Task<List<Poll>> GetVotedPollsAsync(string userId, bool includeAnonymous = true);
}
