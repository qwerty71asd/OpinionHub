using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;
    private readonly ITelegramNotificationService? _telegram;

    public CommentService(ApplicationDbContext db, ITelegramNotificationService? telegram = null)
    {
        _db = db;
        _telegram = telegram; // nullable: бот может быть не настроен в конфиге
    }

    public async Task<IReadOnlyList<CommentNodeViewModel>> GetCommentsTreeAsync(Guid pollId, string? viewerUserId)
    {
        // 1) Автор опроса нужен для двух флагов (IsByPollAuthor и IsLikedByPollAuthor).
        //    Дёшево вытащить отдельным запросом и не тянуть весь Poll. Заодно тянем
        //    IsAnonymousAuthor — при анонимном опросе бейдж «Автор» и звёздочка
        //    «Лайк от автора» должны быть скрыты во всех комментариях.
        var pollInfo = await _db.Polls
            .Where(p => p.Id == pollId)
            .Select(p => new { p.AuthorId, p.IsAnonymousAuthor })
            .FirstOrDefaultAsync();

        if (pollInfo is null)
            return Array.Empty<CommentNodeViewModel>();

        var pollAuthorId = pollInfo.AuthorId;
        var hideAuthorMarkers = pollInfo.IsAnonymousAuthor;

        // 2) Плоский список комментариев + имя автора + имя автора parent'а
        //    (через nav property — EF делает LEFT JOIN, root-комменты получают null).
        //    Сортируем по времени — flat-список потомков под root останется хронологическим.
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
                ParentAuthorUserName = c.ParentComment != null ? c.ParentComment.Author.UserName : null,
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
            node.IsLikedByPollAuthor = !hideAuthorMarkers && likedByAuthorSet.Contains(node.Id);
            node.IsLikedByMe = likedByMeSet.Contains(node.Id);
            if (hideAuthorMarkers) node.IsByPollAuthor = false;
        }

        // 4) TikTok-flat: для каждого root собираем плоский список ВСЕХ потомков любой
        //    глубины в порядке создания. Для root.RootCommentId = root.Id; для каждого
        //    потомка поднимаемся по цепочке parent'ов до root и кешируем результат —
        //    итоговая сложность O(n) по числу комментариев.
        var byId = flat.ToDictionary(n => n.Id);
        var roots = new List<CommentNodeViewModel>();
        var rootOfId = new Dictionary<Guid, Guid>(flat.Count);

        // Сначала отделяем roots — они сами себе корень.
        foreach (var node in flat)
        {
            if (node.ParentCommentId is null || !byId.ContainsKey(node.ParentCommentId.Value))
            {
                node.RootCommentId = node.Id;
                rootOfId[node.Id] = node.Id;
                roots.Add(node);
            }
        }

        // Для всех остальных — поднимаемся до уже известного корня, кешируя путь.
        foreach (var node in flat)
        {
            if (rootOfId.ContainsKey(node.Id)) continue;

            var path = new List<Guid>();
            var cursor = node;
            while (!rootOfId.TryGetValue(cursor.Id, out _))
            {
                path.Add(cursor.Id);
                cursor = byId[cursor.ParentCommentId!.Value];
            }
            var rootId = rootOfId[cursor.Id];
            foreach (var id in path) rootOfId[id] = rootId;

            node.RootCommentId = rootId;
            byId[rootId].Replies.Add(node);
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

        // Проверка опроса (заодно достаём AuthorId опроса — пригодится для IsByPollAuthor —
        // и IsAnonymousAuthor: при анонимном опросе бейдж «Автор» нужно скрыть).
        var pollInfo = await _db.Polls
            .Where(p => p.Id == pollId && !p.IsDeleted)
            .Select(p => new { p.AuthorId, p.IsAnonymousAuthor })
            .FirstOrDefaultAsync();
        if (pollInfo is null)
            throw new InvalidOperationException("Опрос не найден.");
        var pollAuthorId = pollInfo.AuthorId;

        // ParentComment должен принадлежать тому же опросу — иначе можно подсунуть чужой parentId.
        // Заодно вытаскиваем имя автора parent'а (для @mention) и его ParentCommentId
        // — он нужен, чтобы дальше подняться до root треда.
        string? parentAuthorUserName = null;
        Guid? parentParentCommentId = null;
        if (parentCommentId.HasValue)
        {
            var parentInfo = await _db.Comments
                .Where(c => c.Id == parentCommentId.Value && c.PollId == pollId)
                .Select(c => new
                {
                    AuthorUserName = c.Author.UserName,
                    c.ParentCommentId,
                })
                .FirstOrDefaultAsync();
            if (parentInfo is null)
                throw new InvalidOperationException("Родительский комментарий не найден.");
            parentAuthorUserName = parentInfo.AuthorUserName;
            parentParentCommentId = parentInfo.ParentCommentId;
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

        // Telegram-уведомление автору целевой сущности (опроса или parent-коммента).
        // Сервис сам проверит привязку Telegram, per-event флаг и self-notification.
        if (_telegram is not null)
        {
            if (parentCommentId.HasValue)
                await _telegram.NotifyReplyToMyCommentAsync(comment.Id);
            else
                await _telegram.NotifyCommentOnMyPollAsync(comment.Id);
        }

        var authorUserName = await _db.Users
            .Where(u => u.Id == authorId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync() ?? string.Empty;

        // RootCommentId: для root-коммента — он сам; для reply — поднимаемся по цепочке.
        // Глубина треда обычно <5, лишние SQL'ы тут терпимы. Цепочка обрывается, когда
        // у предка ParentCommentId == null.
        Guid rootCommentId = comment.Id;
        if (parentCommentId.HasValue)
        {
            if (parentParentCommentId is null)
            {
                rootCommentId = parentCommentId.Value;
            }
            else
            {
                var cursor = parentParentCommentId.Value;
                while (true)
                {
                    var next = await _db.Comments
                        .Where(c => c.Id == cursor)
                        .Select(c => c.ParentCommentId)
                        .FirstOrDefaultAsync();
                    if (next is null) { rootCommentId = cursor; break; }
                    cursor = next.Value;
                }
            }
        }

        return new CommentNodeViewModel
        {
            Id = comment.Id,
            PollId = comment.PollId,
            ParentCommentId = comment.ParentCommentId,
            AuthorId = comment.AuthorId,
            AuthorUserName = authorUserName,
            IsByPollAuthor = !pollInfo.IsAnonymousAuthor && comment.AuthorId == pollAuthorId,
            ParentAuthorUserName = parentAuthorUserName,
            RootCommentId = rootCommentId,
            Text = comment.Text,
            ImagePath = comment.ImagePath,
            CreatedAtUtc = comment.CreatedAtUtc,
            LikeCount = 0,
            IsLikedByMe = false,
            IsLikedByPollAuthor = false,
        };
    }
}
