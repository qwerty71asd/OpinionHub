using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;

    public SubscriptionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int> GetSubscribersCountAsync(string targetUserId) =>
        _db.UserSubscriptions.CountAsync(s => s.TargetUserId == targetUserId);

    public Task<bool> IsSubscribedAsync(string subscriberId, string targetUserId) =>
        _db.UserSubscriptions.AnyAsync(s =>
            s.SubscriberId == subscriberId && s.TargetUserId == targetUserId);

    public async Task<bool> SubscribeAsync(string subscriberId, string targetUserId)
    {
        if (string.Equals(subscriberId, targetUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("Нельзя подписаться на самого себя.");

        var exists = await IsSubscribedAsync(subscriberId, targetUserId);
        if (exists) return false;

        _db.UserSubscriptions.Add(new UserSubscription
        {
            SubscriberId = subscriberId,
            TargetUserId = targetUserId
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string subscriberId, string targetUserId)
    {
        var row = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriberId == subscriberId && s.TargetUserId == targetUserId);
        if (row is null) return false;

        _db.UserSubscriptions.Remove(row);
        await _db.SaveChangesAsync();
        return true;
    }
}
