using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class EventHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // send time every 100ms while connected
            
            while (!Context.ConnectionAborted.IsCancellationRequested)
            {
                await Clients.Caller.SendAsync("Time", DateTime.UtcNow);
                await Task.Delay(100);
            }
        }
    }
}
