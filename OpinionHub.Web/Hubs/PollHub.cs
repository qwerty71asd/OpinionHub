using Microsoft.AspNetCore.SignalR;

namespace OpinionHub.Web.Hubs;

public class PollHub : Hub
{
    public async Task JoinPoll(string pollId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"poll-{pollId}");
    }
}
