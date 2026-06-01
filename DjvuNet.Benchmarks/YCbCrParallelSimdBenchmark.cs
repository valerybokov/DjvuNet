using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(CustomParallelConfig))]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
    public class YCbCrParallelSimdBenchmark
    {
        private const int InvocationCount = 10;
        private const int MaxWidth = 5448;
        private const int MaxHeight = 3686;
        private const int MaxPixels = MaxWidth * MaxHeight;

        // Varying from very small to very large to find the break-even intersection
        [Params(1024, 4096, 9216, 16384, 36864, 65536, 262144, 1048576, 2096704, 4194304, MaxPixels)]
        public int PixelCount { get; set; }

        [ParamsSource(nameof(ThreadCountValues))]
        public int ThreadCount { get; set; }
        public IEnumerable<int> ThreadCountValues => new[] { 2, 4, 6, 12} /* { Environment.ProcessorCount / 2, Environment.ProcessorCount }*/;

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
        public unsafe void HybridSingleThread()
        {
            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[i].ToPointer();
                byte* baseOutputPointer = (byte*)_nativeOutputBuffers[i].ToPointer();

                sbyte* baseY = (sbyte*)baseOutputPointer;
                sbyte* baseCb = (sbyte*)baseOutputPointer + MaxPixels;
                sbyte* baseCr = (sbyte*)baseOutputPointer + (2 * MaxPixels);

                for (int j = 0; j < ratio; j++)
                {
                    int byteOffset = j * offsetPixels;
                    Pixel* pIn = basePixelPointer + byteOffset;

                    sbyte* pY = baseY + byteOffset;
                    sbyte* pCb = baseCb + byteOffset;
                    sbyte* pCr = baseCr + byteOffset;

                    InterWaveSimd.Rgb2YCbCrHybridVector(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void HybridMultiThread_ParallelFor()
        {
            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[i].ToPointer();
                byte* baseOutputPointer = (byte*)_nativeOutputBuffers[i].ToPointer();

                sbyte* baseY = (sbyte*)baseOutputPointer;
                sbyte* baseCb = (sbyte*)baseOutputPointer + MaxPixels;
                sbyte* baseCr = (sbyte*)baseOutputPointer + (2 * MaxPixels);

                for (int j = 0; j < ratio; j++)
                {
                    int byteOffset = j * offsetPixels;
                    Pixel* pIn = basePixelPointer + byteOffset;

                    sbyte* pY = baseY + byteOffset;
                    sbyte* pCb = baseCb + byteOffset;
                    sbyte* pCr = baseCr + byteOffset;

                    InterWaveSimd.Rgb2YCbCrParallelHybridVector(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width, _options);
                }
            }
        }

        // [Benchmark(OperationsPerInvoke = InvocationCount)]
        // public unsafe void MemoryBandwidthParallelVector128()
        // {
        //     int ratio = ImageRatio;
        //     int offsetPixels = PixelCount;
        //     int width = ImageWidth;
        //     int height = ImageHeight;
        //     int outRowSizeInBytes = width;
        //     int rowSizeInBytes = width * sizeof(Pixel);
        //     var options = new ParallelOptions { MaxDegreeOfParallelism = ThreadCount };

        //     for (int i = 0; i < InvocationCount; i++)
        //     {
        //         Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[i].ToPointer();
        //         byte* baseOutputPointer = (byte*)_nativeOutputBuffers[i].ToPointer();

        //         sbyte* baseY = (sbyte*)baseOutputPointer;
        //         sbyte* baseCb = (sbyte*)baseOutputPointer + MaxPixels;
        //         sbyte* baseCr = (sbyte*)baseOutputPointer + (2 * MaxPixels);

        //         for (int j = 0; j < ratio; j++)
        //         {
        //             int byteOffset = j * offsetPixels;
        //             Pixel* pIn = basePixelPointer + byteOffset;

        //             sbyte* pY = baseY + byteOffset;
        //             sbyte* pCb = baseCb + byteOffset;
        //             sbyte* pCr = baseCr + byteOffset;

        //             Parallel.For(0, height, options, y =>
        //             {
        //                 long inOffset = (long)y * rowSizeInBytes;
        //                 long outOffset = (long)y * outRowSizeInBytes;

        //                 Pixel* pInRow = (Pixel*)((byte*)pIn + inOffset);
        //                 sbyte* pYRow = pY + outOffset;
        //                 sbyte* pCbRow = pCb + outOffset;
        //                 sbyte* pCrRow = pCr + outOffset;

        //                 var dummyData = Vector128<sbyte>.Zero;

        //                 int x = 0;
        //                 while (x <= width - 16)
        //                 {
        //                     byte* readPtr = (byte*)pInRow;
        //                     var vec0 = Vector128.Load(readPtr);
        //                     var vec1 = Vector128.Load(readPtr + 16);
        //                     var vec2 = Vector128.Load(readPtr + 32);

        //                     dummyData.AsByte().Store((byte*)pYRow);
        //                     dummyData.AsByte().Store((byte*)pCbRow);
        //                     dummyData.AsByte().Store((byte*)pCrRow);

        //                     pInRow += 16;
        //                     pYRow += 16;
        //                     pCbRow += 16;
        //                     pCrRow += 16;
        //                     x += 16;
        //                 }
        //             });
        //         }
        //     }
        // }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void MemoryBandwidthParallelVector256()
        {
            if (!Avx2.IsSupported) return;

            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int outRowSizeInBytes = width;
            int rowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[i].ToPointer();
                byte* baseOutputPointer = (byte*)_nativeOutputBuffers[i].ToPointer();

                sbyte* baseY = (sbyte*)baseOutputPointer;
                sbyte* baseCb = (sbyte*)baseOutputPointer + MaxPixels;
                sbyte* baseCr = (sbyte*)baseOutputPointer + (2 * MaxPixels);

                for (int j = 0; j < ratio; j++)
                {
                    int pixelOffset = j * offsetPixels;
                    Pixel* pIn = basePixelPointer + pixelOffset;
                    sbyte* pY = baseY + pixelOffset;
                    sbyte* pCb = baseCb + pixelOffset;
                    sbyte* pCr = baseCr + pixelOffset;

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
                            byte* readPtr = (byte*)pInRow;
                            var vecA = Vector256.Load(readPtr);
                            var vecF = Vector256.Load(readPtr + 32);
                            var vecB = Vector256.Load(readPtr + 64);

                            dummyData.AsByte().Store((byte*)pYRow);
                            dummyData.AsByte().Store((byte*)pCbRow);
                            dummyData.AsByte().Store((byte*)pCrRow);

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