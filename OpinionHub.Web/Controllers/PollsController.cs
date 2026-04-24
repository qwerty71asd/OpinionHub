using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using OpinionHub.Web.Hubs;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Controllers;

[Authorize]
public class PollsController : Controller
{
    private readonly IPollService _pollService;
    private readonly IHubContext<PollHub> _hub;
    private readonly UserManager<ApplicationUser> _userManager;

    public PollsController(IPollService pollService, IHubContext<PollHub> hub, UserManager<ApplicationUser> userManager)
    {
        _pollService = pollService;
        _hub = hub;
        _userManager = userManager;
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
        return View(new CreatePollViewModel());

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
                await _hub.Clients.All.SendAsync("ReceiveNewPoll", new
                {
                    id = poll.Id,
                    title = poll.Title,
                    author = User.Identity?.Name ?? "Аноним",
                    votesCount = 0
                });
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
        if (poll is not null) return View(poll);
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
        catch (Exception ex)
        {
            TempData["VoteError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _pollService.PublishAsync(id, userId);

        var poll = await _pollService.GetPollDetailsAsync(id, userId);
        if (poll != null)
        {
            System.Diagnostics.Debug.WriteLine($"---> SIGNALR: Отправляем новый опрос: {poll.Title}");
            await _hub.Clients.All.SendAsync("ReceiveNewPoll", new
            {
                id = poll.Id,
                title = poll.Title,
                author = User.Identity?.Name ?? "Аноним",
                votesCount = 0
            });
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("---> SIGNALR ERROR: Опрос не найден после публикации!");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? returnUrl = null)
    {
        var gate = await RequireConfirmedEmailOrRedirectAsync(Url.Action(nameof(Details), "Polls", new { id }));
        if (gate is not null) return gate;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            await _pollService.DeleteAsync(id, userId);

            // SignalR: удаляем у всех из ленты
            await _hub.Clients.All.SendAsync("RemovePoll", id.ToString());

            // Если нам передали ссылку для возврата (например, из профиля) — идем по ней
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // Иначе (по умолчанию) идем на главную
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            TempData["VoteError"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
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
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var myPolls = await _pollService.GetUserPollsAsync(userId);
        var votedPolls = await _pollService.GetVotedPollsAsync(userId);

        var viewModel = new UserProfileViewModel
        {
            UserName = user.UserName ?? "Пользователь",
            Email = user.Email ?? "",
            MyPolls = myPolls,
            VotedPolls = votedPolls
        };

        return View(viewModel);
    }

}