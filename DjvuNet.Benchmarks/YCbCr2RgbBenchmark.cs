using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using DjvuNet.Graphics;
using DjvuNet.Wavelet;
using DjvuNet.Tests;
using DjvuNet.DjvuLibre;

namespace DjvuNet.Benchmarks
{
    [Config(typeof(StandardConfig))]
    [KeepBenchmarkFiles]
    public class YCbCr2RgbBenchmark
    {
        private const int InvocationCount = 10;

        // Use Params to test both Padded and Non-Padded (Contiguous) memory layouts
        [Params(false, true)]
        public bool IsPadded { get; set; }

        private sbyte[][] _managedBuffers;
        private IntPtr[] _nativeRgbPtrs;
        private IntPtr _nativeBackupPtr;
        private int _width;
        private int _height = 3686; // Height is constant for both
        private int _totalBytes;

        private string GetArtifactPath(string fileName)
        {
            return Path.Combine(Util.RepoRoot, "artifacts", fileName);
        }

        [GlobalSetup]
        public void Setup()
        {
            // Parse width dynamically from the filename to handle params
            _width = IsPadded ? 5447 : 5448;
            string testImageName = IsPadded ? "TitanIR-padded-5447x3686-24bpp-YCbCr.bin.tar.gz" : "TitanIR-5448x3686-24bpp-YCbCr.bin.tar.gz";

            string archivePath = GetArtifactPath(testImageName);
            if (!File.Exists(archivePath))
                throw new FileNotFoundException($"Benchmark artifact not found: {archivePath}");

            int stride = (_width * 3 + 3) & ~3;
            _totalBytes = stride * _height;

            _managedBuffers = new sbyte[InvocationCount][];
            _nativeRgbPtrs = new IntPtr[InvocationCount];
            _nativeBackupPtr = DjvuMarshal.AllocHGlobal((uint)_totalBytes);

            for (int i = 0; i < InvocationCount; i++)
            {
                _managedBuffers[i] = new sbyte[_totalBytes];
                _nativeRgbPtrs[i] = DjvuMarshal.AllocHGlobal((uint)_totalBytes);
            }

            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
            using (TarReader tar = new TarReader(gzip))
            {
                TarEntry entry = tar.GetNextEntry();
                if (entry == null || entry.DataStream == null)
                    throw new InvalidDataException("No valid file found in the tar.gz archive.");

                byte[] rawBytes = new byte[_totalBytes];
                int totalRead = 0;
                int bytesRead;

                while (totalRead < _totalBytes && (bytesRead = entry.DataStream.Read(rawBytes, totalRead, _totalBytes - totalRead)) > 0)
                {
                    totalRead += bytesRead;
                }

                if (totalRead != _totalBytes)
                    throw new InvalidDataException($"Failed to read complete artifact from tar.gz. Expected {_totalBytes}, got {totalRead}");

                Marshal.Copy(rawBytes, 0, _nativeBackupPtr, _totalBytes);
            }
        }

        [IterationSetup]
        public void IterationSetup()
        {
            unsafe 
            {
                for (int i = 0; i < InvocationCount; i++)
                {
                    Buffer.MemoryCopy((void*)_nativeBackupPtr, (void*)_nativeRgbPtrs[i], _totalBytes, _totalBytes);
                    fixed (sbyte* ptr = _managedBuffers[i])
                    {
                        Buffer.MemoryCopy((void*)_nativeBackupPtr, ptr, _totalBytes, _totalBytes);
                    }
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (_nativeRgbPtrs != null)
            {
                for (int i = 0; i < InvocationCount; i++)
                {
                    if (_nativeRgbPtrs[i] != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeRgbPtrs[i]);
                }
            }
            if (_nativeBackupPtr != IntPtr.Zero) DjvuMarshal.FreeHGlobal(_nativeBackupPtr);
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Scalar()
        {
#pragma warning disable CS0618
            for (int i = 0; i < InvocationCount; i++)
            {
                fixed (sbyte* ptr = _managedBuffers[i])
                {
                    // Note: Will produce corrupt image on padded variant, but times execution fairly.
                    InterWaveTransform.YCbCr2RgbScalar((Pixel*)ptr, _width, _height);
                }
            }
#pragma warning restore CS0618
        }

        [Benchmark(OperationsPerInvoke = InvocationCount)]
        public unsafe void Unified()
        {
            int stride = (_width * 3 + 3) & ~3;
            for (int i = 0; i < InvocationCount; i++)
            {
                fixed (sbyte* ptr = _managedBuffers[i])
                {
                    InterWaveTransform.YCbCr2Rgb((Pixel*)ptr, _width, _height, stride);
                }
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = InvocationCount)]
        public void Native()
        {
            int gpRowSize = ((_width * 3 + 3) & ~3) / 3;
            for (int i = 0; i < InvocationCount; i++)
            {
                NativeMethods.YCbCrToRgb(_nativeRgbPtrs[i], _width, _height, gpRowSize);
            }
        }
    }
}