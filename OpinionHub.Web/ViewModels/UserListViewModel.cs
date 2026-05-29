namespace OpinionHub.Web.ViewModels;

/// <summary>
/// VM для страниц /Profile/{name}/Subscribers и /Profile/{name}/Subscriptions.
/// Списки подписчиков/подписок видны всегда — приватность их не закрывает.
/// </summary>
public class UserListViewModel
{
    public string TargetUserName { get; set; } = string.Empty;

    /// <summary>«Подписчики» или «Подписки» — для ViewData["Title"] и заголовка страницы.</summary>
    public string Title { get; set; } = string.Empty;

    public List<UserListItem> Users { get; set; } = new();
}

public record UserListItem(string UserName, string? AvatarPath);
