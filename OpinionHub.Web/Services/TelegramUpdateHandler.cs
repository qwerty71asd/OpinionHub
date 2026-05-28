using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace OpinionHub.Web.Services;

public class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramLinkTokenService _tokens;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        ITelegramLinkTokenService tokens,
        UserManager<ApplicationUser> userManager,
        ITelegramBotClient bot,
        ILogger<TelegramUpdateHandler> logger)
    {
        _tokens = tokens;
        _userManager = userManager;
        _bot = bot;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct = default)
    {
        var msg = update.Message;
        if (msg is null || string.IsNullOrWhiteSpace(msg.Text)) return;

        var text = msg.Text.Trim();
        if (!text.StartsWith("/start", StringComparison.OrdinalIgnoreCase)) return;

        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            await SafeSendAsync(msg.Chat.Id,
                "Привет! Чтобы привязать аккаунт OpinionHub, откройте «Telegram» в личном кабинете на сайте и нажмите по сгенерированной ссылке.",
                ct);
            return;
        }

        var token = parts[1];
        if (!_tokens.TryConsume(token, out var userId))
        {
            await SafeSendAsync(msg.Chat.Id,
                "❌ Ссылка недействительна или истёк срок. Сгенерируйте новую в личном кабинете на сайте.",
                ct);
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await SafeSendAsync(msg.Chat.Id, "❌ Пользователь не найден.", ct);
            return;
        }

        user.TelegramChatId = msg.Chat.Id.ToString();
        var res = await _userManager.UpdateAsync(user);
        if (!res.Succeeded)
        {
            _logger.LogError("Failed to save TelegramChatId for {UserId}: {Errors}",
                userId, string.Join("; ", res.Errors.Select(e => e.Description)));
            await SafeSendAsync(msg.Chat.Id, "⚠️ Не удалось сохранить привязку. Попробуйте ещё раз.", ct);
            return;
        }

        _logger.LogInformation("Telegram chat {ChatId} linked to user {UserId}", msg.Chat.Id, userId);
        await SafeSendAsync(msg.Chat.Id,
            $"✅ Аккаунт <b>{System.Net.WebUtility.HtmlEncode(user.UserName)}</b> привязан.\n\nТеперь вы будете получать уведомления о новых опросах от тех, на кого подписаны.",
            ct);
    }

    private async Task SafeSendAsync(long chatId, string html, CancellationToken ct)
    {
        try
        {
            await _bot.SendMessage(chatId, html, parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram reply to chat {ChatId}", chatId);
        }
    }
}
