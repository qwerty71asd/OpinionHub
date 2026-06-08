using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels.Search;

namespace OpinionHub.Web.Controllers;

/// <summary>
/// Глобальный поиск. /Search — страница результатов с табами и пагинацией;
/// /api/search/suggest — JSON для overlay-подсказок в навбаре.
/// AllowAnonymous: доступ есть и у гостей — фильтрация видимости опросов
/// делается в SearchService.SearchPollsAsync через IPollService.BuildVisiblePollsQuery.
/// </summary>
[AllowAnonymous]
public class SearchController : Controller
{
    private const int PageSize = 20;
    private const int PreviewSize = 5;
    private const int SuggestSize = 5;

    private readonly ISearchService _search;

    public SearchController(ISearchService search)
    {
        _search = search;
    }

    [HttpGet("/Search")]
    public async Task<IActionResult> Index(string? q, string tab = "all", int page = 1, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (tab != "polls" && tab != "users") tab = "all";

        var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = (q ?? string.Empty).Trim();

        var vm = new SearchPageViewModel
        {
            Query = query,
            Tab = tab,
            Page = page,
            PageSize = PageSize
        };

        if (query.Length < 2)
        {
            // Без запроса просто показываем пустую страницу с инструкцией.
            return View(vm);
        }

        var skip = (page - 1) * PageSize;

        if (tab == "polls")
        {
            var (items, total) = await _search.SearchPollsAsync(query, viewerId, skip, PageSize, ct);
            // Для счётчика «Люди (N)» в табах считаем total отдельно — items не нужны.
            var (_, userTotal) = await _search.SearchUsersAsync(query, 0, 0, ct);
            return View(new SearchPageViewModel
            {
                Query = query,
                Tab = tab,
                Page = page,
                PageSize = PageSize,
                Polls = items,
                TotalPolls = total,
                TotalUsers = userTotal,
                HasMore = skip + items.Count < total
            });
        }

        if (tab == "users")
        {
            var (items, total) = await _search.SearchUsersAsync(query, skip, PageSize, ct);
            var (_, pollTotal) = await _search.SearchPollsAsync(query, viewerId, 0, 0, ct);
            return View(new SearchPageViewModel
            {
                Query = query,
                Tab = tab,
                Page = page,
                PageSize = PageSize,
                Users = items,
                TotalUsers = total,
                TotalPolls = pollTotal,
                HasMore = skip + items.Count < total
            });
        }

        // tab == "all"
        var (pItems, pTotal) = await _search.SearchPollsAsync(query, viewerId, 0, PreviewSize, ct);
        var (uItems, uTotal) = await _search.SearchUsersAsync(query, 0, PreviewSize, ct);
        return View(new SearchPageViewModel
        {
            Query = query,
            Tab = "all",
            Page = 1,
            PageSize = PreviewSize,
            Polls = pItems,
            Users = uItems,
            TotalPolls = pTotal,
            TotalUsers = uTotal,
            HasMore = false
        });
    }

    [HttpGet("/api/search/suggest")]
    public async Task<IActionResult> Suggest(string? q, CancellationToken ct = default)
    {
        var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _search.SuggestAsync(q, viewerId, SuggestSize, ct);
        return Json(result);
    }
}
