using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// JSON-API подписок на пользователя. CSRF-токен передаётся в заголовке RequestVerificationToken.
/// </summary>
[Authorize]
[Route("api/subscriptions")]
[AutoValidateAntiforgeryToken]
public class SubscriptionsController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subs;

    public SubscriptionsController(UserManager<ApplicationUser> userManager, ISubscriptionService subs)
    {
        _userManager = userManager;
        _subs = subs;
    }

    [HttpPost("{userName}")]
    public async Task<IActionResult> Subscribe(string userName)
    {
        var (subscriberId, target) = await ResolveAsync(userName);
        if (target is null) return NotFound();

        if (string.Equals(subscriberId, target.Id, StringComparison.Ordinal))
            return BadRequest(new { message = "Нельзя подписаться на самого себя." });

        await _subs.SubscribeAsync(subscriberId!, target.Id);
        var count = await _subs.GetSubscribersCountAsync(target.Id);
        return Json(new { isSubscribed = true, subscribersCount = count });
    }

    [HttpDelete("{userName}")]
    public async Task<IActionResult> Unsubscribe(string userName)
    {
        var (subscriberId, target) = await ResolveAsync(userName);
        if (target is null) return NotFound();

        await _subs.UnsubscribeAsync(subscriberId!, target.Id);
        var count = await _subs.GetSubscribersCountAsync(target.Id);
        return Json(new { isSubscribed = false, subscribersCount = count });
    }

    private async Task<(string? subscriberId, ApplicationUser? target)> ResolveAsync(string userName)
    {
        var subscriberId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(subscriberId)) return (null, null);

        var target = string.IsNullOrWhiteSpace(userName) ? null : await _userManager.FindByNameAsync(userName);
        return (subscriberId, target);
    }
}
