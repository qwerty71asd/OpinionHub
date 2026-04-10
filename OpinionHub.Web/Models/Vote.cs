namespace OpinionHub.Web.Models;

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public Poll? Poll { get; set; }
    public string VoterAccountId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<VoteSelection> Selections { get; set; } = new List<VoteSelection>();
}
