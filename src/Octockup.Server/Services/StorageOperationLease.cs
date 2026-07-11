// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageOperationLease : IStorageOperationLease
    {
        private readonly StorageOperationCoordinator _coordinator;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StorageOperationLease> _logger;
        private readonly Guid _storageId;
        private readonly Guid _operationId;
        private readonly StorageOperationKind _kind;
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private readonly CancellationTokenSource _leaseLostCancellation = new();
        private readonly Task _heartbeatTask;
        private int _disposed;

        internal StorageOperationLease(
            StorageOperationCoordinator coordinator,
            TimeProvider timeProvider,
            ILogger<StorageOperationLease> logger,
            Guid storageId,
            Guid operationId,
            StorageOperationKind kind)
        {
            _coordinator = coordinator;
            _timeProvider = timeProvider;
            _logger = logger;
            _storageId = storageId;
            _operationId = operationId;
            _kind = kind;
            _heartbeatTask = RunHeartbeatAsync(_lifetimeCancellation.Token);
        }

        public Guid OperationId => _operationId;
        public Guid StorageId => _storageId;
        public CancellationToken LeaseLostToken => _leaseLostCancellation.Token;

        public async Task EnsureOwnedAsync(CancellationToken cancellationToken)
        {
            bool renewed = await _coordinator
                .RenewAsync(_storageId, _operationId, cancellationToken)
                .ConfigureAwait(false);

            if (renewed)
            {
                return;
            }

            MarkLeaseLost();
            LeaseLostToken.ThrowIfCancellationRequested();
        }

        private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(
                            StorageOperationCoordinator.HeartbeatInterval,
                            _timeProvider,
                            cancellationToken)
                        .ConfigureAwait(false);

                    bool renewed = await _coordinator
                        .RenewAsync(_storageId, _operationId, cancellationToken)
                        .ConfigureAwait(false);

                    if (renewed)
                    {
                        continue;
                    }

                    MarkLeaseLost();
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Storage {OperationKind} lease {OperationId} heartbeat failed for storage {StorageId}.",
                    _kind,
                    _operationId,
                    _storageId);
                MarkLeaseLost();
            }
        }

        private void MarkLeaseLost()
        {
            if (_leaseLostCancellation.IsCancellationRequested)
            {
                return;
            }

            _logger.LogError(
                "Storage {OperationKind} lease {OperationId} was lost for storage {StorageId}.",
                _kind,
                _operationId,
                _storageId);
            _leaseLostCancellation.Cancel();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
            await _heartbeatTask.ConfigureAwait(false);

            try
            {
                await _coordinator
                    .ReleaseAsync(_storageId, _operationId)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to release storage {OperationKind} lease {OperationId} for storage {StorageId}.",
                    _kind,
                    _operationId,
                    _storageId);
                throw;
            }
            finally
            {
                _lifetimeCancellation.Dispose();
                _leaseLostCancellation.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
