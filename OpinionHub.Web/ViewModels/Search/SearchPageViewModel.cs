namespace OpinionHub.Web.ViewModels.Search;

/// <summary>
/// Модель страницы /Search. Контроллер заполняет одну из коллекций
/// в зависимости от Tab, либо обе (для Tab="all" — по 5 элементов превью).
/// </summary>
public class SearchPageViewModel
{
    public string Query { get; init; } = string.Empty;

    /// <summary>"all" | "polls" | "users"</summary>
    public string Tab { get; init; } = "all";

    public IReadOnlyList<PollSearchResultItem> Polls { get; init; } = Array.Empty<PollSearchResultItem>();
    public IReadOnlyList<UserSearchResultItem> Users { get; init; } = Array.Empty<UserSearchResultItem>();

    public int TotalPolls { get; init; }
    public int TotalUsers { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>Есть ли страница (page + 1) для текущей вкладки.</summary>
    public bool HasMore { get; init; }
}
