using MediatR;
using System.Net;
using EasyExtensions;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Providers.Storage;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Octockup.Server.Handlers
{
    public class CreateBackupRequestHandler(IEnumerable<IStorageProvider> _providers,
        AppDbContext _dbContext, IHttpContextAccessor _contextAccessor) : IRequestHandler<CreateBackupRequest>
    {
        public Task Handle(CreateBackupRequest request, CancellationToken cancellationToken)
        {
            var foundProvider = _providers.FirstOrDefault(x => x.Name.Equals(request.Provider, StringComparison.InvariantCultureIgnoreCase))
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(CreateBackupRequest), "Provider not found");

            var foundUser = _dbContext.Users.FirstOrDefault(x => x.Id == _contextAccessor.HttpContext!.User.GetId())
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(CreateBackupRequest), "User not found");

            BackupTask backupTask = new()
            {
                Id = default,
                CreatedAt = default,
                UpdatedAt = default,
                StartAt = request.StartAt ?? DateTime.UtcNow,
                Interval = TimeSpan.FromSeconds(request.Interval),
                IsDeleted = false,
                IsEnabled = true,
                CompletedAt = null,
                Name = request.Name,
                Provider = foundProvider.Name,
                Status = BackupTaskStatus.Created,
                Progress = 0,
                UserId = foundUser.Id,
                User = foundUser,
                IsNotificationEnabled = request.IsNotificationEnabled,
            };
            backupTask.SetParameters(request.Parameters);
            _dbContext.BackupTasks.Add(backupTask);
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
