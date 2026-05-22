#if !SKIP_NATIVE_DOCS
using Xunit;
using DjvuNet.Tests;

namespace DjvuNet.DjvuLibre.Tests
{
    [CollectionDefinition("NativeAndManagedDocCollection")]
    public class NativeAndManagedDocCollection : ICollectionFixture<DjvuDocFixture>, ICollectionFixture<NativeDjvuDocFixture> { }
}
#endif