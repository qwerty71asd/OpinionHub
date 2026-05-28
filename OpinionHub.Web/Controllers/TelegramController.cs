using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Web.Controllers;

[Authorize]
public class TelegramController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITelegramLinkTokenService _tokens;
    private readonly IConfiguration _config;

    public TelegramController(
        UserManager<ApplicationUser> userManager,
        ITelegramLinkTokenService tokens,
        IConfiguration config)
    {
        _userManager = userManager;
        _tokens = tokens;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Link()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var vm = new TelegramLinkViewModel
        {
            IsLinked = !string.IsNullOrEmpty(user.TelegramChatId),
            TelegramChatId = user.TelegramChatId,
            TokenLifetime = _tokens.TokenLifetime
        };

        if (!vm.IsLinked)
        {
            var botUsername = _config["TelegramBot:BotUsername"];
            if (string.IsNullOrWhiteSpace(botUsername))
            {
                vm.ConfigError = "Не задан TelegramBot:BotUsername в конфигурации. Без него нельзя построить ссылку привязки.";
            }
            else
            {
                vm.Token = _tokens.IssueToken(user.Id);
                vm.DeepLink = $"https://t.me/{botUsername.TrimStart('@')}?start={vm.Token}";
            }
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlink()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (!string.IsNullOrEmpty(user.TelegramChatId))
        {
            user.TelegramChatId = null;
            await _userManager.UpdateAsync(user);
            TempData["TelegramMessage"] = "Telegram отвязан.";
        }

        return RedirectToAction(nameof(Link));
    }
}
