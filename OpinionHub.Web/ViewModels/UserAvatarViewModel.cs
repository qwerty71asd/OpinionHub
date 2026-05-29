namespace OpinionHub.Web.ViewModels;

/// <summary>
/// Параметры партиала Shared/_UserAvatar.cshtml. Используется в шапке профиля,
/// в строке автора опроса, в списках подписок/подписчиков.
/// </summary>
public record UserAvatarViewModel(string? UserName, string? AvatarPath, int Size = 40);
