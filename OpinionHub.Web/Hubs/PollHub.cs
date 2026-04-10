using Microsoft.AspNetCore.SignalR;

namespace OpinionHub.Web.Hubs;

public class PollHub : Hub
{
    //  лиент вызывает этот метод, когда заходит на страницу опроса
    public async Task JoinPollGroup(string pollId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"poll-{pollId}");
    }

    //  лиент уходит Ч выгон€ем из комнаты, чтобы не спамить
    public async Task LeavePollGroup(string pollId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"poll-{pollId}");
    }
}