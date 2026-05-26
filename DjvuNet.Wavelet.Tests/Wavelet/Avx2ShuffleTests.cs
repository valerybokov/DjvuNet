using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;
using DjvuNet.Graphics;

namespace DjvuNet.Wavelet.Tests
{
    public class Avx2ShuffleTests
    {
        [Fact]
        public unsafe void Verify_LibJpegTurbo_AosToSoa_Deinterleave()
        {
            if (!Avx2.IsSupported) return;

            byte[] input = new byte[96];
            for (int i = 0; i < 96; i++) input[i] = (byte)i;

            fixed (byte* ptr = input)
            {
                var ymmA = Avx.LoadVector256(ptr);
                var ymmF = Avx.LoadVector256(ptr + 32);
                var ymmB = Avx.LoadVector256(ptr + 64);

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

                var ymmH = Vector256<byte>.Zero;

                ymmC = ymmA;
                ymmA = Avx2.UnpackLow(ymmA, ymmH);
                ymmC = Avx2.UnpackHigh(ymmC, ymmH);

                ymmB = ymmE;
                ymmE = Avx2.UnpackLow(ymmE, ymmH);
                ymmB = Avx2.UnpackHigh(ymmB, ymmH);

                ymmF = ymmD;
                ymmD = Avx2.UnpackLow(ymmD, ymmH);
                ymmF = Avx2.UnpackHigh(ymmF, ymmH);

                Console.WriteLine("DE-INTERLEAVE OUTPUT:");
                DumpVector("ymmA", ymmA.AsByte());
                DumpVector("ymmC", ymmC.AsByte());
                DumpVector("ymmE", ymmE.AsByte());
                DumpVector("ymmB", ymmB.AsByte());
                DumpVector("ymmD", ymmD.AsByte());
                DumpVector("ymmF", ymmF.AsByte());
            }
        }

        [Fact]
        public unsafe void Verify_LibJpegTurbo_SoaToAos_Reinterleave()
        {
            if (!Avx2.IsSupported) return;

            short[] tb_even_arr = new short[16];
            short[] tg_even_arr = new short[16];
            short[] tr_even_arr = new short[16];
            short[] tb_odd_arr = new short[16];
            short[] tg_odd_arr = new short[16];
            short[] tr_odd_arr = new short[16];

            for (int i = 0; i < 16; i++)
            {
                tb_even_arr[i] = (short)((i * 2) * 3 + 0);
                tg_even_arr[i] = (short)((i * 2) * 3 + 1);
                tr_even_arr[i] = (short)((i * 2) * 3 + 2);

                tb_odd_arr[i] = (short)((i * 2 + 1) * 3 + 0);
                tg_odd_arr[i] = (short)((i * 2 + 1) * 3 + 1);
                tr_odd_arr[i] = (short)((i * 2 + 1) * 3 + 2);
            }

            fixed (short* pB_E = tb_even_arr)
            fixed (short* pG_E = tg_even_arr)
            fixed (short* pR_E = tr_even_arr)
            fixed (short* pB_O = tb_odd_arr)
            fixed (short* pG_O = tg_odd_arr)
            fixed (short* pR_O = tr_odd_arr)
            {
                var tb_even = Avx.LoadVector256(pB_E);
                var tg_even = Avx.LoadVector256(pG_E);
                var tr_even = Avx.LoadVector256(pR_E);
                var tb_odd = Avx.LoadVector256(pB_O);
                var tg_odd = Avx.LoadVector256(pG_O);
                var tr_odd = Avx.LoadVector256(pR_O);
                
                var ymmA = Avx2.PackUnsignedSaturate(tb_even, tb_even);
                var ymmB = Avx2.PackUnsignedSaturate(tb_odd, tb_odd);
                var ymmC = Avx2.PackUnsignedSaturate(tg_even, tg_even);
                var ymmD = Avx2.PackUnsignedSaturate(tg_odd, tg_odd);
                var ymmE = Avx2.PackUnsignedSaturate(tr_even, tr_even);
                var ymmF = Avx2.PackUnsignedSaturate(tr_odd, tr_odd);

                ymmA = Avx2.UnpackLow(ymmA, ymmC);
                ymmE = Avx2.UnpackLow(ymmE, ymmB);
                ymmD = Avx2.UnpackLow(ymmD, ymmF);

                var ymmH = Avx2.ShiftRightLogical128BitLane(ymmA, 2);
                var ymmG = Avx2.UnpackHigh(ymmA.AsInt16(), ymmE.AsInt16()).AsByte();
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

                Console.WriteLine("RE-INTERLEAVE OUTPUT:");
                DumpVector("ymmA", ymmA);
                DumpVector("ymmD", ymmD);
                DumpVector("ymmF", ymmF);
            }
        }

