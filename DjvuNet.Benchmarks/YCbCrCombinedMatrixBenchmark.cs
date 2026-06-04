using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.DjvuLibre;

namespace DjvuNet.Benchmarks
{
    public class PerOperationTimeColumn : IColumn
    {
        public string Id => nameof(PerOperationTimeColumn);
        public string ColumnName => "Time/Op (Real)";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Time;
        public string Legend => "Time per single PixelCount invocation";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            var mean = summary.Reports.FirstOrDefault(r => r.BenchmarkCase == benchmarkCase)?.ResultStatistics?.Mean;
            if (!mean.HasValue) return "N/A";

            int pixelCount = (int)benchmarkCase.Parameters["PixelCount"];
            int maxPixels = 5448 * 3686;
            int imageRatio = maxPixels / pixelCount;
            int totalOperations = imageRatio;

            double timePerOpNs = mean.Value / totalOperations;

            if (timePerOpNs >= 1_000_000) return (timePerOpNs / 1_000_000.0).ToString("N4") + " ms";
            if (timePerOpNs >= 1_000) return (timePerOpNs / 1_000.0).ToString("N4") + " us";
            return timePerOpNs.ToString("N4") + " ns";
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;
    }

    public class CustomParallelConfig : StandardConfig
    {
        public CustomParallelConfig()
        {
            AddColumn(new PerOperationTimeColumn());
        }
    }

    [Config(typeof(CustomParallelConfig))]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
    public class YCbCrCombinedMatrixBenchmark : DjvuNetBenchmarkBase
    {
        [Params(1024, 4096, 9216, 16384, 36864, 65536, 262144, 1048576, 2096704, 4194304, MaxPixels)]
        public override int PixelCount { get; set; }

        public override IEnumerable<int> ThreadCountValues => new[] { 1 };

        protected override TransformDirection BenchmarkDirection => TransformDirection.ForwardRgbToYCbCr;

        [Benchmark(Baseline = true, OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector128SingleThread()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrVector128(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector128Parallel()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrParallelVector128(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width, _options);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector256SingleThread()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrVector256(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector256Parallel()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrParallelVector256(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width, _options);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void NativeSingleThread()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);
            int outRowSizeInBytes = width;

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

                    NativeMethods.RgbToYCbCr(
                        (IntPtr)pIn, width, height, inRowSizeInBytes,
                        (IntPtr)pY, (IntPtr)pCb, (IntPtr)pCr, outRowSizeInBytes);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void ScalarSingleThread()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveTransform.Rgb2YCbCrScalar(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void MemoryBandwidthParallelVector256()
        {
            int ratio = ImageRatio;
            int width = ImageWidth;
            int height = ImageHeight;
            int outRowSizeInBytes = width;
            int rowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointersRgb2YCbCr(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

                    Parallel.For(0, height, _options, y =>
                    {
                        long inOffset = (long)y * rowSizeInBytes;
                        long outOffset = (long)y * outRowSizeInBytes;

                        Pixel* pInRow = (Pixel*)((byte*)pIn + inOffset);
                        sbyte* pYRow = pY + outOffset;
                        sbyte* pCbRow = pCb + outOffset;
                        sbyte* pCrRow = pCr + outOffset;

                        int x = 0;
                        while (x <= width - 32)
                        {
                            byte* readPtr = (byte*)pInRow;
                            var vec0 = Vector256.Load(readPtr);
                            var vec1 = Vector256.Load(readPtr + 32);
                            var vec2 = Vector256.Load(readPtr + 64);

                            vec0.AsByte().Store((byte*)pYRow);
                            vec1.AsByte().Store((byte*)pCbRow);
                            vec2.AsByte().Store((byte*)pCrRow);

                            pInRow += 32;
                            pYRow += 32;
                            pCbRow += 32;
                            pCrRow += 32;
                            x += 32;
                        }
                    });
                }
            }
        }
    }
}
