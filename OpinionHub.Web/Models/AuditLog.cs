namespace OpinionHub.Web.Models;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public Guid? PollId { get; set; }
    public string? UserId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
