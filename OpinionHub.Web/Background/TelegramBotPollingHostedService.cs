using OpinionHub.Web.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace OpinionHub.Web.Background;

/// <summary>
/// Долгий long-polling апдейтов от Telegram. Подходит для dev/прод без публичного URL.
/// Если установлен webhook, polling вернёт 409 — тогда переключите режим на webhook.
/// </summary>
public class TelegramBotPollingHostedService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TelegramBotPollingHostedService> _logger;

    public TelegramBotPollingHostedService(
        ITelegramBotClient bot,
        IServiceScopeFactory scopes,
        ILogger<TelegramBotPollingHostedService> logger)
    {
        _bot = bot;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
            DropPendingUpdates = false
        };

        _logger.LogInformation("Telegram polling started.");

        try
        {
            await _bot.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram polling stopped unexpectedly.");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Telegram.Bot.Types.Update update, CancellationToken ct)
    {
        // BackgroundService живёт в корневом scope — создаём новый, чтобы безопасно резолвить scoped-сервисы (UserManager + DbContext).
        using var scope = _scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ITelegramUpdateHandler>();
        try
        {
            await handler.HandleUpdateAsync(update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing Telegram update {UpdateId}", update.Id);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram polling error.");
        return Task.CompletedTask;
    }
}
