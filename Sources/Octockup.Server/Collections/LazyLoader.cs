using System.Collections;
using Octockup.Server.Models;

namespace Octockup.Server.Collections
{
    public class LazyLoader<T>(IEnumerable<T> lazyCollection) : IEnumerable<T>
    {
        public int Total { get; private set; } = 0;

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
