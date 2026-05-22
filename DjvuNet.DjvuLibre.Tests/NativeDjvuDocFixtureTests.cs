#if !SKIP_NATIVE_DOCS && DJVUNET_ALL_TESTS
using System;
using DjvuNet.DjvuLibre;
using DjvuNet.Tests;
using Xunit;

namespace DjvuNet.DjvuLibre.Tests
{
    [Collection("NativeDjvuDocCollection")]
    public class NativeDjvuDocFixtureTests
    {
        private readonly NativeDjvuDocFixture _fixture;

        public NativeDjvuDocFixtureTests(NativeDjvuDocFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void GetDocument_SameIndex_ReturnsSameInstance()
        {
            // Act
            DjvuDocumentInfo doc1 = _fixture.GetDocument(1);
            DjvuDocumentInfo doc2 = _fixture.GetDocument(1);

            // Assert - Must be the exact same managed wrapper holding the same native pointer
            Assert.NotNull(doc1);
            Assert.Same(doc1, doc2);
            Assert.Equal(doc1.Document, doc2.Document);
        }

        [Fact]
        public void GetDocument_DifferentIndices_ReturnsDifferentInstances()
        {
            // Act
            DjvuDocumentInfo doc1 = _fixture.GetDocument(1);
            DjvuDocumentInfo doc2 = _fixture.GetDocument(2);

            // Assert
            Assert.NotNull(doc1);
            Assert.NotNull(doc2);
            Assert.NotSame(doc1, doc2);
            Assert.NotEqual(doc1.Document, doc2.Document);
        }
    }
}
#endif