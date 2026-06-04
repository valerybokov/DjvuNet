using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(StandardConfig))]
    public class PigeonTransformBenchmark
    {
        public const int Seed = 42;
        public const int DefaultWidth = 1472;
        public const int DefaultHeight = 1905;

        private sbyte[] _managedBuffer;
        private sbyte[] _managedOutY;
        private sbyte[] _managedOutCb;
        private sbyte[] _managedOutCr;

        [GlobalSetup]
        public void Setup()
        {
            int totalPixels = DefaultWidth * DefaultHeight;
            int totalBytes = totalPixels * 3;

            _managedBuffer = new sbyte[totalBytes];
            _managedOutY = new sbyte[totalPixels];
            _managedOutCb = new sbyte[totalPixels];
            _managedOutCr = new sbyte[totalPixels];

            // Extremely fast, zero-allocation random fill
            Random rnd = new Random(Seed);
            rnd.NextBytes(MemoryMarshal.Cast<sbyte, byte>(_managedBuffer.AsSpan()));
        }

        [Benchmark(Baseline = true)]
        public unsafe void YCbCr2RgbScalar()
        {
            fixed (sbyte* ptr = _managedBuffer)
            {
                InterWaveTransform.YCbCr2RgbScalar((Pixel*)ptr, DefaultWidth, DefaultHeight, DefaultWidth * 3);
            }
        }

        [Benchmark]
        public unsafe void Rgb2YCbCrScalar()
        {
            fixed (sbyte* ptr = _managedBuffer)
            fixed (sbyte* outY = _managedOutY)
            fixed (sbyte* outCb = _managedOutCb)
            fixed (sbyte* outCr = _managedOutCr)
            {
                InterWaveTransform.Rgb2YCbCrScalar((Pixel*)ptr, DefaultWidth, DefaultHeight, DefaultWidth * 3, outY, outCb, outCr, DefaultWidth);
            }
        }
    }
}