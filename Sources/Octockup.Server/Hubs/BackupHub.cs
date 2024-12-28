using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class BackupHub : Hub
    {
    }
}
