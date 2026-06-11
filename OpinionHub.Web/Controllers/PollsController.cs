using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Background;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Controllers;

[Authorize]
public class PollsController : Controller
{
    private readonly AiAnalyticsService _aiService;
    private readonly IPollService _pollService;
    private readonly IHubContext<PollHub> _hub;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILikeService _likes;
    private readonly ICommentService _comments;
    private readonly ILogger<PollsController> _logger;

    public PollsController(IPollService pollService, IHubContext<PollHub> hub, UserManager<ApplicationUser> userManager, AiAnalyticsService aiService, IBackgroundTaskQueue taskQueue, ILikeService likes, ICommentService comments, ILogger<PollsController> logger)
    {
        _pollService = pollService;
        _hub = hub;
        _userManager = userManager;
        _aiService = aiService;
        _taskQueue = taskQueue;
        _likes = likes;
        _comments = comments;
        _logger = logger;
    }

    private async Task<IActionResult?> RequireConfirmedEmailOrRedirectAsync(string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (await _userManager.IsEmailConfirmedAsync(user)) return null;

        TempData["EmailConfirmRequired"] = "Подтвердите почту, чтобы создавать опросы и голосовать.";
        return RedirectToPage("/Account/ConfirmEmailCode", new { area = "Identity", userId = user.Id, returnUrl = returnUrl ?? Url.Content("~/") });
    }

