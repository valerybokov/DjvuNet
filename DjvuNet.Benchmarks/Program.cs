using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DjvuNet.Tests;

namespace DjvuNet.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            var attr = typeof(DjvuDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            string versionStr = attr != null ? attr.InformationalVersion : typeof(DjvuDocument).Assembly.GetName().Version.ToString();

            string safeVersion = versionStr.Replace('+', '-').Replace(' ', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) safeVersion = safeVersion.Replace(c.ToString(), "");
            if (safeVersion.Length > 50)
            {
                int cutoff = safeVersion.IndexOf("_@Branch");
                if (cutoff > 0) safeVersion = safeVersion.Substring(0, cutoff);
                else safeVersion = safeVersion.Substring(0, 50);
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string suffix = $"-{safeVersion}_{timestamp}";

            string displayVersion = versionStr;
            int cutoffDisplay = displayVersion.IndexOf(" @");
            if (cutoffDisplay > 0) displayVersion = displayVersion.Substring(0, cutoffDisplay).Trim();

            string archiveDir = Path.Combine(Util.RepoRoot, "TestResults", "Benchmarks", "reports");
            Directory.CreateDirectory(archiveDir);

            foreach (var summary in summaries)
            {
                string resultsDir = summary.ResultsDirectoryPath;
                string bdnResultsSubdir = Path.Combine(resultsDir, "results");

                var allFiles = new List<string>();
                if (Directory.Exists(resultsDir)) allFiles.AddRange(Directory.GetFiles(resultsDir, "*.*", SearchOption.TopDirectoryOnly));
                if (Directory.Exists(bdnResultsSubdir)) allFiles.AddRange(Directory.GetFiles(bdnResultsSubdir, "*.*", SearchOption.TopDirectoryOnly));

                string filePrefix = "";
                if (summary.BenchmarksCases.Length > 0)
                {
                    filePrefix = summary.BenchmarksCases[0].Descriptor.Type.FullName;
                }

                foreach (var file in allFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (!string.IsNullOrEmpty(filePrefix) && !fileName.StartsWith(filePrefix)) continue;

                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string name = Path.GetFileNameWithoutExtension(file);

                    // Protect historical records from being re-processed
                    if (Util.IsArchivedBenchmarkFile(name)) continue;

                    string newName = Util.GetArchiveFileName(name, suffix);

                    // Logs go in the parent directory, reports go in the /reports/ directory
                    string targetDir = ext == ".log" ? Path.GetDirectoryName(archiveDir) : archiveDir;
                    string newFilePath = Path.Combine(targetDir, newName + ext);

                    string content = File.ReadAllText(file);

                    if (ext == ".md")
                    {
                        // Simplified, plain text formatting as requested
                        string headerMd = $"DjvuNet Benchmark Run | Version: {displayVersion} | Run Time: {timestamp}\n\n";
                        File.WriteAllText(newFilePath, headerMd + content);
                    }
                    else if (ext == ".html")
                    {
                        // 1. Inject the Meta tag into the <head> block for fast indexing
                        string metaTag = $"<meta name=\"djvunet-benchmark\" content=\"version={displayVersion};time={timestamp}\" />\n";
                        var headRegex = new Regex(@"<head[^>]*>", RegexOptions.IgnoreCase);
                        if (headRegex.IsMatch(content))
                            content = headRegex.Replace(content, match => match.Value + "\n" + metaTag, 1);
                        else
                            content = metaTag + content;

                        // 2. Inject the visual header into the <body> block
                        string headerHtml = $"<div style=\"font-family: monospace; padding-bottom: 10px;\"><b>DjvuNet Benchmark Run</b> | Version: {displayVersion} | Run Time: {timestamp}</div>\n";
                        var bodyRegex = new Regex(@"<body[^>]*>", RegexOptions.IgnoreCase);
                        if (bodyRegex.IsMatch(content))
                            content = bodyRegex.Replace(content, match => match.Value + "\n" + headerHtml, 1);
                        else
                            content = headerHtml + content;

                        File.WriteAllText(newFilePath, content);
                    }
                    else if (ext == ".csv")
                    {
                        string headerCsv = $"# DjvuNet Benchmark Run | Version: {displayVersion} | Run Time: {timestamp}\n";
                        File.WriteAllText(newFilePath, headerCsv + content);
                    }
                    else if (ext == ".json")
                    {
                        if (content.StartsWith("{"))
                        {
                            string headerJson = "{\n  \"DjvuNetVersion\": \"" + displayVersion.Replace("\"", "\\\"") + "\",\n  \"RunTime\": \"" + timestamp + "\",";
                            content = headerJson + content.Substring(1);
                        }
                        File.WriteAllText(newFilePath, content);
                    }
                    else
                    {
                        File.Copy(file, newFilePath, true);
                    }

                    File.Delete(file);
                }

                if (Directory.Exists(bdnResultsSubdir) && Directory.GetFiles(bdnResultsSubdir).Length == 0)
                {
                    Directory.Delete(bdnResultsSubdir);
                }
            }
        }
    }
}
