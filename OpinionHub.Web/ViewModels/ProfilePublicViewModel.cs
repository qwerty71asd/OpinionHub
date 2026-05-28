using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewModels;

public class ProfilePublicViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public int SubscribersCount { get; set; }

    /// <summary>Профиль смотрит сам владелец — кнопка подписки прячется.</summary>
    public bool IsOwnProfile { get; set; }

    /// <summary>Зритель уже подписан — кнопка показывается в состоянии "Отписаться".</summary>
    public bool IsSubscribed { get; set; }

    /// <summary>Не авторизован — подписаться нельзя (кнопка прячется).</summary>
    public bool IsViewerAuthenticated { get; set; }

    public List<Poll> Polls { get; set; } = new();
    public List<Poll> VotedPolls { get; set; } = new();
}
