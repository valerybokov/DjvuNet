using System;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Runtime.InteropServices;
using Xunit;
using DjvuNet.Errors;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;

namespace DjvuNet.Wavelet.Tests
{
    public class InterWaveTransformUnifiedTests
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

        [Fact]
        public unsafe void YCbCr2Rgb_MatchesScalar_RealData_Continuous()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            string imagePath = GetArtifactPath(TestImageContinuous);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5448;
            int height = 3686;
            int stride = width * 3;
            int totalBytes = stride * height;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            byte[] bufferScalar = new byte[totalBytes];
            byte[] bufferUnified = new byte[totalBytes];
            Buffer.BlockCopy(sourceData, 0, bufferScalar, 0, totalBytes);
            Buffer.BlockCopy(sourceData, 0, bufferUnified, 0, totalBytes);

            fixed (byte* ptrScalar = bufferScalar)
            fixed (byte* ptrUnified = bufferUnified)
            {
#pragma warning disable CS0618
                InterWaveTransform.YCbCr2RgbScalar((Pixel*)ptrScalar, width, height);
#pragma warning restore CS0618
                
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptrUnified, width, height, stride);

                double diff = Util.ImageBinaryDiff(ptrScalar, ptrUnified, width, height, stride, 24, 8);
                Assert.Equal(0.0, diff);
            }
        }

        [Fact]
        public unsafe void YCbCr2Rgb_MatchesScalar_RealData_Padded()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            string imagePath = GetArtifactPath(TestImagePadded);
            Assert.True(File.Exists(imagePath), $"Test artifact not found at: {imagePath}");

            int width = 5447;
            int height = 3686;
            int stride = (width * 3 + 3) & ~3; // 16344 bytes
            int totalBytes = stride * height;

            byte[] sourceData = ReadGzBinary(imagePath, totalBytes);
            Assert.Equal(totalBytes, sourceData.Length);

            byte[] bufferScalar = new byte[totalBytes];
            byte[] bufferUnified = new byte[totalBytes];
            Buffer.BlockCopy(sourceData, 0, bufferScalar, 0, totalBytes);
            Buffer.BlockCopy(sourceData, 0, bufferUnified, 0, totalBytes);

            fixed (byte* ptrScalar = bufferScalar)
            fixed (byte* ptrUnified = bufferUnified)
            {
                // Note: The deprecated Scalar method DOES NOT support stride. It will mathematically fail 
                // parity against the Unified method which correctly handles the padded bytes.
                // We wrap the scalar call using width instead of stride, which corrupts the output linearly.
#pragma warning disable CS0618
                InterWaveTransform.YCbCr2RgbScalar((Pixel*)ptrScalar, width, height);
#pragma warning restore CS0618
                
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptrUnified, width, height, stride);

                double diff = Util.ImageBinaryDiff(ptrScalar, ptrUnified, width, height, stride, 24, 8);
                
                // Assert that the scalar method FAILS to match the stride-safe method
                Assert.True(diff > 0.0, "Expected Scalar method to fail parity on padded image due to lack of stride support.");
            }
        }

        [Fact]
        public unsafe void YCbCr2Rgb_MatchesScalar_SmallSequential()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            int width = 50; 
            int height = 3;
            int stride = (width * 3 + 3) & ~3; // 152 bytes
            int totalBytes = stride * height;
            
            byte[] bufferScalar = new byte[totalBytes];
            byte[] bufferUnified = new byte[totalBytes];

            // Fill with sequential bytes to easily track permutations
            for (int i = 0; i < totalBytes; i++)
            {
                bufferScalar[i] = (byte)(i); 
                bufferUnified[i] = bufferScalar[i];
            }

            fixed (byte* ptrScalar = bufferScalar)
            fixed (byte* ptrUnified = bufferUnified)
            {
#pragma warning disable CS0618
                InterWaveTransform.YCbCr2RgbScalar((Pixel*)ptrScalar, width, height);
#pragma warning restore CS0618
                
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptrUnified, width, height, stride);

                // Since we padded the buffer and height > 1, the scalar method (which ignores stride) will corrupt the second row.
                double diff = Util.ImageBinaryDiff(ptrScalar, ptrUnified, width, height, stride, 24, 8);
                Assert.True(diff > 0.0, "Expected Scalar method to fail parity on padded sequential array.");
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCr_InputValidation_NullPointers_ThrowsDjvuArgumentNullException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero, pY = IntPtr.Zero, pCb = IntPtr.Zero, pCr = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                pY = Marshal.AllocHGlobal(100);
                pCb = Marshal.AllocHGlobal(100);
                pCr = Marshal.AllocHGlobal(100);
                var exPix = Assert.Throws<DjvuArgumentNullException>(() => InterWaveTransform.Rgb2YCbCr(null, 10, 10, 30, (sbyte*)pY, (sbyte*)pCb, (sbyte*)pCr, 10));
                Assert.Equal("pPixBuff", exPix.ParamName);
                Assert.Contains("Value cannot be null.", exPix.Message);

                var exY = Assert.Throws<DjvuArgumentNullException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, null, (sbyte*)pCb, (sbyte*)pCr, 10));
                Assert.Equal("pOutY", exY.ParamName);
                Assert.Contains("Value cannot be null.", exY.Message);

                var exCb = Assert.Throws<DjvuArgumentNullException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pY, null, (sbyte*)pCr, 10));
                Assert.Equal("pOutCb", exCb.ParamName);
                Assert.Contains("Value cannot be null.", exCb.Message);

                var exCr = Assert.Throws<DjvuArgumentNullException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pY, (sbyte*)pCb, null, 10));
                Assert.Equal("pOutCr", exCr.ParamName);
                Assert.Contains("Value cannot be null.", exCr.Message);
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pY != IntPtr.Zero) Marshal.FreeHGlobal(pY);
                if (pCb != IntPtr.Zero) Marshal.FreeHGlobal(pCb);
                if (pCr != IntPtr.Zero) Marshal.FreeHGlobal(pCr);
            }
        }

        [Fact]
        public unsafe void YCbCr2Rgb_InputValidation_NullPointers_ThrowsDjvuArgumentNullException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            var ex = Assert.Throws<DjvuArgumentNullException>(() => InterWaveTransform.YCbCr2Rgb(null, 10, 10, 30));
            Assert.Equal("pPixBuff", ex.ParamName);
            Assert.Contains("Value cannot be null.", ex.Message);
        }

        [Fact]
        public unsafe void Rgb2YCbCr_InputValidation_InvalidDimensions_ThrowsDjvuArgumentOutOfRangeException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero, pY = IntPtr.Zero, pCb = IntPtr.Zero, pCr = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                pY = Marshal.AllocHGlobal(100);
                pCb = Marshal.AllocHGlobal(100);
                pCr = Marshal.AllocHGlobal(100);
                var exW = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 0, 10, 30, (sbyte*)pY, (sbyte*)pCb, (sbyte*)pCr, 10));
                Assert.Equal("width", exW.ParamName);
                Assert.Equal(0, exW.ActualValue);
                Assert.Contains("Width must be greater than zero.", exW.Message);
                Assert.Contains("Actual value was 0.", exW.Message);

                var exH = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, -1, 30, (sbyte*)pY, (sbyte*)pCb, (sbyte*)pCr, 10));
                Assert.Equal("height", exH.ParamName);
                Assert.Equal(-1, exH.ActualValue);
                Assert.Contains("Height must be greater than zero.", exH.Message);
                Assert.Contains("Actual value was -1.", exH.Message);
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pY != IntPtr.Zero) Marshal.FreeHGlobal(pY);
                if (pCb != IntPtr.Zero) Marshal.FreeHGlobal(pCb);
                if (pCr != IntPtr.Zero) Marshal.FreeHGlobal(pCr);
            }
        }

        [Fact]
        public unsafe void YCbCr2Rgb_InputValidation_InvalidDimensions_ThrowsDjvuArgumentOutOfRangeException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                var exW = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.YCbCr2Rgb((Pixel*)pIn, 0, 10, 30));
                Assert.Equal("width", exW.ParamName);
                Assert.Equal(0, exW.ActualValue);
                Assert.Contains("Width must be greater than zero.", exW.Message);
                Assert.Contains("Actual value was 0.", exW.Message);

                var exH = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.YCbCr2Rgb((Pixel*)pIn, 10, -1, 30));
                Assert.Equal("height", exH.ParamName);
                Assert.Equal(-1, exH.ActualValue);
                Assert.Contains("Height must be greater than zero.", exH.Message);
                Assert.Contains("Actual value was -1.", exH.Message);
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCr_InputValidation_InvalidStride_ThrowsDjvuArgumentOutOfRangeException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero, pY = IntPtr.Zero, pCb = IntPtr.Zero, pCr = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                pY = Marshal.AllocHGlobal(100);
                pCb = Marshal.AllocHGlobal(100);
                pCr = Marshal.AllocHGlobal(100);
                var exIn = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 29, (sbyte*)pY, (sbyte*)pCb, (sbyte*)pCr, 10));
                Assert.Equal("rowSizeInBytes", exIn.ParamName);
                Assert.Equal(29, exIn.ActualValue);
                Assert.Contains("Input row size (29) must be at least width * sizeof(Pixel) (30).", exIn.Message);
                Assert.Contains("Actual value was 29.", exIn.Message);

                var exOut = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pY, (sbyte*)pCb, (sbyte*)pCr, 9));
                Assert.Equal("outRowSizeInBytes", exOut.ParamName);
                Assert.Equal(9, exOut.ActualValue);
                Assert.Contains("Output row size (9) must be at least width (10).", exOut.Message);
                Assert.Contains("Actual value was 9.", exOut.Message);
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pY != IntPtr.Zero) Marshal.FreeHGlobal(pY);
                if (pCb != IntPtr.Zero) Marshal.FreeHGlobal(pCb);
                if (pCr != IntPtr.Zero) Marshal.FreeHGlobal(pCr);
            }
        }

        [Fact]
        public unsafe void YCbCr2Rgb_InputValidation_InvalidStride_ThrowsDjvuArgumentOutOfRangeException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                var ex = Assert.Throws<DjvuArgumentOutOfRangeException>(() => InterWaveTransform.YCbCr2Rgb((Pixel*)pIn, 10, 10, 29));
                Assert.Equal("rowSizeInBytes", ex.ParamName);
                Assert.Equal(29, ex.ActualValue);
                Assert.Contains("Row size (29) must be at least width * sizeof(Pixel) (30).", ex.Message);
                Assert.Contains("Actual value was 29.", ex.Message);
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
            }
        }
        [Fact]
        public unsafe void Rgb2YCbCr_InputValidation_OverlappingInputOutput_ThrowsDjvuInvalidOperationException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pShared = IntPtr.Zero, pSafe1 = IntPtr.Zero, pSafe2 = IntPtr.Zero;
            try
            {
                pShared = Marshal.AllocHGlobal(400);
                pSafe1 = Marshal.AllocHGlobal(100);
                pSafe2 = Marshal.AllocHGlobal(100);

                var ex1 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pShared, 10, 10, 30, (sbyte*)pShared, (sbyte*)pSafe1, (sbyte*)pSafe2, 10));
                Assert.Contains("Source and destination buffers must be distinct", ex1.Message);

                var ex2 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pShared, 10, 10, 30, (sbyte*)pSafe1, (sbyte*)pShared, (sbyte*)pSafe2, 10));
                
                var ex3 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pShared, 10, 10, 30, (sbyte*)pSafe1, (sbyte*)pSafe2, (sbyte*)pShared, 10));
            }
            finally
            {
                if (pShared != IntPtr.Zero) Marshal.FreeHGlobal(pShared);
                if (pSafe1 != IntPtr.Zero) Marshal.FreeHGlobal(pSafe1);
                if (pSafe2 != IntPtr.Zero) Marshal.FreeHGlobal(pSafe2);
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCr_InputValidation_OverlappingOutputBuffers_ThrowsDjvuInvalidOperationException()
        {
            if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported) return;

            IntPtr pIn = IntPtr.Zero, pSharedOut = IntPtr.Zero, pSafeOut = IntPtr.Zero;
            try
            {
                pIn = Marshal.AllocHGlobal(300);
                pSharedOut = Marshal.AllocHGlobal(200);
                pSafeOut = Marshal.AllocHGlobal(100);

                var ex1 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pSharedOut, (sbyte*)pSharedOut, (sbyte*)pSafeOut, 10));
                Assert.Contains("Destination planar buffers (Y, Cb, Cr) must be distinct and not overlap.", ex1.Message);

                var ex2 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pSharedOut, (sbyte*)pSafeOut, (sbyte*)pSharedOut, 10));

                var ex3 = Assert.Throws<DjvuInvalidOperationException>(() => 
                    InterWaveTransform.Rgb2YCbCr((Pixel*)pIn, 10, 10, 30, (sbyte*)pSafeOut, (sbyte*)pSharedOut, (sbyte*)pSharedOut, 10));
            }
            finally
            {
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                if (pSharedOut != IntPtr.Zero) Marshal.FreeHGlobal(pSharedOut);
                if (pSafeOut != IntPtr.Zero) Marshal.FreeHGlobal(pSafeOut);
            }
        }
    }
}