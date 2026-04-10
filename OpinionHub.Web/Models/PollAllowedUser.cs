namespace OpinionHub.Web.Models;

/// <summary>
/// Разрешённые участники опроса (ACL).
/// Если Poll.AudienceType == SelectedUsers, голосовать/видеть опрос могут только участники из этой таблицы (и автор).
/// </summary>
public class PollAllowedUser
{
    public Guid PollId { get; set; }
    public Poll Poll { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
