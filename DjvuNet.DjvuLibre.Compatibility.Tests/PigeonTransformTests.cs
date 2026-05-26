using System;
using System.Runtime.InteropServices;
using DjvuNet.DjvuLibre;
using DjvuNet.Wavelet;
using DjvuNet.Graphics;
using Xunit;
using DjvuNet.Tests;

namespace DjvuNet.DjvuLibre.Compatibility.Tests
{
    public class PigeonTransformTests
    {
        public const int Seed = 42;

        // Realistic buffer sizes: a typical 300 DPI page can be ~3000x5000.
        // The background IW44 layer is typically subsampled, yielding ~850x1100.
        // Tripling the pixel count of an 850x1100 buffer yields roughly 1472x1905.
        // The color transform runs on the entire image grid at once.
        public const int DefaultWidth = 1472;
        public const int DefaultHeight = 1905;

        [Fact]
        public unsafe void YCbCr2RgbNativeParityRandomData()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;
            int rowSize = width; // rowSize in GPixels
            int totalPixels = height * rowSize;
            int totalBytes = totalPixels * 3; // 3 bytes per pixel

            byte[] nativeBuffer = new byte[totalBytes];
            sbyte[] scalarBuffer = new sbyte[totalBytes];
            sbyte[] unifiedBuffer = new sbyte[totalBytes];

            // Generate some random test data to represent Y, Cb, Cr
            Random rnd = new Random(Seed);
            for (int i = 0; i < totalBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeBuffer[i] = val;
                scalarBuffer[i] = unchecked((sbyte)val);
                unifiedBuffer[i] = unchecked((sbyte)val);
            }

            // 1. Process via Scalar C#
            fixed (sbyte* ptr = scalarBuffer)
            {
                // Multiply width by 3 to simulate legacy parameter usage
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, width, height);
            }

