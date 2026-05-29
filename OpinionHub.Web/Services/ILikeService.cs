using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public interface ILikeService
{
    // === Polls ===
    Task<int> GetPollLikesCountAsync(Guid pollId);
    Task<bool> IsPollLikedAsync(string userId, Guid pollId);

    /// <summary>
    /// Опросы, лайкнутые пользователем. Только публичные не-анонимные не-черновики, не soft-deleted —
    /// чтобы публичный список лайков не раскрывал участие в анонимных и не показывал черновики.
    /// Include(Author) — для рендера аватарки автора в карточке.
    /// </summary>
    Task<List<Poll>> GetLikedPollsAsync(string userId);
    /// <summary>Создать лайк опроса. Возвращает AuthorId опроса — нужен контроллеру для milestone-notify. Повторный вызов идемпотентен.</summary>
    Task<string> LikePollAsync(string userId, Guid pollId);
    /// <summary>Снять лайк опроса. Если лайка не было — no-op.</summary>
    Task UnlikePollAsync(string userId, Guid pollId);

    // === Comments ===
    Task<int> GetCommentLikesCountAsync(Guid commentId);
    Task<bool> IsCommentLikedAsync(string userId, Guid commentId);
    /// <summary>Возвращает PollId (для SignalR-broadcast в группу опроса) + AuthorId коммента (для milestone-notify).</summary>
    Task<(Guid PollId, string AuthorId)> LikeCommentAsync(string userId, Guid commentId);
    /// <summary>Возвращает PollId — для broadcast'а. Если лайка не было, всё равно отдаём pollId; если комментария нет — бросает InvalidOperationException.</summary>
    Task<Guid> UnlikeCommentAsync(string userId, Guid commentId);
}
