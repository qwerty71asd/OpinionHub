namespace OpinionHub.Web.ViewModels;

public class TelegramLinkViewModel
{
    public bool IsLinked { get; set; }
    public string? TelegramChatId { get; set; }

    /// <summary>Готовый t.me/...?start=... — показываем кликабельной ссылкой и QR.</summary>
    public string? DeepLink { get; set; }

    /// <summary>Сам токен — для ручного копирования, если ссылка не открылась.</summary>
    public string? Token { get; set; }

    public TimeSpan TokenLifetime { get; set; }

    /// <summary>Если в конфиге не задано TelegramBot:BotUsername — выводим ошибку, ссылку построить нельзя.</summary>
    public string? ConfigError { get; set; }

    // === Per-event переключатели Telegram-уведомлений ===
    // Все попадают на страницу /Telegram/Link и сохраняются через UpdatePreferences.

    /// <summary>Получать ли уведомления об анонимных опросах от подписок (sub-флаг к NewPollFromSubscription).</summary>
    public bool NotifyAnonymousPolls { get; set; }

    /// <summary>Кто-то подписался на меня.</summary>
    public bool NotifyOnNewSubscriber { get; set; }

    /// <summary>Новый опрос от того, на кого подписан. Default true (обратная совместимость).</summary>
    public bool NotifyOnNewPollFromSubscription { get; set; }

    /// <summary>Новый root-комментарий под моим опросом.</summary>
    public bool NotifyOnCommentOnMyPoll { get; set; }

    /// <summary>Ответ на мой комментарий.</summary>
    public bool NotifyOnReplyToMyComment { get; set; }

    /// <summary>Milestone-лайки моего опроса/коммента (10/100/1000).</summary>
    public bool NotifyOnLikeMilestone { get; set; }
}
