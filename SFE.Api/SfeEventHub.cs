using Microsoft.AspNetCore.SignalR;
using SFE.Application.Events;

namespace SFE.Api;

public class SfeEventHub : Hub
{
    // A POS terminal calls this method to shout a message to everyone else
    public async Task BroadcastAppEvent(AppEventArgs args)
    {
        // "ReceiveAppEvent" is the name of the function the clients will listen for
        await Clients.Others.SendAsync("ReceiveAppEvent", args);
    }
}