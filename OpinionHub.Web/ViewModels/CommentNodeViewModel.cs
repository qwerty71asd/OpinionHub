namespace OpinionHub.Web.ViewModels;

/// <summary>
/// Узел дерева комментариев под опросом — то, что уезжает на фронтенд
/// (как через MVC-вью, так и через будущий JSON-эндпоинт).
///
/// Древовидная структура строится по <see cref="ParentCommentId"/>:
/// плоский список приходит из БД, сервис собирает иерархию через <see cref="Replies"/>.
/// </summary>
public class CommentNodeViewModel
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public Guid? ParentCommentId { get; set; }

    // Автор комментария
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorUserName { get; set; } = string.Empty;
    /// <summary>Комментарий написан автором опроса (OP) — для подсветки "от автора".</summary>
    public bool IsByPollAuthor { get; set; }

    // Контент
    public string Text { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Лайки — единственный источник правды для UI: счётчик, состояние кнопки и бейдж "Лайк от автора"
    public int LikeCount { get; set; }
    /// <summary>Текущий авторизованный зритель уже лайкал. Для гостя всегда false.</summary>
    public bool IsLikedByMe { get; set; }
    /// <summary>Автор опроса поставил лайк этому комментарию — рисуем бейдж "Лайк от автора".</summary>
    public bool IsLikedByPollAuthor { get; set; }

    // Дерево ответов
    public List<CommentNodeViewModel> Replies { get; set; } = new();
}
