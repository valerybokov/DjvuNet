using System;
using System.Collections.Concurrent;
using DjvuNet;
using Xunit;

namespace DjvuNet.Tests
{
    public sealed class DjvuDocFixture : IDisposable
    {
        private readonly ConcurrentDictionary<int, DjvuDocument> _cache = new ConcurrentDictionary<int, DjvuDocument>();

        public DjvuDocument GetDocument(int index)
        {
            return _cache.GetOrAdd(index, i => 
            {
                // Use the internal constructor with isTesting = true to enable 
                // caching of intermediate images (Background/Foreground/Mask) 
                // in the DjvuImage class, drastically accelerating subsequent test executions.
                return new DjvuDocument(Util.GetTestFilePath(i), true);
            });
        }

        public void Dispose()
        {
            foreach (DjvuDocument doc in _cache.Values) { doc?.Dispose(); }
            _cache.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
