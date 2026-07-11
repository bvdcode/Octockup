// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Abstractions
{
    public interface IStorageOperationLease : IAsyncDisposable
    {
        CancellationToken LeaseLostToken { get; }
        Task EnsureOwnedAsync(CancellationToken cancellationToken);
    }
}
