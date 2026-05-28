using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;

    public CommentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CommentNodeViewModel>> GetCommentsTreeAsync(Guid pollId, string? viewerUserId)
    {
        // 1) Автор опроса нужен для двух флагов (IsByPollAuthor и IsLikedByPollAuthor).
        //    Дёшево вытащить отдельным запросом и не тянуть весь Poll.
        var pollAuthorId = await _db.Polls
            .Where(p => p.Id == pollId)
            .Select(p => p.AuthorId)
            .FirstOrDefaultAsync();

        if (pollAuthorId is null)
            return Array.Empty<CommentNodeViewModel>();

        // 2) Плоский список комментариев + имя автора. Сортируем по времени —
        //    при сборке дерева порядок ответов внутри узла останется хронологическим.
        var flat = await _db.Comments
            .Where(c => c.PollId == pollId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new CommentNodeViewModel
            {
                Id = c.Id,
                PollId = c.PollId,
                ParentCommentId = c.ParentCommentId,
                AuthorId = c.AuthorId,
                AuthorUserName = c.Author.UserName ?? string.Empty,
                IsByPollAuthor = c.AuthorId == pollAuthorId,
                Text = c.Text,
                ImagePath = c.ImagePath,
                CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToListAsync();

        if (flat.Count == 0)
            return Array.Empty<CommentNodeViewModel>();

        var ids = flat.Select(c => c.Id).ToList();

        // 3) Все лайки этих комментариев одним запросом. Считаем агрегаты на сервере БД,
        //    плюс отдельно вытаскиваем "лайкнул ли OP" и "лайкнул ли зритель" — две булевы
        //    серии. Это N комментариев → один-два SQL, без N+1.
        var counts = await _db.CommentLikes
            .Where(cl => ids.Contains(cl.CommentId))
            .GroupBy(cl => cl.CommentId)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count);

        var likedByAuthor = await _db.CommentLikes
            .Where(cl => cl.UserId == pollAuthorId && ids.Contains(cl.CommentId))
            .Select(cl => cl.CommentId)
            .ToListAsync();
        var likedByAuthorSet = likedByAuthor.ToHashSet();

        HashSet<Guid> likedByMeSet;
        if (!string.IsNullOrEmpty(viewerUserId))
        {
            var likedByMe = await _db.CommentLikes
                .Where(cl => cl.UserId == viewerUserId && ids.Contains(cl.CommentId))
                .Select(cl => cl.CommentId)
                .ToListAsync();
            likedByMeSet = likedByMe.ToHashSet();
        }
        else
        {
            likedByMeSet = new HashSet<Guid>();
        }

        foreach (var node in flat)
        {
            node.LikeCount = counts.TryGetValue(node.Id, out var c) ? c : 0;
            node.IsLikedByPollAuthor = likedByAuthorSet.Contains(node.Id);
            node.IsLikedByMe = likedByMeSet.Contains(node.Id);
        }

        // 4) Сборка дерева. Index by Id, потом раскладываем по родителям.
        //    Если ParentCommentId указывает на отсутствующий узел (не должно случаться,
        //    но FK Restrict его не удалит), считаем такой комментарий корневым.
        var byId = flat.ToDictionary(n => n.Id);
        var roots = new List<CommentNodeViewModel>();

        foreach (var node in flat)
        {
            if (node.ParentCommentId is { } parentId && byId.TryGetValue(parentId, out var parent))
                parent.Replies.Add(node);
            else
                roots.Add(node);
        }

        return roots;
    }

    public async Task<CommentNodeViewModel> CreateAsync(Guid pollId, string authorId, string text, Guid? parentCommentId, string? imagePath = null)
    {
        text = text?.Trim() ?? string.Empty;
        var hasImage = !string.IsNullOrEmpty(imagePath);
        if (text.Length == 0 && !hasImage)
            throw new InvalidOperationException("Комментарий не может быть пустым.");
        if (text.Length > 2000)
            throw new InvalidOperationException("Слишком длинный комментарий (максимум 2000 символов).");

        // Проверка опроса (заодно достаём AuthorId опроса — пригодится для IsByPollAuthor).
        var pollAuthorId = await _db.Polls
            .Where(p => p.Id == pollId && !p.IsDeleted)
            .Select(p => p.AuthorId)
            .FirstOrDefaultAsync();
        if (pollAuthorId is null)
            throw new InvalidOperationException("Опрос не найден.");

        // ParentComment должен принадлежать тому же опросу — иначе можно подсунуть чужой parentId.
        if (parentCommentId.HasValue)
        {
            var parentOk = await _db.Comments
                .AnyAsync(c => c.Id == parentCommentId.Value && c.PollId == pollId);
            if (!parentOk)
                throw new InvalidOperationException("Родительский комментарий не найден.");
        }

        var comment = new Comment
        {
            PollId = pollId,
            AuthorId = authorId,
            Text = text,
            ImagePath = imagePath,
            ParentCommentId = parentCommentId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        var authorUserName = await _db.Users
            .Where(u => u.Id == authorId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync() ?? string.Empty;

        return new CommentNodeViewModel
        {
            Id = comment.Id,
            PollId = comment.PollId,
            ParentCommentId = comment.ParentCommentId,
            AuthorId = comment.AuthorId,
            AuthorUserName = authorUserName,
            IsByPollAuthor = comment.AuthorId == pollAuthorId,
            Text = comment.Text,
            ImagePath = comment.ImagePath,
            CreatedAtUtc = comment.CreatedAtUtc,
            LikeCount = 0,
            IsLikedByMe = false,
            IsLikedByPollAuthor = false,
        };
    }
}
