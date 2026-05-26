using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
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
        /// Core hardware-accelerated Color Space Conversion from BGR to YCbCr with explicit byte-stride support.
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
        /// This implementation is currently optimized using an AVX2 unified loop. Future iterations will include 
        /// fallbacks for other hardware acceleration targets, such as Vector128 (ARM64 NEON) and SSSE3.
        /// 
        /// ARCHITECTURAL NOTE: This implementation intentionally diverges from the native DjVuLibre C++ behavior.
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
        public static unsafe void Rgb2YCbCr(Pixel* pPixBuff, int width, int height, int rowSizeInBytes, sbyte* pOutY, sbyte* pOutCb, sbyte* pOutCr, int outRowSizeInBytes)
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

            if (Avx2.IsSupported && width >= 32)
            {
                var v128 = Vector256.Create((int)128);
                var v32768 = Vector256.Create((int)32768);
                var vZero16 = Vector256<short>.Zero;
                var vZero8 = Vector256<byte>.Zero;

                short cY_R = 19946, cY_G1 = 19946, cY_G2 = 19945, cY_B = 5698;
                short cCr_R = 30393, cCr_G = -26594, cCr_B = -3799;
                short cCb_R = -11397, cCb_G = -22795, cCb_B1 = 17096, cCb_B2 = 17096;

                var vCoeffY_RG = Vector256.Create(cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1);
                var vCoeffY_BG = Vector256.Create(cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2);
                var vCoeffCr_RG = Vector256.Create(cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G);
                var vCoeffCr_BZ = Vector256.Create(cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0);
                var vCoeffCb_RG = Vector256.Create(cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G);
                var vCoeffCb_BB = Vector256.Create(cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2);

                long inPadBytes = rowSizeInBytes - ((long)width * sizeof(Pixel));
                int outPadBytes = outRowSizeInBytes - width;

                int vectorBound = width - 32;
                int tailShift = (32 - (width % 32)) % 32;
                int tailShiftBytes = tailShift * sizeof(Pixel);

                for (int y = 0; y < height; y++)
                {
                    int x = 0;

                    while (x < width)
                    {
                        // Highly predictable condition; RyuJIT compiles to fast CMOV
                        int shift = (x > vectorBound) ? tailShift : 0;
                        int shiftBytes = (x > vectorBound) ? tailShiftBytes : 0;

                        // 1. READ (x86 Hardware Addressing Mode Calculation)
                        var ymmA = Vector256.Load((byte*)pIn - shiftBytes);
                        var ymmF = Vector256.Load((byte*)pIn - shiftBytes + 32);
                        var ymmB = Vector256.Load((byte*)pIn - shiftBytes + 64);

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

                        ymmC = ymmA;
                        ymmA = Avx2.UnpackLow(ymmA, vZero8);
                        ymmC = Avx2.UnpackHigh(ymmC, vZero8);

                        ymmB = ymmE;
                        ymmE = Avx2.UnpackLow(ymmE, vZero8);
                        ymmB = Avx2.UnpackHigh(ymmB, vZero8);

                        ymmF = ymmD;
                        ymmD = Avx2.UnpackLow(ymmD, vZero8);
                        ymmF = Avx2.UnpackHigh(ymmF, vZero8);

                        var b_e = ymmA.AsInt16();
                        var g_e = ymmC.AsInt16();
                        var r_e = ymmE.AsInt16();
                        var b_o  = ymmB.AsInt16();
                        var g_o  = ymmD.AsInt16();
                        var r_o  = ymmF.AsInt16();

                        // MATH: EVEN 16 PIXELS
                        var vRG_L = Avx2.UnpackLow(r_e, g_e);
                        var vBG_L = Avx2.UnpackLow(b_e, g_e);
                        var vBZ_L = Avx2.UnpackLow(b_e, vZero16);
                        var vBB_L = Avx2.UnpackLow(b_e, b_e);

                        var vY32_L = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L, vCoeffY_RG), Avx2.MultiplyAddAdjacent(vBG_L, vCoeffY_BG)), v32768), 16);
                        var vCr32_L = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L, vCoeffCr_RG), Avx2.MultiplyAddAdjacent(vBZ_L, vCoeffCr_BZ)), v32768), 16);
                        var vCb32_L = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L, vCoeffCb_RG), Avx2.MultiplyAddAdjacent(vBB_L, vCoeffCb_BB)), v32768), 16);

                        vY32_L = Avx2.Subtract(vY32_L, v128);

                        var vRG_H = Avx2.UnpackHigh(r_e, g_e);
                        var vBG_H = Avx2.UnpackHigh(b_e, g_e);
                        var vBZ_H = Avx2.UnpackHigh(b_e, vZero16);
                        var vBB_H = Avx2.UnpackHigh(b_e, b_e);

                        var vY32_H = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H, vCoeffY_RG), Avx2.MultiplyAddAdjacent(vBG_H, vCoeffY_BG)), v32768), 16);
                        var vCr32_H = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H, vCoeffCr_RG), Avx2.MultiplyAddAdjacent(vBZ_H, vCoeffCr_BZ)), v32768), 16);
                        var vCb32_H = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H, vCoeffCb_RG), Avx2.MultiplyAddAdjacent(vBB_H, vCoeffCb_BB)), v32768), 16);

                        vY32_H = Avx2.Subtract(vY32_H, v128);

                        var vY16_E = Avx2.PackSignedSaturate(vY32_L, vY32_H);
                        var vCr16_E = Avx2.PackSignedSaturate(vCr32_L, vCr32_H);
                        var vCb16_E = Avx2.PackSignedSaturate(vCb32_L, vCb32_H);

                        // MATH: ODD 16 PIXELS
                        var vRG_L_o = Avx2.UnpackLow(r_o, g_o);
                        var vBG_L_o = Avx2.UnpackLow(b_o, g_o);
                        var vBZ_L_o = Avx2.UnpackLow(b_o, vZero16);
                        var vBB_L_o = Avx2.UnpackLow(b_o, b_o);

                        var vY32_L_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L_o, vCoeffY_RG), Avx2.MultiplyAddAdjacent(vBG_L_o, vCoeffY_BG)), v32768), 16);
                        var vCr32_L_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L_o, vCoeffCr_RG), Avx2.MultiplyAddAdjacent(vBZ_L_o, vCoeffCr_BZ)), v32768), 16);
                        var vCb32_L_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_L_o, vCoeffCb_RG), Avx2.MultiplyAddAdjacent(vBB_L_o, vCoeffCb_BB)), v32768), 16);

                        vY32_L_o = Avx2.Subtract(vY32_L_o, v128);

                        var vRG_H_o = Avx2.UnpackHigh(r_o, g_o);
                        var vBG_H_o = Avx2.UnpackHigh(b_o, g_o);
                        var vBZ_H_o = Avx2.UnpackHigh(b_o, vZero16);
                        var vBB_H_o = Avx2.UnpackHigh(b_o, b_o);

                        var vY32_H_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H_o, vCoeffY_RG), Avx2.MultiplyAddAdjacent(vBG_H_o, vCoeffY_BG)), v32768), 16);
                        var vCr32_H_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H_o, vCoeffCr_RG), Avx2.MultiplyAddAdjacent(vBZ_H_o, vCoeffCr_BZ)), v32768), 16);
                        var vCb32_H_o = Avx2.ShiftRightArithmetic(Avx2.Add(Avx2.Add(Avx2.MultiplyAddAdjacent(vRG_H_o, vCoeffCb_RG), Avx2.MultiplyAddAdjacent(vBB_H_o, vCoeffCb_BB)), v32768), 16);

                        vY32_H_o = Avx2.Subtract(vY32_H_o, v128);

                        var vY16_O = Avx2.PackSignedSaturate(vY32_L_o, vY32_H_o);
                        var vCr16_O = Avx2.PackSignedSaturate(vCr32_L_o, vCr32_H_o);
                        var vCb16_O = Avx2.PackSignedSaturate(vCb32_L_o, vCb32_H_o);

                        // 3. FULL 32-PIXEL FAST STORE
                        var outYVec = Avx2.PackSignedSaturate(Avx2.UnpackLow(vY16_E, vY16_O), Avx2.UnpackHigh(vY16_E, vY16_O));
                        var outCbVec = Avx2.PackSignedSaturate(Avx2.UnpackLow(vCb16_E, vCb16_O), Avx2.UnpackHigh(vCb16_E, vCb16_O));
                        var outCrVec = Avx2.PackSignedSaturate(Avx2.UnpackLow(vCr16_E, vCr16_O), Avx2.UnpackHigh(vCr16_E, vCr16_O));

                        outYVec.Store(pY - shift);
                        outCbVec.Store(pCb - shift);
                        outCrVec.Store(pCr - shift);

                        // 4. ADVANCE POINTERS
                        int advance = 32 - shift;
                        pIn += advance;
                        pY += advance;
                        pCb += advance;
                        pCr += advance;
                        x += advance;
                    }

                    // Apply strict byte-padding jumps at the end of every row
                    pIn = (Pixel*)((byte*)pIn + inPadBytes);
                    pY += outPadBytes;
                    pCb += outPadBytes;
                    pCr += outPadBytes;
                }
                return;
            }

            // Scalar Fallback (Matches Rgb2YCbCr explicitly)
            EnsureLutsInitialized();
            fixed (int* pRedYLUT = redYLUT)
            fixed (int* pGreenYLUT = greenYLUT)
            fixed (int* pBlueYLUT = blueYLUT)
            fixed (int* pRedCbLUT = redCbLUT)
            fixed (int* pGreenCbLUT = greenCbLUT)
            fixed (int* pBlueCbLUT = blueCbLUT)
            fixed (int* pRedCrLUT = redCrLUT)
            fixed (int* pGreenCrLUT = greenCrLUT)
            fixed (int* pBlueCrLUT = blueCrLUT)
            {
                int inPadBytes = rowSizeInBytes - (width * sizeof(Pixel));
                int outPadBytes = outRowSizeInBytes - width;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++, pIn++, pCr++, pCb++, pY++)
                    {
                        byte red = unchecked((byte)pIn->Red);
                        byte green = unchecked((byte)pIn->Green);
                        byte blue = unchecked((byte)pIn->Blue);

                        int y_val = pRedYLUT[red] + pGreenYLUT[green] + pBlueYLUT[blue] + 32768;
                        *pY = (sbyte)((y_val >> 16) - 128);

                        int cb = pRedCbLUT[red] + pGreenCbLUT[green] + pBlueCbLUT[blue] + 32768;
                        *pCb = (sbyte)Math.Max(-128, Math.Min(127, cb >> 16));

                        int cr = pRedCrLUT[red] + pGreenCrLUT[green] + pBlueCrLUT[blue] + 32768;
                        *pCr = (sbyte)Math.Max(-128, Math.Min(127, cr >> 16));
                    }
                    
                    pIn = (Pixel*)((byte*)pIn + inPadBytes);
                    pY += outPadBytes;
                    pCb += outPadBytes;
                    pCr += outPadBytes;
                }
            }
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