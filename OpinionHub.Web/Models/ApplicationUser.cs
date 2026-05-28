using Microsoft.AspNetCore.Identity;

namespace OpinionHub.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string? TelegramChatId { get; set; }

    public ICollection<Poll> CreatedPolls { get; set; } = new List<Poll>();

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    public ICollection<UserSubscription> Subscribers { get; set; } = new List<UserSubscription>();

    public ICollection<PollLike> PollLikes { get; set; } = new List<PollLike>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();
}
