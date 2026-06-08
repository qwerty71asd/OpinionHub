namespace OpinionHub.Web.ViewModels.Search;

/// <summary>
/// Минимальные поля пользователя для строки результатов поиска.
/// PollsCount — число опросов, опубликованных автором (видимых текущему зрителю).
/// Для подсказок overlay допустимо передавать null, чтобы не делать лишних подзапросов.
/// </summary>
public record UserSearchResultItem(
    string UserName,
    string? AvatarPath,
    string? Bio,
    int? PollsCount);