            // 2. Process via Unified AVX2 C#
            fixed (sbyte* ptr = unifiedBuffer)
            {
                int rowSizeInBytes = width * sizeof(Pixel);
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, width, height, rowSizeInBytes);
            }

            // 3. Process via Native C++ DjVuLibre
            IntPtr nativePtr = IntPtr.Zero;
            try
            {
                nativePtr = DjvuMarshal.AllocHGlobal((uint)totalBytes);
                Marshal.Copy(nativeBuffer, 0, nativePtr, totalBytes);
                bool success = NativeMethods.YCbCrToRgb(nativePtr, width, height, rowSize);
                Assert.True(success);
                Marshal.Copy(nativePtr, nativeBuffer, 0, totalBytes);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativePtr);
            }

            // 4. Compute Diffs and Assert
            double diffSc, diffUn, diffScUn;

            fixed (sbyte* pSc = scalarBuffer)
            fixed (sbyte* pUn = unifiedBuffer)
            fixed (byte* pNt = nativeBuffer)
            {
                int strideBytes = rowSize * sizeof(Pixel);
                diffSc = Util.ImageBinaryDiff((byte*)pSc, pNt, width, height, strideBytes);
                diffUn = Util.ImageBinaryDiff((byte*)pUn, pNt, width, height, strideBytes);
                diffScUn = Util.ImageBinaryDiff((byte*)pSc, (byte*)pUn, width, height, strideBytes);
            }

            bool hasError = diffSc > 0 || diffUn > 0 || diffScUn > 0;

            Assert.True(!hasError,
                $"Parity Mismatch (Contiguous):\n" +
                $"C# Scalar vs Native C++    -> Diff: {diffSc:F4}\n" +
                $"C# Unified vs Native C++   -> Diff: {diffUn:F4}\n" +
                $"C# Scalar vs C# Unified    -> Diff: {diffScUn:F4}");
        }

        [Fact]
        public unsafe void YCbCr2RgbNativeParityWithStride()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;

            int paddingPixels = 17;
            int rowSize = width + paddingPixels; // Input stride (in pixels)

            int totalInPixels = height * rowSize;
            int totalInBytes = totalInPixels * 3; // 3 bytes per pixel

            byte[] nativeBuffer = new byte[totalInBytes];
            sbyte[] scalarBuffer = new sbyte[totalInBytes];
            sbyte[] unifiedBuffer = new sbyte[totalInBytes];

            Random rnd = new Random(Seed);
            for (int i = 0; i < totalInBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeBuffer[i] = val;
                scalarBuffer[i] = unchecked((sbyte)val);
                unifiedBuffer[i] = unchecked((sbyte)val);
            }

            // 1. Process via Scalar C#
            // NOTE: The legacy YCbCr2Rgb has no stride support. It will fail.
            // We run it anyway to prove it fails exactly like Rgb2YCbCrScalar did.
            fixed (sbyte* ptr = scalarBuffer)
            {
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, width, height);
            }

            // 2. Process via Unified AVX2 C#
            fixed (sbyte* ptr = unifiedBuffer)
            {
                int rowSizeInBytes = rowSize * sizeof(Pixel);
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, width, height, rowSizeInBytes);
            }

            // 3. Process via Native C++ DjVuLibre
            IntPtr nativePtr = IntPtr.Zero;
            try
            {
                nativePtr = DjvuMarshal.AllocHGlobal((uint)totalInBytes);
                Marshal.Copy(nativeBuffer, 0, nativePtr, totalInBytes);

                bool success = NativeMethods.YCbCrToRgb(nativePtr, width, height, rowSize);
                Assert.True(success);

                Marshal.Copy(nativePtr, nativeBuffer, 0, totalInBytes);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativePtr);
            }

            // 4. Assert Byte-for-Byte Parity (ignoring padding)
            double diffScNt, diffUnNt, diffScUn;

            fixed (sbyte* pSc = scalarBuffer)
            fixed (sbyte* pUn = unifiedBuffer)
            fixed (byte* pNt = nativeBuffer)
            {
                int strideBytes = rowSize * 3; // 3 bytes per pixel
                diffScNt = Util.ImageBinaryDiff((byte*)pSc, pNt, width, height, strideBytes);
                diffUnNt = Util.ImageBinaryDiff((byte*)pUn, pNt, width, height, strideBytes);
                diffScUn = Util.ImageBinaryDiff((byte*)pSc, (byte*)pUn, width, height, strideBytes);
            }

            bool scalarVsNativeFailed = diffScNt > 0.0;
            bool scalarVsUnifiedFailed = diffScUn > 0.0;

            Assert.Equal(0.0, diffUnNt);

            Assert.True(scalarVsNativeFailed && scalarVsUnifiedFailed,
                "Expected the legacy scalar method to fail parity against both Native and Unified outputs on padded buffers.");
        }

        [Fact]
        public unsafe void Rgb2YCbCrNativeParityRandomData()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;
            int rowSize = width; // rowSize in GPixels
            int outRowSize = width;
            int totalPixels = height * rowSize;
            int totalBytes = totalPixels * 3; // 3 bytes per pixel

            byte[] nativeRgbBuffer = new byte[totalBytes];
            sbyte[] managedRgbBuffer = new sbyte[totalBytes];

            // Generate some random test data to represent RGB
            Random rnd = new Random(Seed);
            for (int i = 0; i < totalBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeRgbBuffer[i] = val;
                managedRgbBuffer[i] = unchecked((sbyte)val);
            }

            sbyte[] managedOutY = new sbyte[totalPixels];
            sbyte[] managedOutCb = new sbyte[totalPixels];
            sbyte[] managedOutCr = new sbyte[totalPixels];

            byte[] nativeOutY = new byte[totalPixels];
            byte[] nativeOutCb = new byte[totalPixels];
            byte[] nativeOutCr = new byte[totalPixels];

            // 1. Process via Managed C#
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = managedOutY)
            fixed (sbyte* outCb = managedOutCb)
            fixed (sbyte* outCr = managedOutCr)
            {
                // Rgb2YCbCr sets up LUTs and extracts all 3 planes
                int inRowBytes = rowSize * sizeof(Pixel);
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, width, height, inRowBytes, outY, outCb, outCr, outRowSize);
            }

            // 2. Process via Native C++ DjVuLibre
            IntPtr nativeRgbPtr = IntPtr.Zero;
            IntPtr nativeYPtr = IntPtr.Zero;
            IntPtr nativeCbPtr = IntPtr.Zero;
            IntPtr nativeCrPtr = IntPtr.Zero;
            try
            {
                nativeRgbPtr = DjvuMarshal.AllocHGlobal((uint)totalBytes);
                nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);

                Marshal.Copy(nativeRgbBuffer, 0, nativeRgbPtr, totalBytes);

                Assert.True(NativeMethods.RgbToY(nativeRgbPtr, width, height, rowSize, nativeYPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCb(nativeRgbPtr, width, height, rowSize, nativeCbPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCr(nativeRgbPtr, width, height, rowSize, nativeCrPtr, outRowSize));

                Marshal.Copy(nativeYPtr, nativeOutY, 0, totalPixels);
                Marshal.Copy(nativeCbPtr, nativeOutCb, 0, totalPixels);
                Marshal.Copy(nativeCrPtr, nativeOutCr, 0, totalPixels);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativeRgbPtr);
                DjvuMarshal.FreeHGlobal(nativeYPtr);
                DjvuMarshal.FreeHGlobal(nativeCbPtr);
                DjvuMarshal.FreeHGlobal(nativeCrPtr);
            }

            // 3. Assert Byte-for-Byte Parity
            fixed (sbyte* pY = managedOutY)
            fixed (sbyte* pCb = managedOutCb)
            fixed (sbyte* pCr = managedOutCr)
            fixed (byte* pNtY = nativeOutY)
            fixed (byte* pNtCb = nativeOutCb)
            fixed (byte* pNtCr = nativeOutCr)
            {
                double diffY = Util.ImageBinaryDiff((byte*)pY, pNtY, width, height, width, 8, 8);
                double diffCb = Util.ImageBinaryDiff((byte*)pCb, pNtCb, width, height, width, 8, 8);
                double diffCr = Util.ImageBinaryDiff((byte*)pCr, pNtCr, width, height, width, 8, 8);

                Assert.Equal(0.0, diffY);
                Assert.Equal(0.0, diffCb);
                Assert.Equal(0.0, diffCr);
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCrNativeParityWithStride()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;

            // Inject padding to force rowSize > width.
            // This tests that the implementation respects pointer arithmetic
            // rather than flattening the 2D array into 1D.
            int paddingPixels = 17;
            int rowSize = width + paddingPixels; // Input stride (in pixels)
            int outRowSize = width + 5;          // Output stride (in bytes)

            int totalInPixels = height * rowSize;
            int totalInBytes = totalInPixels * 3; // 3 bytes per pixel for RGB

            int totalOutPixels = height * outRowSize;

            byte[] nativeRgbBuffer = new byte[totalInBytes];
            sbyte[] managedRgbBuffer = new sbyte[totalInBytes];

            // Generate synthetic random test data for the entire buffer (including padding)
            Random rnd = new Random(Seed);
            for (int i = 0; i < totalInBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeRgbBuffer[i] = val;
                managedRgbBuffer[i] = unchecked((sbyte)val);
            }

            sbyte[] managedOutY = new sbyte[totalOutPixels];
            sbyte[] managedOutCb = new sbyte[totalOutPixels];
            sbyte[] managedOutCr = new sbyte[totalOutPixels];

            byte[] nativeOutY = new byte[totalOutPixels];
            byte[] nativeOutCb = new byte[totalOutPixels];
            byte[] nativeOutCr = new byte[totalOutPixels];

            // 1. Process via Managed C#
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = managedOutY)
            fixed (sbyte* outCb = managedOutCb)
            fixed (sbyte* outCr = managedOutCr)
            {
                int inRowBytes = rowSize * sizeof(Pixel);
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, width, height, inRowBytes, outY, outCb, outCr, outRowSize);
            }

            // 2. Process via Native C++ DjVuLibre
            IntPtr nativeRgbPtr = IntPtr.Zero;
            IntPtr nativeYPtr = IntPtr.Zero;
            IntPtr nativeCbPtr = IntPtr.Zero;
            IntPtr nativeCrPtr = IntPtr.Zero;
            try
            {
                nativeRgbPtr = DjvuMarshal.AllocHGlobal((uint)totalInBytes);
                nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);
                nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);
                nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);

                Marshal.Copy(nativeRgbBuffer, 0, nativeRgbPtr, totalInBytes);

                // Call C++ reference implementation
                Assert.True(NativeMethods.RgbToY(nativeRgbPtr, width, height, rowSize, nativeYPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCb(nativeRgbPtr, width, height, rowSize, nativeCbPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCr(nativeRgbPtr, width, height, rowSize, nativeCrPtr, outRowSize));

                Marshal.Copy(nativeYPtr, nativeOutY, 0, totalOutPixels);
                Marshal.Copy(nativeCbPtr, nativeOutCb, 0, totalOutPixels);
                Marshal.Copy(nativeCrPtr, nativeOutCr, 0, totalOutPixels);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativeRgbPtr);
                DjvuMarshal.FreeHGlobal(nativeYPtr);
                DjvuMarshal.FreeHGlobal(nativeCbPtr);
                DjvuMarshal.FreeHGlobal(nativeCrPtr);
            }

            // 3. Assert Byte-for-Byte Parity
            // We ignore the padding bytes at the end of each row by providing outRowSize as stride.
            fixed (sbyte* pY = managedOutY)
            fixed (sbyte* pCb = managedOutCb)
            fixed (sbyte* pCr = managedOutCr)
            fixed (byte* pNtY = nativeOutY)
            fixed (byte* pNtCb = nativeOutCb)
            fixed (byte* pNtCr = nativeOutCr)
            {
                double diffY = Util.ImageBinaryDiff((byte*)pY, pNtY, width, height, outRowSize, 8, 8);
                double diffCb = Util.ImageBinaryDiff((byte*)pCb, pNtCb, width, height, outRowSize, 8, 8);
                double diffCr = Util.ImageBinaryDiff((byte*)pCr, pNtCr, width, height, outRowSize, 8, 8);

                Assert.Equal(0.0, diffY);
                Assert.Equal(0.0, diffCb);
                Assert.Equal(0.0, diffCr);
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCrUnifiedNativeParityRandomData()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;
            int rowSize = width; // rowSize in GPixels
            int outRowSize = width;
            int totalPixels = height * rowSize;
            int totalBytes = totalPixels * 3; // 3 bytes per pixel

            byte[] nativeRgbBuffer = new byte[totalBytes];
            sbyte[] managedRgbBuffer = new sbyte[totalBytes];

            Random rnd = new Random(Seed);
            for (int i = 0; i < totalBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeRgbBuffer[i] = val;
                managedRgbBuffer[i] = unchecked((sbyte)val);
            }

            sbyte[] scalarOutY = new sbyte[totalPixels];
            sbyte[] scalarOutCb = new sbyte[totalPixels];
            sbyte[] scalarOutCr = new sbyte[totalPixels];

            sbyte[] unifiedOutY = new sbyte[totalPixels];
            sbyte[] unifiedOutCb = new sbyte[totalPixels];
            sbyte[] unifiedOutCr = new sbyte[totalPixels];

            byte[] nativeOutY = new byte[totalPixels];
            byte[] nativeOutCb = new byte[totalPixels];
            byte[] nativeOutCr = new byte[totalPixels];

            // 1. Process via Scalar C#
#pragma warning disable CS0618 // Type or member is obsolete
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = scalarOutY, outCb = scalarOutCb, outCr = scalarOutCr)
            {
                InterWaveTransform.Rgb2YCbCrScalar((Pixel*)ptr, width, height, rowSize, outY, outCb, outCr, outRowSize);
            }
