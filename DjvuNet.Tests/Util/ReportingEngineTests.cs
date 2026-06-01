using Xunit;

namespace DjvuNet.Tests.Reporting
{
    public class ReportingEngineTests
    {
        [Theory]
        // Should NOT be skipped (Native BDN files or incomplete/invalid signatures)
        [InlineData("DjvuNetBench-report-github", false)]
        [InlineData("DjvuNetBench_2026-05-28_10-00-00", false)]
        [InlineData("DjvuNetBench-0.10.26146.2_2026-05-28_10-00-00", false)] // Missing -report/-asm suffix

        // Should be skipped (True archival signatures matching BuildMajorVersion)
        [InlineData("DjvuNetBench-0.10.26146.2_2026-05-28_10-00-00-report", true)] // Fallback version
        [InlineData("DjvuNetBench-0.10.26146.2-d5580f5_2026-05-29_20-43-00-asm", true)] // Clean repo
        [InlineData("DjvuNetBench-0.10.26146.2-d5580f5-dev_2026-05-29_20-43-00-report", true)] // Dirty repo
        [InlineData("DjvuNetBench-1.0.0-beta_2026-05-29_20-43-00-asm", true)] // 3-part SemVer with pre-release
        public void IsArchivedBenchmarkFile_CorrectlyIdentifiesHistoricalRecords(string fileName, bool expectedResult)
        {
            bool result = DjvuNet.Tests.Util.IsArchivedBenchmarkFile(fileName);
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("DjvuNetBench-report-github", "-0.10.2_time", "DjvuNetBench-0.10.2_time-report")]
        [InlineData("DjvuNetBench-report-default", "-0.10.2_time", "DjvuNetBench-0.10.2_time-report")]
        [InlineData("DjvuNetBench-report", "-0.10.2_time", "DjvuNetBench-0.10.2_time-report")]
        [InlineData("DjvuNetBench-asm", "-0.10.2_time", "DjvuNetBench-0.10.2_time-asm")]
        public void GetArchiveFileName_AppendsSuffix_WithoutCorruptingBDNNaming(string input, string suffix, string expected)
        {
            string result = DjvuNet.Tests.Util.GetArchiveFileName(input, suffix);
            Assert.Equal(expected, result);
        }
    }
}