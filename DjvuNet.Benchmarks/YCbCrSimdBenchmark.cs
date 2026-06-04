using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using DjvuNet.DjvuLibre;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(StandardConfig))]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
    public class YCbCrSimdBenchmark
    {
        [Params(512 * 512, 5448 * 3686)]
        public int PixelCount { get; set; }

        public int ImageWidth => PixelCount == 262144 ? 512 : 5448;
        public int ImageHeight => PixelCount == 262144 ? 512 : 3686;

        private IntPtr nativePixelBuffer;
        private unsafe Pixel* pixelPointer;

        private IntPtr nativeOutputBuffer;
        private unsafe byte* outputPointer;

        // Class-level sinks to prevent JIT Dead Code Elimination
        public Vector128<byte> blue128A, green128A, red128A;
        public Vector128<byte> blue128B, green128B, red128B;

        public Vector256<short> lumaEven256, blueEven256, redEven256;
        public Vector256<short> lumaOdd256, blueOdd256, redOdd256;

        // Sinks for Vector128 Math outputs
        public Vector128<sbyte> luma128A, chromaBlue128A, chromaRed128A;
        public Vector128<sbyte> luma128B, chromaBlue128B, chromaRed128B;

        // Sinks for Vector256 Math outputs
        public Vector256<sbyte> lumaEvenOut256, chromaBlueEvenOut256, chromaRedEvenOut256;
        public Vector256<sbyte> lumaOddOut256, chromaBlueOddOut256, chromaRedOddOut256;

        // Inputs for Math Vector256 (missing green)
        public Vector256<short> greenEven256, greenOdd256;

        // Inputs for Math YCbCrToRgb (simulating loaded planar buffers)
        public Vector128<sbyte> inLuma128, inChromaBlue128, inChromaRed128;
        public Vector256<short> inLumaEven256, inBlueEven256, inRedEven256;
        public Vector256<short> inLumaOdd256, inBlueOdd256, inRedOdd256;

        // Sinks for Math YCbCrToRgb outputs (these will also be the inputs for Interlace)
        public Vector128<byte> outBlue128A, outGreen128A, outRed128A;
        public Vector128<byte> outBlue128B, outGreen128B, outRed128B;
        public Vector256<byte> outBlueEven256, outBlueOdd256, outGreenEven256, outGreenOdd256, outRedEven256, outRedOdd256;

        private string GetArtifactPath(string fileName)
        {
            return Path.Combine(Util.RepoRoot, "artifacts", fileName);
        }

        [GlobalSetup]
        public unsafe void GlobalSetup()
        {
            int maxPixels = 5448 * 3686;
            nativePixelBuffer = DjvuMarshal.AllocHGlobal((uint)(maxPixels * sizeof(Pixel)));
            pixelPointer = (Pixel*)nativePixelBuffer.ToPointer();

            nativeOutputBuffer = DjvuMarshal.AllocHGlobal((uint)(maxPixels * sizeof(Pixel)));
            outputPointer = (byte*)nativeOutputBuffer.ToPointer();

            string imagePath = GetArtifactPath("TitanIR-24bgr.png");
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Benchmark artifact not found: {imagePath}");

            using (var bmp = new Bitmap(imagePath))
            {
                int totalBytes = maxPixels * 3;
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, 5448, 3686),
                                               ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                Buffer.MemoryCopy(data.Scan0.ToPointer(), pixelPointer, totalBytes, totalBytes);
                bmp.UnlockBits(data);
            }
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (nativePixelBuffer != IntPtr.Zero)
            {
                DjvuMarshal.FreeHGlobal(nativePixelBuffer);
                nativePixelBuffer = IntPtr.Zero;
            }
            if (nativeOutputBuffer != IntPtr.Zero)
            {
                DjvuMarshal.FreeHGlobal(nativeOutputBuffer);
                nativeOutputBuffer = IntPtr.Zero;
            }
        }

        [Benchmark]
        public unsafe void DeinterlaceVector128()
        {
            InterWaveSimd.DeinterlaceBgrVector128FullImage(pixelPointer, ImageWidth, ImageHeight, ImageWidth * 3, out blue128A, out green128A, out red128A);
        }

        [Benchmark]
        public unsafe void DeinterlaceVector256()
        {
            InterWaveSimd.DeinterlaceBgrVector256FullImage(
                pixelPointer, ImageWidth, ImageHeight, ImageWidth * 3,
                out lumaEven256, out blueEven256, out redEven256,
                out lumaOdd256, out blueOdd256, out redOdd256);
        }

        // [Benchmark]
        // public void MathRgbToYCbCrVector128()
        // {
        //     // Execute 16 pixels
        //     InterWaveSimd.TransformRgbToYCbCrVector128(
        //         blue128A, green128A, red128A,
        //         out luma128A, out chromaBlue128A, out chromaRed128A);

        //     // Execute next 16 pixels to match Vector256 workload (32 pixels total)
        //     InterWaveSimd.TransformRgbToYCbCrVector128(
        //         blue128B, green128B, red128B,
        //         out luma128B, out chromaBlue128B, out chromaRed128B);
        // }

        // [Benchmark]
        // public void MathRgbToYCbCrVector256()
        // {
        //     // Execute 32 pixels
        //     InterWaveSimd.TransformRgbToYCbCrVector256(
        //         blueEven256, greenEven256, redEven256,
        //         blueOdd256, greenOdd256, redOdd256,
        //         out lumaEvenOut256, out chromaBlueEvenOut256, out chromaRedEvenOut256,
        //         out lumaOddOut256, out chromaBlueOddOut256, out chromaRedOddOut256);
        // }

        // [Benchmark]
        // public void MathYCbCrToRgbVector128()
        // {
        //     // Execute 16 pixels
        //     InterWaveSimd.TransformYCbCrToRgbVector128(
        //         inLuma128, inChromaBlue128, inChromaRed128,
        //         out outBlue128A, out outGreen128A, out outRed128A);

        //     // Execute next 16 pixels
        //     InterWaveSimd.TransformYCbCrToRgbVector128(
        //         inLuma128, inChromaBlue128, inChromaRed128,
        //         out outBlue128B, out outGreen128B, out outRed128B);
        // }

        // [Benchmark]
        // public void MathYCbCrToRgbVector256()
        // {
        //     // Execute 32 pixels
        //     InterWaveSimd.TransformYCbCrToRgbVector256(
        //         inLumaEven256, inBlueEven256, inRedEven256,
        //         inLumaOdd256, inBlueOdd256, inRedOdd256,
        //         out outBlueEven256, out outBlueOdd256,
        //         out outGreenEven256, out outGreenOdd256,
        //         out outRedEven256, out outRedOdd256);
        // }

        // [Benchmark]
        // public unsafe void InterlaceVector128()
        // {
        //     // Execute 16 pixels
        //     InterWaveSimd.InterlaceBgrVector128(
        //         outBlue128A, outGreen128A, outRed128A,
        //         out var out0, out var out1, out var out2);

        //     out0.Store(outputPointer);
        //     out1.Store(outputPointer + 16);
        //     out2.Store(outputPointer + 32);

        //     // Execute next 16 pixels (32 total)
        //     InterWaveSimd.InterlaceBgrVector128(
        //         outBlue128B, outGreen128B, outRed128B,
        //         out var out3, out var out4, out var out5);

        //     out3.Store(outputPointer + 48);
        //     out4.Store(outputPointer + 64);
        //     out5.Store(outputPointer + 80);
        // }

        // [Benchmark]
        // public unsafe void InterlaceVector256()
        // {
        //     // Execute 32 pixels
        //     InterWaveSimd.InterlaceBgrVector256(
        //         outBlueEven256, outBlueOdd256,
        //         outGreenEven256, outGreenOdd256,
        //         outRedEven256, outRedOdd256,
        //         out var outA, out var outD, out var outF);

        //     outA.Store(outputPointer);
        //     outD.Store(outputPointer + 32);
        //     outF.Store(outputPointer + 64);
        // }

        [Benchmark]
        public unsafe void Rgb2YCbCrVector128()
        {
            int width = ImageWidth;
            int height = ImageHeight;
            InterWaveSimd.Rgb2YCbCrVector128(pixelPointer, width, height, width * 3, (sbyte*)outputPointer, (sbyte*)(outputPointer + PixelCount), (sbyte*)(outputPointer + 2 * PixelCount), width);
        }

        [Benchmark]
        public unsafe void Rgb2YCbCrVector256()
        {
            int width = ImageWidth;
            int height = ImageHeight;
            InterWaveSimd.Rgb2YCbCrVector256(pixelPointer, width, height, width * 3, (sbyte*)outputPointer, (sbyte*)(outputPointer + PixelCount), (sbyte*)(outputPointer + 2 * PixelCount), width);
        }

        [Benchmark]
        public unsafe void Rgb2YCbCrHybridVector()
        {
            int width = ImageWidth;
            int height = ImageHeight;
            InterWaveSimd.Rgb2YCbCrHybridVector(pixelPointer, width, height, width * 3, (sbyte*)outputPointer, (sbyte*)(outputPointer + PixelCount), (sbyte*)(outputPointer + 2 * PixelCount), width);
        }

        [Benchmark]
        public unsafe void MemoryThroughputBaselineVector128()
        {
            int width = ImageWidth;
            int height = ImageHeight;
            Pixel* pIn = pixelPointer;
            sbyte* pY = (sbyte*)outputPointer;
            sbyte* pCb = (sbyte*)(outputPointer + PixelCount);
            sbyte* pCr = (sbyte*)(outputPointer + 2 * PixelCount);

            var dummyData = Vector128<sbyte>.Zero;

            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x <= width - 16)
                {
                    // 1. Simulate READ of 16 pixels (48 bytes)
                    byte* readPtr = (byte*)pIn;
                    var vec0 = Vector128.Load(readPtr);
                    var vec1 = Vector128.Load(readPtr + 16);
                    var vec2 = Vector128.Load(readPtr + 32);

                    // 2. Simulate WRITE to 3 separate planar locations
                    vec0.AsByte().Store((byte*)pY);
                    vec1.AsByte().Store((byte*)pCb);
                    vec2.AsByte().Store((byte*)pCr);

                    pIn += 16;
                    pY += 16;
                    pCb += 16;
                    pCr += 16;
                    x += 16;
                }
            }
        }

        [Benchmark]
        public unsafe void MemoryThroughputBaselineVector256()
        {
            if (!Avx2.IsSupported) return;

            int width = ImageWidth;
            int height = ImageHeight;
            Pixel* pIn = pixelPointer;
            sbyte* pY = (sbyte*)outputPointer;
            sbyte* pCb = (sbyte*)(outputPointer + PixelCount);
            sbyte* pCr = (sbyte*)(outputPointer + 2 * PixelCount);

            var dummyData = Vector256<sbyte>.Zero;

            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x <= width - 32)
                {
                    // 1. Simulate READ of 32 pixels (96 bytes)
                    byte* readPtr = (byte*)pIn;
                    var vecA = Vector256.Load(readPtr);
                    var vecF = Vector256.Load(readPtr + 32);
                    var vecB = Vector256.Load(readPtr + 64);

                    // 2. Simulate WRITE to 3 separate planar locations
                    vecA.AsByte().Store((byte*)pY);
                    vecF.AsByte().Store((byte*)pCb);
                    vecB.AsByte().Store((byte*)pCr);

                    pIn += 32;
                    pY += 32;
                    pCb += 32;
                    pCr += 32;
                    x += 32;
                }
            }
        }
    }
}