using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Controllers;

[Route("Profile")]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPollService _polls;
    private readonly ISubscriptionService _subs;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IPollService polls,
        ISubscriptionService subs)
    {
        _userManager = userManager;
        _polls = polls;
        _subs = subs;
    }

    [AllowAnonymous]
    [HttpGet("{userName}")]
    public async Task<IActionResult> Index(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return NotFound();

        var target = await _userManager.FindByNameAsync(userName);
        if (target is null) return NotFound();

        var viewerId = _userManager.GetUserId(User);
        var isOwn = viewerId is not null && viewerId == target.Id;

        // Свои опросы — все (включая soft-удалённые: GetUserPollsAsync не фильтрует IsDeleted).
        var polls = await _polls.GetUserPollsAsync(target.Id);

        // Анонимные опросы скрываем только при просмотре чужого профиля.
        var voted = await _polls.GetVotedPollsAsync(target.Id, includeAnonymous: isOwn);

        var subscribersCount = await _subs.GetSubscribersCountAsync(target.Id);
        var isSubscribed = viewerId is not null && !isOwn
            && await _subs.IsSubscribedAsync(viewerId, target.Id);

        return View(new ProfilePublicViewModel
        {
            UserId = target.Id,
            UserName = target.UserName ?? userName,
            SubscribersCount = subscribersCount,
            IsOwnProfile = isOwn,
            IsSubscribed = isSubscribed,
            IsViewerAuthenticated = viewerId is not null,
            Polls = polls,
            VotedPolls = voted
        });
    }
}
