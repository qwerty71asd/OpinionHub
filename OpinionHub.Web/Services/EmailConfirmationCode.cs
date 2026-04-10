using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

/// <summary>
/// Простой код подтверждения email (6 цифр) поверх AspNetUserTokens.
///
/// Почему так:
/// - не нужны новые таблицы/миграции;
/// - данные хранятся в стандартной таблице AspNetUserTokens.
/// </summary>
public static class EmailConfirmationCode
{
    public const string Provider = "EmailConfirm";
    public const string Name = "Code";

    public static string Generate6Digits()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static async Task SetAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string code, DateTime expiresUtc)
    {
        var hash = Hash(code);
        var packed = $"{hash}|{expiresUtc:O}";
        await userManager.SetAuthenticationTokenAsync(user, Provider, Name, packed);
    }

    public static async Task<(bool ok, string? error)> ValidateAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string code)
    {
        var packed = await userManager.GetAuthenticationTokenAsync(user, Provider, Name);
        if (string.IsNullOrWhiteSpace(packed))
            return (false, "Код не найден. Нажмите «Отправить код ещё раз». ");

        if (!TryUnpack(packed, out var expectedHash, out var expiresUtc))
            return (false, "Некорректный код. Отправьте новый.");

        if (DateTime.UtcNow > expiresUtc)
            return (false, "Срок действия кода истёк. Отправьте новый.");

        var actualHash = Hash(code);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHash),
                Encoding.UTF8.GetBytes(actualHash)))
        {
            return (false, "Неверный код.");
        }

        return (true, null);
    }

    public static async Task ClearAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
        => await userManager.RemoveAuthenticationTokenAsync(user, Provider, Name);

    private static bool TryUnpack(string packed, out string hash, out DateTime expiresUtc)
    {
        hash = string.Empty;
        expiresUtc = default;

        var parts = packed.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        hash = parts[0];
        return DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out expiresUtc);
    }

    private static string Hash(string code)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}
