using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Services;
using Telegram.Bot.Types;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// Endpoint для Telegram-вебхука. URL должен быть зарегистрирован через bot.SetWebhook
/// с заголовком secret_token, который сравнивается с настройкой TelegramBot:WebhookSecret.
/// Альтернатива polling-режиму — используется в проде с публичным HTTPS.
/// </summary>
[ApiController]
[Route("api/telegram")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ITelegramUpdateHandler _handler;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        ITelegramUpdateHandler handler,
        IConfiguration config,
        ILogger<TelegramWebhookController> logger)
    {
        _handler = handler;
        _config = config;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken ct)
    {
        // Если в конфиге задан секрет — отбиваем любые запросы без правильного заголовка.
        var expected = _config["TelegramBot:WebhookSecret"];
        if (!string.IsNullOrEmpty(expected))
        {
            var received = Request.Headers[SecretHeader].ToString();
            if (!string.Equals(received, expected, StringComparison.Ordinal))
            {
                _logger.LogWarning("Telegram webhook rejected: bad secret token.");
                return Unauthorized();
            }
        }

        if (update is null) return BadRequest();

        try
        {
            await _handler.HandleUpdateAsync(update, ct);
        }
        catch (Exception ex)
        {
            // Telegram повторяет недоставленные апдейты, поэтому возвращаем 200 даже на исключения,
            // чтобы не зациклить ретраи на сломанном апдейте. Ошибки — в лог.
            _logger.LogError(ex, "Unhandled error while processing Telegram webhook update {UpdateId}", update.Id);
        }

        return Ok();
    }
}
