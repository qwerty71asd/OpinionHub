namespace OpinionHub.Web.Models;

public enum ReportTargetType
{
    Poll = 0,
    Comment = 1,
    User = 2
}

public enum ReportReason
{
    Spam = 0,
    Abuse = 1,
    Nsfw = 2,
    Misinformation = 3,
    Other = 99
}

public enum ReportStatus
{
    Pending = 0,
    Reviewed = 1
}

/// <summary>
/// Жалоба пользователя на опрос/комментарий/чужой профиль.
/// Хранит обобщённый TargetKey (строка с Guid или userId — в зависимости от TargetType),
/// чтобы не плодить три FK и не таскать nullable-ссылки.
/// </summary>
public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ReportTargetType TargetType { get; set; }

    /// <summary>Guid опроса/комментария или Id пользователя — зависит от TargetType.</summary>
    public string TargetKey { get; set; } = string.Empty;

    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser? Reporter { get; set; }

    public ReportReason Reason { get; set; }

    /// <summary>Свободный комментарий репортера, до 500 символов. Опционально.</summary>
    public string? Comment { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>Id админа, который пометил «рассмотрено». Без FK-навигации.</summary>
    public string? ReviewedById { get; set; }

    /// <summary>Заметка админа при рассмотрении. До 1000 символов.</summary>
    public string? AdminNote { get; set; }
}
