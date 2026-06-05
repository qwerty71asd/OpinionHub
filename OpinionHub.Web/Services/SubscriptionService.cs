using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly ITelegramNotificationService? _telegram;

    public SubscriptionService(ApplicationDbContext db, ITelegramNotificationService? telegram = null)
    {
        _db = db;
        _telegram = telegram; // nullable: бот может быть не настроен в конфиге (TelegramBot:Token пуст)
    }

    // Удалённые аккаунты исключаем из всех публичных списков и счётчиков подписок,
    // чтобы они не светились в /Subscribers и /Subscriptions и не накручивали статистику.
    public Task<int> GetSubscribersCountAsync(string targetUserId) =>
        _db.UserSubscriptions.CountAsync(s => s.TargetUserId == targetUserId && !s.Subscriber.IsDeleted);

    public Task<int> GetSubscriptionsCountAsync(string subscriberId) =>
        _db.UserSubscriptions.CountAsync(s => s.SubscriberId == subscriberId && !s.TargetUser.IsDeleted);

    public Task<bool> IsSubscribedAsync(string subscriberId, string targetUserId) =>
        _db.UserSubscriptions.AnyAsync(s =>
            s.SubscriberId == subscriberId && s.TargetUserId == targetUserId);

    public Task<List<ApplicationUser>> GetSubscribersAsync(string targetUserId) =>
        _db.UserSubscriptions
            .Where(s => s.TargetUserId == targetUserId && !s.Subscriber.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => s.Subscriber)
            .ToListAsync();

    public Task<List<ApplicationUser>> GetSubscriptionsAsync(string subscriberId) =>
        _db.UserSubscriptions
            .Where(s => s.SubscriberId == subscriberId && !s.TargetUser.IsDeleted)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => s.TargetUser)
            .ToListAsync();

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

        // Telegram-уведомление — fire-and-forget по факту: сервис сам проверит привязку
        // и флаг NotifyOnNewSubscriber. Если бот не зарегистрирован в DI — _telegram == null.
        if (_telegram is not null)
        {
            await _telegram.NotifyNewSubscriberAsync(subscriberId, targetUserId);
        }

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
