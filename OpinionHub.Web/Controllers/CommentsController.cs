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

    /// <summary>
    /// Админ-удаление комментария. Используется как из админки, так и кнопкой «Удалить»
    /// прямо со страницы опроса. Абсолютный маршрут, чтобы не наследовать api/polls/{pollId}/...
    /// </summary>
    [HttpPost("/Comments/{id:guid}/AdminDelete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdminDelete(Guid id, string? returnUrl, [FromServices] IReportService reports)
    {
        var adminId = _userManager.GetUserId(User)!;

        try
        {
            var result = await _comments.AdminDeleteAsync(id, adminId);
            if (!result.Success)
            {
                TempData["AdminError"] = result.ErrorMessage ?? "Не удалось удалить комментарий.";
            }
            else
            {
                TempData["AdminMessage"] = result.WasRoot
                    ? $"Корневой комментарий удалён, дочерних: {result.RemovedDescendantIds.Count}."
                    : "Комментарий удалён.";

                // Резолвим все pending-жалобы на этот коммент (если кто-то жаловался) — чтоб не висели.
                await reports.ResolveByTargetAsync(ReportTargetType.Comment, id.ToString(), adminId, "Комментарий удалён администратором");

                // SignalR: оповещаем зрителей о удалении — клиент уберёт ноды из DOM без перезагрузки.
                try
                {
                    var payload = new
                    {
                        commentId = id,
                        rootCommentId = result.RootCommentId,
                        isReply = !result.WasRoot,
                        removedDescendants = result.RemovedDescendantIds,
                    };
                    await _hub.Clients.Group($"poll-{result.PollId}").SendAsync("commentDeleted", payload);
                }
                catch (Exception broadcastEx)
                {
                    _logger.LogWarning(broadcastEx, "SignalR broadcast for commentDeleted failed (comment={CommentId})", id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminDelete failed for comment {CommentId}", id);
            TempData["AdminError"] = "Ошибка при удалении комментария.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Без returnUrl — пытаемся вернуться на страницу опроса, если знаем его id.
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri) &&
            string.Equals(refUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(referer);
        }
        return RedirectToAction("Index", "Home");
    }
}
