using Xunit;
using DjvuNet;
using System;

namespace DjvuNet.Tests
{
    public class IffParserExceptionTests
    {
        [Fact()]
        public void IffParserExceptionTest()
        {
            IffParserException exception = null;
            string testStr = "test";
            exception = new IffParserException(testStr);

            Assert.NotNull(exception);
            Assert.NotNull(exception.Message);
            Assert.Equal(testStr, exception.Message);
            Assert.Null(exception.InnerException);
        }

        [Fact()]
        public void IffParserExceptionTest1()
        {
            IffParserException exception = null;
            Exception innerException = new Exception();
            string testStr = "test";
            exception = new IffParserException(testStr, innerException);

            Assert.NotNull(exception);
            Assert.NotNull(exception.Message);
            Assert.Equal(testStr, exception.Message);
            Assert.NotNull(exception.InnerException);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact()]
        public void IffParserExceptionTest2()
        {
            IffParserException exception = null;
            exception = new IffParserException();

            Assert.NotNull(exception);
            Assert.Null(exception.InnerException);
        }
    }
}
