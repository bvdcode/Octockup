// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageOperationCoordinator(
        IServiceScopeFactory _scopeFactory,
        TimeProvider _timeProvider,
        ILogger<StorageOperationCoordinator> _logger,
        ILogger<StorageOperationLease> _leaseLogger) : IStorageOperationCoordinator
    {
        internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
        internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

        public async Task<IStorageOperationLease?> TryAcquireAsync(
            Guid storageId,
            StorageOperationKind kind,
            CancellationToken cancellationToken)
        {
            Guid operationId = Guid.NewGuid();
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            DateTime leaseExpiresAt = now.Add(LeaseDuration);

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int updated = await dbContext.Modules
                .Where(x =>
                    x.Id == storageId &&
                    x.Destination == ModuleDestination.Target &&
                    (x.ActiveStorageOperationId == null ||
                        x.StorageOperationLeaseExpiresAt == null ||
                        x.StorageOperationLeaseExpiresAt <= now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ActiveStorageOperationId, operationId)
                        .SetProperty(x => x.ActiveStorageOperationKind, kind)
                        .SetProperty(x => x.StorageOperationLeaseExpiresAt, leaseExpiresAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated != 1)
            {
                _logger.LogInformation(
                    "Storage {StorageId} is busy and cannot start {OperationKind}.",
                    storageId,
                    kind);
                return null;
            }

            _logger.LogInformation(
                "Acquired {OperationKind} lease {OperationId} for storage {StorageId}.",
                kind,
                operationId,
                storageId);

            return new StorageOperationLease(
                this,
                _timeProvider,
                _leaseLogger,
                storageId,
                operationId,
                kind);
        }

        internal async Task<bool> RenewAsync(
            Guid storageId,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            DateTime leaseExpiresAt = _timeProvider
                .GetUtcNow()
                .UtcDateTime
                .Add(LeaseDuration);

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int updated = await dbContext.Modules
                .Where(x =>
                    x.Id == storageId &&
                    x.ActiveStorageOperationId == operationId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.StorageOperationLeaseExpiresAt,
                        leaseExpiresAt),
                    cancellationToken)
                .ConfigureAwait(false);

            return updated == 1;
        }

        internal async Task ReleaseAsync(Guid storageId, Guid operationId)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int updated = await dbContext.Modules
                .Where(x =>
                    x.Id == storageId &&
                    x.ActiveStorageOperationId == operationId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ActiveStorageOperationId, (Guid?)null)
                        .SetProperty(x => x.ActiveStorageOperationKind, (StorageOperationKind?)null)
                        .SetProperty(x => x.StorageOperationLeaseExpiresAt, (DateTime?)null),
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (updated == 1)
            {
                _logger.LogInformation(
                    "Released storage operation lease {OperationId} for storage {StorageId}.",
                    operationId,
                    storageId);
            }
        }
    }
}
