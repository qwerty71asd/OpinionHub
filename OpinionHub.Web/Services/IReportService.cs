using OpinionHub.Web.Models;

namespace OpinionHub.Web.Services;

public class ReportResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? ReportId { get; init; }

    public static ReportResult Ok(Guid id) => new() { Success = true, ReportId = id };
    public static ReportResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public interface IReportService
{
    Task<ReportResult> CreateAsync(string reporterId, ReportTargetType targetType, string targetKey, ReportReason reason, string? comment);
    Task<bool> MarkReviewedAsync(Guid reportId, string adminId, string? adminNote);
    Task<int> ResolveByTargetAsync(ReportTargetType targetType, string targetKey, string adminId, string note);
}
