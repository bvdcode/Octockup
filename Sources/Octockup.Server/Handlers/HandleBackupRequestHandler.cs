using MediatR;
using System.Diagnostics;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Handlers
{
    public class HandleBackupRequestHandler(AppDbContext _dbContext) : IRequestHandler<HandleBackupRequest>
    {
        public async Task Handle(HandleBackupRequest request, CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            BackupTask job = await _dbContext.BackupTasks.FindAsync([request.BackupTaskId], cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Backup task with the specified ID not found: " + request.BackupTaskId);
            job.Status = BackupTaskStatus.Running;

            for (int i = 0; i < 50; i++)
            {
                job.Progress = 0.01 * i;
                job.Elapsed = stopwatch.Elapsed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await Task.Delay(1000, cancellationToken);
            }
            throw new Exception("Test exception");
        }
    }
}
