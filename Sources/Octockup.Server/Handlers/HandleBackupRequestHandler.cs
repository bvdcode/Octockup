using MediatR;
using System.Diagnostics;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Handlers
{
    public class HandleBackupRequestHandler(AppDbContext _dbContext, JobCancellationService _jobCancellations) : IRequestHandler<HandleBackupRequest>
    {
        public async Task Handle(HandleBackupRequest request, CancellationToken cancellationToken)
        {
            CancellationToken token = _jobCancellations.GetCancellationToken(request.BackupTaskId);
            CancellationToken merged = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken).Token;
            Stopwatch stopwatch = Stopwatch.StartNew();
            BackupTask job = await _dbContext.BackupTasks.FindAsync([request.BackupTaskId], cancellationToken: merged)
                ?? throw new InvalidOperationException("Backup task with the specified ID not found: " + request.BackupTaskId);
            job.Status = BackupTaskStatus.Running;
            job.LastError = null;

            for (int i = 0; i < 50; i++)
            {
                job.Progress = 0.01 * i;
                job.Elapsed = stopwatch.Elapsed;
                await _dbContext.SaveChangesAsync(merged);
                await Task.Delay(1000, merged);
            }
            throw new Exception("Test exception");
        }
    }
}
