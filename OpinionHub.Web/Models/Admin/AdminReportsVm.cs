using OpinionHub.Web.Models;

namespace OpinionHub.Web.Models.Admin;

public class AdminReportsVm
{
    public List<AdminReportRowVm> Pending { get; set; } = new();
    public List<AdminReportRowVm> Reviewed { get; set; } = new();

    public int CountAll { get; set; }
    public int CountActive { get; set; }
    public int CountBanned { get; set; }
    public int CountDeleted { get; set; }
    public int CountAdmin { get; set; }
    public int PendingReports { get; set; }
    public int PendingAppeals { get; set; }
}

public class AdminReportRowVm
{
    public Guid Id { get; set; }
    public ReportTargetType TargetType { get; set; }
    public string TargetKey { get; set; } = string.Empty;
    public string? TargetLabel { get; set; }
    public string? TargetUrl { get; set; }
    /// <summary>Id автора цели — для кнопки «Забанить автора». null если автор удалён или не применимо.</summary>
    public string? TargetAuthorId { get; set; }
    public string? TargetAuthorUserName { get; set; }
    public string ReporterId { get; set; } = string.Empty;
    public string? ReporterUserName { get; set; }
    public ReportReason Reason { get; set; }
    public string? Comment { get; set; }
    public ReportStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? AdminNote { get; set; }
}
