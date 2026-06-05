namespace OpinionHub.Web.ViewModels;

/// <summary>
/// Мини-VM для общего партиала «автор опроса»: либо ссылка на профиль, либо «Аноним».
/// Используется из Home/Index, Polls/Details, Profile/Index — везде, где раньше
/// выводилось имя автора как текст.
/// </summary>
public record PollAuthorLinkViewModel(bool IsAnonymous, string? UserName, string? AvatarPath = null, bool IsDeleted = false);
