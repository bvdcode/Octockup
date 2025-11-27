using Quartz;
using Octockup.Server.Database;
using Octockup.Server.Abstractions;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        AppDbContext _dbContext,
        ILogger<ExecuteBackupJob> _logger,
        IEnumerable<IBackupProvider> _providers) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation(GetType().FullName);
        }
    }
}
