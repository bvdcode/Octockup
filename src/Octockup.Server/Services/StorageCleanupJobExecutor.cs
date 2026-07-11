// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageCleanupJobExecutor(
        IServiceScopeFactory _scopeFactory,
        IStorageOperationCoordinator _operationCoordinator,
        StorageCleanupJobStore _jobStore,
        StorageCleanupCancellationRegistry _cancellationRegistry,
        IStorageCleanupProgressPublisher _progressPublisher,
        ILogger<StorageCleanupJobExecutor> _logger)
    {
        public async Task ExecutePendingAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<StorageCleanupJob> jobs = await _jobStore
                .GetRunnableJobsAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (StorageCleanupJob job in jobs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (job.CancellationRequested && job.RunId is null)
                {
                    StorageCleanupJobDto? canceled = await _jobStore
                        .FinalizePendingCancellationAsync(job.Id)
                        .ConfigureAwait(false);
                    if (canceled is not null)
                    {
                        await _progressPublisher
                            .PublishAsync(canceled, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    continue;
                }

                await TryExecuteJobAsync(job, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task TryExecuteJobAsync(
            StorageCleanupJob candidate,
            CancellationToken cancellationToken)
        {
            IStorageOperationLease? storageLease = await _operationCoordinator
                .TryAcquireAsync(
                    candidate.StorageId,
                    StorageOperationKind.Cleanup,
                    cancellationToken)
                .ConfigureAwait(false);

            if (storageLease is null)
            {
                return;
            }

            await using (storageLease)
            {
                StorageCleanupJob? job = await _jobStore
                    .PrepareRunAsync(candidate.Id, storageLease.OperationId, cancellationToken)
                    .ConfigureAwait(false);
                if (job is null)
                {
                    return;
                }

                using CancellationTokenSource runCancellation = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken, storageLease.LeaseLostToken);
                if (!_cancellationRegistry.TryRegister(job.Id, runCancellation))
                {
                    _logger.LogWarning(
                        "Cleanup job {JobId} is already registered in this process.",
                        job.Id);
                    return;
                }

                StorageCleanupJobState state = new(job.ToDto());

                try
                {
                    if (job.CancellationRequested)
                    {
                        runCancellation.Cancel();
                        runCancellation.Token.ThrowIfCancellationRequested();
                    }

                    await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                    StorageCleanupRunner runner = scope.ServiceProvider
                        .GetRequiredService<StorageCleanupRunner>();
                    await runner.RunAsync(
                            state,
                            (progress, ct) => ValidateAndPublishAsync(
                                progress,
                                storageLease.OperationId,
                                runCancellation,
                                ct),
                            (progress, ct) => PersistCheckpointAndPublishAsync(
                                progress,
                                storageLease.OperationId,
                                runCancellation,
                                ct),
                            storageLease,
                            runCancellation.Token)
                        .ConfigureAwait(false);

                    state.Update(x =>
                    {
                        x.Status = StorageCleanupStatus.Completed;
                        x.Phase = StorageCleanupPhase.Completed;
                        x.FinishedAt = DateTime.UtcNow;
                        x.CurrentPath = null;
                    });
                    StorageCleanupJobDto? completed = await _jobStore
                        .FinalizeRunAsync(
                            state.Snapshot(),
                            storageLease.OperationId,
                            StorageCleanupStatus.Completed,
                            null)
                        .ConfigureAwait(false);
                    if (completed is not null)
                    {
                        await _progressPublisher
                            .PublishAsync(completed, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    await HandleCancellationAsync(job, state, storageLease.OperationId)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await HandleFailureAsync(job, state, storageLease.OperationId, ex)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _cancellationRegistry.Unregister(job.Id, runCancellation);
                }
            }
        }

        private async Task ValidateAndPublishAsync(
            StorageCleanupJobDto progress,
            Guid runId,
            CancellationTokenSource runCancellation,
            CancellationToken cancellationToken)
        {
            bool canContinue = await _jobStore
                .CanContinueRunAsync(progress.JobId, runId, cancellationToken)
                .ConfigureAwait(false);
            if (!canContinue)
            {
                runCancellation.Cancel();
                runCancellation.Token.ThrowIfCancellationRequested();
            }

            await _progressPublisher
                .PublishAsync(progress, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task PersistCheckpointAndPublishAsync(
            StorageCleanupJobDto progress,
            Guid runId,
            CancellationTokenSource runCancellation,
            CancellationToken cancellationToken)
        {
            bool persisted = await _jobStore
                .UpdateProgressAsync(progress, runId, cancellationToken)
                .ConfigureAwait(false);
            if (!persisted)
            {
                runCancellation.Cancel();
                runCancellation.Token.ThrowIfCancellationRequested();
            }

            bool canContinue = await _jobStore
                .CanContinueRunAsync(progress.JobId, runId, cancellationToken)
                .ConfigureAwait(false);
            if (!canContinue)
            {
                runCancellation.Cancel();
                runCancellation.Token.ThrowIfCancellationRequested();
            }

            await _progressPublisher
                .PublishAsync(progress, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task HandleCancellationAsync(
            StorageCleanupJob job,
            StorageCleanupJobState state,
            Guid runId)
        {
            bool cancellationRequested = await _jobStore
                .IsCancellationRequestedAsync(job.Id)
                .ConfigureAwait(false);
            if (!cancellationRequested)
            {
                _logger.LogWarning(
                    "Cleanup job {JobId} was interrupted and remains available for recovery.",
                    job.Id);
                return;
            }

            state.Update(x =>
            {
                x.Status = StorageCleanupStatus.Canceled;
                x.Phase = StorageCleanupPhase.Completed;
                x.FinishedAt = DateTime.UtcNow;
                x.CurrentPath = null;
            });
            StorageCleanupJobDto? canceled = await _jobStore
                .FinalizeRunAsync(
                    state.Snapshot(),
                    runId,
                    StorageCleanupStatus.Canceled,
                    null)
                .ConfigureAwait(false);
            if (canceled is not null)
            {
                await _progressPublisher
                    .PublishAsync(canceled, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        private async Task HandleFailureAsync(
            StorageCleanupJob job,
            StorageCleanupJobState state,
            Guid runId,
            Exception exception)
        {
            _logger.LogError(
                exception,
                "Storage cleanup job {JobId} failed for storage {StorageId}.",
                job.Id,
                job.StorageId);
            string errorMessage = GetCleanupErrorMessage(exception);
            state.Update(x =>
            {
                x.Status = StorageCleanupStatus.Failed;
                x.FinishedAt = DateTime.UtcNow;
                x.ErrorMessage = errorMessage;
                x.CurrentPath = null;
            });
            StorageCleanupJobDto? failed = await _jobStore
                .FinalizeRunAsync(
                    state.Snapshot(),
                    runId,
                    StorageCleanupStatus.Failed,
                    errorMessage)
                .ConfigureAwait(false);
            if (failed is not null)
            {
                await _progressPublisher
                    .PublishAsync(failed, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        private static string GetCleanupErrorMessage(Exception exception)
        {
            Exception rootCause = exception.GetBaseException();
            if (!ReferenceEquals(rootCause, exception) &&
                !string.IsNullOrWhiteSpace(rootCause.Message))
            {
                return rootCause.Message;
            }

            return exception.Message;
        }
    }
}