        private void DumpVector(string name, Vector256<byte> v)
        {
            byte[] arr = new byte[32];
            v.CopyTo(arr);
            Console.WriteLine($"{name}: {string.Join(", ", arr)}");
        }

        [Fact]
        public unsafe void Verify_Djvu_SignedMath_Chunk2()
        {
            if (!Avx2.IsSupported) return;

            sbyte input = -10;
            byte[] arr = new byte[32];
            for (int i = 0; i < 32; i++) arr[i] = (byte)input;

            fixed (byte* ptr = arr)
            {
                var ymmA = Avx.LoadVector256(ptr);
                var ymmH = Vector256<byte>.Zero;

                var widened = Avx2.UnpackLow(ymmA, ymmH).AsInt16();
                var signExtended = Avx2.ShiftRightArithmetic(Avx2.ShiftLeftLogical(widened, 8), 8);

                short[] outArr = new short[16];
                widened.CopyTo(outArr);
                Console.WriteLine($"Widened (Zero-Extended): {outArr[0]}");

                signExtended.CopyTo(outArr);
                Console.WriteLine($"Sign Extended: {outArr[0]}");

                Assert.Equal(-10, outArr[0]);
            }
        }
        [Fact]
        public unsafe void Verify_Djvu_ForwardMath_Chunk2()
        {
            if (!Avx2.IsSupported) return;

            // Extreme colors to test overflow and saturation
            byte r = 255;
            byte g = 255;
            byte b = 255;

            // Ensure baseline LUTs exist even if skipped by AVX2 fail-fast paths
            InterWaveTransform.EnsureLutsInitialized();

            // SCALAR BASELINE
            int y_scalar = InterWaveTransform.redYLUT[r] + InterWaveTransform.greenYLUT[g] + InterWaveTransform.blueYLUT[b] + 32768;
            sbyte outY_scalar = (sbyte)((y_scalar >> 16) - 128);

            int cb_scalar = InterWaveTransform.redCbLUT[r] + InterWaveTransform.greenCbLUT[g] + InterWaveTransform.blueCbLUT[b] + 32768;
            sbyte outCb_scalar = (sbyte)Math.Max(-128, Math.Min(127, cb_scalar >> 16));

            int cr_scalar = InterWaveTransform.redCrLUT[r] + InterWaveTransform.greenCrLUT[g] + InterWaveTransform.blueCrLUT[b] + 32768;
            sbyte outCr_scalar = (sbyte)Math.Max(-128, Math.Min(127, cr_scalar >> 16));

            // AVX2 COEFFICIENTS
            short cY_R = 19946;  
            short cY_G1 = 19946; // Split 39891 (Green Y)
            short cY_G2 = 19945; 
            short cY_B = 5698;

            short cCr_R = 30393;
            short cCr_G = -26594;
            short cCr_B = -3799;

            short cCb_R = -11397;
            short cCb_G = -22795;
            short cCb_B1 = 17096; // Split 34192 (Blue Cb)
            short cCb_B2 = 17096;

            // Mock SoA 16-bit Vectors
            var vR = Vector256.Create((short)r);
            var vG = Vector256.Create((short)g);
            var vB = Vector256.Create((short)b);
            var vZero = Vector256<short>.Zero;
            var v32768 = Vector256.Create(32768);

            // Interleave logic mimicking libjpeg-turbo vpunpcklwd
            var vRG = Avx2.UnpackLow(vR, vG); // [R0, G0, R1, G1...]
            var vBG = Avx2.UnpackLow(vB, vG); // [B0, G0, B1, G1...]
            var vRZ = Avx2.UnpackLow(vR, vZero);
            var vBZ = Avx2.UnpackLow(vB, vZero);
            var vGZ = Avx2.UnpackLow(vG, vZero);

            // SIMD MATH (MultiplyAddAdjacent computes: a0*b0 + a1*b1 -> 32-bit int)
            // Y = (R*19946 + G*19946) + (B*5698 + G*19945) + 32768
            var vCoeffY_RG = Vector256.Create(cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1, cY_R, cY_G1);
            var vCoeffY_BG = Vector256.Create(cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2, cY_B, cY_G2);
            var vY_RG = Avx2.MultiplyAddAdjacent(vRG, vCoeffY_RG);
            var vY_BG = Avx2.MultiplyAddAdjacent(vBG, vCoeffY_BG);
            var vY32 = Avx2.Add(Avx2.Add(vY_RG, vY_BG), v32768);
            
            // Cr = (R*30393 + G*-26594) + (B*-3799 + 0) + 32768
            var vCoeffCr_RG = Vector256.Create(cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G, cCr_R, cCr_G);
            var vCoeffCr_BZ = Vector256.Create(cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0, cCr_B, (short)0);
            var vCr_RG = Avx2.MultiplyAddAdjacent(vRG, vCoeffCr_RG);
            var vCr_BZ = Avx2.MultiplyAddAdjacent(vBZ, vCoeffCr_BZ);
            var vCr32 = Avx2.Add(Avx2.Add(vCr_RG, vCr_BZ), v32768);

            // Cb = (R*-11397 + G*-22795) + (B*17096 + B*17096) + 32768
            var vCoeffCb_RG = Vector256.Create(cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G, cCb_R, cCb_G);
            var vCoeffCb_BB = Vector256.Create(cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2, cCb_B1, cCb_B2);
            var vCb_RG = Avx2.MultiplyAddAdjacent(vRG, vCoeffCb_RG);
            var vCb_BB = Avx2.MultiplyAddAdjacent(Avx2.UnpackLow(vB, vB), vCoeffCb_BB);
            var vCb32 = Avx2.Add(Avx2.Add(vCb_RG, vCb_BB), v32768);

            // Shift right 16
            vY32 = Avx2.ShiftRightArithmetic(vY32, 16);
            vCr32 = Avx2.ShiftRightArithmetic(vCr32, 16);
            vCb32 = Avx2.ShiftRightArithmetic(vCb32, 16);

            // Sub 128 for Y
            vY32 = Avx2.Subtract(vY32, Vector256.Create(128));

            // Pack 32-bit to 16-bit
            var vY16 = Avx2.PackSignedSaturate(vY32, vY32);
            var vCr16 = Avx2.PackSignedSaturate(vCr32, vCr32);
            var vCb16 = Avx2.PackSignedSaturate(vCb32, vCb32);

            short[] arrY = new short[16];
            short[] arrCr = new short[16];
            short[] arrCb = new short[16];
            vY16.CopyTo(arrY);
            vCr16.CopyTo(arrCr);
            vCb16.CopyTo(arrCb);

            Console.WriteLine($"Y  | Scl: {outY_scalar}, Vec: {(sbyte)arrY[0]}");
            Console.WriteLine($"Cr | Scl: {outCr_scalar}, Vec: {(sbyte)arrCr[0]}");
            Console.WriteLine($"Cb | Scl: {outCb_scalar}, Vec: {(sbyte)arrCb[0]}");

            Assert.Equal(outY_scalar, (sbyte)arrY[0]);
            Assert.Equal(outCr_scalar, (sbyte)arrCr[0]);
            Assert.Equal(outCb_scalar, (sbyte)arrCb[0]);
        }
    }
}