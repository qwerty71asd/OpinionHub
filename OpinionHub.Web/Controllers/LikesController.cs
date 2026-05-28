using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// JSON-API лайков. CSRF-токен передаётся в заголовке RequestVerificationToken.
/// </summary>
[Authorize]
[Route("api/likes")]
[AutoValidateAntiforgeryToken]
public class LikesController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILikeService _likes;

    public LikesController(UserManager<ApplicationUser> userManager, ILikeService likes)
    {
        _userManager = userManager;
        _likes = likes;
    }

    [HttpPost("polls/{pollId:guid}")]
    public async Task<IActionResult> LikePoll(Guid pollId)
    {
        var userId = _userManager.GetUserId(User)!;
        try
        {
            await _likes.LikePollAsync(userId, pollId);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var count = await _likes.GetPollLikesCountAsync(pollId);
        return Json(new { liked = true, count });
    }

    [HttpDelete("polls/{pollId:guid}")]
    public async Task<IActionResult> UnlikePoll(Guid pollId)
    {
        var userId = _userManager.GetUserId(User)!;
        await _likes.UnlikePollAsync(userId, pollId);
        var count = await _likes.GetPollLikesCountAsync(pollId);
        return Json(new { liked = false, count });
    }

    [HttpPost("comments/{commentId:guid}")]
    public async Task<IActionResult> LikeComment(Guid commentId)
    {
        var userId = _userManager.GetUserId(User)!;
        try
        {
            await _likes.LikeCommentAsync(userId, commentId);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }

        var count = await _likes.GetCommentLikesCountAsync(commentId);
        return Json(new { liked = true, count });
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> UnlikeComment(Guid commentId)
    {
        var userId = _userManager.GetUserId(User)!;
        await _likes.UnlikeCommentAsync(userId, commentId);
        var count = await _likes.GetCommentLikesCountAsync(commentId);
        return Json(new { liked = false, count });
    }
}
