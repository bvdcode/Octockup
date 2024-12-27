using MediatR;
using Octockup.Server.Database;
using Octockup.Server.Models;

namespace Octockup.Server.Handlers
{
    public class HandleBackupRequestHandler(AppDbContext _dbContext) : IRequestHandler<HandleBackupRequest>
    {
        public async Task Handle(HandleBackupRequest request, CancellationToken cancellationToken)
        {
            BackupTask? job = await _dbContext.BackupTasks.FindAsync([request.BackupTaskId], cancellationToken: cancellationToken);

            for (int i = 0; i < 50; i++)
            {
                job.Progress = 0.01 * i;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            throw new Exception("Test exception");
        }
    }
}
