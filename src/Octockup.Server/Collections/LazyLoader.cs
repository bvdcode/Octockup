// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Collections;
using System.Runtime.ExceptionServices;

namespace Octockup.Server.Collections
{
    public class LazyLoader<T>(IEnumerable<T> lazyCollection) : IEnumerable<T>, IDisposable
    {
        private readonly IEnumerable<T> _lazyCollection = lazyCollection ?? throw new ArgumentNullException(nameof(lazyCollection));

        private readonly object _sync = new();
        private readonly Queue<T> _buffer = new();

        private bool _loadingStarted;
        private bool _isCompleted;
        private int _totalLoaded;
        private Exception? _loadingException;
        private readonly ManualResetEventSlim _itemOrCompleted = new(initialState: false);

        public bool IsEnumerationCompleted
        {
            get
            {
                lock (_sync)
                {
                    return _isCompleted;
                }
            }
        }

        public int Total
        {
            get
            {
                lock (_sync)
                {
                    return _totalLoaded;
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            StartBackgroundLoadingIfNeeded();

            while (TryTakeNext(out T item))
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<T[]> GetBatches(int maxBatchSize, TimeSpan flushAfterIdle)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxBatchSize, 1);
            if (flushAfterIdle <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(flushAfterIdle));
            }

            StartBackgroundLoadingIfNeeded();

            while (TryTakeNext(out T first))
            {
                List<T> batch = [first];

                while (batch.Count < maxBatchSize)
                {
                    bool isCompleted;

                    lock (_sync)
                    {
                        while (batch.Count < maxBatchSize && _buffer.Count > 0)
                        {
                            batch.Add(_buffer.Dequeue());
                        }

                        isCompleted = _isCompleted;
                        if (batch.Count < maxBatchSize && !isCompleted)
                        {
                            _itemOrCompleted.Reset();
                        }
                    }

                    if (batch.Count == maxBatchSize || isCompleted)
                    {
                        break;
                    }

                    if (!_itemOrCompleted.Wait(flushAfterIdle))
                    {
                        break;
                    }
                }

                yield return batch.ToArray();
            }
        }

        private bool TryTakeNext(out T item)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_buffer.Count > 0)
                    {
                        item = _buffer.Dequeue();
                        return true;
                    }

                    if (_isCompleted)
                    {
                        if (_loadingException is not null)
                        {
                            ExceptionDispatchInfo.Capture(_loadingException).Throw();
                        }

                        item = default!;
                        return false;
                    }

                    _itemOrCompleted.Reset();
                }

                _itemOrCompleted.Wait();
            }
        }

        private void StartBackgroundLoadingIfNeeded()
        {
            lock (_sync)
            {
                if (_loadingStarted)
                {
                    return;
                }

                _loadingStarted = true;

                _ = Task.Run(() =>
                {
                    try
                    {
                        foreach (var item in _lazyCollection)
                        {
                            lock (_sync)
                            {
                                _buffer.Enqueue(item);
                                _totalLoaded++;
                                _itemOrCompleted.Set();
                            }
                        }

                        lock (_sync)
                        {
                            _isCompleted = true;
                            _itemOrCompleted.Set();
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_sync)
                        {
                            _loadingException = ex;
                            _isCompleted = true;
                            _itemOrCompleted.Set();
                        }
                    }
                });
            }
        }

        public void Dispose()
        {
            _itemOrCompleted.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
