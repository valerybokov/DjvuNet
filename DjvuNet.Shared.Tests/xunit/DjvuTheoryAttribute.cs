using System;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;
using Xunit.Sdk;

namespace DjvuNet.Tests.Xunit
{
    [XunitTestCaseDiscoverer(typeof(DjvuTheoryDiscoverer))]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DjvuTheoryAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : TheoryAttribute(sourceFilePath, sourceLineNumber)
    {
    }
}
