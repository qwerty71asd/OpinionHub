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
}
