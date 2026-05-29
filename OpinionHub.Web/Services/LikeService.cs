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

    public async Task<string> LikePollAsync(string userId, Guid pollId)
    {
        // Один запрос вместо двух: достаём AuthorId и проверяем существование/soft-delete.
        // На soft-deleted лайкать запрещаем, чтобы не плодить активность на скрытом контенте.
        var authorId = await _db.Polls
            .Where(p => p.Id == pollId && !p.IsDeleted)
            .Select(p => p.AuthorId)
            .FirstOrDefaultAsync();
        if (authorId is null)
            throw new InvalidOperationException("Опрос не найден.");

        var exists = await IsPollLikedAsync(userId, pollId);
        if (exists) return authorId;

        _db.PollLikes.Add(new PollLike { UserId = userId, PollId = pollId });
        await _db.SaveChangesAsync();
        return authorId;
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

    public async Task<(Guid PollId, string AuthorId)> LikeCommentAsync(string userId, Guid commentId)
    {
        // Один запрос: достаём pollId, AuthorId коммента и проверяем существование.
        var info = await _db.Comments
            .Where(c => c.Id == commentId)
            .Select(c => new { c.PollId, c.AuthorId })
            .FirstOrDefaultAsync();
        if (info is null)
            throw new InvalidOperationException("Комментарий не найден.");

        var exists = await IsCommentLikedAsync(userId, commentId);
        if (exists) return (info.PollId, info.AuthorId);

        _db.CommentLikes.Add(new CommentLike { UserId = userId, CommentId = commentId });
        await _db.SaveChangesAsync();
        return (info.PollId, info.AuthorId);
    }

    public async Task<Guid> UnlikeCommentAsync(string userId, Guid commentId)
    {
        var pollId = await _db.Comments
            .Where(c => c.Id == commentId)
            .Select(c => (Guid?)c.PollId)
            .FirstOrDefaultAsync();
        if (pollId is null)
            throw new InvalidOperationException("Комментарий не найден.");

        var row = await _db.CommentLikes.FirstOrDefaultAsync(cl => cl.UserId == userId && cl.CommentId == commentId);
        if (row is null) return pollId.Value;
        _db.CommentLikes.Remove(row);
        await _db.SaveChangesAsync();
        return pollId.Value;
    }
}
