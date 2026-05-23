using System;
using System.Runtime.InteropServices;
using DjvuNet.DjvuLibre;
using DjvuNet.Wavelet;
using DjvuNet.Graphics;
using Xunit;

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
            
            sbyte[] managedBuffer = new sbyte[totalBytes];
            byte[] nativeBuffer = new byte[totalBytes]; 

            // Generate some random test data to represent Y, Cb, Cr
            Random rnd = new Random(Seed);
            for (int i = 0; i < totalBytes; i++)
            {
                byte val = (byte)rnd.Next(256);
                managedBuffer[i] = unchecked((sbyte)val);
                nativeBuffer[i] = val;
            }

            // 1. Process via Managed C#
            fixed (sbyte* ptr = managedBuffer)
            {
                // YCbCr2Rgb assumes continuous array of Pixels.
                InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, width, height);
            }

            // 2. Process via Native C++ DjVuLibre
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

            // 3. Assert Byte-for-Byte Parity
            for (int i = 0; i < totalBytes; i++)
            {
                byte managedByte = unchecked((byte)managedBuffer[i]);
                Assert.Equal(nativeBuffer[i], managedByte);
            }
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
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, width, height, rowSize, outY, outCb, outCr, outRowSize);
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
            for (int i = 0; i < totalPixels; i++)
            {
                byte managedByteY = unchecked((byte)managedOutY[i]);
                Assert.Equal(nativeOutY[i], managedByteY);

                byte managedByteCb = unchecked((byte)managedOutCb[i]);
                Assert.Equal(nativeOutCb[i], managedByteCb);

                byte managedByteCr = unchecked((byte)managedOutCr[i]);
                Assert.Equal(nativeOutCr[i], managedByteCr);
            }
        }
    }
}