// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Octockup.Tests
{
    internal class SaveChangesBarrierInterceptor(
        int participantCount,
        params Type[] entityTypes) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivalCount;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (entityTypes.Length > 0
                && (eventData.Context is null
                    || !eventData.Context.ChangeTracker.Entries().Any(
                        entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                            && entityTypes.Contains(entry.Metadata.ClrType))))
            {
                return result;
            }

            if (Interlocked.Increment(ref _arrivalCount) == participantCount)
            {
                _release.TrySetResult(true);
            }

            await _release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