#pragma warning restore CS0618

            // 2. Process via Unified AVX2 C#
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = unifiedOutY, outCb = unifiedOutCb, outCr = unifiedOutCr)
            {
                int inRowBytes = rowSize * sizeof(Pixel);
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, width, height, inRowBytes, outY, outCb, outCr, outRowSize);
            }

            // 3. Process via Native C++ DjVuLibre
            IntPtr nativeRgbPtr = IntPtr.Zero;
            IntPtr nativeYPtr = IntPtr.Zero;
            IntPtr nativeCbPtr = IntPtr.Zero;
            IntPtr nativeCrPtr = IntPtr.Zero;
            try
            {
                nativeRgbPtr = DjvuMarshal.AllocHGlobal((uint)totalBytes);
                nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);

                // Zero out native buffers to prevent garbage padding mismatches
                new Span<byte>(nativeYPtr.ToPointer(), totalPixels).Clear();
                new Span<byte>(nativeCbPtr.ToPointer(), totalPixels).Clear();
                new Span<byte>(nativeCrPtr.ToPointer(), totalPixels).Clear();

                Marshal.Copy(nativeRgbBuffer, 0, nativeRgbPtr, totalBytes);

                Assert.True(NativeMethods.RgbToY(nativeRgbPtr, width, height, rowSize, nativeYPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCb(nativeRgbPtr, width, height, rowSize, nativeCbPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCr(nativeRgbPtr, width, height, rowSize, nativeCrPtr, outRowSize));

                Marshal.Copy(nativeYPtr, nativeOutY, 0, totalPixels);
                Marshal.Copy(nativeCbPtr, nativeOutCb, 0, totalPixels);
                Marshal.Copy(nativeCrPtr, nativeOutCr, 0, totalPixels);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativeRgbPtr);
                DjvuMarshal.FreeHGlobal(nativeYPtr);
                DjvuMarshal.FreeHGlobal(nativeCbPtr);
                DjvuMarshal.FreeHGlobal(nativeCrPtr);
            }

            // 4. Assert Byte-for-Byte Parity
            fixed (sbyte* pScY = scalarOutY)
            fixed (sbyte* pScCb = scalarOutCb)
            fixed (sbyte* pScCr = scalarOutCr)
            fixed (sbyte* pUnY = unifiedOutY)
            fixed (sbyte* pUnCb = unifiedOutCb)
            fixed (sbyte* pUnCr = unifiedOutCr)
            fixed (byte* pNtY = nativeOutY)
            fixed (byte* pNtCb = nativeOutCb)
            fixed (byte* pNtCr = nativeOutCr)
            {
                // Native vs Unified
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnY, pNtY, width, height, width, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnCb, pNtCb, width, height, width, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnCr, pNtCr, width, height, width, 8, 8));

                // Scalar vs Unified
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pScY, (byte*)pUnY, width, height, width, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pScCb, (byte*)pUnCb, width, height, width, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pScCr, (byte*)pUnCr, width, height, width, 8, 8));
            }
        }

        [Fact]
        public unsafe void Rgb2YCbCrUnifiedNativeParityWithStride()
        {
            int width = DefaultWidth;
            int height = DefaultHeight;

            int paddingPixels = 17;
            int rowSize = width + paddingPixels; // Input stride (in pixels)
            int outRowSize = width + 5;          // Output stride (in bytes)

            int totalInPixels = height * rowSize;
            int totalInBytes = totalInPixels * 3; // 3 bytes per pixel for RGB

            int totalOutPixels = height * outRowSize;

            byte[] nativeRgbBuffer = new byte[totalInBytes];
            sbyte[] managedRgbBuffer = new sbyte[totalInBytes];

            Random rnd = new Random(Seed);
            for (int i = 0; i < totalInBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                nativeRgbBuffer[i] = val;
                managedRgbBuffer[i] = unchecked((sbyte)val);
            }

            sbyte[] scalarOutY = new sbyte[totalOutPixels];
            sbyte[] scalarOutCb = new sbyte[totalOutPixels];
            sbyte[] scalarOutCr = new sbyte[totalOutPixels];

            sbyte[] unifiedOutY = new sbyte[totalOutPixels];
            sbyte[] unifiedOutCb = new sbyte[totalOutPixels];
            sbyte[] unifiedOutCr = new sbyte[totalOutPixels];

            byte[] nativeOutY = new byte[totalOutPixels];
            byte[] nativeOutCb = new byte[totalOutPixels];
            byte[] nativeOutCr = new byte[totalOutPixels];

            // 1. Process via Scalar C#
#pragma warning disable CS0618 // Type or member is obsolete
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = scalarOutY, outCb = scalarOutCb, outCr = scalarOutCr)
            {
                InterWaveTransform.Rgb2YCbCrScalar((Pixel*)ptr, width, height, rowSize, outY, outCb, outCr, outRowSize);
            }
