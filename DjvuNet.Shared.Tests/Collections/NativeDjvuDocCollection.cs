#if !SKIP_NATIVE_DOCS
using Xunit;
using DjvuNet.Tests;

namespace DjvuNet.DjvuLibre.Tests
{
    [CollectionDefinition("NativeDjvuDocCollection")]
    public class NativeDjvuDocCollection : ICollectionFixture<NativeDjvuDocFixture> { }
}
#endif