#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.v3;
using Xunit.Sdk;

namespace DjvuNet.Tests.Xunit
{
    public class DjvuTheoryDiscoverer : TheoryDiscoverer
    {
        protected override ValueTask<IReadOnlyCollection<IXunitTestCase>> CreateTestCasesForDataRow(
            ITestFrameworkDiscoveryOptions discoveryOptions,
            IXunitTestMethod testMethod,
            ITheoryAttribute theoryAttribute,
            ITheoryDataRow dataRow,
            object?[] testMethodArguments)
        {
            var details = TestIntrospectionHelper.GetTestCaseDetailsForTheoryDataRow(discoveryOptions, testMethod, theoryAttribute, dataRow, testMethodArguments);
            var traits = TestIntrospectionHelper.GetTraits(testMethod, dataRow);

            string customDisplayName = details.TestCaseDisplayName;

            // Emulate old DjvuTheory behavior: Use the first argument as the identifying name
            if (testMethodArguments != null && testMethodArguments.Length > 0 && testMethodArguments[0] != null)
            {
                string firstArgStr = testMethodArguments[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(firstArgStr))
                {
                    customDisplayName = $"{testMethod.MethodName}({firstArgStr})";
                }
            }

            var testCase = new XunitTestCase(
                details.ResolvedTestMethod,
                customDisplayName,
                details.UniqueID,
                details.Explicit,
                details.SkipExceptions,
                details.SkipReason,
                details.SkipType,
                details.SkipUnless,
                details.SkipWhen,
                traits,
                testMethodArguments,
                sourceFilePath: details.SourceFilePath,
                sourceLineNumber: details.SourceLineNumber,
                timeout: details.Timeout
            );

#pragma warning disable IDE0300 // Changes the semantics
            return new ValueTask<IReadOnlyCollection<IXunitTestCase>>(new[] { testCase });
#pragma warning restore IDE0300
        }
    }
}