    public async Task<IActionResult> Create()
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Create), "Polls"));
        if (gate is not null) return gate;

        var vm = new CreatePollViewModel();

        // Префилл из AI-генерации (страница /Polls/CreateWithAi кладёт сюда JSON).
        if (TempData["AiPrefill"] is string raw && !string.IsNullOrEmpty(raw))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<GeneratedPollDto>(raw);
                if (dto is not null)
                {
                    vm.Title = dto.Title;
                    vm.Description = dto.Description;
                    vm.Options = dto.Options
                        .Select(t => new CreatePollOptionVm { Text = t })
                        .ToList();
                    while (vm.Options.Count < 2) vm.Options.Add(new CreatePollOptionVm());
                }
            }
            catch
            {
                // Битый prefill — игнорируем, рендерим пустую форму.
            }
        }

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> GeneratePollAi([FromBody] GeneratePollAiRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Topic))
            return Json(new { ok = false, error = "Укажите тему опроса." });

        var dto = await _aiService.GeneratePollAsync(req.Topic, req.OptionCount);
        if (dto is null)
            return Json(new { ok = false, error = "Не удалось сгенерировать опрос. Попробуйте ещё раз или переформулируйте тему." });

        return Json(new { ok = true, title = dto.Title, description = dto.Description, options = dto.Options });
    }

    [HttpGet]
    public async Task<IActionResult> CreateWithAi()
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(CreateWithAi), "Polls"));
        if (gate is not null) return gate;
        return View(new CreateWithAiInputVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWithAi(CreateWithAiInputVm vm)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(CreateWithAi), "Polls"));
        if (gate is not null) return gate;

        if (!ModelState.IsValid) return View(vm);

        var dto = await _aiService.GeneratePollAsync(vm.Topic, vm.OptionCount);
        if (dto is null)
        {
            ModelState.AddModelError(string.Empty, "Не удалось сгенерировать опрос. Попробуйте ещё раз или переформулируйте тему.");
            return View(vm);
        }

        TempData["AiPrefill"] = JsonSerializer.Serialize(dto);
        return RedirectToAction(nameof(Create));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePollViewModel model)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Create), "Polls"));
        if (gate is not null) return gate;

        if (!ModelState.IsValid)
        {
            // Теперь Options — это список CreatePollOptionVm, а не строк
            model.Options ??= new List<CreatePollOptionVm>();
            while (model.Options.Count < 2) model.Options.Add(new CreatePollOptionVm());
            return View(model);
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var poll = await _pollService.CreateDraftAsync(model, userId);
            if (poll.Status == PollStatus.Active)
            {
                // SignalR-broadcast только тем, кто НЕ в этой вкладке: инициатор увидит карточку через
                // server-render на /Home/Index, а клиент дополнительно дедупит по id (poll-feed.js).
                var connId = Request.Headers["X-SignalR-Connection-Id"].ToString();
                await _pollService.PublishBroadcastAsync(poll.Id, string.IsNullOrEmpty(connId) ? null : connId);

                // Telegram-постинг в канал требует pollUrl со scheme/host — формируем здесь,
                // а сам сетевой вызов уносим в очередь, чтобы не тормозить редирект.
                var pollUrl = Url.Action("Details", "Polls", new { id = poll.Id }, Request.Scheme);
                if (pollUrl is not null)
                {
                    var title = poll.Title;
                    await _taskQueue.QueueAsync((sp, ct) =>
                        sp.GetRequiredService<ITelegramNotificationService>().SendPollNotificationAsync(
                            title,
                            "Заходите проголосовать! Ваш голос очень важен.",
                            pollUrl,
                            null));
                }
            }

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(Guid id)
    {
        var viewerUserId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        if (viewerUserId is not null)
        {
            var u = await _userManager.GetUserAsync(User);
            ViewBag.IsEmailConfirmed = u?.EmailConfirmed == true;
        }

        var poll = await _pollService.GetPollDetailsAsync(id, viewerUserId);

        if (poll is not null)
        {
            ViewBag.LikeCount = await _likes.GetPollLikesCountAsync(poll.Id);
            ViewBag.IsLiked = viewerUserId is not null && await _likes.IsPollLikedAsync(viewerUserId, poll.Id);
            ViewBag.Comments = await _comments.GetCommentsTreeAsync(poll.Id, viewerUserId);
            // Просто возвращаем View. ИИ будет вызван только если юзер нажмет кнопку на странице.
            return View(poll);
        }
        if (viewerUserId is null) return Challenge();

        return Forbid();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(Guid id, List<Guid> optionIds)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _pollService.VoteAsync(id, userId, optionIds);

            var updatedPoll = await _pollService.GetPollDetailsAsync(id, userId);
            if (updatedPoll != null)
            {
                var total = updatedPoll.Votes.Count;
                var stats = updatedPoll.Options.Select(o => new
                {
                    id = o.Id,
                    count = updatedPoll.Votes.Count(v => v.Selections.Any(s => s.PollOptionId == o.Id)),
                    percent = total == 0 ? 0 : Math.Round((double)updatedPoll.Votes.Count(v => v.Selections.Any(s => s.PollOptionId == o.Id)) * 100 / total, 1)
                }).ToList();

                await _hub.Clients.Group($"poll-{id}").SendAsync("updateStats", new { Total = total, Stats = stats });
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw; // DomainExceptionFilter → 403
        }
        catch (InvalidOperationException ex)
        {
            // Доменные ошибки (`Опрос не найден`, `Срок истёк`, `Ваш голос уже учтён.`) — friendly текст.
            TempData["VoteError"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vote save failed for poll {PollId}", id);
            TempData["VoteError"] = "Не удалось сохранить голос. Попробуйте ещё раз.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Edit), "Polls", new { id }));
        if (gate is not null) return gate;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        // EntityNotFound/Forbidden/InvalidOp перехватит DomainExceptionFilter (404/403/400).
        var draft = await _pollService.GetDraftForEditAsync(id, userId);
        return View(MapToEditViewModel(draft));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditPollViewModel model)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Edit), "Polls", new { id = model.Id }));
        if (gate is not null) return gate;

        if (!ModelState.IsValid)
        {
            await RehydrateEditModelAsync(model);
            while (model.Options.Count < 2) model.Options.Add(new EditPollOptionVm());
            return View(model);
        }

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var poll = await _pollService.UpdateDraftAsync(model.Id, model, userId, model.PublishNow);

            if (model.PublishNow && poll.Status == PollStatus.Active)
            {
                // Та же логика, что в Create POST для PublishNow=true: SignalR-карточка + Telegram-постинг.
                var connId = Request.Headers["X-SignalR-Connection-Id"].ToString();
                await _pollService.PublishBroadcastAsync(poll.Id, string.IsNullOrEmpty(connId) ? null : connId);

                var pollUrl = Url.Action("Details", "Polls", new { id = poll.Id }, Request.Scheme);
                if (pollUrl is not null)
                {
                    var title = poll.Title;
                    await _taskQueue.QueueAsync((sp, ct) =>
                        sp.GetRequiredService<ITelegramNotificationService>().SendPollNotificationAsync(
                            title,
                            "Заходите проголосовать! Ваш голос очень важен.",
                            pollUrl,
                            null));
                }
            }

            return RedirectToAction(nameof(Details), new { id = poll.Id });
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await RehydrateEditModelAsync(model);
            return View(model);
        }
    }

    private static EditPollViewModel MapToEditViewModel(Poll p)
    {
        return new EditPollViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            PollType = p.PollType,
            VisibilityType = p.VisibilityType,
            AudienceType = p.AudienceType,
            CanChangeVote = p.CanChangeVote,
            IsAnonymousAuthor = p.IsAnonymousAuthor,
            EndDateUtc = p.EndDateUtc,
            ExistingCoverPath = p.CoverImagePath,
            AllowedUserIds = p.AllowedUsers.Select(a => a.UserId).ToList(),
            Options = p.Options.Select(o => new EditPollOptionVm
            {
                Id = o.Id,
                Text = o.Text,
                ExistingImagePath = o.ImagePath
            }).ToList(),
            ExistingAttachments = p.Attachments.Select(a => new ExistingAttachmentVm
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                FilePath = a.FilePath,
                FileSize = a.FileSize
            }).ToList()
        };
    }

    /// <summary>
    /// При невалидном POST форма должна снова показать превью обложки/опций/аттачментов —
    /// эти поля в POST не приходят, перечитываем их из БД и проставляем в модель.
    /// </summary>
    private async Task RehydrateEditModelAsync(EditPollViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;
        Poll draft;
        try
        {
            draft = await _pollService.GetDraftForEditAsync(model.Id, userId);
        }
        catch
        {
            return;
        }

        model.ExistingCoverPath = draft.CoverImagePath;
        var optionPaths = draft.Options.ToDictionary(o => o.Id, o => o.ImagePath);
        foreach (var opt in model.Options)
        {
            if (opt.Id.HasValue && optionPaths.TryGetValue(opt.Id.Value, out var path))
                opt.ExistingImagePath = path;
        }
        model.ExistingAttachments = draft.Attachments.Select(a => new ExistingAttachmentVm
        {
            Id = a.Id,
            OriginalFileName = a.OriginalFileName,
            FilePath = a.FilePath,
            FileSize = a.FileSize
        }).ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AdminDelete(Guid id, string? returnUrl, [FromServices] IReportService reports)
    {
        // EntityNotFoundException из сервиса перехватит DomainExceptionFilter и вернёт 404.
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _pollService.AdminSoftDeleteAsync(id, adminId);
        await _hub.Clients.All.SendAsync("RemovePoll", id.ToString());
        await reports.ResolveByTargetAsync(ReportTargetType.Poll, id.ToString(), adminId, "Опрос удалён администратором");
        TempData["AdminMessage"] = "Опрос удалён.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? returnUrl = null)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;

        // EntityNotFoundException → 404, ForbiddenAccessException → 403 (через DomainExceptionFilter).
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _pollService.DeleteAsync(id, userId);

        // SignalR: удаляем у всех из ленты
        await _hub.Clients.All.SendAsync("RemovePoll", id.ToString());

        // Если нам передали ссылку для возврата (например, из профиля) — идем по ней
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Иначе (по умолчанию) идем на главную
        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> ExportCsv(Guid id)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bytes = await _pollService.ExportCsvAsync(id, userId);
        return File(bytes, "text/csv", "results.csv");
    }

    public async Task<IActionResult> ExportXlsx(Guid id)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bytes = await _pollService.ExportXlsxAsync(id, userId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "results.xlsx");
    }
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAiAnalysis(Guid id)
    {
        // viewerUserId здесь не критичен для аналитики общих результатов
        var poll = await _pollService.GetPollDetailsAsync(id, null);

        if (poll == null) return NotFound();

        var analysis = await _aiService.AnalyzePollResultsAsync(poll);

        return Json(new { analysis });
    }
}