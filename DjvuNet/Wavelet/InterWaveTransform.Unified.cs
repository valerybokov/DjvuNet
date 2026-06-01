using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using DjvuNet.Errors;
using DjvuNet.Graphics;

namespace DjvuNet.Wavelet
{
    public static partial class InterWaveTransform
    {
        /// <summary>
        /// Convenience overload for contiguous input buffers. Calculates the input row stride in bytes as (width * sizeof(Pixel))
        /// and delegates to the primary byte-stride implementation.
        /// </summary>
        /// <param name="pPixBuff">Pointer to the input BGR pixel buffer.</param>
        /// <param name="width">The visible width of the image in pixels.</param>
        /// <param name="height">The visible height of the image in pixels.</param>
        /// <param name="pOutY">Pointer to the output Y (luminance) buffer.</param>
        /// <param name="pOutCb">Pointer to the output Cb (blue chrominance) buffer.</param>
        /// <param name="pOutCr">Pointer to the output Cr (red chrominance) buffer.</param>
        /// <param name="outRowSizeInBytes">The exact stride of the output planar buffers in bytes.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Rgb2YCbCr(Pixel* pPixBuff, int width, int height, sbyte* pOutY, sbyte* pOutCb, sbyte* pOutCr, int outRowSizeInBytes)
        {
            int inRowBytes = width * sizeof(Pixel);
            Rgb2YCbCr(pPixBuff, width, height, inRowBytes, pOutY, pOutCb, pOutCr, outRowSizeInBytes);
        }

        /// <summary>
        /// Calculates the optimal number of threads for parallel SIMD execution based on empirical throughput curves.
        /// Returns 1 to indicate the caller should bypass the TPL and fall back to a single-threaded scalar loop.
        /// </summary>
        /// <remarks>
        /// ARCHITECTURAL RATIONALE:
        /// This step-function matrix is derived from extensive benchmarking of the TPL (Task Parallel Library) overhead
        /// versus SIMD throughput. Below is the full empirical data demonstrating the exact threshold crossings
        /// where scaling threads becomes profitable, measured in Time per Operation (Real).
        ///
        /// | Image Size |   V128 (1T) |  V128 (2T) |  V128 (4T) |  V128 (6T) | V128 (12T) |   V256 (1T) |  V256 (2T) |  V256 (4T) |  V256 (6T) | V256 (12T) |
        /// |-----------:|------------:|-----------:|-----------:|-----------:|-----------:|------------:|-----------:|-----------:|-----------:|-----------:|
        /// |      1,024 |   *1.81 µs* |    2.59 µs |    4.12 µs |    4.80 µs |    6.16 µs |   *0.55 µs* |    1.82 µs |    2.85 µs |    3.49 µs |    4.60 µs |
        /// |      4,096 |   *7.15 µs* |    5.83 µs |    7.16 µs |    7.90 µs |    9.29 µs |   *2.13 µs* |    3.55 µs |    4.94 µs |    5.57 µs |    6.87 µs |
        /// |      9,216 |   16.00 µs  | *10.60 µs* | *10.60 µs* |   11.00 µs |   13.10 µs |   *4.88 µs* |    5.06 µs |    6.85 µs |    7.68 µs |    9.14 µs |
        /// |     16,384 |   28.50 µs  |   19.00 µs | *13.50 µs* |   13.80 µs |   16.60 µs |    8.59 µs  |  *7.05 µs* |    8.67 µs |    9.45 µs |   11.90 µs |
        /// |     36,864 |   63.50 µs  |   38.10 µs |   24.80 µs | *23.40 µs* |   25.20 µs |   18.90 µs  |   14.20 µs | *12.00 µs* |   13.10 µs |   16.20 µs |
        /// |     65,536 |  113.20 µs  |   61.70 µs |   37.00 µs | *33.00 µs* |   35.60 µs |   34.30 µs  |   23.10 µs | *16.80 µs* |   18.80 µs |   22.60 µs |
        /// |    262,144 |  455.00 µs  |  237.60 µs |  127.70 µs | *92.60 µs* |   98.30 µs |  139.70 µs  |   76.50 µs | *53.00 µs* |   54.30 µs |   59.90 µs |
        /// |  1,048,576 | 1813.00 µs  |  935.10 µs |  491.60 µs |  345.80 µs |*323.40 µs* |  567.40 µs  |  300.40 µs |*203.80 µs* |  209.40 µs |  223.50 µs |
        /// |  2,096,704 | 3645.00 µs  | 1860.00 µs |  964.40 µs |  673.10 µs |*601.00 µs* | 1118.00 µs  |  592.80 µs |  429.00 µs |*426.70 µs* |  450.40 µs |
        /// |  4,194,304 | 7262.00 µs  | 3752.00 µs | 1919.00 µs | 1316.00 µs |*1183.00 µs*| 2249.00 µs  | 1319.00 µs |  985.20 µs |*968.40 µs* | 1027.00 µs |
        /// | 20,081,328 |34860.00 µs  |17694.00 µs | 9124.00 µs | 6387.00 µs |*6151.00 µs*|10588.00 µs  | 6128.00 µs |*5888.00 µs*| 6081.00 µs | 6158.00 µs |
        ///
        /// 1. Vector256 (AVX2): The pipeline executes extremely fast, meaning TPL synchronization overhead dominates on
        ///    smaller payloads. AVX2 requires roughly 12,000 pixels before 2 threads beat a single thread, and ~24,000
        ///    pixels before 4 threads become optimal. Furthermore, benchmark data (e.g., processing a 20 Megapixel image)
        ///    shows absolute memory bus saturation at 4 to 6 threads. Throwing 12 threads at AVX2 actively harms
        ///    performance due to cache thrashing. Therefore, AVX2 parallelism is strictly capped at 6.
        ///
        /// 2. Vector128 (SSSE3/AdvSimd): This pipeline executes slower, meaning the TPL overhead is amortized much earlier
        ///    (breaking even at ~2,000 pixels for 2 threads). Because the data ingestion rate per core is lower, the
        ///    memory bus can sustain more active threads before saturating, allowing positive scaling up to 12 threads
        ///    on large images (1M+ pixels).
        ///
        /// MEMORY TOPOLOGY DISCLAIMER:
        /// The saturation caps (6 for AVX2, 12 for Vector128) are calibrated against standard dual-channel (2-channel)
        /// memory configurations typical of consumer hardware. We did not benchmark quad-channel or octa-channel
        /// memory configurations, as DjvuNet is not expected to be primarily utilized on enterprise server-grade
        /// or workstation hardware (e.g., EPYC, Threadripper, Xeon). This missing optimization for high-bandwidth
        /// server topologies can be addressed in future iterations if required.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetOptimalDegreeOfParallelism(long pixelCount)
        {
            int targetThreads = 1;
            int maxAvailableThreads = Environment.ProcessorCount;

            if (Avx2.IsSupported)
            {
                // AVX2 Optimization Matrix
                // Saturates memory bus quickly; capped at 6. TPL overhead is heavy due to very fast execution time.
                if (pixelCount >= 2_000_000)
                    targetThreads = 6;
                else if (pixelCount >= 24_000)
                    targetThreads = 4;
                else if (pixelCount >= 12_000)
                    targetThreads = 2;
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                // Vector128 (SSSE3/AdvSimd) Optimization Matrix
                // Processes slower; requires less payload per thread to overcome TPL overhead,
                // and memory bus saturates later (capped at 12).
                if (pixelCount >= 1_000_000)
                    targetThreads = 12;
                else if (pixelCount >= 24_000)
                    targetThreads = 6;
                else if (pixelCount >= 9_216)
                    targetThreads = 4;
                else if (pixelCount >= 2_000)
                    targetThreads = 2;
            }

            return Math.Min(targetThreads, maxAvailableThreads);
        }


