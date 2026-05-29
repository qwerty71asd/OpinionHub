using Microsoft.AspNetCore.Identity;

namespace OpinionHub.Web.Models;

public class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string? TelegramChatId { get; set; }

    /// <summary>Относительный путь к аватарке вида /uploads/avatars/&lt;guid&gt;.jpg. null — fallback на букву.</summary>
    [PersonalData]
    public string? AvatarPath { get; set; }

    /// <summary>Описание профиля (bio), plain text, до 500 символов.</summary>
    [PersonalData]
    public string? Bio { get; set; }

    /// <summary>Показывать чужим лайкнутые опросы. Default false — приватность по умолчанию.</summary>
    [PersonalData]
    public bool ShowLikedPolls { get; set; } = false;

    /// <summary>Показывать чужим список опросов, в которых пользователь голосовал. Default false.</summary>
    [PersonalData]
    public bool ShowVotedPolls { get; set; } = false;

    /// <summary>
    /// Получать ли в Telegram уведомления об **анонимных** опросах от тех, на кого подписан.
    /// По умолчанию false — на анонимные опросы рассылка не идёт. Управляется на странице
    /// /Telegram/Link через TelegramController.UpdatePreferences.
    /// </summary>
    [PersonalData]
    public bool NotifyAnonymousPolls { get; set; } = false;

    // === Per-event переключатели Telegram-уведомлений. ===
    // Все защищены проверкой "Telegram привязан" (TelegramChatId != null). Self-notifications
    // (свой коммент на свой опрос, свой лайк своего коммента) сервис отсекает сам.

    /// <summary>Кто-то подписался на меня → уведомление в Telegram. Default off.</summary>
    [PersonalData]
    public bool NotifyOnNewSubscriber { get; set; } = false;

    /// <summary>
    /// Новый опрос от того, на кого подписан → уведомление. Default ON для обратной
    /// совместимости с текущим поведением рассылки NotifySubscribersOfNewPollAsync.
    /// </summary>
    [PersonalData]
    public bool NotifyOnNewPollFromSubscription { get; set; } = true;

    /// <summary>Новый ROOT-комментарий под моим опросом (не reply). Default off.</summary>
    [PersonalData]
    public bool NotifyOnCommentOnMyPoll { get; set; } = false;

    /// <summary>Кто-то ответил на мой коммент (parent = мой коммент). Default off.</summary>
    [PersonalData]
    public bool NotifyOnReplyToMyComment { get; set; } = false;

    /// <summary>
    /// Milestone-лайки моего опроса/коммента (10/100/1000). Не каждый лайк — только
    /// при достижении порога; см. LikeMilestones.IsMilestone. Default off.
    /// </summary>
    [PersonalData]
    public bool NotifyOnLikeMilestone { get; set; } = false;

    public ICollection<Poll> CreatedPolls { get; set; } = new List<Poll>();

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    public ICollection<UserSubscription> Subscribers { get; set; } = new List<UserSubscription>();

    public ICollection<PollLike> PollLikes { get; set; } = new List<PollLike>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();
}
