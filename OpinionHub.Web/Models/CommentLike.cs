namespace OpinionHub.Web.Models;

/// <summary>
/// Лайк комментария от пользователя. Композитный ключ (UserId, CommentId) исключает повторный лайк.
/// </summary>
public class CommentLike
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public Guid CommentId { get; set; }
    public Comment Comment { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
