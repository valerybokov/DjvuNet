using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Xunit;
using DjvuNet.Graphics;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;

namespace DjvuNet.Wavelet.Tests.Wavelet
{
    public class Rgb2YCbCrVectorizedTests
    {
        public const int Seed = 42;

        [Fact]
        public void DeinterlaceBgrVector128_UnpacksCorrectly()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            // Create 48 bytes of sequential data: 1, 2, 3, 4, 5... 48
            byte[] inputBytes = new byte[48];
            for (int i = 0; i < 48; i++)
            {
                inputBytes[i] = (byte)(i + 1);
            }

            Vector128<byte> xmmA, xmmB, xmmC;
            unsafe
            {
                fixed (byte* p = inputBytes)
                {
                    xmmA = Vector128.Load(p);
                    xmmB = Vector128.Load(p + 16);
                    xmmC = Vector128.Load(p + 32);
                }
            }

            Console.WriteLine("--- INPUT VECTORS ---");
            Console.WriteLine($"xmmA: {FormatVector(xmmA)}");
            Console.WriteLine($"xmmB: {FormatVector(xmmB)}");
            Console.WriteLine($"xmmC: {FormatVector(xmmC)}");
            Console.WriteLine("");

            // Act - Mirrored inline logic
            Vector128<byte> bOut, gOut, rOut;
            if (AdvSimd.Arm64.IsSupported)
            {
                var bMask0 = Vector128.Create((byte)0, 3, 6, 9, 12, 15, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255);
                var bMask1 = Vector128.Create((byte)255, 255, 255, 255, 255, 255, 2, 5, 8, 11, 14, 255, 255, 255, 255, 255);
                var bMask2 = Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 1, 4, 7, 10, 13);
                var gMask0 = Vector128.Create((byte)1, 4, 7, 10, 13, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255);
                var gMask1 = Vector128.Create((byte)255, 255, 255, 255, 255, 0, 3, 6, 9, 12, 15, 255, 255, 255, 255, 255);
                var gMask2 = Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 2, 5, 8, 11, 14);
                var rMask0 = Vector128.Create((byte)2, 5, 8, 11, 14, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255);
                var rMask1 = Vector128.Create((byte)255, 255, 255, 255, 255, 1, 4, 7, 10, 13, 255, 255, 255, 255, 255, 255);
                var rMask2 = Vector128.Create((byte)255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 3, 6, 9, 12, 15);

