using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Xunit;

namespace DjvuNet.Tests.UtilTests
{
    public class ImageBinaryDiffTests
    {
        [Fact]
        public unsafe void ImageBinaryDiff_RawPointers_WithPadding_CalculatesCorrectly()
        {
            // We want > 128 bytes per row to hit the AVX2 loop (Avx2.LoadDquVector256)
            // 24bpp = 3 bytes per pixel.
            // Width 130 * 3 = 390 bytes per row.
            // We add 2 bytes of padding, so Stride = 392 bytes.
            int width = 130;
            int height = 3;
            int pixelSize = 24; // 24 bpp (3 bytes/pixel)
            int stride = 392; 
            int totalBytes = height * stride;

            byte[] buffer1 = new byte[totalBytes];
            byte[] buffer2 = new byte[totalBytes];

            // Initialize buffers with identical data
            for (int i = 0; i < totalBytes; i++)
            {
                byte val = (byte)(i % 256);
                buffer1[i] = val;
                buffer2[i] = val;
            }

            // Introduce 1 intentional difference in the visible area of Row 1
            // Row 1 offset = 1 * stride = 392.
            int diffIndex1 = 392 + 50; 
            buffer2[diffIndex1] = (byte)((buffer1[diffIndex1] + 10) % 256);

            // Introduce 1 intentional difference in the visible area of Row 2
            int diffIndex2 = (2 * stride) + 120;
            buffer2[diffIndex2] = (byte)((buffer1[diffIndex2] + 20) % 256);

            // Introduce a difference in the PADDING area of Row 0.
            // Visible bytes end at index 389. Indices 390 and 391 are padding.
            buffer2[390] = 255; 

            // Calculate expected difference
            // ImageBinaryDiff calculates the average absolute difference per channel per pixel across the whole image.
            // Total visible pixels = 130 * 3 = 390
            // Total channels = 390 * 3 = 1170.
            // Max channel value = 255.
            // Diff 1 = 10. Diff 2 = 20.
            // Expected result = (10 + 20) / (width * height * (pixelSize/channelSize) * 255)
            double expectedDiff = (10.0 + 20.0) / (130 * 3 * 3 * 255.0);

            fixed (byte* ptr1 = buffer1)
            fixed (byte* ptr2 = buffer2)
            {
                // The method should ignore the difference in the padding byte (index 390)
                double actualDiff = Util.ImageBinaryDiff(ptr1, ptr2, width, height, stride, pixelSize);

                // AVX2 uses floats internally for some calculations before casting to double, 
                // so we assert with a small precision tolerance.
                Assert.Equal(expectedDiff, actualDiff, 5);
            }
        }

        [Fact]
        public void ImageBinaryDiff_BitmapData_WithPadding_CalculatesCorrectly()
        {
            int width = 130;
            int height = 3;
            
            // Create two identical bitmaps
            using (Bitmap bmp1 = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (Bitmap bmp2 = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                // Bitmaps automatically pad strides to 4-byte boundaries. 
                // 130 * 3 = 390 bytes. Padded to nearest multiple of 4 = 392 bytes.
                
                // Introduce differences
                bmp1.SetPixel(10, 1, Color.FromArgb(100, 100, 100));
                bmp2.SetPixel(10, 1, Color.FromArgb(110, 100, 100)); // R diff = 10

                bmp1.SetPixel(40, 2, Color.FromArgb(50, 50, 50));
                bmp2.SetPixel(40, 2, Color.FromArgb(50, 70, 50)); // G diff = 20

                double expectedDiff = (10.0 + 20.0) / (width * height * 3 * 255.0);

                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData data1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                BitmapData data2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                try
                {
                    double actualDiff = Util.ImageBinaryDiff(data1, data2);
                    Assert.Equal(expectedDiff, actualDiff, 5);
                }
                finally
                {
                    bmp1.UnlockBits(data1);
                    bmp2.UnlockBits(data2);
                }
            }
        }
    }
}
