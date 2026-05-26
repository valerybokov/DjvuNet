using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace DjvuNet.Benchmarks
{
    public class StandardConfig : ManualConfig
    {
        public StandardConfig()
        {
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(JsonExporter.Full);
            
            ArtifactsPath = System.IO.Path.Combine(DjvuNet.Tests.Util.RepoRoot, "TestResults", "Benchmarks", "Baselines");

            var toolchain = new InProcessEmitToolchain(
                timeout: TimeSpan.FromMinutes(30),
                logOutput: false);

            AddJob(Job.Default
                .WithToolchain(toolchain)
                .WithGcServer(true));
        }
    }
}