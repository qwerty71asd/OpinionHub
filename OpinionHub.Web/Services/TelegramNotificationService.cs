using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
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
    private readonly ApplicationDbContext _db;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TelegramNotificationService(
        ITelegramBotClient botClient,
        IConfiguration config,
        ILogger<TelegramNotificationService> logger,
        IWebHostEnvironment env,
        ApplicationDbContext db,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _botClient = botClient;
        _config = config;
        _logger = logger;
        _env = env;
        _db = db;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SendPollNotificationAsync(string pollTitle, string pollDescription, string pollUrl, string? imagePath)
    {
        try
        {
            var channelId = _config["TelegramBot:ChannelId"];
            if (string.IsNullOrEmpty(channelId)) return;

            pollUrl = NormalizeLocalhost(pollUrl);

            var text = $"📊 <b>Новый опрос!</b>\n\n<b>{pollTitle}</b>\n<i>{pollDescription}</i>";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("🗳 Проголосовать", pollUrl)
            });

            if (!string.IsNullOrEmpty(imagePath))
            {
                var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await _botClient.SendPhoto(
                        chatId: channelId,
                        photo: InputFile.FromStream(stream, Path.GetFileName(fullPath)),
                        caption: text,
                        parseMode: ParseMode.Html,
                        replyMarkup: inlineKeyboard);
                    return;
                }
            }

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

    public async Task NotifySubscribersOfNewPollAsync(Guid pollId)
    {
        try
        {
            // Тянем минимум — без Options/Votes/Attachments.
            var poll = await _db.Polls
                .AsNoTracking()
                .Where(p => p.Id == pollId && !p.IsDeleted)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Status,
                    AuthorId = p.AuthorId,
                    AuthorUserName = p.Author!.UserName
                })
                .FirstOrDefaultAsync();

            if (poll is null) return;
            // Уведомляем только когда опрос реально опубликован.
            if (poll.Status != Models.PollStatus.Active) return;

            // Подписчики автора с привязанным Telegram. Один запрос, плоский список chatId.
            var chatIds = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.TargetUserId == poll.AuthorId)
                .Join(_db.Users,
                    s => s.SubscriberId,
                    u => u.Id,
                    (s, u) => u.TelegramChatId)
                .Where(chatId => chatId != null && chatId != "")
                .ToListAsync();

            if (chatIds.Count == 0) return;

            var pollUrl = BuildPollUrl(poll.Id);
            var authorName = string.IsNullOrWhiteSpace(poll.AuthorUserName) ? "Пользователь" : poll.AuthorUserName!;
            var title = poll.Title ?? string.Empty;

            var text = $"🔔 <b>{System.Net.WebUtility.HtmlEncode(authorName)}</b> опубликовал(а) новый опрос:\n\n<b>{System.Net.WebUtility.HtmlEncode(title)}</b>";
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("🗳 Открыть опрос", pollUrl)
            });

            foreach (var chatId in chatIds)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId!,
                        text: text,
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard);
                }
                catch (Exception ex)
                {
                    // Один невалидный chatId не должен ломать остальную рассылку
                    // (например, юзер заблокировал бота — тогда придёт 403).
                    _logger.LogWarning(ex, "Не удалось отправить уведомление в чат {ChatId}", chatId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при рассылке уведомлений о новом опросе {PollId}", pollId);
        }
    }

    private string BuildPollUrl(Guid pollId)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var url = _linkGenerator.GetUriByAction(
                ctx,
                action: "Details",
                controller: "Polls",
                values: new { id = pollId });
            if (!string.IsNullOrEmpty(url)) return NormalizeLocalhost(url);
        }

        // Запасной вариант для фонового контекста (если этот сервис вдруг вызовут вне HTTP)
        var fallbackHost = _config["TelegramBot:PublicHost"] ?? "opinionhub.site";
        return $"https://{fallbackHost}/Polls/Details/{pollId}";
    }

    /// <summary>
    /// Тот же хак, что был в исходнике: локалхостный URL заменяется на прод-домен,
    /// чтобы Telegram-инлайн-кнопка не ругалась. Пока стенд не имеет публичного URL — пусть остаётся.
    /// </summary>
    private static string NormalizeLocalhost(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (!url.Contains("localhost")) return url;
        return url.Replace("localhost:7060", "opinionhub.site")
                  .Replace("localhost:5060", "opinionhub.site");
    }
}
