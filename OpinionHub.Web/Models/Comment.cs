namespace OpinionHub.Web.Models;

/// <summary>
/// Комментарий к опросу. Поддерживает древовидные ответы через ParentCommentId.
/// </summary>
public class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PollId { get; set; }
    public Poll Poll { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? ImagePath { get; set; }

    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    public ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();

    /// <summary>
    /// Soft-delete для админ-удаления. Запись остаётся, чтобы дочки-ответы
    /// сохраняли ссылку на parent через @mention. UI показывает «Комментарий удалён»
    /// без имени автора.
    /// </summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>Id админа, выполнившего удаление. Без FK-навигации, как BannedById.</summary>
    public string? DeletedById { get; set; }
}
