using Xunit;

namespace DjvuNet.Tests
{
    [CollectionDefinition("DjvuDocCollection")]
    public class DjvuDocCollection : ICollectionFixture<DjvuDocFixture> { }
}