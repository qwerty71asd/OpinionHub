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
    private readonly ILikeService _likes;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IPollService polls,
        ISubscriptionService subs,
        ILikeService likes)
    {
        _userManager = userManager;
        _polls = polls;
        _subs = subs;
        _likes = likes;
    }

    [AllowAnonymous]
    [HttpGet("{userName}")]
    public async Task<IActionResult> Index(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return NotFound();

        var target = await _userManager.FindByNameAsync(userName);
        if (target is null) return NotFound();

        if (target.IsDeleted)
            return View("Deleted");

        var viewerId = _userManager.GetUserId(User);
        var isOwn = viewerId is not null && viewerId == target.Id;

        // Свои опросы — все (включая soft-удалённые: GetUserPollsAsync не фильтрует IsDeleted).
        var polls = await _polls.GetUserPollsAsync(target.Id);

        // Privacy: владелец видит всегда; чужие — только если владелец включил флаг.
        var canSeeVoted = isOwn || target.ShowVotedPolls;
        var canSeeLiked = isOwn || target.ShowLikedPolls;

        // Анонимные опросы скрываем при просмотре чужого профиля (НЕ раскрываем участие).
        var voted = canSeeVoted
            ? await _polls.GetVotedPollsAsync(target.Id, includeAnonymous: isOwn)
            : new List<Poll>();

        // Список лайков уже фильтрует анонимные/черновики/soft-deleted на уровне сервиса.
        var liked = canSeeLiked
            ? await _likes.GetLikedPollsAsync(target.Id)
            : new List<Poll>();

        var subscribersCount = await _subs.GetSubscribersCountAsync(target.Id);
        var subscriptionsCount = await _subs.GetSubscriptionsCountAsync(target.Id);
        var isSubscribed = viewerId is not null && !isOwn
            && await _subs.IsSubscribedAsync(viewerId, target.Id);

        return View(new ProfilePublicViewModel
        {
            UserId = target.Id,
            UserName = target.UserName ?? userName,
            AvatarPath = target.AvatarPath,
            Bio = target.Bio,
            SubscribersCount = subscribersCount,
            SubscriptionsCount = subscriptionsCount,
            IsOwnProfile = isOwn,
            IsSubscribed = isSubscribed,
            IsViewerAuthenticated = viewerId is not null,
            Polls = polls,
            VotedPolls = voted,
            LikedPolls = liked,
            CanSeeVotedPolls = canSeeVoted,
            CanSeeLikedPolls = canSeeLiked,
            VotedHiddenByOwner = !isOwn && !target.ShowVotedPolls,
            LikedHiddenByOwner = !isOwn && !target.ShowLikedPolls
        });
    }

    [AllowAnonymous]
    [HttpGet("{userName}/Subscribers")]
    public async Task<IActionResult> Subscribers(string userName)
    {
        var target = await ResolveAsync(userName);
        if (target is null) return NotFound();

        var users = await _subs.GetSubscribersAsync(target.Id);
        return View("UserList", new UserListViewModel
        {
            TargetUserName = target.UserName ?? userName,
            Title = "Подписчики",
            Users = users
                .Select(u => new UserListItem(u.UserName ?? string.Empty, u.AvatarPath))
                .Where(u => !string.IsNullOrEmpty(u.UserName))
                .ToList()
        });
    }

    [AllowAnonymous]
    [HttpGet("{userName}/Subscriptions")]
    public async Task<IActionResult> Subscriptions(string userName)
    {
        var target = await ResolveAsync(userName);
        if (target is null) return NotFound();

        var users = await _subs.GetSubscriptionsAsync(target.Id);
        return View("UserList", new UserListViewModel
        {
            TargetUserName = target.UserName ?? userName,
            Title = "Подписки",
            Users = users
                .Select(u => new UserListItem(u.UserName ?? string.Empty, u.AvatarPath))
                .Where(u => !string.IsNullOrEmpty(u.UserName))
                .ToList()
        });
    }

    private async Task<ApplicationUser?> ResolveAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;
        var u = await _userManager.FindByNameAsync(userName);
        return u is null || u.IsDeleted ? null : u;
    }
}
