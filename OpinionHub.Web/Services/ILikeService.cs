namespace OpinionHub.Web.Services;

public interface ILikeService
{
    // === Polls ===
    Task<int> GetPollLikesCountAsync(Guid pollId);
    Task<bool> IsPollLikedAsync(string userId, Guid pollId);
    /// <summary>Создать лайк опроса. Повторный вызов идемпотентен.</summary>
    Task LikePollAsync(string userId, Guid pollId);
    /// <summary>Снять лайк опроса. Если лайка не было — no-op.</summary>
    Task UnlikePollAsync(string userId, Guid pollId);

    // === Comments ===
    Task<int> GetCommentLikesCountAsync(Guid commentId);
    Task<bool> IsCommentLikedAsync(string userId, Guid commentId);
    Task LikeCommentAsync(string userId, Guid commentId);
    Task UnlikeCommentAsync(string userId, Guid commentId);
}
