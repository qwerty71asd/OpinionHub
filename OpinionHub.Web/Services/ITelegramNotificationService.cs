using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace OpinionHub.Web.Services;

public interface ITelegramNotificationService
{
    Task SendPollNotificationAsync(string pollTitle, string pollDescription, string pollUrl, string? imagePath);
}
