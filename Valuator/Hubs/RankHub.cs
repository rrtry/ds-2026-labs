using Microsoft.AspNetCore.SignalR;

namespace Valuator.Hubs;

public class RankHub : Hub
{
    public async Task SubscribeToRank(string id)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, id);
    }

    public async Task UnsubscribeFromRank(string id)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, id);
    }
}