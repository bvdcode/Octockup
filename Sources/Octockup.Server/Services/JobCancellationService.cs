using System.Collections.Concurrent;

namespace Octockup.Server.Services
{
    public class JobCancellationService
    {
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _tokens = new();

        public CancellationToken GetCancellationToken(int id)
        {
            if (_tokens.TryGetValue(id, out var token))
            {
                return token.Token;
            }
            _tokens[id] = new CancellationTokenSource();
            return _tokens[id].Token;
        }

        public void Cancel(int id)
        {
            if (_tokens.TryRemove(id, out var tokenSource))
            {
                tokenSource.Cancel();
            }
            _tokens[id] = new CancellationTokenSource();
        }
    }
}
