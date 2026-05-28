using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace OpinionHub.Web.Services;

public class TelegramLinkTokenService : ITelegramLinkTokenService
{
    private const string Prefix = "tg-link-token:";
    private readonly IMemoryCache _cache;

    public TimeSpan TokenLifetime => TimeSpan.FromMinutes(15);

    public TelegramLinkTokenService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string IssueToken(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("userId required", nameof(userId));

        // 16 случайных байт → 32-символьный hex. Достаточно энтропии и не вылезает за лимит deep-link param.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _cache.Set(Prefix + token, userId, TokenLifetime);
        return token;
    }

    public bool TryConsume(string token, out string userId)
    {
        userId = string.Empty;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var key = Prefix + token.Trim();
        if (!_cache.TryGetValue<string>(key, out var uid) || string.IsNullOrEmpty(uid))
            return false;

        // Удаляем сразу — токен одноразовый.
        _cache.Remove(key);
        userId = uid;
        return true;
    }
}
