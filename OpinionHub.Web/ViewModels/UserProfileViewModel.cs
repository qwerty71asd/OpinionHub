namespace OpinionHub.Web.ViewModels;

public class UserProfileViewModel
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public List<Models.Poll> MyPolls { get; set; }
}