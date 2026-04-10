using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Threading.Tasks;
using OpinionHub.Web.Data;
using OpinionHub.Web.Models;
using OpinionHub.Web.Services;
using OpinionHub.Web.ViewModels;

namespace OpinionHub.Tests;

public class PollServiceTests
{
    private static ApplicationDbContext BuildDb()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task SingleChoiceRejectsMultipleOptions()
    {
        using var db = BuildDb();
        var svc = new PollService(db, NullLogger<PollService>.Instance);
        var poll = await svc.CreateDraftAsync(new CreatePollViewModel
        {
            Title = "Q",
            PollType = PollType.SingleChoice,
            Options = new List<string> { "A", "B" },
            PublishNow = true
        }, "author");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.VoteAsync(poll.Id, "u1", poll.Options.Select(o => o.Id).ToList()));
    }

    [Fact]
    public async Task ExpiredActivePollGetsCompleted()
    {
        using var db = BuildDb();
        db.Polls.Add(new Poll
        {
            Title = "Expired",
            AuthorId = "author",
            Status = PollStatus.Active,
            EndDateUtc = DateTime.UtcNow.AddMinutes(-1),
            Options = new List<PollOption> { new() { Text = "A" }, new() { Text = "B" } }
        });
        await db.SaveChangesAsync();

        var svc = new PollService(db, NullLogger<PollService>.Instance);
        var changed = await svc.CompleteExpiredPollsAsync();

        Assert.Equal(1, changed);
        Assert.Equal(PollStatus.Completed, db.Polls.Single().Status);
    }

    [Fact]
    public async Task AnonymousPollDisallowsSecondVoteWhenChangeDisabled()
    {
        using var db = BuildDb();
        var svc = new PollService(db, NullLogger<PollService>.Instance);
        var poll = await svc.CreateDraftAsync(new CreatePollViewModel
        {
            Title = "Anonymous",
            PollType = PollType.SingleChoice,
            VisibilityType = VisibilityType.Anonymous,
            CanChangeVote = false,
            Options = new List<string> { "A", "B" },
            PublishNow = true
        }, "author");

        var firstOptionId = poll.Options.First().Id;
        await svc.VoteAsync(poll.Id, "user-1", new[] { firstOptionId });

        var secondOptionId = poll.Options.Last().Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.VoteAsync(poll.Id, "user-1", new[] { secondOptionId }));

        var storedVote = await db.Votes.SingleAsync();
        Assert.Equal("user-1", storedVote.VoterAccountId);
        Assert.Null(storedVote.UserId);
    }
}
