namespace OpinionHub.Web.ViewModels;

public class UserProfileViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<Models.Poll> MyPolls { get; set; } = new();
    public List<Models.Poll> VotedPolls { get; set; } = new();
}