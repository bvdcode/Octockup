using System.Collections;
using System.Runtime.ExceptionServices;

namespace Octockup.Server.Collections
{
    public class LazyLoader<T>(IEnumerable<T> lazyCollection) : IEnumerable<T>, IDisposable
    {
        private readonly IEnumerable<T> _lazyCollection = lazyCollection ?? throw new ArgumentNullException(nameof(lazyCollection));

        private readonly object _sync = new();
        private readonly List<T> _buffer = [];

        private bool _loadingStarted;
        private bool _isCompleted;
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
                    return _buffer.Count;
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            StartBackgroundLoadingIfNeeded();

            var index = 0;

            while (true)
            {
                T item;
                bool shouldWait;

                lock (_sync)
                {
                    if (index < _buffer.Count)
                    {
                        item = _buffer[index++];
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
                        item = default!;
                    }
                }

                if (shouldWait)
                {
                    _itemOrCompleted.Wait();
                    _itemOrCompleted.Reset();
                    continue;
                }

                yield return item;
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
                                _buffer.Add(item);
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
