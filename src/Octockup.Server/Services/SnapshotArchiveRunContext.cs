// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveRunContext(
        SnapshotArchiveJob job,
        Guid runId,
        string fileName,
        IStorageOperationLease _storageLease) : IAsyncDisposable
    {
        public SnapshotArchiveJob Job { get; } = job;
        public Guid RunId { get; } = runId;
        public string FileName { get; } = fileName;
        public CancellationToken LeaseLostToken => _storageLease.LeaseLostToken;

        public Task EnsureLeaseOwnedAsync(CancellationToken cancellationToken)
        {
            return _storageLease.EnsureOwnedAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _storageLease.DisposeAsync();
        }
    }
}
