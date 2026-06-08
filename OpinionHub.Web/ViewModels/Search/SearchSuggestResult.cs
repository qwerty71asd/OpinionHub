namespace OpinionHub.Web.ViewModels.Search;

/// <summary>
/// JSON-ответ для overlay-подсказок (/api/search/suggest).
/// Возвращает топ-N опросов и топ-N людей для одного и того же запроса.
/// </summary>
public record SearchSuggestResult(
    string Query,
    IReadOnlyList<PollSearchResultItem> Polls,
    IReadOnlyList<UserSearchResultItem> Users);
