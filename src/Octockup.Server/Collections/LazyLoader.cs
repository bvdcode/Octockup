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

            while (true)
            {
                T item;
                bool shouldWait;
                bool hasItem;

                lock (_sync)
                {
                    if (_buffer.Count > 0)
                    {
                        item = _buffer.Dequeue();
                        hasItem = true;
                        shouldWait = false;
                    }
                    else if (_isCompleted)
                    {
                        if (_loadingException is not null)
                        {
                            ExceptionDispatchInfo.Capture(_loadingException).Throw();
                        }

                        yield break;
                    }
                    else
                    {
                        shouldWait = true;
                        hasItem = false;
                        item = default!;
                    }
                }

                if (shouldWait)
                {
                    _itemOrCompleted.Wait();
                    _itemOrCompleted.Reset();
                    continue;
                }

                if (hasItem)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
