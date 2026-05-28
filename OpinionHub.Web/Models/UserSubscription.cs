namespace OpinionHub.Web.Models;

/// <summary>
/// Подписка одного пользователя (Subscriber) на другого (Target).
/// Композитный ключ (SubscriberId, TargetUserId) гарантирует уникальность подписки.
/// </summary>
public class UserSubscription
{
    public string SubscriberId { get; set; } = string.Empty;
    public ApplicationUser Subscriber { get; set; } = null!;

    public string TargetUserId { get; set; } = string.Empty;
    public ApplicationUser TargetUser { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
