namespace OpinionHub.Web.Services;

/// <summary>Тип сущности для milestone-уведомления о лайках.</summary>
public enum LikeTarget { Poll, Comment }

public interface ITelegramNotificationService
{
    /// <summary>Анонс в общий канал TelegramBot:ChannelId.</summary>
    Task SendPollNotificationAsync(string pollTitle, string pollDescription, string pollUrl, string? imagePath);

    /// <summary>
    /// Персональная рассылка по личным чатам подписчиков автора. Берёт всех, кто подписан
    /// на автора опроса и имеет привязанный TelegramChatId + включённый NotifyOnNewPollFromSubscription.
    /// Если опрос анонимный — дополнительно фильтрует по NotifyAnonymousPolls и заменяет имя
    /// автора на «Аноним». Опрос должен быть активным и не soft-удалён.
    /// </summary>
    Task NotifySubscribersOfNewPollAsync(Guid pollId);

    /// <summary>
    /// Уведомление о новом подписчике. Целевой пользователь (на которого подписались)
    /// должен иметь привязанный Telegram + NotifyOnNewSubscriber = true.
    /// Self-subscribe не возможен (отсекается в SubscribeAsync).
    /// </summary>
    Task NotifyNewSubscriberAsync(string subscriberId, string targetUserId);

    /// <summary>
    /// Новый ROOT-комментарий (parentCommentId == null) под опросом, который создал текущий
    /// пользователь. Не шлётся, если автор опроса сам же его и комментирует.
    /// Требует NotifyOnCommentOnMyPoll = true.
    /// </summary>
    Task NotifyCommentOnMyPollAsync(Guid commentId);

    /// <summary>
    /// Reply на коммент текущего пользователя. Не шлётся, если автор reply == автор parent.
    /// Требует NotifyOnReplyToMyComment = true.
    /// </summary>
    Task NotifyReplyToMyCommentAsync(Guid replyCommentId);

    /// <summary>
    /// Лайки достигли милестоуна (10/100/1000) на опросе или комментарии. Owner —
    /// автор сущности, которому шлём. Требует NotifyOnLikeMilestone = true.
    /// </summary>
    Task NotifyLikeMilestoneAsync(string ownerUserId, LikeTarget target, Guid entityId, int count);
}
