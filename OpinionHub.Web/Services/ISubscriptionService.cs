namespace OpinionHub.Web.Services;

public interface ISubscriptionService
{
    Task<int> GetSubscribersCountAsync(string targetUserId);
    Task<bool> IsSubscribedAsync(string subscriberId, string targetUserId);

    /// <summary>true — подписка создана; false — уже была.</summary>
    Task<bool> SubscribeAsync(string subscriberId, string targetUserId);

    /// <summary>true — подписка удалена; false — её и не было.</summary>
    Task<bool> UnsubscribeAsync(string subscriberId, string targetUserId);
}
