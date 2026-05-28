using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class LikeService : ILikeService
{
    private readonly ApplicationDbContext _db;

    public LikeService(ApplicationDbContext db)
    {
        _db = db;
    }

    // === Polls ===

    public Task<int> GetPollLikesCountAsync(Guid pollId) =>
        _db.PollLikes.CountAsync(pl => pl.PollId == pollId);

    public Task<bool> IsPollLikedAsync(string userId, Guid pollId) =>
        _db.PollLikes.AnyAsync(pl => pl.UserId == userId && pl.PollId == pollId);

    public async Task LikePollAsync(string userId, Guid pollId)
    {
        var exists = await IsPollLikedAsync(userId, pollId);
        if (exists) return;

        // Перед записью проверим, что опрос существует (и не soft-удалён нам это не мешает —
        // автор удалил, но кто-то может ещё видеть карточку из истории; на soft-deleted лайкать
        // запрещаем, чтобы не плодить активность на скрытом контенте).
        var pollExistsAndVisible = await _db.Polls.AnyAsync(p => p.Id == pollId && !p.IsDeleted);
        if (!pollExistsAndVisible)
            throw new InvalidOperationException("Опрос не найден.");

        _db.PollLikes.Add(new PollLike { UserId = userId, PollId = pollId });
        await _db.SaveChangesAsync();
    }

    public async Task UnlikePollAsync(string userId, Guid pollId)
    {
        var row = await _db.PollLikes.FirstOrDefaultAsync(pl => pl.UserId == userId && pl.PollId == pollId);
        if (row is null) return;
        _db.PollLikes.Remove(row);
        await _db.SaveChangesAsync();
    }

    // === Comments ===

    public Task<int> GetCommentLikesCountAsync(Guid commentId) =>
        _db.CommentLikes.CountAsync(cl => cl.CommentId == commentId);

    public Task<bool> IsCommentLikedAsync(string userId, Guid commentId) =>
        _db.CommentLikes.AnyAsync(cl => cl.UserId == userId && cl.CommentId == commentId);

    public async Task LikeCommentAsync(string userId, Guid commentId)
    {
        var exists = await IsCommentLikedAsync(userId, commentId);
        if (exists) return;

        var commentExists = await _db.Comments.AnyAsync(c => c.Id == commentId);
        if (!commentExists)
            throw new InvalidOperationException("Комментарий не найден.");

        _db.CommentLikes.Add(new CommentLike { UserId = userId, CommentId = commentId });
        await _db.SaveChangesAsync();
    }

    public async Task UnlikeCommentAsync(string userId, Guid commentId)
    {
        var row = await _db.CommentLikes.FirstOrDefaultAsync(cl => cl.UserId == userId && cl.CommentId == commentId);
        if (row is null) return;
        _db.CommentLikes.Remove(row);
        await _db.SaveChangesAsync();
    }
}
