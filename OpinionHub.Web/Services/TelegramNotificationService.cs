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
            // Тянем минимум — без Options/Votes/Attachments. IsAnonymousAuthor нужен,
            // чтобы маскировать имя автора и фильтровать подписчиков, не желающих
            // получать анонимные опросы.
            var poll = await _db.Polls
                .AsNoTracking()
                .Where(p => p.Id == pollId && !p.IsDeleted)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Status,
                    AuthorId = p.AuthorId,
                    AuthorUserName = p.Author!.UserName,
                    p.IsAnonymousAuthor
                })
                .FirstOrDefaultAsync();

            if (poll is null) return;
            // Уведомляем только когда опрос реально опубликован.
            if (poll.Status != Models.PollStatus.Active) return;

            // Подписчики автора с привязанным Telegram. Если опрос анонимный — фильтруем
            // дополнительно по NotifyAnonymousPolls (юзер должен явно включить эту опцию).
            // Один SQL-запрос с Join'ом и Where — без N+1.
            var chatIds = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.TargetUserId == poll.AuthorId)
                .Join(_db.Users,
                    s => s.SubscriberId,
                    u => u.Id,
                    (s, u) => u)
                .Where(u => u.TelegramChatId != null && u.TelegramChatId != ""
                            && u.NotifyOnNewPollFromSubscription
                            && (!poll.IsAnonymousAuthor || u.NotifyAnonymousPolls))
                .Select(u => u.TelegramChatId)
                .ToListAsync();

            if (chatIds.Count == 0) return;

            var pollUrl = BuildPollUrl(poll.Id);
            var authorName = poll.IsAnonymousAuthor
                ? "Аноним"
                : (string.IsNullOrWhiteSpace(poll.AuthorUserName) ? "Пользователь" : poll.AuthorUserName!);
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

    public async Task NotifyNewSubscriberAsync(string subscriberId, string targetUserId)
    {
        try
        {
            if (string.Equals(subscriberId, targetUserId, StringComparison.Ordinal)) return;

            var target = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == targetUserId)
                .Select(u => new { u.TelegramChatId, u.NotifyOnNewSubscriber })
                .FirstOrDefaultAsync();
            if (target is null || string.IsNullOrEmpty(target.TelegramChatId)) return;
            if (!target.NotifyOnNewSubscriber) return;

            var subscriberName = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == subscriberId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync() ?? "Пользователь";

            var profileUrl = BuildProfileUrl(subscriberName);
            var text = $"🔔 <b>{System.Net.WebUtility.HtmlEncode(subscriberName)}</b> подписался(-ась) на ваши опросы";
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("👤 Посмотреть профиль", profileUrl)
            });

            await TrySendAsync(target.TelegramChatId!, text, keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при уведомлении о новом подписчике {SubscriberId} → {TargetId}", subscriberId, targetUserId);
        }
    }

    public async Task NotifyCommentOnMyPollAsync(Guid commentId)
    {
        try
        {
            // Один запрос: получаем автора коммента, автора опроса и заголовок опроса.
            var info = await _db.Comments
                .AsNoTracking()
                .Where(c => c.Id == commentId)
                .Select(c => new
                {
                    CommentAuthorId = c.AuthorId,
                    CommentAuthorUserName = c.Author.UserName,
                    c.PollId,
                    PollAuthorId = c.Poll.AuthorId,
                    PollTitle = c.Poll.Title,
                })
                .FirstOrDefaultAsync();
            if (info is null) return;
            if (string.Equals(info.CommentAuthorId, info.PollAuthorId, StringComparison.Ordinal)) return;

            var pollAuthor = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == info.PollAuthorId)
                .Select(u => new { u.TelegramChatId, u.NotifyOnCommentOnMyPoll })
                .FirstOrDefaultAsync();
            if (pollAuthor is null || string.IsNullOrEmpty(pollAuthor.TelegramChatId)) return;
            if (!pollAuthor.NotifyOnCommentOnMyPoll) return;

            var commentAuthorName = string.IsNullOrWhiteSpace(info.CommentAuthorUserName) ? "Пользователь" : info.CommentAuthorUserName!;
            var pollTitle = info.PollTitle ?? string.Empty;
            var pollUrl = BuildPollUrl(info.PollId);

            var text = $"💬 <b>{System.Net.WebUtility.HtmlEncode(commentAuthorName)}</b> прокомментировал(а) ваш опрос «<b>{System.Net.WebUtility.HtmlEncode(pollTitle)}</b>»";
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("🗳 Открыть опрос", pollUrl)
            });

            await TrySendAsync(pollAuthor.TelegramChatId!, text, keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при уведомлении о комменте на свой опрос {CommentId}", commentId);
        }
    }

    public async Task NotifyReplyToMyCommentAsync(Guid replyCommentId)
    {
        try
        {
            var info = await _db.Comments
                .AsNoTracking()
                .Where(c => c.Id == replyCommentId && c.ParentCommentId != null)
                .Select(c => new
                {
                    ReplyAuthorId = c.AuthorId,
                    ReplyAuthorUserName = c.Author.UserName,
                    c.PollId,
                    ParentAuthorId = c.ParentComment!.AuthorId,
                    ReplyText = c.Text,
                })
                .FirstOrDefaultAsync();
            if (info is null) return;
            if (string.Equals(info.ReplyAuthorId, info.ParentAuthorId, StringComparison.Ordinal)) return;

            var parentAuthor = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == info.ParentAuthorId)
                .Select(u => new { u.TelegramChatId, u.NotifyOnReplyToMyComment })
                .FirstOrDefaultAsync();
            if (parentAuthor is null || string.IsNullOrEmpty(parentAuthor.TelegramChatId)) return;
            if (!parentAuthor.NotifyOnReplyToMyComment) return;

            var replyAuthorName = string.IsNullOrWhiteSpace(info.ReplyAuthorUserName) ? "Пользователь" : info.ReplyAuthorUserName!;
            var pollUrl = BuildPollUrl(info.PollId);

            // Текст ответа в уведомление (картинку не прикладываем — по требованию).
            // Обрезаем длинные ответы до 500 символов, чтобы TG-сообщение не вырастало
            // непредсказуемо. HTML-escape обязателен, иначе можно сломать parseMode=Html
            // или провернуть инъекцию в сообщение.
            var replyText = (info.ReplyText ?? string.Empty).Trim();
            if (replyText.Length > 500) replyText = replyText.Substring(0, 500) + "…";
            var encodedText = System.Net.WebUtility.HtmlEncode(replyText);
            var body = encodedText.Length > 0 ? $"<i>«{encodedText}»</i>" : "<i>(без текста)</i>";

            var text = $"↩️ <b>{System.Net.WebUtility.HtmlEncode(replyAuthorName)}</b> "
                     + $"ответил(а) на ваш комментарий:\n\n{body}";
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("💬 Открыть обсуждение", pollUrl)
            });

            await TrySendAsync(parentAuthor.TelegramChatId!, text, keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при уведомлении об ответе на коммент {ReplyId}", replyCommentId);
        }
    }

    public async Task NotifyLikeMilestoneAsync(string ownerUserId, LikeTarget target, Guid entityId, int count)
    {
        try
        {
            var owner = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == ownerUserId)
                .Select(u => new { u.TelegramChatId, u.NotifyOnLikeMilestone })
                .FirstOrDefaultAsync();
            if (owner is null || string.IsNullOrEmpty(owner.TelegramChatId)) return;
            if (!owner.NotifyOnLikeMilestone) return;

            string text;
            string url;
            if (target == LikeTarget.Poll)
            {
                var poll = await _db.Polls
                    .AsNoTracking()
                    .Where(p => p.Id == entityId)
                    .Select(p => new { p.Title, p.Id })
                    .FirstOrDefaultAsync();
                if (poll is null) return;
                url = BuildPollUrl(poll.Id);
                text = $"⭐ Ваш опрос «<b>{System.Net.WebUtility.HtmlEncode(poll.Title ?? string.Empty)}</b>» достиг <b>{count}</b> лайков!";
            }
            else
            {
                var c = await _db.Comments
                    .AsNoTracking()
                    .Where(cm => cm.Id == entityId)
                    .Select(cm => new { cm.PollId, cm.Text })
                    .FirstOrDefaultAsync();
                if (c is null) return;
                url = BuildPollUrl(c.PollId);
                var snippet = (c.Text ?? string.Empty).Trim();
                if (snippet.Length > 60) snippet = snippet.Substring(0, 60) + "…";
                text = $"⭐ Ваш комментарий <i>«{System.Net.WebUtility.HtmlEncode(snippet)}»</i> достиг <b>{count}</b> лайков!";
            }

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithUrl("🗳 Открыть", url)
            });

            await TrySendAsync(owner.TelegramChatId!, text, keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при уведомлении о milestone {Count} для {Target} {EntityId}", count, target, entityId);
        }
    }

    private async Task TrySendAsync(string chatId, string text, InlineKeyboardMarkup keyboard)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось отправить уведомление в чат {ChatId}", chatId);
        }
    }

    private string BuildProfileUrl(string userName)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var url = _linkGenerator.GetUriByAction(
                ctx,
                action: "Index",
                controller: "Profile",
                values: new { userName });
            if (!string.IsNullOrEmpty(url)) return NormalizeLocalhost(url);
        }
        var fallbackHost = _config["TelegramBot:PublicHost"] ?? "opinionhub.site";
        return $"https://{fallbackHost}/Profile/{Uri.EscapeDataString(userName)}";
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
