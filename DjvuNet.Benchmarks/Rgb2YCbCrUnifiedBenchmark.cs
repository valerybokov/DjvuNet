using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(CustomParallelConfig))]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
    public class Rgb2YCbCrUnifiedBenchmark
    {
        private const int InvocationCount = 10;
        private const int MaxWidth = 5448;
        private const int MaxHeight = 3686;
        private const int MaxPixels = MaxWidth * MaxHeight;

        // Varying from very small to very large to capture the whole spectrum
        [Params(1024, 4096, 9216, 16384, 36864, 65536, 262144, 1048576, 2096704, 4194304, MaxPixels)]
        public int PixelCount { get; set; }

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

        [Benchmark(Baseline = true, OperationsPerInvoke = InvocationCount)]
        public unsafe void Unified_Rgb2YCbCr()
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

                    // Calling the actual router method
                    InterWaveTransform.Rgb2YCbCr(pIn, width, height, inRowSizeInBytes, pY, pCb, pCr, width);
                }
            }
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Unified_YCbCr2Rgb()
        {
            int ratio = ImageRatio;
            int offsetPixels = PixelCount;
            int width = ImageWidth;
            int height = ImageHeight;
            int inRowSizeInBytes = width * sizeof(Pixel);

            for (int i = 0; i < InvocationCount; i++)
            {
                Pixel* basePixelPointer = (Pixel*)_nativePixelBuffers[i].ToPointer();

                for (int j = 0; j < ratio; j++)
                {
                    int byteOffset = j * offsetPixels;
                    Pixel* pIn = basePixelPointer + byteOffset;

                    // Calling the actual router method (in-place)
                    InterWaveTransform.YCbCr2Rgb(pIn, width, height, inRowSizeInBytes);
                }
            }
        }
    }
}