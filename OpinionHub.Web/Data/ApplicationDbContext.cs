using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpinionHub.Web.Models;

namespace OpinionHub.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<VoteSelection> VoteSelections => Set<VoteSelection>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PollAllowedUser> PollAllowedUsers => Set<PollAllowedUser>();
    public DbSet<PollAttachment> PollAttachments { get; set; }
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PollLike> PollLikes => Set<PollLike>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentLike> CommentLikes => Set<CommentLike>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Appeal> Appeals => Set<Appeal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<VoteSelection>().HasKey(vs => new { vs.VoteId, vs.PollOptionId });

        builder.Entity<Poll>()
            .HasMany(p => p.Options)
            .WithOne(o => o.Poll)
            .HasForeignKey(o => o.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Vote>()
            .HasMany(v => v.Selections)
            .WithOne(s => s.Vote)
            .HasForeignKey(s => s.VoteId);

        builder.Entity<Poll>()
            .HasIndex(p => new { p.Status, p.EndDateUtc });

        builder.Entity<Vote>()
            .Property(v => v.VoterAccountId)
            .IsRequired();

        builder.Entity<Vote>()
            .HasIndex(v => new { v.PollId, v.VoterAccountId })
            .IsUnique();

        builder.Entity<PollAllowedUser>()
            .HasKey(x => new { x.PollId, x.UserId });

        builder.Entity<PollAllowedUser>()
            .HasOne(x => x.Poll)
            .WithMany(p => p.AllowedUsers)
            .HasForeignKey(x => x.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PollAllowedUser>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PollAttachment>()
            .HasOne(pa => pa.Poll)
            .WithMany(p => p.Attachments)
            .HasForeignKey(pa => pa.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        // === Social features ===

        // UserSubscription: композитный ключ + Restrict на обе стороны,
        // чтобы избежать множественных каскадных путей через одну и ту же таблицу пользователей.
        builder.Entity<UserSubscription>()
            .HasKey(s => new { s.SubscriberId, s.TargetUserId });

        builder.Entity<UserSubscription>()
            .HasOne(s => s.Subscriber)
            .WithMany(u => u.Subscriptions)
            .HasForeignKey(s => s.SubscriberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserSubscription>()
            .HasOne(s => s.TargetUser)
            .WithMany(u => u.Subscribers)
            .HasForeignKey(s => s.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserSubscription>()
            .HasIndex(s => s.TargetUserId);

        // PollLike: композитный ключ. Каскад от Poll, Restrict от User —
        // удаление пользователя не должно тащить каскад через несколько путей.
        builder.Entity<PollLike>()
            .HasKey(pl => new { pl.UserId, pl.PollId });

        builder.Entity<PollLike>()
            .HasOne(pl => pl.Poll)
            .WithMany()
            .HasForeignKey(pl => pl.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PollLike>()
            .HasOne(pl => pl.User)
            .WithMany(u => u.PollLikes)
            .HasForeignKey(pl => pl.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PollLike>()
            .HasIndex(pl => pl.PollId);

        // Comment: каскад от Poll; Restrict на Author и ParentComment,
        // чтобы не было цикличных/множественных каскадных путей (Poll -> Comment -> Comment).
        builder.Entity<Comment>()
            .HasOne(c => c.Poll)
            .WithMany()
            .HasForeignKey(c => c.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasOne(c => c.Author)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Comment>()
            .HasIndex(c => new { c.PollId, c.CreatedAtUtc });

        builder.Entity<Comment>()
            .HasIndex(c => c.ParentCommentId);

        // CommentLike: композитный ключ. Каскад от Comment, Restrict от User.
        builder.Entity<CommentLike>()
            .HasKey(cl => new { cl.UserId, cl.CommentId });

        builder.Entity<CommentLike>()
            .HasOne(cl => cl.Comment)
            .WithMany(c => c.Likes)
            .HasForeignKey(cl => cl.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CommentLike>()
            .HasOne(cl => cl.User)
            .WithMany(u => u.CommentLikes)
            .HasForeignKey(cl => cl.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CommentLike>()
            .HasIndex(cl => cl.CommentId);

        // === Moderation: Reports / Appeals ===

        builder.Entity<Report>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Report>()
            .HasIndex(r => new { r.Status, r.CreatedAtUtc });

        builder.Entity<Report>()
            .HasIndex(r => new { r.TargetType, r.TargetKey });

        builder.Entity<Report>()
            .Property(r => r.TargetKey)
            .HasMaxLength(64);

        builder.Entity<Report>()
            .Property(r => r.Comment)
            .HasMaxLength(500);

        builder.Entity<Report>()
            .Property(r => r.AdminNote)
            .HasMaxLength(1000);

        builder.Entity<Appeal>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Appeal>()
            .HasIndex(a => new { a.Status, a.CreatedAtUtc });

        builder.Entity<Appeal>()
            .HasIndex(a => a.UserId);

        builder.Entity<Appeal>()
            .Property(a => a.Message)
            .HasMaxLength(2000);

        builder.Entity<Appeal>()
            .Property(a => a.AdminResponse)
            .HasMaxLength(1000);

        builder.Entity<Appeal>()
            .Property(a => a.BanReasonSnapshot)
            .HasMaxLength(1000);
    }
}
