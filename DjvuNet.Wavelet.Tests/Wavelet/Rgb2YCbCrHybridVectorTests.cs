using System;
using System.Runtime.Intrinsics.X86;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using DjvuNet.Graphics;
using DjvuNet.Tests;
using DjvuNet.Wavelet;
using Xunit;
using System.Threading.Tasks;

namespace DjvuNet.Wavelet.Tests.Wavelet
{
    public class Rgb2YCbCrHybridVectorTests
    {
        private const string TestImageContinuous = "TitanIR-5448x3686-24bpp-YCbCr.bin.tar.gz";
        private const string TestImagePadded = "TitanIR-padded-5447x3686-24bpp-YCbCr.bin.tar.gz";

        private string GetArtifactPath(string fileName)
        {
            return Path.Combine(Util.ArtifactsPath, fileName);
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

        // =========================================================================
        // Hybrid SingleThread Tests
        // =========================================================================

        [Fact]
        public unsafe void SingleThread_MatchesScalar_RealData_Continuous()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
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

                InterWaveSimd.Rgb2YCbCrHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void SingleThread_MatchesScalar_RealData_Padded()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            }

            string imagePath = GetArtifactPath(TestImagePadded);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5447;
            int height = 3686;
            int inStride = (width * 3 + 3) & ~3; // 16344 bytes
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

                InterWaveSimd.Rgb2YCbCrHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void SingleThread_MatchesScalar_SmallSequential()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
            }

            int width = 50;
            int height = 3;
            int inStride = (width * 3 + 3) & ~3; // 152 bytes
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

                InterWaveSimd.Rgb2YCbCrHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        // =========================================================================
        // Hybrid MultiThread (ParallelFor) Tests
        // =========================================================================

        [Fact]
        public unsafe void MultiThread_MatchesScalar_RealData_Continuous()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
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

                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                InterWaveSimd.Rgb2YCbCrParallelHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void MultiThread_MatchesScalar_RealData_Padded()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
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

                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                InterWaveSimd.Rgb2YCbCrParallelHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }

        [Fact]
        public unsafe void MultiThread_MatchesScalar_SmallSequential()
        {
            if (!Avx2.IsSupported)
            {
                Assert.Skip("AVX2 hardware acceleration is not supported on this CPU.");
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

                var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
                InterWaveSimd.Rgb2YCbCrParallelHybridVector((Pixel*)pIn, width, height, inStride, pYVec, pCbVec, pCrVec, outStride, options);
            }

            Assert.Equal(yScl, yVec);
            Assert.Equal(cbScl, cbVec);
            Assert.Equal(crScl, crVec);
        }
    }
}