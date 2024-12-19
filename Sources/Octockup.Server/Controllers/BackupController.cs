using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class BackupController : ControllerBase
    {
        [HttpGet(nameof(Status))]
        public async Task<IEnumerable<BackupStatus>> Status()
        {
            // mock data
            return new List<BackupStatus>
            {
                new BackupStatus(51, "Job1", DateTime.UtcNow, TimeSpan.FromMinutes(10), 
                BackupStatusType.Completed, Random.Shared.NextDouble()),
                new BackupStatus(52, "Job2", DateTime.UtcNow.AddDays(-1), TimeSpan.FromMinutes(5), 
                BackupStatusType.Running, Random.Shared.NextDouble()),
                new BackupStatus(53, "Job3", DateTime.Now, TimeSpan.FromMinutes(15), 
                BackupStatusType.Failed, Random.Shared.NextDouble()),
            };
        }
    }
}
