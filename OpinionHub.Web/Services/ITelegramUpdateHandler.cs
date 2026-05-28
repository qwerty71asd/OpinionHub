using Telegram.Bot.Types;

namespace OpinionHub.Web.Services;

/// <summary>
/// Обработчик апдейтов от Telegram. Одна и та же реализация вызывается как из
/// long-polling (BackgroundService), так и из webhook-контроллера.
/// </summary>
public interface ITelegramUpdateHandler
{
    Task HandleUpdateAsync(Update update, CancellationToken ct = default);
}
