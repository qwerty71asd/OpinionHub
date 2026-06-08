using OpinionHub.Web.Models;

namespace OpinionHub.Web.ViewModels;

public class ProfilePublicViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>Относительный путь к аватарке (/uploads/avatars/...). null — рисуем кружок с буквой.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>Описание профиля; plain text, переводы строк сохраняются через CSS white-space:pre-wrap.</summary>
    public string? Bio { get; set; }

    public int SubscribersCount { get; set; }
    public int SubscriptionsCount { get; set; }

    /// <summary>Профиль смотрит сам владелец — кнопка подписки прячется.</summary>
    public bool IsOwnProfile { get; set; }

    /// <summary>Зритель уже подписан — кнопка показывается в состоянии "Отписаться".</summary>
    public bool IsSubscribed { get; set; }

    /// <summary>Не авторизован — подписаться нельзя (кнопка прячется).</summary>
    public bool IsViewerAuthenticated { get; set; }

    public List<Poll> Polls { get; set; } = new();
    public List<Poll> VotedPolls { get; set; } = new();
    public List<Poll> LikedPolls { get; set; } = new();

    /// <summary>Черновики автора — заполняется только когда IsOwnProfile == true; иначе пустой.</summary>
    public List<Poll> Drafts { get; set; } = new();

    /// <summary>true — содержимое вкладки «Участвовал(а)» доступно зрителю (свой профиль или владелец включил).</summary>
    public bool CanSeeVotedPolls { get; set; }

    /// <summary>true — содержимое вкладки «Лайкнутые» доступно зрителю.</summary>
    public bool CanSeeLikedPolls { get; set; }

    /// <summary>Чужой профиль с выключенным флагом — показать сообщение «автор скрыл участие».</summary>
    public bool VotedHiddenByOwner { get; set; }

    /// <summary>Чужой профиль с выключенным флагом — показать сообщение «автор скрыл лайки».</summary>
    public bool LikedHiddenByOwner { get; set; }
}
