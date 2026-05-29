using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public interface ISubscriptionService
{
    Task<int> GetSubscribersCountAsync(string targetUserId);

    /// <summary>На скольких пользователей подписан данный.</summary>
    Task<int> GetSubscriptionsCountAsync(string subscriberId);

    Task<bool> IsSubscribedAsync(string subscriberId, string targetUserId);

    /// <summary>true — подписка создана; false — уже была.</summary>
    Task<bool> SubscribeAsync(string subscriberId, string targetUserId);

    /// <summary>true — подписка удалена; false — её и не было.</summary>
    Task<bool> UnsubscribeAsync(string subscriberId, string targetUserId);

    /// <summary>Кто подписан на targetUserId. Сортировка по дате подписки (свежие первыми).</summary>
    Task<List<ApplicationUser>> GetSubscribersAsync(string targetUserId);

    /// <summary>На кого подписан subscriberId. Сортировка по дате подписки (свежие первыми).</summary>
    Task<List<ApplicationUser>> GetSubscriptionsAsync(string subscriberId);
}
