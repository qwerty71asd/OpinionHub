using Microsoft.AspNetCore.Identity;

namespace OpinionHub.Web.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<Poll> CreatedPolls { get; set; } = new List<Poll>();
}
