using Xunit;
using DjvuNet.Wavelet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DjvuNet.Graphics;
using System.Runtime.InteropServices;
using DjvuNet.Graphics.Tests;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DjvuNet.Wavelet.Tests
{
    public class InterWaveTransformTests
    {
        int width = 4160;
        int height = 2160;
        int bytesPerPixel = 3;
        int testsToSkip = 0;
        int testCount = 1;

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void ForwardTest()
        {
            Assert.Fail("This test needs an implementation");
        }

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void BackwardTest()
        {
            Assert.Fail("This test needs an implementation");
        }

        [Theory]
        // --- Data Generated via Native C++ DjVuLibre (ddjvu_iw44_rgb_to_y) ---
        [InlineData(  0,   0,   0, 128)] // Black
        [InlineData(255, 255, 255, 127)] // White
        [InlineData(255,   0,   0, 206)] // Pure Red
        [InlineData(  0, 255,   0,  27)] // Pure Green
        [InlineData(  0,   0, 255, 150)] // Pure Blue
        [InlineData(255, 255,   0, 105)] // Yellow
        [InlineData(  0, 255, 255,  49)] // Cyan
        [InlineData(255,   0, 255, 228)] // Magenta
        [InlineData(128, 128, 128,   0)] // Mid Gray
        [InlineData( 64,  64,  64, 192)] // Dark Gray
        [InlineData(192, 192, 192,  64)] // Light Gray
        [InlineData(128,   0,   0, 167)] // Half Red
        [InlineData(  0, 128,   0, 206)] // Half Green
        [InlineData(  0,   0, 128, 139)] // Half Blue
        [InlineData(255, 128,   0,  28)] // Orange
        [InlineData(255,   0, 128, 217)] // Pink/Rose
        [InlineData(128, 255,   0,  66)] // Yellow-Green
        [InlineData(  0, 255, 128,  38)] // Sea Green
        [InlineData( 64, 128, 192, 242)] // Steel Blue
        [InlineData(192, 128,  64,  14)] // Tan/Brown
        public unsafe void Rgb2Y_MathParity(byte r, byte g, byte b, byte expY)
        {
            Pixel[] pixBuffer = new Pixel[256];
            int[] edgeCases = new int[] { 0, 15, 16, 128, 254, 255 };

            foreach (int i in edgeCases)
            {
                pixBuffer[i].Red = unchecked((sbyte)r);
                pixBuffer[i].Green = unchecked((sbyte)g);
                pixBuffer[i].Blue = unchecked((sbyte)b);
            }

            sbyte[] outY = new sbyte[256];
            fixed (Pixel* pBuff = pixBuffer)
            fixed (sbyte* pY = outY)
            {
                InterWaveTransform.Rgb2Y(pBuff, 16, 16, 16, pY, 16);
            }

            foreach (int i in edgeCases)
            {
                Assert.Equal(expY, unchecked((byte)outY[i]));
            }
        }

        [Theory]
        // --- Data Generated via Native C++ DjVuLibre (ddjvu_iw44_rgb_to_ycbcr) ---
        [InlineData(  0,   0,   0,   0)] // Black
        [InlineData(255, 255, 255,   0)] // White
        [InlineData(255,   0,   0, 212)] // Pure Red
        [InlineData(  0, 255,   0, 167)] // Pure Green
        [InlineData(  0,   0, 255, 127)] // Pure Blue
        [InlineData(255, 255,   0, 128)] // Yellow
        [InlineData(  0, 255, 255,  44)] // Cyan
        [InlineData(255,   0, 255,  89)] // Magenta
        [InlineData(128, 128, 128,   0)] // Mid Gray
        [InlineData( 64,  64,  64,   0)] // Dark Gray
        [InlineData(192, 192, 192,   0)] // Light Gray
        [InlineData(128,   0,   0, 234)] // Half Red
        [InlineData(  0, 128,   0, 211)] // Half Green
        [InlineData(  0,   0, 128,  67)] // Half Blue
        [InlineData(255, 128,   0, 167)] // Orange
        [InlineData(255,   0, 128,  22)] // Pink/Rose
        [InlineData(128, 255,   0, 145)] // Yellow-Green
        [InlineData(  0, 255, 128, 234)] // Sea Green
        [InlineData( 64, 128, 192,  45)] // Steel Blue
        [InlineData(192, 128,  64, 211)] // Tan/Brown
        public unsafe void Rgb2Cb_MathParity(byte r, byte g, byte b, byte expCb)
        {
            Pixel[] pixBuffer = new Pixel[256];
            int[] edgeCases = new int[] { 0, 15, 16, 128, 254, 255 };

            foreach (int i in edgeCases)
            {
                pixBuffer[i].Red = unchecked((sbyte)r);
                pixBuffer[i].Green = unchecked((sbyte)g);
                pixBuffer[i].Blue = unchecked((sbyte)b);
            }

            sbyte[] outCb = new sbyte[256];
            fixed (Pixel* pBuff = pixBuffer)
            fixed (sbyte* pCb = outCb)
            {
                InterWaveTransform.Rgb2Cb(pBuff, 16, 16, 16, pCb, 16);
            }

            foreach (int i in edgeCases)
            {
                Assert.Equal(expCb, unchecked((byte)outCb[i]));
            }
        }

        [Theory]
        [InlineData(  0,   0,   0,   0)] // Black
        [InlineData(255, 255, 255,   0)] // White
        [InlineData(255,   0,   0, 118)] // Pure Red
        [InlineData(  0, 255,   0, 153)] // Pure Green
        [InlineData(  0,   0, 255, 241)] // Pure Blue
        [InlineData(255, 255,   0,  15)] // Yellow
        [InlineData(  0, 255, 255, 138)] // Cyan
        [InlineData(255,   0, 255, 103)] // Magenta
        [InlineData(128, 128, 128,   0)] // Mid Gray
        [InlineData( 64,  64,  64,   0)] // Dark Gray
        [InlineData(192, 192, 192,   0)] // Light Gray
        [InlineData(128,   0,   0,  59)] // Half Red
        [InlineData(  0, 128,   0, 204)] // Half Green
        [InlineData(  0,   0, 128, 249)] // Half Blue
        [InlineData(255, 128,   0,  66)] // Orange
        [InlineData(255,   0, 128, 111)] // Pink/Rose
        [InlineData(128, 255,   0, 212)] // Yellow-Green
        [InlineData(  0, 255, 128, 145)] // Sea Green
        [InlineData( 64, 128, 192, 223)] // Steel Blue
        [InlineData(192, 128,  64,  33)] // Tan/Brown
        public unsafe void Rgb2Cr_MathParity(byte r, byte g, byte b, byte expCr)
        {
            Pixel[] pixBuffer = new Pixel[256];
            int[] edgeCases = new int[] { 0, 15, 16, 128, 254, 255 };

            foreach (int i in edgeCases)
            {
                pixBuffer[i].Red = unchecked((sbyte)r);
                pixBuffer[i].Green = unchecked((sbyte)g);
                pixBuffer[i].Blue = unchecked((sbyte)b);
            }

            sbyte[] outCr = new sbyte[256];
            fixed (Pixel* pBuff = pixBuffer)
            fixed (sbyte* pCr = outCr)
            {
                InterWaveTransform.Rgb2Cr(pBuff, 16, 16, 16, pCr, 16);
            }

            foreach (int i in edgeCases)
            {
                Assert.Equal(expCr, unchecked((byte)outCr[i]));
            }
        }

        [Theory]
        // --- Data Generated via Native C++ DjVuLibre ---
        //                         Y,   Cb,   Cr,    R,    G,    B
        [InlineData(-128,    0,    0,    0,    0,    0)] // Black (Original: 0, 0, 0)
        [InlineData( 127,    0,    0,  255,  255,  255)] // White (Original: 255, 255, 255)
        [InlineData( -50,  -44,  118,  255,    1,    1)] // Pure Red (Original: 255, 0, 0)
        [InlineData(  27,  -89, -103,    0,  255,    0)] // Pure Green (Original: 0, 255, 0)
        [InlineData(-106,  127,  -15,    0,    3,  245)] // Pure Blue (Original: 0, 0, 255) (245 = -11)
        [InlineData( 105, -128,   15,  255,  254,    9)] // Yellow (Original: 255, 255, 0) (254 = -2)
        [InlineData(  49,   44, -118,    0,  255,  254)] // Cyan (Original: 0, 255, 255)
        [InlineData( -28,   89,  103,  254,    1,  255)] // Magenta (Original: 255, 0, 255)
        [InlineData(   0,    0,    0,  128,  128,  128)] // Mid Gray (Original: 128, 128, 128)
        [InlineData( -64,    0,    0,   64,   64,   64)] // Dark Gray (Original: 64, 64, 64)
        [InlineData(  64,    0,    0,  192,  192,  192)] // Light Gray (Original: 192, 192, 192)
        [InlineData( -89,  -22,   59,  127,    1,    1)] // Half Red (Original: 128, 0, 0)
        [InlineData( -50,  -45,  -52,    0,  129,    0)] // Half Green (Original: 0, 128, 0) (129 = -127)
        [InlineData(-117,   67,   -7,    0,    1,  129)] // Half Blue (Original: 0, 0, 128)
        [InlineData(  28,  -89,   66,  255,  130,    1)] // Orange (Original: 255, 128, 0) (130 = -126)
        [InlineData( -39,   22,  111,  255,    1,  128)] // Pink/Rose (Original: 255, 0, 128)
        [InlineData(  66, -111,  -44,  128,  255,    0)] // Yellow-Green (Original: 128, 255, 0)
        [InlineData(  38,  -22, -111,    0,  255,  128)] // Sea Green (Original: 0, 255, 128)
        [InlineData( -14,   45,  -33,   64,  128,  193)] // Steel Blue (Original: 64, 128, 192) (193 = -63)
        [InlineData(  14,  -45,   33,  191,  130,   64)] // Tan/Brown (Original: 192, 128, 64) (191 = -65)
        public unsafe void YCbCr2Rgb_MathParity(sbyte inY, sbyte inCb, sbyte inCr, byte expR, byte expG, byte expB)
        {
            Pixel[] pixBuffer = new Pixel[256];
            int[] edgeCases = new int[] { 0, 15, 16, 128, 254, 255 };

            // Initialize buffer
            foreach (int i in edgeCases)
            {
                pixBuffer[i].Blue = inY;
                pixBuffer[i].Green = inCb;
                pixBuffer[i].Red = inCr;
            }

            fixed (Pixel* pBuff = pixBuffer)
            {
                InterWaveTransform.YCbCr2Rgb(pBuff, 16, 16, 16 * sizeof(Pixel));
            }

            // Assert exact conversion
            foreach (int i in edgeCases)
            {
                Assert.Equal(expR, unchecked((byte)pixBuffer[i].Red));
                Assert.Equal(expG, unchecked((byte)pixBuffer[i].Green));
                Assert.Equal(expB, unchecked((byte)pixBuffer[i].Blue));
            }
        }

        [Fact()]
#if NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void Rgb2YCbCrFragmentedTest()
        {
            sbyte[] data = PixelMapTests.GetRandomData(width, height, bytesPerPixel);

            long time = 0;
            Stopwatch watch = new Stopwatch();

            sbyte[] outY = new sbyte[width * height];
            sbyte[] outCb = new sbyte[width * height];
            sbyte[] outCr = new sbyte[width * height];

            for (int i = 0; i < testCount; i++)
            {
                watch.Restart();
                unsafe
                {
                    GCHandle hData = GCHandle.Alloc(data, GCHandleType.Pinned);
                    GCHandle hOutY = GCHandle.Alloc(outY, GCHandleType.Pinned);
                    GCHandle hOutCb = GCHandle.Alloc(outCb, GCHandleType.Pinned);
                    GCHandle hOutCr = GCHandle.Alloc(outCr, GCHandleType.Pinned);
                    sbyte* pY = (sbyte*) hOutY.AddrOfPinnedObject();
                    sbyte* pCb = (sbyte*) hOutCb.AddrOfPinnedObject();
                    sbyte* pCr = (sbyte*) hOutCr.AddrOfPinnedObject();
                    Pixel* pPix = (Pixel*)hData.AddrOfPinnedObject();

                    InterWaveTransform.Rgb2Y(pPix, width, height, width * bytesPerPixel, pY, width);
                    InterWaveTransform.Rgb2Cb(pPix, width, height, width * bytesPerPixel, pCb, width);
                    InterWaveTransform.Rgb2Cr(pPix, width, height, width * bytesPerPixel, pCr, width);

                    hData.Free();
                    hOutY.Free();
                    hOutCb.Free();
                    hOutCr.Free();
                }
                watch.Stop();
                if (i >= testsToSkip)
                    time += watch.ElapsedMilliseconds;
            }

            Console.WriteLine($"Fragmented Rgb2YCbCr conversion time ms {((double)time / testCount).ToString("0#.000")}");
        }

        [Fact()]
#if NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void Rgb2YCbCrOptimizedTest()
        {

            sbyte[] data = PixelMapTests.GetRandomData(width, height, bytesPerPixel);

            long time = 0;
            Stopwatch watch = new Stopwatch();

            for (int i = 0; i < testCount + testsToSkip; i++)
            {
                watch.Restart();
                sbyte[] outY = new sbyte[width * height];
                sbyte[] outCb = new sbyte[width * height];
                sbyte[] outCr = new sbyte[width * height];

                unsafe
                {
                    fixed (sbyte* pData = data)
                    fixed (sbyte* pOutY = outY)
                    fixed (sbyte* pOutCb = outCb)
                    fixed (sbyte* pOutCr = outCr)
                    {
                        Pixel* pPix = (Pixel*)pData;
                        InterWaveTransform.Rgb2YCbCr(pPix, width, height, width * bytesPerPixel, pOutY, pOutCb, pOutCr, width);
                    }
                }
                watch.Stop();
                if (i >= testsToSkip)
                    time += watch.ElapsedMilliseconds;
            }

            Console.WriteLine($"Optimized Rgb2YCbCr conversion time ms {((double)time / testCount).ToString("0#.000")}");
        }

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void FilterFvTest()
        {
            Assert.Fail("This test needs an implementation");
        }

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void FilterFhTest()
        {
            Assert.Fail("This test needs an implementation");
        }

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void FilterBvTest()
        {

        }

        [Fact(Skip = "Not implemented"), Trait("Category", "Skip")]
        public void FilterBhTest()
        {
            Assert.Fail("This test needs an implementation");
        }
    }
}
