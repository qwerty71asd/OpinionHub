namespace OpinionHub.Web.ViewModels.Search;

/// <summary>
/// Минимальные поля опроса для отображения в строке результатов поиска
/// (overlay-подсказки и страница /Search). Маппится из Poll через Select,
/// чтобы не тащить из БД ничего лишнего.
/// </summary>
public record PollSearchResultItem(
    Guid Id,
    string Title,
    string? Description,
    string? CoverImagePath,
    bool IsAnonymousAuthor,
    string? AuthorUserName,
    string? AuthorAvatarPath,
    bool AuthorIsDeleted);
