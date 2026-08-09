// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Services
{
    public class StorageOperationLock
    {
        private readonly SemaphoreSlim _turnstile = new(1, 1);
        private readonly SemaphoreSlim _resource = new(1, 1);
        private readonly SemaphoreSlim _readersMutex = new(1, 1);
        private int _readers;

        public async Task<StorageOperationLease> AcquireBackupAsync(CancellationToken cancellationToken)
        {
            bool turnstileAcquired = false;
            bool readersMutexAcquired = false;
            bool resourceAcquired = false;

            try
            {
                await _turnstile.WaitAsync(cancellationToken);
                turnstileAcquired = true;
                await _readersMutex.WaitAsync(cancellationToken);
                readersMutexAcquired = true;

                if (_readers == 0)
                {
                    await _resource.WaitAsync(cancellationToken);
                    resourceAcquired = true;
                }

                _readers++;
                resourceAcquired = false;
                return new StorageOperationLease(ReleaseBackupAsync);
            }
            finally
            {
                if (resourceAcquired)
                {
                    _resource.Release();
                }
                if (readersMutexAcquired)
                {
                    _readersMutex.Release();
                }
                if (turnstileAcquired)
                {
                    _turnstile.Release();
                }
            }
        }

        public StorageOperationLease? TryAcquireCleanup()
        {
            if (!_turnstile.Wait(0))
            {
                return null;
            }

            try
            {
                if (!_resource.Wait(0))
                {
                    return null;
                }

                return new StorageOperationLease(ReleaseCleanupAsync);
            }
            finally
            {
                _turnstile.Release();
            }
        }

        private async ValueTask ReleaseBackupAsync()
        {
            await _readersMutex.WaitAsync();
            try
            {
                _readers--;
                if (_readers == 0)
                {
                    _resource.Release();
                }
            }
            finally
            {
                _readersMutex.Release();
            }
        }

        private ValueTask ReleaseCleanupAsync()
        {
            _resource.Release();
            return ValueTask.CompletedTask;
        }
    }
}
