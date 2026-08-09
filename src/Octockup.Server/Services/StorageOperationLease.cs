// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Services
{
    public class StorageOperationLease(Func<ValueTask> release) : IAsyncDisposable
    {
        private Func<ValueTask>? _release = release;

        public ValueTask DisposeAsync()
        {
            Func<ValueTask>? release = Interlocked.Exchange(ref _release, null);
            return release is null ? ValueTask.CompletedTask : release();
        }
    }
}
