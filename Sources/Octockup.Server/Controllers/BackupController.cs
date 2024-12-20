using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Enums;
using Octockup.Server.Providers.Storage;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class BackupController(IEnumerable<IStorageProvider> _storageProviders) : ControllerBase
    {
        [Authorize]
        [HttpGet("status")]
        public async Task<IEnumerable<BackupStatus>> GetStatusAsync()
        {
            // mock data
            return
            [
                BackupStatus.Create(51, "Job1", DateTime.UtcNow, TimeSpan.FromMinutes(10), 
                BackupStatusType.Created, Random.Shared.NextDouble()),

                BackupStatus.Create(52, "Job2", DateTime.UtcNow.AddDays(-1), TimeSpan.FromMinutes(5), 
                BackupStatusType.Running, Random.Shared.NextDouble()),

                BackupStatus.Create(53, "Job3", DateTime.Now, TimeSpan.FromMinutes(15),
                BackupStatusType.Completed, Random.Shared.NextDouble()),

                BackupStatus.Create(56, "Job3", DateTime.Now, TimeSpan.FromMinutes(5452),
                BackupStatusType.Failed, Random.Shared.NextDouble()),
            ];
        }

        [Authorize]
        [HttpGet("providers")]
        public IEnumerable<StorageProviderInfo> GetProviders()
        {
            return _storageProviders.Select(p => new StorageProviderInfo()
            {
                Name = p.Name,
                Parameters = p.Parameters
            });
        }
    }
}
