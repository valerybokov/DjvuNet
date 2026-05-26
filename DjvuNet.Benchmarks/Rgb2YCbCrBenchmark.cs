using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using DjvuNet.DjvuLibre;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(StandardConfig))]
    public class Rgb2YCbCrBenchmark
    {
        [Params(false, true)]
        public bool IsPadded { get; set; }

        private sbyte[] _managedBuffer;
        private sbyte[] _outY;
        private sbyte[] _outCb;
        private sbyte[] _outCr;
        
        // Native Pointers
        private IntPtr _nativeRgbPtr;
        private IntPtr _nativeYPtr;
        private IntPtr _nativeCbPtr;
        private IntPtr _nativeCrPtr;

        private int _width;
        private int _height;

        private string GetArtifactPath(string fileName)
        {
            return Path.Combine(Util.RepoRoot, "artifacts", fileName);
        }

        [GlobalSetup]
        public void Setup()
        {
            string testImageName = IsPadded ? "TitanIR-24bgr-padded.png" : "TitanIR-24bgr.png";
            string imagePath = GetArtifactPath(testImageName);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Benchmark artifact not found: {imagePath}");

            using (var bmp = new Bitmap(imagePath))
            {
                _width = bmp.Width;
                _height = bmp.Height;
                int totalPixels = _width * _height;
                int totalBytes = totalPixels * 3;

                _managedBuffer = new sbyte[totalBytes];
                _outY = new sbyte[totalPixels];
                _outCb = new sbyte[totalPixels];
                _outCr = new sbyte[totalPixels];

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, _width, _height), 
                                               ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                
                unsafe
                {
                    new ReadOnlySpan<sbyte>(data.Scan0.ToPointer(), totalBytes)
                        .CopyTo(new Span<sbyte>(_managedBuffer));
                }
                    
                bmp.UnlockBits(data);
                
                // Allocate unmanaged memory for the native benchmark
                _nativeRgbPtr = DjvuMarshal.AllocHGlobal((uint)totalBytes);
                _nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                _nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                _nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalPixels);
                
                unsafe 
                {
                    fixed (sbyte* ptr = _managedBuffer)
                    {
                        Buffer.MemoryCopy(ptr, (void*)_nativeRgbPtr, totalBytes, totalBytes);
                    }
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (_nativeRgbPtr != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeRgbPtr);
            if (_nativeYPtr != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeYPtr);
            if (_nativeCbPtr != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeCbPtr);
            if (_nativeCrPtr != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeCrPtr);
        }

        [Benchmark]
        public unsafe void Scalar()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            fixed (sbyte* ptr = _managedBuffer)
            fixed (sbyte* pY = _outY)
            fixed (sbyte* pCb = _outCb)
            fixed (sbyte* pCr = _outCr)
            {
                // Multiply width by 3 to simulate legacy parameter usage
                InterWaveTransform.Rgb2YCbCrScalar((Pixel*)ptr, _width, _height, _width * 3, pY, pCb, pCr, _width);
            }
#pragma warning restore CS0618
        }

        [Benchmark]
        public unsafe void Vector256()
        {
            fixed (sbyte* ptr = _managedBuffer)
            fixed (sbyte* pY = _outY)
            fixed (sbyte* pCb = _outCb)
            fixed (sbyte* pCr = _outCr)
            {
                InterWaveTransform.Rgb2YCbCr((Pixel*)ptr, _width, _height, _width * 3, pY, pCb, pCr, _width);
            }
        }

        [Benchmark(Baseline = true)]
        public void Native()
        {
            NativeMethods.RgbToYCbCr(_nativeRgbPtr, _width, _height, _width, _nativeYPtr, _nativeCbPtr, _nativeCrPtr, _width);
        }
    }
}