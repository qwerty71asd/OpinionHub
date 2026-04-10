using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// Вспомогательный API для поиска пользователей по имени (UserName).
/// Используется при создании "закрытых" опросов.
/// </summary>
[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(Array.Empty<object>());

        // Поиск по UserName (он уникальный в Identity).
        // Для PostgreSQL делаем ToLower() по обеим сторонам, чтобы не зависеть от collation.
        var qLower = q.ToLower();

        var users = await _userManager.Users
            .Where(u => u.UserName != null && u.UserName.ToLower().Contains(qLower))
            .OrderBy(u => u.UserName)
            .Take(10)
            .Select(u => new { id = u.Id, userName = u.UserName! })
            .ToListAsync();

        return Ok(users);
    }
}