                bOut = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, bMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, bMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, bMask2)));
                gOut = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, gMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, gMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, gMask2)));
                rOut = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, rMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, rMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, rMask2)));
            }
            else if (Ssse3.IsSupported)
            {
                var bMask0 = Vector128.Create((byte)0, 3, 6, 9, 12, 15, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
                var bMask1 = Vector128.Create((byte)128, 128, 128, 128, 128, 128, 2, 5, 8, 11, 14, 128, 128, 128, 128, 128);
                var bMask2 = Vector128.Create((byte)128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 1, 4, 7, 10, 13);
                var gMask0 = Vector128.Create((byte)1, 4, 7, 10, 13, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
                var gMask1 = Vector128.Create((byte)128, 128, 128, 128, 128, 0, 3, 6, 9, 12, 15, 128, 128, 128, 128, 128);
                var gMask2 = Vector128.Create((byte)128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 2, 5, 8, 11, 14);
                var rMask0 = Vector128.Create((byte)2, 5, 8, 11, 14, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128);
                var rMask1 = Vector128.Create((byte)128, 128, 128, 128, 128, 1, 4, 7, 10, 13, 128, 128, 128, 128, 128, 128);
                var rMask2 = Vector128.Create((byte)128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 0, 3, 6, 9, 12, 15);

                bOut = Sse2.Or(Ssse3.Shuffle(xmmA, bMask0), Sse2.Or(Ssse3.Shuffle(xmmB, bMask1), Ssse3.Shuffle(xmmC, bMask2)));
                gOut = Sse2.Or(Ssse3.Shuffle(xmmA, gMask0), Sse2.Or(Ssse3.Shuffle(xmmB, gMask1), Ssse3.Shuffle(xmmC, gMask2)));
                rOut = Sse2.Or(Ssse3.Shuffle(xmmA, rMask0), Sse2.Or(Ssse3.Shuffle(xmmB, rMask1), Ssse3.Shuffle(xmmC, rMask2)));
            }
            else
            {
                bOut = Vector128<byte>.Zero; gOut = Vector128<byte>.Zero; rOut = Vector128<byte>.Zero;
            }

            Console.WriteLine("--- OUTPUT VECTORS ---");
            Console.WriteLine($"B:  {FormatVector(bOut)}");
            Console.WriteLine($"G:  {FormatVector(gOut)}");
            Console.WriteLine($"R:  {FormatVector(rOut)}");

            // Assert
            for (int i = 0; i < 16; i++)
            {
                Assert.Equal((byte)(i * 3 + 1), bOut.GetElement(i));
                Assert.Equal((byte)(i * 3 + 2), gOut.GetElement(i));
                Assert.Equal((byte)(i * 3 + 3), rOut.GetElement(i));
            }
        }

        private static string FormatVector(Vector128<byte> vec)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                sb.Append($"{vec.GetElement(i),2} ");
            }
            return sb.ToString().TrimEnd();
        }

        [Fact]
        public void TransformRgbToYCbCrVector128_MathParity()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            // Generate 16 pixels of full White (R=255, G=255, B=255)
            var bVec = Vector128.Create((byte)255);
            var gVec = Vector128.Create((byte)255);
            var rVec = Vector128.Create((byte)255);

            // Mirrored inline logic
            var cY_R = Vector128.Create((int)19946);
            var cY_G = Vector128.Create((int)39891);
            var cY_B = Vector128.Create((int)5698);
            var cCb_R = Vector128.Create((int)-11397);
            var cCb_G = Vector128.Create((int)-22795);
            var cCb_B = Vector128.Create((int)34192);
            var cCr_R = Vector128.Create((int)30393);
            var cCr_G = Vector128.Create((int)-26594);
            var cCr_B = Vector128.Create((int)-3799);
            var v32768 = Vector128.Create((int)32768);
            var v128 = Vector128.Create((int)128);

            var b_L16 = Vector128.WidenLower(bVec); var b_H16 = Vector128.WidenUpper(bVec);
            var g_L16 = Vector128.WidenLower(gVec); var g_H16 = Vector128.WidenUpper(gVec);
            var r_L16 = Vector128.WidenLower(rVec); var r_H16 = Vector128.WidenUpper(rVec);

            var b32_LL = Vector128.WidenLower(b_L16).AsInt32(); var g32_LL = Vector128.WidenLower(g_L16).AsInt32(); var r32_LL = Vector128.WidenLower(r_L16).AsInt32();
            var yLL = Vector128.Subtract(Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LL, cY_R), Vector128.Multiply(g32_LL, cY_G)), Vector128.Multiply(b32_LL, cY_B)), v32768), 16), v128);
            var cbLL = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LL, cCb_R), Vector128.Multiply(g32_LL, cCb_G)), Vector128.Multiply(b32_LL, cCb_B)), v32768), 16);
            var crLL = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LL, cCr_R), Vector128.Multiply(g32_LL, cCr_G)), Vector128.Multiply(b32_LL, cCr_B)), v32768), 16);

            var b32_LH = Vector128.WidenUpper(b_L16).AsInt32(); var g32_LH = Vector128.WidenUpper(g_L16).AsInt32(); var r32_LH = Vector128.WidenUpper(r_L16).AsInt32();
            var yLH = Vector128.Subtract(Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LH, cY_R), Vector128.Multiply(g32_LH, cY_G)), Vector128.Multiply(b32_LH, cY_B)), v32768), 16), v128);
            var cbLH = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LH, cCb_R), Vector128.Multiply(g32_LH, cCb_G)), Vector128.Multiply(b32_LH, cCb_B)), v32768), 16);
            var crLH = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_LH, cCr_R), Vector128.Multiply(g32_LH, cCr_G)), Vector128.Multiply(b32_LH, cCr_B)), v32768), 16);

            var b32_HL = Vector128.WidenLower(b_H16).AsInt32(); var g32_HL = Vector128.WidenLower(g_H16).AsInt32(); var r32_HL = Vector128.WidenLower(r_H16).AsInt32();
            var yHL = Vector128.Subtract(Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HL, cY_R), Vector128.Multiply(g32_HL, cY_G)), Vector128.Multiply(b32_HL, cY_B)), v32768), 16), v128);
            var cbHL = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HL, cCb_R), Vector128.Multiply(g32_HL, cCb_G)), Vector128.Multiply(b32_HL, cCb_B)), v32768), 16);
            var crHL = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HL, cCr_R), Vector128.Multiply(g32_HL, cCr_G)), Vector128.Multiply(b32_HL, cCr_B)), v32768), 16);

            var b32_HH = Vector128.WidenUpper(b_H16).AsInt32(); var g32_HH = Vector128.WidenUpper(g_H16).AsInt32(); var r32_HH = Vector128.WidenUpper(r_H16).AsInt32();
            var yHH = Vector128.Subtract(Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HH, cY_R), Vector128.Multiply(g32_HH, cY_G)), Vector128.Multiply(b32_HH, cY_B)), v32768), 16), v128);
            var cbHH = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HH, cCb_R), Vector128.Multiply(g32_HH, cCb_G)), Vector128.Multiply(b32_HH, cCb_B)), v32768), 16);
            var crHH = Vector128.ShiftRightArithmetic(Vector128.Add(Vector128.Add(Vector128.Add(Vector128.Multiply(r32_HH, cCr_R), Vector128.Multiply(g32_HH, cCr_G)), Vector128.Multiply(b32_HH, cCr_B)), v32768), 16);

            var yL16 = Vector128.Narrow(yLL, yLH); var yH16 = Vector128.Narrow(yHL, yHH);
            var cbL16 = Vector128.Narrow(cbLL, cbLH); var cbH16 = Vector128.Narrow(cbHL, cbHH);
            var crL16 = Vector128.Narrow(crLL, crLH); var crH16 = Vector128.Narrow(crHL, crHH);

            var yOut = Vector128.Narrow(yL16, yH16);
            var cbOut = Vector128.Narrow(cbL16, cbH16);
            var crOut = Vector128.Narrow(crL16, crH16);

            // Calculate scalar ground truth for White (255, 255, 255)
            int y_val = (255 * 19946) + (255 * 39891) + (255 * 5698) + 32768;
            sbyte expectedY = (sbyte)((y_val >> 16) - 128); // 127

            int cb_val = (255 * -11397) + (255 * -22795) + (255 * 34192) + 32768;
            sbyte expectedCb = (sbyte)Math.Max(-128, Math.Min(127, cb_val >> 16)); // 0

            int cr_val = (255 * 30393) + (255 * -26594) + (255 * -3799) + 32768;
            sbyte expectedCr = (sbyte)Math.Max(-128, Math.Min(127, cr_val >> 16)); // 0

            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(expectedY, yOut.GetElement(i));
                Assert.Equal(expectedCb, cbOut.GetElement(i));
                Assert.Equal(expectedCr, crOut.GetElement(i));
            }
        }

        private const string TestImageContinuous = "TitanIR-5448x3686-24bpp-YCbCr.bin.tar.gz";
        private const string TestImagePadded = "TitanIR-padded-5447x3686-24bpp-YCbCr.bin.tar.gz";

        private string GetArtifactPath(string fileName)
        {
            return Path.Combine(DjvuNet.Tests.Util.ArtifactsPath, fileName);
        }

        private byte[] ReadGzBinary(string archivePath, int totalBytes)
        {
            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
            using (TarReader tar = new TarReader(gzip))
            {
                TarEntry entry = tar.GetNextEntry();
                if (entry == null || entry.DataStream == null)
                    throw new InvalidDataException("No valid file found in the tar.gz archive.");

                byte[] rawBytes = new byte[totalBytes];
                int totalRead = 0;
                int bytesRead;

                while (totalRead < totalBytes && (bytesRead = entry.DataStream.Read(rawBytes, totalRead, totalBytes - totalRead)) > 0)
                {
                    totalRead += bytesRead;
                }

                if (totalRead != totalBytes)
                    throw new InvalidDataException($"Failed to read complete artifact from tar.gz. Expected {totalBytes}, got {totalRead}");

                return rawBytes;
            }
        }

        [Fact]
        public unsafe void Vector128SingleThreaded_MatchesScalar_RealData_Continuous()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            string imagePath = GetArtifactPath(TestImageContinuous);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5448;
            int height = 3686;
            int inStride = width * 3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = sourceData)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                InterWaveSimd.Rgb2YCbCrVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector128SingleThreaded_MatchesScalar_RealData_Padded()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            string imagePath = GetArtifactPath(TestImagePadded);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5447;
            int height = 3686;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = sourceData)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                InterWaveSimd.Rgb2YCbCrVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector128SingleThreaded_MatchesScalar_SmallSequential()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            int width = 50;
            int height = 3;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] bufferScalar = new byte[totalBytes];
            for (int i = 0; i < totalBytes; i++)
            {
                bufferScalar[i] = (byte)(i);
            }

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = bufferScalar)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                InterWaveSimd.Rgb2YCbCrVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }
        [Fact]
        public unsafe void Vector128MultiThreaded_MatchesScalar_RealData_Continuous()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            string imagePath = GetArtifactPath(TestImageContinuous);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5448;
            int height = 3686;
            int inStride = width * 3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = sourceData)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                InterWaveSimd.Rgb2YCbCrParallelVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector128MultiThreaded_MatchesScalar_RealData_Padded()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            string imagePath = GetArtifactPath(TestImagePadded);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5447;
            int height = 3686;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = sourceData)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                InterWaveSimd.Rgb2YCbCrParallelVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector128MultiThreaded_MatchesScalar_SmallSequential()
        {
            if (!Vector128.IsHardwareAccelerated)
            {
                Assert.Skip("Vector128 hardware acceleration is not supported on this architecture.");
            }

            int width = 50;
            int height = 3;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] bufferScalar = new byte[totalBytes];
            for (int i = 0; i < totalBytes; i++)
            {
                bufferScalar[i] = (byte)(i);
            }

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = bufferScalar)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                InterWaveSimd.Rgb2YCbCrParallelVector128((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector256SingleThreaded_MatchesScalar_RealData_Continuous()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            string testImagePath = Path.Combine(DjvuNet.Tests.Util.RepoRoot, "artifacts", "TitanIR-24bgr.png");
            if (!File.Exists(testImagePath)) return;

            using (var bmp = new System.Drawing.Bitmap(testImagePath))
            {
                int width = bmp.Width;
                int height = bmp.Height;
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int inStride = data.Stride;
                    int outStride = width;

                    sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
                    sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

                    fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
                    fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
                    {
                        InterWaveTransform.Rgb2YCbCr((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                        InterWaveSimd.Rgb2YCbCrVector256((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
                    }

                    Assert.Equal(yScl, yVec);
                    Assert.Equal(cbScl, cbVec);
                    Assert.Equal(crScl, crVec);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        [Fact]
        public unsafe void Vector256SingleThreaded_MatchesScalar_RealData_Padded()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            string testImagePath = Path.Combine(DjvuNet.Tests.Util.RepoRoot, "artifacts", "Pismis24.png");
            if (!File.Exists(testImagePath)) return;

            using (var bmp = new System.Drawing.Bitmap(testImagePath))
            {
                int width = bmp.Width;
                int height = bmp.Height;
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int inStride = data.Stride;
                    int outStride = width;

                    sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
                    sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

                    fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
                    fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
                    {
                        InterWaveTransform.Rgb2YCbCr((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                        InterWaveSimd.Rgb2YCbCrVector256((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
                    }

                    Assert.Equal(yScl, yVec);
                    Assert.Equal(cbScl, cbVec);
                    Assert.Equal(crScl, crVec);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        [Fact]
        public unsafe void Vector256SingleThreaded_MatchesScalar_SmallSequential()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            int width = 50;
            int height = 3;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] bufferScalar = new byte[totalBytes];
            for (int i = 0; i < totalBytes; i++)
            {
                bufferScalar[i] = (byte)(i);
            }

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = bufferScalar)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                InterWaveSimd.Rgb2YCbCrVector256((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void Vector256MultiThreaded_MatchesScalar_RealData_Continuous()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            string testImagePath = Path.Combine(DjvuNet.Tests.Util.RepoRoot, "artifacts", "TitanIR-24bgr.png");
            if (!File.Exists(testImagePath)) return;

            using (var bmp = new System.Drawing.Bitmap(testImagePath))
            {
                int width = bmp.Width;
                int height = bmp.Height;
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int inStride = data.Stride;
                    int outStride = width;

                    sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
                    sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

                    fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
                    fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
                    {
                        InterWaveTransform.Rgb2YCbCr((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                        InterWaveSimd.Rgb2YCbCrParallelVector256((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
                    }

                    Assert.Equal(yScl, yVec);
                    Assert.Equal(cbScl, cbVec);
                    Assert.Equal(crScl, crVec);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        [Fact]
        public unsafe void Vector256MultiThreaded_MatchesScalar_RealData_Padded()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            string testImagePath = Path.Combine(DjvuNet.Tests.Util.RepoRoot, "artifacts", "Pismis24.png");
            if (!File.Exists(testImagePath)) return;

            using (var bmp = new System.Drawing.Bitmap(testImagePath))
            {
                int width = bmp.Width;
                int height = bmp.Height;
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                try
                {
                    int inStride = data.Stride;
                    int outStride = width;

                    sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
                    sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

                    fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
                    fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
                    {
                        InterWaveTransform.Rgb2YCbCr((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                        InterWaveSimd.Rgb2YCbCrParallelVector256((Pixel*)data.Scan0.ToPointer(), width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
                    }

                    Assert.Equal(yScl, yVec);
                    Assert.Equal(cbScl, cbVec);
                    Assert.Equal(crScl, crVec);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        [Fact]
        public unsafe void Vector256MultiThreaded_MatchesScalar_SmallSequential()
        {
            if (!Avx2.IsSupported) Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            int width = 50;
            int height = 3;
            int inStride = (width * 3 + 3) & ~3;
            int totalBytes = inStride * height;
            int outStride = width;

            byte[] bufferScalar = new byte[totalBytes];
            for (int i = 0; i < totalBytes; i++)
            {
                bufferScalar[i] = (byte)(i);
            }

            sbyte[] yScl = new sbyte[height * outStride], cbScl = new sbyte[height * outStride], crScl = new sbyte[height * outStride];
            sbyte[] yVec = new sbyte[height * outStride], cbVec = new sbyte[height * outStride], crVec = new sbyte[height * outStride];

            fixed (byte* pIn = bufferScalar)
            fixed (sbyte* pYScl = yScl, pCbScl = cbScl, pCrScl = crScl)
            fixed (sbyte* pYVec = yVec, pCbVec = cbVec, pCrVec = crVec)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, width, height, inStride, pYScl, pCbScl, pCrScl, outStride);
                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                InterWaveSimd.Rgb2YCbCrParallelVector256((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }
    }
}