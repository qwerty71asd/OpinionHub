using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Services;

namespace OpinionHub.Web.Controllers;

public class HomeController : Controller
{
    private readonly IPollService _pollService;

    public HomeController(IPollService pollService)
    {
        _pollService = pollService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var polls = await _pollService.GetFeedAsync(userId);
        return View(polls);
    }
}
