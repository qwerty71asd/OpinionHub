using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// JSON-API лайков. CSRF-токен передаётся в заголовке RequestVerificationToken.
/// После каждого изменения счётчика — broadcast в группу poll-{pollId} через PollHub,
/// чтобы остальные зрители видели обновление без перезагрузки.
/// </summary>
[Authorize]
[Route("api/likes")]
[AutoValidateAntiforgeryToken]
public class LikesController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILikeService _likes;
    private readonly IHubContext<PollHub> _hub;
    private readonly ILogger<LikesController> _logger;
    private readonly ITelegramNotificationService? _telegram;

    public LikesController(
        UserManager<ApplicationUser> userManager,
        ILikeService likes,
        IHubContext<PollHub> hub,
        ILogger<LikesController> logger,
        ITelegramNotificationService? telegram = null)
    {
        _userManager = userManager;
        _likes = likes;
        _hub = hub;
        _logger = logger;
        _telegram = telegram; // nullable: бот может быть не настроен в конфиге
    }

    [HttpPost("polls/{pollId:guid}")]
    public async Task<IActionResult> LikePoll(Guid pollId)
    {
        var userId = _userManager.GetUserId(User)!;
        string authorId;
        try
        {
            authorId = await _likes.LikePollAsync(userId, pollId);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var count = await _likes.GetPollLikesCountAsync(pollId);
        await BroadcastPollLikeAsync(pollId, count);

        // Milestone-уведомление автору в Telegram. Не самому себе. Сервис сам проверит
        // привязку Telegram и флаг NotifyOnLikeMilestone.
        if (_telegram is not null
            && LikeMilestones.IsMilestone(count)
            && !string.Equals(authorId, userId, StringComparison.Ordinal))
        {
            await _telegram.NotifyLikeMilestoneAsync(authorId, LikeTarget.Poll, pollId, count);
        }

        return Json(new { liked = true, count });
    }

    [HttpDelete("polls/{pollId:guid}")]
    public async Task<IActionResult> UnlikePoll(Guid pollId)
    {
        var userId = _userManager.GetUserId(User)!;
        await _likes.UnlikePollAsync(userId, pollId);
        var count = await _likes.GetPollLikesCountAsync(pollId);
        await BroadcastPollLikeAsync(pollId, count);
        return Json(new { liked = false, count });
    }

    [HttpPost("comments/{commentId:guid}")]
    public async Task<IActionResult> LikeComment(Guid commentId)
    {
        var userId = _userManager.GetUserId(User)!;
        Guid pollId;
        string commentAuthorId;
        try
        {
            (pollId, commentAuthorId) = await _likes.LikeCommentAsync(userId, commentId);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var count = await _likes.GetCommentLikesCountAsync(commentId);
        await BroadcastCommentLikeAsync(pollId, commentId, count);

        if (_telegram is not null
            && LikeMilestones.IsMilestone(count)
            && !string.Equals(commentAuthorId, userId, StringComparison.Ordinal))
        {
            await _telegram.NotifyLikeMilestoneAsync(commentAuthorId, LikeTarget.Comment, commentId, count);
        }

        return Json(new { liked = true, count });
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> UnlikeComment(Guid commentId)
    {
        var userId = _userManager.GetUserId(User)!;
        Guid pollId;
        try
        {
            pollId = await _likes.UnlikeCommentAsync(userId, commentId);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var count = await _likes.GetCommentLikesCountAsync(commentId);
        await BroadcastCommentLikeAsync(pollId, commentId, count);
        return Json(new { liked = false, count });
    }

    // Broadcast'ы вынесены в приватные методы с try/catch — падение хаба не должно
    // ломать HTTP-ответ инициатору (лайк в БД уже сохранён).
    //
    // GroupForPoll исключает инициатора через Clients.GroupExcept, если клиент прислал
    // свой connectionId в заголовке X-SignalR-Connection-Id. У инициатора счётчик уже
    // обновлён через AJAX-response, поэтому дубль ему не нужен. Если заголовок пуст
    // (SignalR ещё не подключился) — fallback на обычный Group, а клиент сам отфильтрует
    // по локальным состояниям.
    private IClientProxy GroupForPoll(Guid pollId)
    {
        var groupName = $"poll-{pollId}";
        var connId = Request.Headers["X-SignalR-Connection-Id"].ToString();
        return string.IsNullOrEmpty(connId)
            ? _hub.Clients.Group(groupName)
            : _hub.Clients.GroupExcept(groupName, new[] { connId });
    }

    private async Task BroadcastPollLikeAsync(Guid pollId, int count)
    {
        try
        {
            await GroupForPoll(pollId).SendAsync("pollLikeUpdated", new { pollId, count });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast pollLikeUpdated failed for poll {PollId}", pollId);
        }
    }

    private async Task BroadcastCommentLikeAsync(Guid pollId, Guid commentId, int count)
    {
        try
        {
            await GroupForPoll(pollId).SendAsync("commentLikeUpdated", new { commentId, count });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast commentLikeUpdated failed for comment {CommentId}", commentId);
        }
    }
}
