using OpinionHub.Web.ViewModels.Search;

namespace OpinionHub.Web.Services;

/// <summary>
/// Глобальный поиск по опросам и аккаунтам. Видимость опросов делегирована
/// IPollService.BuildVisiblePollsQuery — единая точка правил для ленты и поиска.
/// </summary>
public interface ISearchService
{
    /// <summary>Лёгкие подсказки для overlay (по N элементов каждой коллекции).</summary>
    Task<SearchSuggestResult> SuggestAsync(string? q, string? viewerUserId, int perKind, CancellationToken ct = default);

    /// <summary>Постраничный поиск опросов. Возвращает (items, total).</summary>
    Task<(IReadOnlyList<PollSearchResultItem> Items, int Total)> SearchPollsAsync(
        string? q, string? viewerUserId, int skip, int take, CancellationToken ct = default);

    /// <summary>Постраничный поиск пользователей. PollsCount — только публичные опубликованные опросы автора.</summary>
    Task<(IReadOnlyList<UserSearchResultItem> Items, int Total)> SearchUsersAsync(
        string? q, int skip, int take, CancellationToken ct = default);
}
