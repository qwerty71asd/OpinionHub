using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Background;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Services;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _db;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ITelegramNotificationService? _telegram;

    public CommentService(ApplicationDbContext db, IBackgroundTaskQueue taskQueue, ITelegramNotificationService? telegram = null)
    {
        _db = db;
        _taskQueue = taskQueue;
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
                IsAuthorDeleted = c.Author.IsDeleted,
                IsByPollAuthor = c.AuthorId == pollAuthorId,
                ParentAuthorUserName = c.ParentComment != null ? c.ParentComment.Author.UserName : null,
                IsParentAuthorDeleted = c.ParentComment != null && c.ParentComment.Author.IsDeleted,
                IsDeleted = c.IsDeleted,
                IsParentDeleted = c.ParentComment != null && c.ParentComment.IsDeleted,
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
        bool parentAuthorDeleted = false;
        bool parentDeleted = false;
        Guid? parentParentCommentId = null;
        if (parentCommentId.HasValue)
        {
            var parentInfo = await _db.Comments
                .Where(c => c.Id == parentCommentId.Value && c.PollId == pollId)
                .Select(c => new
                {
                    AuthorUserName = c.Author.UserName,
                    AuthorDeleted = c.Author.IsDeleted,
                    c.IsDeleted,
                    c.ParentCommentId,
                })
                .FirstOrDefaultAsync();
            if (parentInfo is null)
                throw new InvalidOperationException("Родительский комментарий не найден.");
            parentAuthorUserName = parentInfo.AuthorUserName;
            parentAuthorDeleted = parentInfo.AuthorDeleted;
            parentDeleted = parentInfo.IsDeleted;
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
        // Уносим в фон: сетевой вызов в Telegram не должен задерживать ответ клиенту.
        // Резолвим сервис ИЗ scope в лямбде, не захватываем _telegram (он Transient над Scoped DbContext).
        if (_telegram is not null)
        {
            var commentIdLocal = comment.Id;
            var isReply = parentCommentId.HasValue;
            await _taskQueue.QueueAsync((sp, ct) =>
            {
                var telegram = sp.GetRequiredService<ITelegramNotificationService>();
                return isReply
                    ? telegram.NotifyReplyToMyCommentAsync(commentIdLocal)
                    : telegram.NotifyCommentOnMyPollAsync(commentIdLocal);
            });
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
                // MAX_DEPTH — защита от циклической ссылки. В БД ParentComment имеет
                // DeleteBehavior.Restrict, цикл «по правилам» невозможен — но если случится
                // ручная правка/баг, лучше выкинуть ошибку, чем уйти в бесконечный цикл.
                const int MAX_DEPTH = 100;
                var depth = 0;
                var cursor = parentParentCommentId.Value;
                while (true)
                {
                    if (++depth > MAX_DEPTH)
                        throw new InvalidOperationException("Превышена глубина дерева комментариев.");
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
            IsAuthorDeleted = false,
            IsByPollAuthor = !pollInfo.IsAnonymousAuthor && comment.AuthorId == pollAuthorId,
            ParentAuthorUserName = parentAuthorUserName,
            IsParentAuthorDeleted = parentAuthorDeleted,
            IsParentDeleted = parentDeleted,
            IsDeleted = false,
            RootCommentId = rootCommentId,
            Text = comment.Text,
            ImagePath = comment.ImagePath,
            CreatedAtUtc = comment.CreatedAtUtc,
            LikeCount = 0,
            IsLikedByMe = false,
            IsLikedByPollAuthor = false,
        };
    }

    public async Task<CommentDeleteResult> AdminDeleteAsync(Guid commentId, string adminId)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null)
            return new CommentDeleteResult { Success = false, ErrorMessage = "Комментарий не найден." };
        if (comment.IsDeleted)
            return new CommentDeleteResult { Success = false, ErrorMessage = "Комментарий уже удалён." };

        var isRoot = comment.ParentCommentId is null;
        var pollId = comment.PollId;
        // RootCommentId: для root-коммента — он сам; для reply — поднимаемся по цепочке.
        // Тот же паттерн, что и в CreateAsync (строки ~217-240). Глубина треда обычно <5.
        Guid rootCommentId = comment.Id;
        if (comment.ParentCommentId is { } parentId)
        {
            // Тот же guard, что и в CreateAsync — защита от циклической ссылки.
            const int MAX_DEPTH = 100;
            var depth = 0;
            var cursor = parentId;
            while (true)
            {
                if (++depth > MAX_DEPTH)
                    throw new InvalidOperationException("Превышена глубина дерева комментариев.");
                var next = await _db.Comments
                    .Where(c => c.Id == cursor)
                    .Select(c => c.ParentCommentId)
                    .FirstOrDefaultAsync();
                if (next is null) { rootCommentId = cursor; break; }
                cursor = next.Value;
            }
        }

        if (isRoot)
        {
            // Каскадный hard-delete всех потомков (любой глубины) + самого рут-узла.
            // Никакого tombstone — на странице удалённый рут не отображается вообще.
            var pollComments = await _db.Comments
                .Where(c => c.PollId == comment.PollId)
                .Select(c => new { c.Id, c.ParentCommentId })
                .ToListAsync();

            var childrenByParent = pollComments
                .Where(c => c.ParentCommentId.HasValue)
                .GroupBy(c => c.ParentCommentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            var descendants = new List<Guid>();
            var stack = new Stack<Guid>();
            stack.Push(comment.Id);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur != comment.Id) descendants.Add(cur);
                if (childrenByParent.TryGetValue(cur, out var kids))
                    foreach (var k in kids) stack.Push(k);
            }

            var allIds = new List<Guid>(descendants) { comment.Id };
            // Лайки удалятся каскадом (DeleteBehavior.Cascade на CommentLike → Comment).
            await _db.Comments.Where(c => allIds.Contains(c.Id)).ExecuteDeleteAsync();

            return new CommentDeleteResult
            {
                Success = true,
                WasRoot = true,
                RemovedDescendantIds = descendants,
                PollId = pollId,
                RootCommentId = rootCommentId,
            };
        }
        else
        {
            // Reply с возможными дочками — soft-delete на самом узле, дети остаются
            // (если есть). На странице tombstone не рисуется, текст коммента скрывается
            // через клиентский decorate.
            comment.IsDeleted = true;
            comment.DeletedAtUtc = DateTime.UtcNow;
            comment.DeletedById = adminId;
            await _db.SaveChangesAsync();
            return new CommentDeleteResult
            {
                Success = true,
                WasRoot = false,
                PollId = pollId,
                RootCommentId = rootCommentId,
            };
        }
    }
}

public class CommentDeleteResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool WasRoot { get; set; }
    public List<Guid> RemovedDescendantIds { get; set; } = new();
    public Guid PollId { get; set; }
    public Guid RootCommentId { get; set; }
}
