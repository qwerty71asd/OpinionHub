namespace OpinionHub.Web.Models;

/// <summary>
/// Лайк опроса от пользователя. Композитный ключ (UserId, PollId) исключает двойной лайк.
/// </summary>
public class PollLike
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public Guid PollId { get; set; }
    public Poll Poll { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
