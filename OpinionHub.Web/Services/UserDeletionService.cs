using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public interface IUserDeletionService
{
    Task<UserDeletionResult> SoftDeleteAsync(string userId);
}

public record UserDeletionResult(bool Success, string? ErrorMessage, string OriginalName);

public class UserDeletionService : IUserDeletionService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<UserDeletionService> _logger;

    public UserDeletionService(UserManager<ApplicationUser> users, ILogger<UserDeletionService> logger)
    {
        _users = users;
        _logger = logger;
    }

    // Soft-delete: реальный DeleteAsync падает из-за FK Restrict на Comments/Polls/Likes
    // и стирает следы действий, которые нужно сохранить как «Удалённый аккаунт».
    // Вместо этого очищаем личные данные, блокируем логин и помечаем флагом IsDeleted.
    public async Task<UserDeletionResult> SoftDeleteAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
            return new UserDeletionResult(false, "Пользователь не найден.", string.Empty);

        var originalName = user.UserName ?? user.Id;

        if (user.IsDeleted)
            return new UserDeletionResult(false, "Пользователь уже удалён.", originalName);

        // Защита: нельзя оставить систему без единственного админа — паттерн как в AdminController.Edit.
        var isAdmin = await _users.IsInRoleAsync(user, Roles.Admin);
        if (isAdmin)
        {
            var admins = await _users.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
                return new UserDeletionResult(false, "Нельзя удалить единственного администратора.", originalName);
        }

        var roles = await _users.GetRolesAsync(user);
        if (roles.Count > 0)
            await _users.RemoveFromRolesAsync(user, roles);

        foreach (var login in await _users.GetLoginsAsync(user))
            await _users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);

        if (await _users.HasPasswordAsync(user))
            await _users.RemovePasswordAsync(user);

        var shortId = user.Id.Length >= 8 ? user.Id[..8] : user.Id;
        user.UserName = $"deleted_{shortId}";
        await _users.UpdateNormalizedUserNameAsync(user);

        // Email НЕ обнуляем в null: RequireUniqueEmail=true в Program.cs приводит к ошибке
        // InvalidEmail в UserValidator и UpdateAsync падает. Подставляем уникальный плейсхолдер
        // на зарезервированном RFC 2606 домене .invalid — он гарантированно не доставит почту.
        var placeholderEmail = $"deleted_{shortId}@deleted.invalid";
        user.Email = placeholderEmail;
        user.NormalizedEmail = placeholderEmail.ToUpperInvariant();
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;

        user.Bio = null;
        user.AvatarPath = null;
        user.TelegramChatId = null;
        user.NotifyAnonymousPolls = false;
        user.NotifyOnNewSubscriber = false;
        user.NotifyOnNewPollFromSubscription = false;
        user.NotifyOnCommentOnMyPoll = false;
        user.NotifyOnReplyToMyComment = false;
        user.NotifyOnLikeMilestone = false;

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        user.IsDeleted = true;
        user.DeletedAt = DateTimeOffset.UtcNow;
        user.BanReason = null;
        user.BannedAt = null;
        user.BannedById = null;

        var upd = await _users.UpdateAsync(user);
        if (!upd.Succeeded)
        {
            var message = string.Join("\n", upd.Errors.Select(e => e.Description));
            _logger.LogWarning("Soft-delete пользователя {UserId} провалился: {Errors}", userId, message);
            return new UserDeletionResult(false, message, originalName);
        }

        // Инвалидируем существующие cookie/токены: SecurityStamp обновится и Identity на следующем
        // запросе разлогинит. BannedUserMiddleware ловит это раньше через IsDeleted, но SecurityStamp — страховка.
        await _users.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Пользователь {UserId} ({OriginalName}) soft-удалён.", userId, originalName);
        return new UserDeletionResult(true, null, originalName);
    }
}
