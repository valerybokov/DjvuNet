#if !SKIP_NATIVE_DOCS
using System;
using System.Collections.Concurrent;
using DjvuNet.DjvuLibre;
using DjvuNet.Tests;
using Xunit;

namespace DjvuNet.Tests
{
    public sealed class NativeDjvuDocFixture : IDisposable
    {
        private readonly ConcurrentDictionary<int, DjvuDocumentInfo> _cache = new ConcurrentDictionary<int, DjvuDocumentInfo>();

        public DjvuDocumentInfo GetDocument(int index)
        {
            return _cache.GetOrAdd(index, i => DjvuDocumentInfo.CreateDjvuDocumentInfo(Util.GetTestFilePath(i)));
        }

        public void Dispose()
        {
            foreach (DjvuDocumentInfo doc in _cache.Values) { doc?.Dispose(); }
            _cache.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
#endif