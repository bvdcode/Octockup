using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        [HttpGet(nameof(Status))]
        public async Task<IEnumerable<BackupStatus>> Status()
        {
            // mock data
            return new List<BackupStatus>
            {
                new BackupStatus("Job1", DateTime.UtcNow, TimeSpan.FromMinutes(10), BackupStatusType.Completed),
                new BackupStatus("Job2", DateTime.UtcNow.AddDays(-1), TimeSpan.FromMinutes(5), BackupStatusType.Running),
                new BackupStatus("Job3", DateTime.Now, TimeSpan.FromMinutes(15), BackupStatusType.Failed),
            };
        }
    }
}
