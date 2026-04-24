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
    }
}
