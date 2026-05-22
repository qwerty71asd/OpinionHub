using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OpinionHub.Web.Services;

public class TelegramNotificationService : ITelegramNotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _config;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly IWebHostEnvironment _env;

    public TelegramNotificationService(
        ITelegramBotClient botClient,
        IConfiguration config,
        ILogger<TelegramNotificationService> logger,
        IWebHostEnvironment env)
    {
        _botClient = botClient;
        _config = config;
        _logger = logger;
        _env = env;
    }

    public async Task SendPollNotificationAsync(string pollTitle, string pollDescription, string pollUrl, string? imagePath)
    {
        try
        {
            var channelId = _config["TelegramBot:ChannelId"];
            if (string.IsNullOrEmpty(channelId)) return;

            if (pollUrl.Contains("localhost"))
            {
                pollUrl = pollUrl.Replace("localhost:7060", "opinionhub.site")
                                 .Replace("localhost:5060", "opinionhub.site");
            }

            var text = $"📊 <b>Новый опрос!</b>\n\n<b>{pollTitle}</b>\n<i>{pollDescription}</i>";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("🗳 Проголосовать", pollUrl)
            });

            // Если есть картинка
            if (!string.IsNullOrEmpty(imagePath))
            {
                var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // В новых версиях метод называется SendPhoto
                    await _botClient.SendPhoto(
                        chatId: channelId,
                        photo: InputFile.FromStream(stream, Path.GetFileName(fullPath)),
                        caption: text,
                        parseMode: ParseMode.Html,
                        replyMarkup: inlineKeyboard);
                    return;
                }
            }

            // В новых версиях метод называется SendMessage
            await _botClient.SendMessage(
                chatId: channelId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: inlineKeyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке уведомления в Telegram");
        }
    }
}