#pragma warning restore CS0618

            // 2. Process via Unified AVX2 C#
            fixed (sbyte* ptr = managedRgbBuffer)
            fixed (sbyte* outY = unifiedOutY, outCb = unifiedOutCb, outCr = unifiedOutCr)
            {
                int inRowBytes = rowSize * sizeof(Pixel);
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, width, height, inRowBytes, outY, outCb, outCr, outRowSize);
            }

            // 3. Process via Native C++ DjVuLibre
            IntPtr nativeRgbPtr = IntPtr.Zero;
            IntPtr nativeYPtr = IntPtr.Zero;
            IntPtr nativeCbPtr = IntPtr.Zero;
            IntPtr nativeCrPtr = IntPtr.Zero;
            try
            {
                nativeRgbPtr = DjvuMarshal.AllocHGlobal((uint)totalInBytes);
                nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);
                nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);
                nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalOutPixels);

                // Zero out native buffers to prevent garbage padding mismatches
                new Span<byte>(nativeYPtr.ToPointer(), totalOutPixels).Clear();
                new Span<byte>(nativeCbPtr.ToPointer(), totalOutPixels).Clear();
                new Span<byte>(nativeCrPtr.ToPointer(), totalOutPixels).Clear();

                Marshal.Copy(nativeRgbBuffer, 0, nativeRgbPtr, totalInBytes);

                Assert.True(NativeMethods.RgbToY(nativeRgbPtr, width, height, rowSize, nativeYPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCb(nativeRgbPtr, width, height, rowSize, nativeCbPtr, outRowSize));
                Assert.True(NativeMethods.RgbToCr(nativeRgbPtr, width, height, rowSize, nativeCrPtr, outRowSize));

                Marshal.Copy(nativeYPtr, nativeOutY, 0, totalOutPixels);
                Marshal.Copy(nativeCbPtr, nativeOutCb, 0, totalOutPixels);
                Marshal.Copy(nativeCrPtr, nativeOutCr, 0, totalOutPixels);
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(nativeRgbPtr);
                DjvuMarshal.FreeHGlobal(nativeYPtr);
                DjvuMarshal.FreeHGlobal(nativeCbPtr);
                DjvuMarshal.FreeHGlobal(nativeCrPtr);
            }

            // 4. Assert Byte-for-Byte Parity (ignoring padding)
            fixed (sbyte* pScY = scalarOutY)
            fixed (sbyte* pScCb = scalarOutCb)
            fixed (sbyte* pScCr = scalarOutCr)
            fixed (sbyte* pUnY = unifiedOutY)
            fixed (sbyte* pUnCb = unifiedOutCb)
            fixed (sbyte* pUnCr = unifiedOutCr)
            fixed (byte* pNtY = nativeOutY)
            fixed (byte* pNtCb = nativeOutCb)
            fixed (byte* pNtCr = nativeOutCr)
            {
                // Native vs Unified (Must match exactly)
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnY, pNtY, width, height, outRowSize, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnCb, pNtCb, width, height, outRowSize, 8, 8));
                Assert.Equal(0.0, Util.ImageBinaryDiff((byte*)pUnCr, pNtCr, width, height, outRowSize, 8, 8));

                // Scalar vs Unified (Must fail because Scalar ignores stride)
                double diffScY = Util.ImageBinaryDiff((byte*)pScY, (byte*)pUnY, width, height, outRowSize, 8, 8);
                double diffScCb = Util.ImageBinaryDiff((byte*)pScCb, (byte*)pUnCb, width, height, outRowSize, 8, 8);
                double diffScCr = Util.ImageBinaryDiff((byte*)pScCr, (byte*)pUnCr, width, height, outRowSize, 8, 8);

                bool scalarFailed = diffScY > 0.0 || diffScCb > 0.0 || diffScCr > 0.0;
                Assert.True(scalarFailed, "Expected the legacy scalar method to fail parity on padded buffers.");
            }
        }
    }
}