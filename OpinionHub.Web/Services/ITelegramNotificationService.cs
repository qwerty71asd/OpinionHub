namespace OpinionHub.Web.Services;

public interface ITelegramNotificationService
{
    /// <summary>Анонс в общий канал TelegramBot:ChannelId.</summary>
    Task SendPollNotificationAsync(string pollTitle, string pollDescription, string pollUrl, string? imagePath);

    /// <summary>
    /// Персональная рассылка по личным чатам подписчиков автора. Берёт всех, кто подписан
    /// на автора опроса и имеет привязанный TelegramChatId. Опрос должен быть активным
    /// и не soft-удалён.
    /// </summary>
    Task NotifySubscribersOfNewPollAsync(Guid pollId);
}
