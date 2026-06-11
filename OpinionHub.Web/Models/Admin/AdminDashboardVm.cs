using OpinionHub.Web.Models;

namespace OpinionHub.Web.Models.Admin;

public class AdminDashboardVm
{
    public int TotalUsers { get; set; }
    public int ActiveUsersWeek { get; set; }
    public int TotalPolls { get; set; }
    public int NewPollsWeek { get; set; }
    public int TotalVotes { get; set; }
    public int VotesWeek { get; set; }
    public int PendingReports { get; set; }
    public int PendingAppeals { get; set; }

    public List<string> ChartLabels { get; set; } = new();
    public List<int> ChartData { get; set; } = new();

    public Dictionary<PollStatus, int> StatusBreakdown { get; set; } = new();

    public List<TopPollRow> TopPolls { get; set; } = new();

    public int CountAll { get; set; }
    public int CountActive { get; set; }
    public int CountBanned { get; set; }
    public int CountDeleted { get; set; }
    public int CountAdmin { get; set; }
}

public class TopPollRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int VoteCount { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
