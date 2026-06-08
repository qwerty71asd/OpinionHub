using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.ViewModels.Search;

namespace OpinionHub.Web.Services;

public class SearchService : ISearchService
{
    private readonly ApplicationDbContext _db;
    private readonly IPollService _polls;

    public SearchService(ApplicationDbContext db, IPollService polls)
    {
        _db = db;
        _polls = polls;
    }

    public async Task<SearchSuggestResult> SuggestAsync(string? q, string? viewerUserId, int perKind, CancellationToken ct = default)
    {
        var trimmed = (q ?? string.Empty).Trim();
        if (trimmed.Length < 2)
            return new SearchSuggestResult(trimmed, Array.Empty<PollSearchResultItem>(), Array.Empty<UserSearchResultItem>());

        var (polls, _) = await SearchPollsAsync(trimmed, viewerUserId, 0, perKind, ct);
        var (users, _) = await SearchUsersAsync(trimmed, 0, perKind, ct);
        return new SearchSuggestResult(trimmed, polls, users);
    }

    public async Task<(IReadOnlyList<PollSearchResultItem> Items, int Total)> SearchPollsAsync(
        string? q, string? viewerUserId, int skip, int take, CancellationToken ct = default)
    {
        var trimmed = (q ?? string.Empty).Trim();
        if (trimmed.Length < 2)
            return (Array.Empty<PollSearchResultItem>(), 0);

        var escaped = EscapeLike(trimmed);
        var contains = "%" + escaped + "%";
        var prefix = escaped + "%";

        // BuildVisiblePollsQuery даёт всё, что зритель имеет право увидеть.
        // Для глобального поиска дополнительно режем черновики (включая свои):
        // глобальный поиск — про публичную ленту, для своих черновиков есть профиль.
        var baseQuery = _polls.BuildVisiblePollsQuery(viewerUserId)
            .Where(p => p.Status != PollStatus.Draft)
            .Where(p => EF.Functions.ILike(p.Title, contains)
                        || (p.Description != null && EF.Functions.ILike(p.Description, contains)));

        var total = await baseQuery.CountAsync(ct);
        if (total == 0)
            return (Array.Empty<PollSearchResultItem>(), 0);

        var items = await baseQuery
            // Совпадение в начале названия — наверх, остальное — по новизне.
            .OrderByDescending(p => EF.Functions.ILike(p.Title, prefix) ? 1 : 0)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(p => new PollSearchResultItem(
                p.Id,
                p.Title,
                p.Description,
                p.CoverImagePath,
                p.IsAnonymousAuthor,
                p.Author != null ? p.Author.UserName : null,
                p.Author != null ? p.Author.AvatarPath : null,
                p.Author == null || p.Author.IsDeleted))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(IReadOnlyList<UserSearchResultItem> Items, int Total)> SearchUsersAsync(
        string? q, int skip, int take, CancellationToken ct = default)
    {
        var trimmed = (q ?? string.Empty).Trim();
        if (trimmed.Length < 2)
            return (Array.Empty<UserSearchResultItem>(), 0);

        var escaped = EscapeLike(trimmed);
        var contains = "%" + escaped + "%";
        var prefix = escaped + "%";

        var baseQuery = _db.Users
            .Where(u => !u.IsDeleted)
            .Where(u => (u.UserName != null && EF.Functions.ILike(u.UserName, contains))
                        || (u.Bio != null && EF.Functions.ILike(u.Bio, contains)));

        var total = await baseQuery.CountAsync(ct);
        if (total == 0)
            return (Array.Empty<UserSearchResultItem>(), 0);

        var items = await baseQuery
            .OrderByDescending(u => u.UserName != null && EF.Functions.ILike(u.UserName, prefix) ? 1 : 0)
            .ThenBy(u => u.UserName)
            .Skip(skip)
            .Take(take)
            .Select(u => new UserSearchResultItem(
                u.UserName!,
                u.AvatarPath,
                u.Bio,
                _db.Polls.Count(p => p.AuthorId == u.Id
                                     && !p.IsDeleted
                                     && p.Status != PollStatus.Draft
                                     && p.AudienceType == AudienceType.Everyone)))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Экранирует LIKE/ILIKE-метасимволы. Без этого ввод "%" / "_" / "\"
    /// расширил бы паттерн и отдал весь каталог либо сломал поиск.
    /// </summary>
    private static string EscapeLike(string input)
    {
        // Порядок важен: backslash первым, иначе следующие шаги его задвоят.
        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
