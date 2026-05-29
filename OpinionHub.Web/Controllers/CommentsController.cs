using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Controllers;

[Authorize]
[Route("api/polls/{pollId:guid}/comments")]
[AutoValidateAntiforgeryToken]
public class CommentsController : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 МБ

    private readonly ICommentService _comments;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorage;
    private readonly IPartialViewRenderer _viewRenderer;
    private readonly IHubContext<PollHub> _hub;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(
        ICommentService comments,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorage,
        IPartialViewRenderer viewRenderer,
        IHubContext<PollHub> hub,
        ILogger<CommentsController> logger)
    {
        _comments = comments;
        _userManager = userManager;
        _fileStorage = fileStorage;
        _viewRenderer = viewRenderer;
        _hub = hub;
        _logger = logger;
    }

    [HttpPost("")]
    [RequestSizeLimit(MaxImageBytes + 64 * 1024)]
    public async Task<IActionResult> Create(
        Guid pollId,
        [FromForm] string? text,
        [FromForm] Guid? parentCommentId,
        [FromForm] IFormFile? image)
    {
        string? imagePath = null;
        try
        {
            // Защита от устаревшего клиента: если страница в кэше и шлёт application/json
            // вместо multipart/form-data — отдаём осмысленный 415, а не 500 со стектрейсом.
            if (!Request.HasFormContentType)
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                    new { message = "Устаревшая версия страницы. Перезагрузите её (Ctrl+F5)." });
            }

            text = (text ?? string.Empty).Trim();
            var hasImage = image is { Length: > 0 };
            if (text.Length == 0 && !hasImage)
                return BadRequest(new { message = "Введите текст комментария или прикрепите изображение." });

            if (hasImage)
            {
                if (image!.Length > MaxImageBytes)
                    return BadRequest(new { message = "Изображение должно быть не больше 5 МБ." });
                if (string.IsNullOrEmpty(image.ContentType) || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Можно прикрепить только изображение." });
            }

            var userId = _userManager.GetUserId(User)!;

            if (hasImage)
                imagePath = await _fileStorage.SaveFileAsync(image!, "comments");

            CommentNodeViewModel node;
            try
            {
                node = await _comments.CreateAsync(pollId, userId, text, parentCommentId, imagePath);
            }
            catch (InvalidOperationException ex)
            {
                if (imagePath is not null) _fileStorage.DeleteFile(imagePath);
                return BadRequest(new { message = ex.Message });
            }

            // Root → полный шаблон с секцией ответов; reply → плоский партиал, который
            // JS вставит в .replies-list корня (root узнаётся через node.RootCommentId).
            // Рендерим HTML один раз и переиспользуем для HTTP-ответа и SignalR broadcast'а —
            // чтобы Razor не пробегал дважды.
            var partialName = parentCommentId.HasValue ? "_CommentReply" : "_Comment";
            var html = await _viewRenderer.RenderAsync(partialName, node, HttpContext);

            // Broadcast зрителям опроса. Через GroupExcept исключаем самого инициатора
            // (его id приходит в заголовке X-SignalR-Connection-Id) — он уже вставит
            // коммент через AJAX-response, дубль не нужен. DOM-дедуп в клиенте остаётся
            // как fallback для случая, когда заголовок пуст (SignalR ещё не подключился).
            try
            {
                var connId = Request.Headers["X-SignalR-Connection-Id"].ToString();
                var clients = string.IsNullOrEmpty(connId)
                    ? _hub.Clients.Group($"poll-{pollId}")
                    : _hub.Clients.GroupExcept($"poll-{pollId}", new[] { connId });

                await clients.SendAsync("commentCreated", new
                {
                    html,
                    commentId = node.Id,
                    rootCommentId = node.RootCommentId,
                    isReply = parentCommentId.HasValue,
                });
            }
            catch (Exception broadcastEx)
            {
                _logger.LogWarning(broadcastEx,
                    "SignalR broadcast failed for comment {CommentId} in poll {PollId}",
                    node.Id, pollId);
            }

            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            // Catch-all: что бы ни упало в любой точке — клиент получит JSON, не HTML-страницу
            // DeveloperExceptionPage. В alert будет читаемое сообщение, в консоли — детали.
            _logger.LogError(ex, "Unhandled error in CommentsController.Create for poll {PollId}", pollId);
            if (imagePath is not null)
            {
                try { _fileStorage.DeleteFile(imagePath); } catch { /* swallow */ }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Внутренняя ошибка сервера при сохранении комментария.", details = ex.Message });
        }
    }
}
