namespace OpinionHub.Web.Services;

/// <summary>
/// Одноразовые токены для привязки Telegram-аккаунта.
/// Живут 15 минут в IMemoryCache, потребляются при первом успешном /start.
/// </summary>
public interface ITelegramLinkTokenService
{
    /// <summary>Сгенерировать токен и положить связку token→userId в кэш.</summary>
    string IssueToken(string userId);

    /// <summary>Снять привязку из кэша. true — токен валиден и удалён, userId установлен.</summary>
    bool TryConsume(string token, out string userId);

    TimeSpan TokenLifetime { get; }
}
