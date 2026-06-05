namespace OpinionHub.Web.Models;

public enum AppealStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Апелляция пользователя на активный бан. Создаётся через /Appeals/Create
/// с явной авторизацией (userName+password), потому что забаненный логиниться не может.
/// </summary>
public class Appeal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Снимок BanReason на момент подачи (юзер должен видеть, что оспаривал).</summary>
    public string? BanReasonSnapshot { get; set; }

    /// <summary>Снимок BannedAt — на случай повторных банов одного и того же юзера.</summary>
    public DateTimeOffset? BanCreatedAtSnapshot { get; set; }

    public string Message { get; set; } = string.Empty;

    public AppealStatus Status { get; set; } = AppealStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewedById { get; set; }

    /// <summary>Ответ админа. До 1000 символов.</summary>
    public string? AdminResponse { get; set; }
}
