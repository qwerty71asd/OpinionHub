using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

/// <summary>
/// Код подтверждения смены email (6 цифр) поверх AspNetUserTokens.
/// В отличие от <see cref="EmailConfirmationCode"/>, payload содержит ещё и новый email
/// — чтобы на странице подтверждения было что применить.
/// </summary>
public static class EmailChangeCode
{
    public const string Provider = "EmailChange";
    public const string Name = "CodeAndEmail";

    public static string Generate6Digits()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static async Task SetAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string code,
        string newEmail,
        DateTime expiresUtc)
    {
        var hash = Hash(code);
        var packed = $"{hash}|{expiresUtc:O}|{newEmail}";
        await userManager.SetAuthenticationTokenAsync(user, Provider, Name, packed);
    }

    public static async Task<(bool ok, string? error, string? newEmail)> ValidateAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string code)
    {
        var packed = await userManager.GetAuthenticationTokenAsync(user, Provider, Name);
        if (string.IsNullOrWhiteSpace(packed))
            return (false, "Код не найден. Запросите новый.", null);

        if (!TryUnpack(packed, out var expectedHash, out var expiresUtc, out var newEmail))
            return (false, "Некорректный код. Запросите новый.", null);

        if (DateTime.UtcNow > expiresUtc)
            return (false, "Срок действия кода истёк. Запросите новый.", null);

        var actualHash = Hash(code);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHash),
                Encoding.UTF8.GetBytes(actualHash)))
        {
            return (false, "Неверный код.", null);
        }

        return (true, null, newEmail);
    }

    public static async Task<string?> PeekNewEmailAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        var packed = await userManager.GetAuthenticationTokenAsync(user, Provider, Name);
        if (string.IsNullOrWhiteSpace(packed)) return null;
        return TryUnpack(packed, out _, out _, out var newEmail) ? newEmail : null;
    }

    public static Task ClearAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
        => userManager.RemoveAuthenticationTokenAsync(user, Provider, Name);

    private static bool TryUnpack(string packed, out string hash, out DateTime expiresUtc, out string newEmail)
    {
        hash = string.Empty;
        expiresUtc = default;
        newEmail = string.Empty;

        var parts = packed.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;

        hash = parts[0];
        if (!DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out expiresUtc))
            return false;

        newEmail = parts[2];
        return true;
    }

    private static string Hash(string code)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}
