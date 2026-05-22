#if DJVUNET_ALL_TESTS
using System;
using Xunit;

namespace DjvuNet.Tests
{
    [Collection("DjvuDocCollection")]
    public class DjvuDocFixtureTests
    {
        private readonly DjvuDocFixture _fixture;

        public DjvuDocFixtureTests(DjvuDocFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void GetDocument_SameIndex_ReturnsSameInstance()
        {
            // Act
            DjvuDocument doc1 = _fixture.GetDocument(1);
            DjvuDocument doc2 = _fixture.GetDocument(1);

            // Assert - Must be the exact same reference
            Assert.NotNull(doc1);
            Assert.Same(doc1, doc2);
        }

        [Fact]
        public void GetDocument_DifferentIndices_ReturnsDifferentInstances()
        {
            // Act
            DjvuDocument doc1 = _fixture.GetDocument(1);
            DjvuDocument doc2 = _fixture.GetDocument(2);

            // Assert - Must be different references
            Assert.NotNull(doc1);
            Assert.NotNull(doc2);
            Assert.NotSame(doc1, doc2);
        }
    }
}
#endif