using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Octockup.Server.Providers.Storage;

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

        [Authorize]
        [HttpGet("providers")]
        public IEnumerable<StorageProviderInfo> GetProviders()
        {
            return _storageProviders.Select(p => new StorageProviderInfo(p.Name, p.Parameters));
        }
    }

    public record StorageProviderInfo(string Name, IEnumerable<string> Parameters);
}
