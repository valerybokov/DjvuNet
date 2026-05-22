using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace DjvuNet.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddDiagnoser(MemoryDiagnoser.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(JsonExporter.Full);
            ArtifactsPath = "../docs/benchmarks/history";

            // Use In-Process toolchain to bypass the auto-generated .csproj 
            // which breaks due to our custom Directory.Build.props output paths.
            // Explicitly enable Server GC as a benchmark configuration option.
            // Increase the timeout drastically because the 34s decoding triggers the default timeout.
            var toolchain = new InProcessEmitToolchain(
                timeout: TimeSpan.FromMinutes(30),
                logOutput: true);

            AddJob(Job.ShortRun
                .WithToolchain(toolchain)
                .WithGcServer(true)
                .WithStrategy(BenchmarkDotNet.Engines.RunStrategy.ColdStart)
                .WithWarmupCount(2)
                .WithIterationCount(3));
        }
    }
}
