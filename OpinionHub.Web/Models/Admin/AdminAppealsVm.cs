using OpinionHub.Web.Models;

namespace OpinionHub.Web.Models.Admin;

public enum AdminAppealStatusFilter
{
    All,
    Pending,
    Reviewed,
    Approved,
    Rejected
}

public enum AdminAppealSort
{
    DateDesc,
    DateAsc
}

public class AdminAppealsVm
{
    public List<AdminAppealRowVm> Pending { get; set; } = new();
    public List<AdminAppealRowVm> Reviewed { get; set; } = new();

    /// <summary>Сводка по предыдущим апелляциям юзера (без учёта текущей карточки).</summary>
    public Dictionary<string, AppealHistorySummary> History { get; set; } = new();

    public int CountAll { get; set; }
    public int CountActive { get; set; }
    public int CountBanned { get; set; }
    public int CountDeleted { get; set; }
    public int CountAdmin { get; set; }
    public int PendingReports { get; set; }
    public int PendingAppeals { get; set; }

    // Текущие фильтры — для подсветки и сохранения после submit.
    public AdminAppealStatusFilter StatusFilter { get; set; } = AdminAppealStatusFilter.All;
    public string? UserQuery { get; set; }
    public AdminAppealSort Sort { get; set; } = AdminAppealSort.DateDesc;
}

public class AdminAppealRowVm
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? BanReasonSnapshot { get; set; }
    public DateTimeOffset? BanCreatedAtSnapshot { get; set; }
    public string Message { get; set; } = string.Empty;
    public AppealStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? AdminResponse { get; set; }

    /// <summary>Сейчас юзер всё ещё забанен? Может оказаться false, если бан уже снят вручную.</summary>
    public bool StillBanned { get; set; }
    public string? CurrentBanReason { get; set; }
}

public class AppealHistorySummary
{
    public int Total { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public List<AppealHistoryItem> Items { get; set; } = new();
}

public class AppealHistoryItem
{
    public Guid Id { get; set; }
    public AppealStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? AdminResponse { get; set; }
}