        /// <summary>
        /// Core hardware-accelerated Color Space Conversion from BGR to YCbCr with explicit byte-stride support.
        /// Dynamically routes execution to optimal SIMD implementations (AVX2, SSSE3/AdvSimd) or falls back to
        /// scalar processing based on hardware support and image dimensions.
        /// </summary>
        /// <param name="pPixBuff">Pointer to the input BGR pixel buffer.</param>
        /// <param name="width">The visible width of the image in pixels.</param>
        /// <param name="height">The visible height of the image in pixels.</param>
        /// <param name="rowSizeInBytes">The exact stride of the input BGR buffer in bytes, including any padding.</param>
        /// <param name="pOutY">Pointer to the output Y (luminance) buffer.</param>
        /// <param name="pOutCb">Pointer to the output Cb (blue chrominance) buffer.</param>
        /// <param name="pOutCr">Pointer to the output Cr (red chrominance) buffer.</param>
        /// <param name="outRowSizeInBytes">The exact stride of the output planar buffers in bytes, including any padding.</param>
        /// <exception cref="DjvuArgumentNullException">Thrown if any of the pointer arguments are null.</exception>
        /// <exception cref="DjvuArgumentOutOfRangeException">Thrown if width or height is less than or equal to zero, or if stride dimensions are mathematically invalid for the given width.</exception>
        /// <exception cref="DjvuInvalidOperationException">Thrown if the input buffer overlaps with any of the output buffers.</exception>
        /// <remarks>
        /// ARCHITECTURAL NOTE: This implementation intentionally diverges from the native DjVuLibre C++ behavior.
        ///
        /// The native C++ library assumes the input stride is always a perfect multiple of the Pixel struct size (3 bytes),
        /// relying on simple GPixel pointer arithmetic. This creates a brittle constraint that fails if the input buffer
        /// utilizes arbitrary byte-aligned padding (e.g., 4-byte boundaries common in Windows GDI+ Bitmaps).
        ///
        /// To ensure DjvuNet is robust in all interop scenarios, this method explicitly defines the input
        /// stride (`rowSizeInBytes`) strictly in bytes. It relies on the out-of-place nature of the transform
        /// to utilize a highly optimized pointer-shift technique for processing row tails, ensuring memory safety
        /// against any arbitrary OS boundary without duplicating math logic.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void Rgb2YCbCr(
            Pixel* pPixBuff,
            int width,
            int height,
            int rowSizeInBytes,
            sbyte* pOutY,
            sbyte* pOutCb,
            sbyte* pOutCr,
            int outRowSizeInBytes)
        {
            if (pPixBuff == null)
            {
                throw new DjvuArgumentNullException(nameof(pPixBuff));
            }
            if (pOutY == null)
            {
                throw new DjvuArgumentNullException(nameof(pOutY));
            }
            if (pOutCb == null)
            {
                throw new DjvuArgumentNullException(nameof(pOutCb));
            }
            if (pOutCr == null)
            {
                throw new DjvuArgumentNullException(nameof(pOutCr));
            }
            if (width <= 0)
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(width),
                    width,
                    "Width must be greater than zero.");
            }
            if (height <= 0)
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(height),
                    height,
                    "Height must be greater than zero.");
            }
            if (rowSizeInBytes < (long)width * sizeof(Pixel))
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(rowSizeInBytes),
                    rowSizeInBytes,
                    $"Input row size ({rowSizeInBytes}) must be at least width * sizeof(Pixel) ({(long)width * sizeof(Pixel)}).");
            }
            if (outRowSizeInBytes < width)
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(outRowSizeInBytes),
                    outRowSizeInBytes,
                    $"Output row size ({outRowSizeInBytes}) must be at least width ({width}).");
            }

            Pixel* pIn = pPixBuff;
            sbyte* pY = pOutY;
            sbyte* pCb = pOutCb;
            sbyte* pCr = pOutCr;

            // SAFETY CHECK: Ensure input buffer does not overlap with output buffers.
            // If they overlap, the pointer-shift tail optimization will cause read-after-write corruption.
            byte* startIn = (byte*)pPixBuff;
            byte* endIn = startIn + ((long)height * rowSizeInBytes);

            byte* startY = (byte*)pOutY;
            byte* endY = startY + ((long)height * outRowSizeInBytes);
            byte* startCb = (byte*)pOutCb;
            byte* endCb = startCb + ((long)height * outRowSizeInBytes);
            byte* startCr = (byte*)pOutCr;
            byte* endCr = startCr + ((long)height * outRowSizeInBytes);
            if ((startIn < endY && endIn > startY) ||
                (startIn < endCb && endIn > startCb) ||
                (startIn < endCr && endIn > startCr))
            {
                throw new DjvuInvalidOperationException("Source and destination buffers must be distinct and non-overlapping in Rgb2YCbCr out-of-place transform.");
            }

            if ((startY < endCb && endY > startCb) ||
                (startY < endCr && endY > startCr) ||
                (startCb < endCr && endCb > startCr))
            {
                throw new DjvuInvalidOperationException("Destination planar buffers (Y, Cb, Cr) must be distinct and not overlap.");
            }

            long pixelCount = (long)width * height;

            if (Avx2.IsSupported)
            {
                if (width >= 32)
                {
                    int optimalThreads = GetOptimalDegreeOfParallelism(pixelCount);
                    if (optimalThreads > 1)
                    {
                        var options = new ParallelOptions { MaxDegreeOfParallelism = optimalThreads };
                        InterWaveSimd.Rgb2YCbCrParallelVector256(pIn, width, height, rowSizeInBytes, pY, pCb, pCr, outRowSizeInBytes, options);
                    }
                    else
                    {
                        InterWaveSimd.Rgb2YCbCrVector256(pIn, width, height, rowSizeInBytes, pY, pCb, pCr, outRowSizeInBytes);
                    }
                    return;
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                if (width >= 16)
                {
                    int optimalThreads = GetOptimalDegreeOfParallelism(pixelCount);
                    if (optimalThreads > 1)
                    {
                        var options = new ParallelOptions { MaxDegreeOfParallelism = optimalThreads };
                        InterWaveSimd.Rgb2YCbCrParallelVector128(pIn, width, height, rowSizeInBytes, pY, pCb, pCr, outRowSizeInBytes, options);
                    }
                    else
                    {
                        InterWaveSimd.Rgb2YCbCrVector128(pIn, width, height, rowSizeInBytes, pY, pCb, pCr, outRowSizeInBytes);
                    }
                    return;
                }
            }

            // Scalar Fallback
            EnsureLutsInitialized();
            InterWaveTransform.Rgb2YCbCrScalar(pPixBuff, width, height, rowSizeInBytes, pOutY, pOutCb, pOutCr, outRowSizeInBytes);
        }

        /// <summary>
        /// Convenience overload for contiguous input buffers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void YCbCr2Rgb(Pixel* pPixBuff, int width, int height)
        {
            int rowSizeInBytes = width * sizeof(Pixel);
            YCbCr2Rgb(pPixBuff, width, height, rowSizeInBytes);
        }

        /// <summary>
        /// Next-generation unified AVX2 loop for IN-PLACE YCbCr to RGB conversion.
        /// Architecture: Branchless Read-Ahead Pipeline.
        /// Overlapping tails cause read-after-write corruption in in-place transforms. To avoid this
        /// without branching or code duplication, the loop loads the NEXT memory chunk at the very beginning
        /// of the CURRENT iteration. This buffers the overlapping tail bytes *before* the current iteration's
        /// store operation overwrites them, guaranteeing perfect memory safety via pure CMOV hardware instructions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void YCbCr2Rgb(Pixel* pPixBuff, int width, int height, int rowSizeInBytes)
        {
            if (pPixBuff == null)
            {
                throw new DjvuArgumentNullException(nameof(pPixBuff));
            }
            if (width <= 0)
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(width),
                    width,
                    "Width must be greater than zero.");
            }
            if (height <= 0)
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(height),
                    height,
                    "Height must be greater than zero.");
            }
            if (rowSizeInBytes < (long)width * sizeof(Pixel))
            {
                throw new DjvuArgumentOutOfRangeException(
                    nameof(rowSizeInBytes),
                    rowSizeInBytes,
                    $"Row size ({rowSizeInBytes}) must be at least width * sizeof(Pixel) ({(long)width * sizeof(Pixel)}).");
            }

            Pixel* p = pPixBuff;

            if (Avx2.IsSupported && width >= 32)
            {
                var vec128 = Vector256.Create((short)128);
                var vec0   = Vector256.Create((short)0);
                var vec255 = Vector256.Create((short)255);
                var vecZero8 = Vector256<byte>.Zero;

                int vectorBound = width - 32;
                int tailShift = (32 - (width % 32)) % 32;
                int tailShiftBytes = tailShift * sizeof(Pixel);

                for (int y = 0; y < height; y++)
                {
                    byte* rowBase = (byte*)p;
                    byte* pByte = rowBase;

                    // PROLOGUE: Load the first chunk into the pipeline
                    var ymmA = Vector256.Load(pByte);
                    var ymmF = Vector256.Load(pByte + 32);
                    var ymmB = Vector256.Load(pByte + 64);

                    int x = 0;
                    while (x < width)
                    {
                        // 1. PIPELINED READ-AHEAD (Branchless x86 CMOV)
                        // Load the memory for the NEXT iteration.
                        // If we are at the tail, shift the read pointer backward.
                        // If we are at the end of the image (nextX >= width), we execute a safe dummy read
                        // from the rowBase to flush the pipeline without page faulting.
                        int nextX = x + 32;
                        int nextShiftBytes = (nextX > vectorBound) ? tailShiftBytes : 0;
                        byte* nextPtr = (nextX >= width) ? rowBase : (pByte + 96 - nextShiftBytes);

                        var nextYmmA = Vector256.Load(nextPtr);
                        var nextYmmF = Vector256.Load(nextPtr + 32);
                        var nextYmmB = Vector256.Load(nextPtr + 64);

                        // 2. UNIFIED MATH BLOCK
                        var ymmC = ymmA;
                        ymmA = Avx2.InsertVector128(ymmF, ymmA.GetLower(), 0);
                        ymmC = Avx2.InsertVector128(ymmC, ymmB.GetLower(), 0);
                        ymmB = Avx2.InsertVector128(ymmB, ymmF.GetLower(), 0);
                        ymmF = Avx2.Permute2x128(ymmC, ymmC, 1);

                        var ymmG = ymmA;
                        ymmA = Avx2.ShiftLeftLogical128BitLane(ymmA, 8);
                        ymmG = Avx2.ShiftRightLogical128BitLane(ymmG, 8);
                        ymmA = Avx2.UnpackHigh(ymmA, ymmF);
                        ymmF = Avx2.ShiftLeftLogical128BitLane(ymmF, 8);
                        ymmG = Avx2.UnpackLow(ymmG, ymmB);
                        ymmF = Avx2.UnpackHigh(ymmF, ymmB);

                        var ymmD = ymmA;
                        ymmA = Avx2.ShiftLeftLogical128BitLane(ymmA, 8);
                        ymmD = Avx2.ShiftRightLogical128BitLane(ymmD, 8);
                        ymmA = Avx2.UnpackHigh(ymmA, ymmG);
                        ymmG = Avx2.ShiftLeftLogical128BitLane(ymmG, 8);
                        ymmD = Avx2.UnpackLow(ymmD, ymmF);
                        ymmG = Avx2.UnpackHigh(ymmG, ymmF);

                        var ymmE = ymmA;
                        ymmA = Avx2.ShiftLeftLogical128BitLane(ymmA, 8);
                        ymmE = Avx2.ShiftRightLogical128BitLane(ymmE, 8);
                        ymmA = Avx2.UnpackHigh(ymmA, ymmD);
                        ymmD = Avx2.ShiftLeftLogical128BitLane(ymmD, 8);
                        ymmE = Avx2.UnpackLow(ymmE, ymmG);
                        ymmD = Avx2.UnpackHigh(ymmD, ymmG);

                        var ymmH = vecZero8;

                        ymmC = ymmA;
                        ymmA = Avx2.UnpackLow(ymmA, ymmH);
                        ymmC = Avx2.UnpackHigh(ymmC, ymmH);

                        ymmB = ymmE;
                        ymmE = Avx2.UnpackLow(ymmE, ymmH);
                        ymmB = Avx2.UnpackHigh(ymmB, ymmH);

                        ymmF = ymmD;
                        ymmD = Avx2.UnpackLow(ymmD, ymmH);
                        ymmF = Avx2.UnpackHigh(ymmF, ymmH);

                        var y_even = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmA.AsInt16(), 8), 8);
                        var b_even = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmC.AsInt16(), 8), 8);
                        var r_even = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmE.AsInt16(), 8), 8);
                        var y_odd  = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmB.AsInt16(), 8), 8);
                        var b_odd  = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmD.AsInt16(), 8), 8);
                        var r_odd  = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(ymmF.AsInt16(), 8), 8);

                        var tr_even = vec0;
                        var tg_even = vec0;
                        var tb_even = vec0;
                        {
                            var t1_even    = Avx2.ShiftRightArithmetic(b_even, 2);
                            var r_sh1_even = Avx2.ShiftRightArithmetic(r_even, 1);
                            var t2_even    = Avx2.Add(r_even, r_sh1_even);
                            var y_128_even = Avx2.Add(y_even, vec128);
                            var t3_even    = Avx2.Subtract(y_128_even, t1_even);

                            tr_even = Avx2.Add(y_128_even, t2_even);
                            var t2_sh1_even = Avx2.ShiftRightArithmetic(t2_even, 1);
                            var b_sh1_even  = Avx2.ShiftLeftLogical(b_even, 1);

                            tg_even = Avx2.Subtract(t3_even, t2_sh1_even);
                            tb_even = Avx2.Add(t3_even, b_sh1_even);

                            tr_even = Avx2.Max(vec0, Avx2.Min(vec255, tr_even));
                            tg_even = Avx2.Max(vec0, Avx2.Min(vec255, tg_even));
                            tb_even = Avx2.Max(vec0, Avx2.Min(vec255, tb_even));
                        }

                        var tr_odd = vec0;
                        var tg_odd = vec0;
                        var tb_odd = vec0;
                        {
                            var t1_odd    = Avx2.ShiftRightArithmetic(b_odd, 2);
                            var r_sh1_odd = Avx2.ShiftRightArithmetic(r_odd, 1);
                            var t2_odd    = Avx2.Add(r_odd, r_sh1_odd);
                            var y_128_odd = Avx2.Add(y_odd, vec128);
                            var t3_odd    = Avx2.Subtract(y_128_odd, t1_odd);

                            tr_odd = Avx2.Add(y_128_odd, t2_odd);
                            var t2_sh1_odd = Avx2.ShiftRightArithmetic(t2_odd, 1);
                            var b_sh1_odd  = Avx2.ShiftLeftLogical(b_odd, 1);

                            tg_odd = Avx2.Subtract(t3_odd, t2_sh1_odd);
                            tb_odd = Avx2.Add(t3_odd, b_sh1_odd);

                            tr_odd = Avx2.Max(vec0, Avx2.Min(vec255, tr_odd));
                            tg_odd = Avx2.Max(vec0, Avx2.Min(vec255, tg_odd));
                            tb_odd = Avx2.Max(vec0, Avx2.Min(vec255, tb_odd));
                        }

                        ymmA = Avx2.PackUnsignedSaturate(tb_even, tb_even);
                        ymmB = Avx2.PackUnsignedSaturate(tb_odd, tb_odd);
                        ymmC = Avx2.PackUnsignedSaturate(tg_even, tg_even);
                        ymmD = Avx2.PackUnsignedSaturate(tg_odd, tg_odd);
                        ymmE = Avx2.PackUnsignedSaturate(tr_even, tr_even);
                        ymmF = Avx2.PackUnsignedSaturate(tr_odd, tr_odd);

                        ymmA = Avx2.UnpackLow(ymmA, ymmC);
                        ymmE = Avx2.UnpackLow(ymmE, ymmB);
                        ymmD = Avx2.UnpackLow(ymmD, ymmF);

                        ymmH = Avx2.ShiftRightLogical128BitLane(ymmA, 2);
                        ymmG = Avx2.UnpackHigh(ymmA.AsInt16(), ymmE.AsInt16()).AsByte();
                        ymmA = Avx2.UnpackLow(ymmA.AsInt16(), ymmE.AsInt16()).AsByte();

                        ymmE = Avx2.ShiftRightLogical128BitLane(ymmE, 2);
                        ymmB = Avx2.ShiftRightLogical128BitLane(ymmD, 2);
                        ymmC = Avx2.UnpackHigh(ymmD.AsInt16(), ymmH.AsInt16()).AsByte();
                        ymmD = Avx2.UnpackLow(ymmD.AsInt16(), ymmH.AsInt16()).AsByte();

                        ymmF = Avx2.UnpackHigh(ymmE.AsInt16(), ymmB.AsInt16()).AsByte();
                        ymmE = Avx2.UnpackLow(ymmE.AsInt16(), ymmB.AsInt16()).AsByte();

                        ymmH = Avx2.Shuffle(ymmA.AsInt32(), 0x4E).AsByte();
                        ymmA = Avx2.UnpackLow(ymmA.AsInt32(), ymmD.AsInt32()).AsByte();
                        ymmD = Avx2.UnpackHigh(ymmD.AsInt32(), ymmE.AsInt32()).AsByte();
                        ymmE = Avx2.UnpackLow(ymmE.AsInt32(), ymmH.AsInt32()).AsByte();

                        ymmH = Avx2.Shuffle(ymmG.AsInt32(), 0x4E).AsByte();
                        ymmG = Avx2.UnpackLow(ymmG.AsInt32(), ymmC.AsInt32()).AsByte();
                        ymmC = Avx2.UnpackHigh(ymmC.AsInt32(), ymmF.AsInt32()).AsByte();
                        ymmF = Avx2.UnpackLow(ymmF.AsInt32(), ymmH.AsInt32()).AsByte();

                        ymmH = Avx2.UnpackLow(ymmA.AsInt64(), ymmE.AsInt64()).AsByte();
                        ymmG = Avx2.UnpackLow(ymmD.AsInt64(), ymmG.AsInt64()).AsByte();
                        ymmC = Avx2.UnpackLow(ymmF.AsInt64(), ymmC.AsInt64()).AsByte();

                        ymmA = Avx2.Permute2x128(ymmH, ymmG, 0x20);
                        ymmD = Avx2.Permute2x128(ymmC, ymmH, 0x30);
                        ymmF = Avx2.Permute2x128(ymmG, ymmC, 0x31);

                        // 3. STORE (Branchless x86 CMOV)
                        int currentShiftBytes = (x > vectorBound) ? tailShiftBytes : 0;
                        byte* storePtr = pByte - currentShiftBytes;

                        ymmA.Store(storePtr);
                        ymmD.Store(storePtr + 32);
                        ymmF.Store(storePtr + 64);

                        // 4. PIPELINE ADVANCE
                        ymmA = nextYmmA;
                        ymmF = nextYmmF;
                        ymmB = nextYmmB;

                        pByte += 96;
                        x += 32;
                    }

                    // Strict byte-padding jump to the next row
                    p = (Pixel*)(rowBase + rowSizeInBytes);
                }
                return;
            }
            else if (Vector128.IsHardwareAccelerated && width >= 16)
            {
                var v128 = Vector128.Create((short)128);
                var vZero = Vector128<short>.Zero;
                var v255 = Vector128.Create((short)255);

                int vectorBound = width - 16;
                int tailShift = (16 - (width % 16)) % 16;
                int tailShiftBytes = tailShift * sizeof(Pixel);

                for (int y = 0; y < height; y++)
                {
                    byte* rowBase = (byte*)p;
                    byte* pByte = rowBase;

                    var xmmA = Vector128.Load(pByte);
                    var xmmB = Vector128.Load(pByte + 16);
                    var xmmC = Vector128.Load(pByte + 32);

                    int x = 0;
                    while (x < width)
                    {
                        int nextX = x + 16;
                        int nextShiftBytes = (nextX > vectorBound) ? tailShiftBytes : 0;
                        byte* nextPtr = (nextX >= width) ? rowBase : (pByte + 48 - nextShiftBytes);

                        var nextXmmA = Vector128.Load(nextPtr);
                        var nextXmmB = Vector128.Load(nextPtr + 16);
                        var nextXmmC = Vector128.Load(nextPtr + 32);

                        Vector128<byte> xmmYin, xmmCbin, xmmCrin;
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

                            xmmYin = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, bMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, bMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, bMask2)));
                            xmmCbin = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, gMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, gMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, gMask2)));
                            xmmCrin = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmA, rMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmB, rMask1), AdvSimd.Arm64.VectorTableLookup(xmmC, rMask2)));
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

                            xmmYin = Sse2.Or(Ssse3.Shuffle(xmmA, bMask0), Sse2.Or(Ssse3.Shuffle(xmmB, bMask1), Ssse3.Shuffle(xmmC, bMask2)));
                            xmmCbin = Sse2.Or(Ssse3.Shuffle(xmmA, gMask0), Sse2.Or(Ssse3.Shuffle(xmmB, gMask1), Ssse3.Shuffle(xmmC, gMask2)));
                            xmmCrin = Sse2.Or(Ssse3.Shuffle(xmmA, rMask0), Sse2.Or(Ssse3.Shuffle(xmmB, rMask1), Ssse3.Shuffle(xmmC, rMask2)));
                        }
                        else
                        {
                            xmmYin = Vector128<byte>.Zero;
                            xmmCbin = Vector128<byte>.Zero;
                            xmmCrin = Vector128<byte>.Zero;
                        }

                        var xmmYSbyte = xmmYin.AsSByte();
                        var xmmCbSbyte = xmmCbin.AsSByte();
                        var xmmCrSbyte = xmmCrin.AsSByte();

                        var xmmYL = Vector128.WidenLower(xmmYSbyte);
                        var xmmYH = Vector128.WidenUpper(xmmYSbyte);
                        var xmmCbL = Vector128.WidenLower(xmmCbSbyte);
                        var xmmCbH = Vector128.WidenUpper(xmmCbSbyte);
                        var xmmCrL = Vector128.WidenLower(xmmCrSbyte);
                        var xmmCrH = Vector128.WidenUpper(xmmCrSbyte);

                        var xmmT1L = Vector128.ShiftRightArithmetic(xmmCbL, 2);
                        var xmmCrSh1L = Vector128.ShiftRightArithmetic(xmmCrL, 1);
                        var xmmT2L = Vector128.Add(xmmCrL, xmmCrSh1L);
                        var xmmY128L = Vector128.Add(xmmYL, v128);
                        var xmmT3L = Vector128.Subtract(xmmY128L, xmmT1L);

                        var xmmRL = Vector128.Add(xmmY128L, xmmT2L);
                        var xmmT2Sh1L = Vector128.ShiftRightArithmetic(xmmT2L, 1);
                        var xmmCbSh1L = Vector128.ShiftLeft(xmmCbL, 1);

                        var xmmGL = Vector128.Subtract(xmmT3L, xmmT2Sh1L);
                        var xmmBL = Vector128.Add(xmmT3L, xmmCbSh1L);

                        xmmRL = Vector128.Max(vZero, Vector128.Min(v255, xmmRL));
                        xmmGL = Vector128.Max(vZero, Vector128.Min(v255, xmmGL));
                        xmmBL = Vector128.Max(vZero, Vector128.Min(v255, xmmBL));

                        var xmmT1H = Vector128.ShiftRightArithmetic(xmmCbH, 2);
                        var xmmCrSh1H = Vector128.ShiftRightArithmetic(xmmCrH, 1);
                        var xmmT2H = Vector128.Add(xmmCrH, xmmCrSh1H);
                        var xmmY128H = Vector128.Add(xmmYH, v128);
                        var xmmT3H = Vector128.Subtract(xmmY128H, xmmT1H);

                        var xmmRH = Vector128.Add(xmmY128H, xmmT2H);
                        var xmmT2Sh1H = Vector128.ShiftRightArithmetic(xmmT2H, 1);
                        var xmmCbSh1H = Vector128.ShiftLeft(xmmCbH, 1);

                        var xmmGH = Vector128.Subtract(xmmT3H, xmmT2Sh1H);
                        var xmmBH = Vector128.Add(xmmT3H, xmmCbSh1H);

                        xmmRH = Vector128.Max(vZero, Vector128.Min(v255, xmmRH));
                        xmmGH = Vector128.Max(vZero, Vector128.Min(v255, xmmGH));
                        xmmBH = Vector128.Max(vZero, Vector128.Min(v255, xmmBH));

                        var xmmBOut = Vector128.Narrow(xmmBL.AsUInt16(), xmmBH.AsUInt16());
                        var xmmGOut = Vector128.Narrow(xmmGL.AsUInt16(), xmmGH.AsUInt16());
                        var xmmROut = Vector128.Narrow(xmmRL.AsUInt16(), xmmRH.AsUInt16());

                        Vector128<byte> xmmOut0, xmmOut1, xmmOut2;
                        if (AdvSimd.Arm64.IsSupported)
                        {
                            var bMask0 = Vector128.Create((byte)0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255, 255, 5);
                            var gMask0 = Vector128.Create((byte)255, 0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255, 255);
                            var rMask0 = Vector128.Create((byte)255, 255, 0, 255, 255, 1, 255, 255, 2, 255, 255, 3, 255, 255, 4, 255);
                            var bMask1 = Vector128.Create((byte)255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255, 10, 255);
                            var gMask1 = Vector128.Create((byte)5, 255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255, 10);
                            var rMask1 = Vector128.Create((byte)255, 5, 255, 255, 6, 255, 255, 7, 255, 255, 8, 255, 255, 9, 255, 255);
                            var bMask2 = Vector128.Create((byte)255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15, 255, 255);
                            var gMask2 = Vector128.Create((byte)255, 255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15, 255);
                            var rMask2 = Vector128.Create((byte)10, 255, 255, 11, 255, 255, 12, 255, 255, 13, 255, 255, 14, 255, 255, 15);

                            xmmOut0 = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmBOut, bMask0), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmGOut, gMask0), AdvSimd.Arm64.VectorTableLookup(xmmROut, rMask0)));
                            xmmOut1 = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmBOut, bMask1), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmGOut, gMask1), AdvSimd.Arm64.VectorTableLookup(xmmROut, rMask1)));
                            xmmOut2 = AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmBOut, bMask2), AdvSimd.Or(AdvSimd.Arm64.VectorTableLookup(xmmGOut, gMask2), AdvSimd.Arm64.VectorTableLookup(xmmROut, rMask2)));
                        }
                        else if (Ssse3.IsSupported)
                        {
                            var bMask0 = Vector128.Create((byte)0, 128, 128, 1, 128, 128, 2, 128, 128, 3, 128, 128, 4, 128, 128, 5);
                            var gMask0 = Vector128.Create((byte)128, 0, 128, 128, 1, 128, 128, 2, 128, 128, 3, 128, 128, 4, 128, 128);
                            var rMask0 = Vector128.Create((byte)128, 128, 0, 128, 128, 1, 128, 128, 2, 128, 128, 3, 128, 128, 4, 128);
                            var bMask1 = Vector128.Create((byte)128, 128, 6, 128, 128, 7, 128, 128, 8, 128, 128, 9, 128, 128, 10, 128);
                            var gMask1 = Vector128.Create((byte)5, 128, 128, 6, 128, 128, 7, 128, 128, 8, 128, 128, 9, 128, 128, 10);
                            var rMask1 = Vector128.Create((byte)128, 5, 128, 128, 6, 128, 128, 7, 128, 128, 8, 128, 128, 9, 128, 128);
                            var bMask2 = Vector128.Create((byte)128, 11, 128, 128, 12, 128, 128, 13, 128, 128, 14, 128, 128, 15, 128, 128);
                            var gMask2 = Vector128.Create((byte)128, 128, 11, 128, 128, 12, 128, 128, 13, 128, 128, 14, 128, 128, 15, 128);
                            var rMask2 = Vector128.Create((byte)10, 128, 128, 11, 128, 128, 12, 128, 128, 13, 128, 128, 14, 128, 128, 15);

                            xmmOut0 = Sse2.Or(Ssse3.Shuffle(xmmBOut, bMask0), Sse2.Or(Ssse3.Shuffle(xmmGOut, gMask0), Ssse3.Shuffle(xmmROut, rMask0)));
                            xmmOut1 = Sse2.Or(Ssse3.Shuffle(xmmBOut, bMask1), Sse2.Or(Ssse3.Shuffle(xmmGOut, gMask1), Ssse3.Shuffle(xmmROut, rMask1)));
                            xmmOut2 = Sse2.Or(Ssse3.Shuffle(xmmBOut, bMask2), Sse2.Or(Ssse3.Shuffle(xmmGOut, gMask2), Ssse3.Shuffle(xmmROut, rMask2)));
                        }
                        else
                        {
                            xmmOut0 = Vector128<byte>.Zero; xmmOut1 = Vector128<byte>.Zero; xmmOut2 = Vector128<byte>.Zero;
                        }

                        int currentShiftBytes = (x > vectorBound) ? tailShiftBytes : 0;
                        byte* storePtr = pByte - currentShiftBytes;

                        xmmOut0.Store(storePtr);
                        xmmOut1.Store(storePtr + 16);
                        xmmOut2.Store(storePtr + 32);

                        xmmA = nextXmmA;
                        xmmB = nextXmmB;
                        xmmC = nextXmmC;

                        pByte += 48;
                        x += 16;
                    }

                    p = (Pixel*)(rowBase + rowSizeInBytes);
                }
                return;
            }

            // Scalar Fallback
            long inPadBytesScalar = rowSizeInBytes - ((long)width * sizeof(Pixel));
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, p++)
                {
                    sbyte y_val = p->Blue;
                    sbyte b_val = p->Green;
                    sbyte r_val = p->Red;

                    int t1 = b_val >> 2;
                    int t2 = r_val + (r_val >> 1);
                    int t3 = y_val + 128 - t1;
                    int tr = y_val + 128 + t2;
                    int tg = t3 - (t2 >> 1);
                    int tb = t3 + (b_val << 1);

                    p->Red   = (sbyte)Math.Max(0, Math.Min(255, tr));
                    p->Green = (sbyte)Math.Max(0, Math.Min(255, tg));
                    p->Blue  = (sbyte)Math.Max(0, Math.Min(255, tb));
                }
                p = (Pixel*)((byte*)p + inPadBytesScalar);
            }
        }
    }
}