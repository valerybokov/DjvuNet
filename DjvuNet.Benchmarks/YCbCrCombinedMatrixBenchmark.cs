using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using DjvuNet.DjvuLibre;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;

using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

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
    public class YCbCrCombinedMatrixBenchmark
    {
        private const int InvocationCount = 15;
        private const int MaxWidth = 5448;
        private const int MaxHeight = 3686;
        private const int MaxPixels = MaxWidth * MaxHeight;

        // Varying from very small to very large to find the break-even intersection
        [Params(1024, 4096, 9216, 16384, 36864, 65536, 262144, 1048576, 2096704, 4194304, MaxPixels)]
        public int PixelCount { get; set; }

        [ParamsSource(nameof(ThreadCountValues))]
        public int ThreadCount { get; set; }
        public IEnumerable<int> ThreadCountValues => new[] { 1 } /* { Environment.ProcessorCount / 2, Environment.ProcessorCount }*/;

        public int ImageWidth =>
            PixelCount == 1024 ? 32 :
            PixelCount == 4096 ? 64 :
            PixelCount == 9216 ? 96 :
            PixelCount == 16384 ? 128 :
            PixelCount == 36864 ? 192 :
            PixelCount == 65536 ? 256 :
            PixelCount == 262144 ? 512 :
            PixelCount == 1048576 ? 1024 :
            PixelCount == 2096704 ? 1448 :
            PixelCount == 4194304 ? 2048 : MaxWidth;

        public int ImageHeight =>
            PixelCount == 1024 ? 32 :
            PixelCount == 4096 ? 64 :
            PixelCount == 9216 ? 96 :
            PixelCount == 16384 ? 128 :
            PixelCount == 36864 ? 192 :
            PixelCount == 65536 ? 256 :
            PixelCount == 262144 ? 512 :
            PixelCount == 1048576 ? 1024 :
            PixelCount == 2096704 ? 1448 :
            PixelCount == 4194304 ? 2048 : MaxHeight;

        public int ImageRatio => MaxPixels / PixelCount;

        private IntPtr[] _nativePixelBuffers;
        private IntPtr[] _nativeOutputBuffers;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private unsafe void GetIterationPointers(int invocationIndex, int ratioIndex, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr)
        {
            Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[invocationIndex].ToPointer();
            sbyte* baseY = (sbyte*)_nativeOutputBuffers[invocationIndex].ToPointer();

            sbyte* baseCb = baseY + MaxPixels;
            sbyte* baseCr = baseCb + MaxPixels;

            int pixelOffset = ratioIndex * PixelCount;

            pIn = basePixelPointer + pixelOffset;
            pY = baseY + pixelOffset;
            pCb = baseCb + pixelOffset;
            pCr = baseCr + pixelOffset;
        }

        [GlobalSetup]
        public unsafe void GlobalSetup()
        {
            long inputBytes = MaxPixels * sizeof(Pixel);
            long outputBytes = MaxPixels * 3 * sizeof(sbyte); // Y, Cb, Cr planar

            _nativePixelBuffers = new IntPtr[InvocationCount];
            _nativeOutputBuffers = new IntPtr[InvocationCount];

            // Allocate a temporary master buffer to load the image into once
            IntPtr masterInputBuffer = Marshal.AllocHGlobal((IntPtr)inputBytes);

            using (var bmp = new Bitmap(Path.Combine(Util.RepoRoot, "artifacts", "TitanIR-24bgr.png")))
            {
                var data = bmp.LockBits(new Rectangle(0, 0, MaxWidth, MaxHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                Buffer.MemoryCopy(data.Scan0.ToPointer(), masterInputBuffer.ToPointer(), inputBytes, inputBytes);
                bmp.UnlockBits(data);
            }

            for (int i = 0; i < InvocationCount; i++)
            {
                _nativePixelBuffers[i] = Marshal.AllocHGlobal((IntPtr)inputBytes);
                _nativeOutputBuffers[i] = Marshal.AllocHGlobal((IntPtr)outputBytes);

                // Copy from master to each individual invocation buffer
                Buffer.MemoryCopy(masterInputBuffer.ToPointer(), _nativePixelBuffers[i].ToPointer(), inputBytes, inputBytes);
            }

            Marshal.FreeHGlobal(masterInputBuffer);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (_nativePixelBuffers != null)
            {
                for (int i = 0; i < InvocationCount; i++)
                {
                    if (_nativePixelBuffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_nativePixelBuffers[i]);
                }
            }

            if (_nativeOutputBuffers != null)
            {
                for (int i = 0; i < InvocationCount; i++)
                {
                    if (_nativeOutputBuffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_nativeOutputBuffers[i]);
                }
            }
        }

        private ParallelOptions _options;

        [IterationSetup]
        public void IterationSetup()
        {
            _options = new ParallelOptions { MaxDegreeOfParallelism = ThreadCount };
        }

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
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrVector128(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector128Parallel()
        {
            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

                    InterWaveSimd.Rgb2YCbCrParallelVector128(pIn, width, height, inRowSizeInBytes,
                        pY, pCb, pCr, width, _options);
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
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveSimd.Rgb2YCbCrVector256(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Vector256Parallel()
        {
            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                for (int j = 0; j < ratio; j++)
                {
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

                    InterWaveSimd.Rgb2YCbCrParallelVector256(pIn, width, height, inRowSizeInBytes,
                        pY, pCb, pCr, width, _options);
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
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

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
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);
                    InterWaveTransform.Rgb2YCbCrScalar(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        // Improve hardware support requirements info
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
                    GetIterationPointers(i, j, out Pixel* pIn, out sbyte* pY, out sbyte* pCb, out sbyte* pCr);

                    Parallel.For(0, height, _options, y =>
                    {
                        long inOffset = (long)y * rowSizeInBytes;
                        long outOffset = (long)y * outRowSizeInBytes;

                        Pixel* pInRow = (Pixel*)((byte*)pIn + inOffset);
                        sbyte* pYRow = pY + outOffset;
                        sbyte* pCbRow = pCb + outOffset;
                        sbyte* pCrRow = pCr + outOffset;

                        var dummyData = Vector256<sbyte>.Zero;

                        int x = 0;
                        while (x <= width - 32)
                        {
                            // 1. READ of 32 pixels (96 bytes)
                            byte* readPtr = (byte*)pInRow;
                            var vec0 = Vector256.Load(readPtr);
                            var vec1 = Vector256.Load(readPtr + 32);
                            var vec2 = Vector256.Load(readPtr + 64);

                            // 2. WRITE to 3 separate planar locations
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