using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
#if NETCOREAPP
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DjvuNet.DjvuLibre;
using DjvuNet.Serialization;
using Xunit;

namespace DjvuNet.Tests
{
    public static partial class Util
    {
        private static string _ArtifactsPath;
        private static string _ArtifactsContentPath;
        private static string _ArtifactsDataPath;
        private static string _ArtifactsJsonPath;

        private static SortedDictionary<int, Tuple<int, int, DocumentType, string> > _TestDocumentData;

        public static SortedDictionary<int, Tuple<int, int, DocumentType, string>> TestDocumentData
        {
            get
            {
                if (_TestDocumentData != null)
                {
                    return _TestDocumentData;
                }
                else
                {
                    var dict = new SortedDictionary<int, Tuple<int, int, DocumentType, string>>();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };

                    for (int i = 1; i <= 77; i++)
                    {
                        string filePath = GetTestFilePath(i);
                        filePath = Path.Combine(Util.ArtifactsJsonPath,
                            Path.GetFileNameWithoutExtension(filePath) + ".json");

                        string json = File.ReadAllText(filePath, new UTF8Encoding(false));

                        DjvuDoc doc = JsonSerializer.Deserialize<DjvuDoc>(json, options);

                        Tuple<int, int, DocumentType, string> docData;
                        if (doc.DjvuData is DjvmForm djvm)
                        {
                            var docType = (DocumentType) Enum.Parse(typeof(DocumentType), djvm.Dirm.DocumentType, true);

                            docData = Tuple.Create<int, int, DocumentType, string>(
                                djvm.Dirm.PageCount, djvm.Dirm.FileCount, docType, null);
                        }
                        else
                        {
                            var djvu = doc.DjvuData as DjvuForm;
                            docData = Tuple.Create<int, int, DocumentType, string>(
                                1, 1, DocumentType.SinglePage, null);
                        }
                        if (!dict.ContainsKey(i))
                            dict.Add(i, docData);
                    }

                    _TestDocumentData = dict;
                    return _TestDocumentData;
                }
            }
        }

        public static int GetTestDocumentPageCount(int index)
        {
            return TestDocumentData[index].Item1;
        }

        public static int GetTestDocumentFileCount(int index)
        {
            return TestDocumentData[index].Item2;
        }

        public static DocumentType GetTestDocumentType(int index)
        {
            return TestDocumentData[index].Item3;
        }

        public static string GetTestDocumentJsonDump(int index)
        {
            return TestDocumentData[index].Item4;
        }

        public static void FailOnException(Exception ex, string message, params object[] data)
        {
            string info = $"\nTest Failed -> Unexpected Exception: " +
                $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}\n\n";

            if (data?.Length > 0)
            {
                info += (String.Format(message, data) + "\n" + ex.ToString());
            }
            else
            {
                info += (message + "\n" + ex.ToString());
            }

            Assert.Fail(info);
        }

        public static string GetTestFilePathTemplate()
        {
            char dirSep = Path.DirectorySeparatorChar;
            string filePathTempl = $"artifacts{dirSep}test{{0:00#}}C.djvu";
            string rootDir = Util.RepoRoot;
            return Path.Combine(rootDir, filePathTempl);
        }

        public static string GetTestFilePath(int index)
        {
            string filePathTempl = GetTestFilePathTemplate();
            string filePath = String.Format(filePathTempl, index);
            return filePath;
        }

        public static byte[] ReadFileToEnd(string bzzFile)
        {
            using (FileStream stream = File.OpenRead(Path.Combine(Util.RepoRoot, bzzFile)))
            {
                byte[] buffer = new byte[stream.Length];
                int countRead = stream.Read(buffer, 0, buffer.Length);
                if (countRead != buffer.Length)
                    throw new IOException($"Unable to read file with test data: {bzzFile}");
                return buffer;
            }
        }

        public static string ArtifactsPath
        {
            get
            {
                if (_ArtifactsPath != null)
                {
                    return _ArtifactsPath;
                }
                else
                {
                    _ArtifactsPath = Path.Combine(Util.RepoRoot, "artifacts");
                    return _ArtifactsPath;
                }
            }
        }

        // Pre-compile at class level for performance:
        private static readonly Regex ArchiveSignatureRegex = new Regex(
            @"-\d+\.\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9\-\.]+)?_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-(report|asm)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Determines if a file has already been processed and archived by the benchmark reporting engine.
        /// </summary>
        /// <remarks>
        /// The goal of this function is Archival Integrity. Historical benchmark data must remain
        /// untouched as an immutable record of past optimizations.
        /// By detecting the complete, unique signature of the DjvuNet archival engine—which consists of
        /// the dynamic repository version string (Major.Minor.yyDOY.Order[-hash[-dev]]), the execution timestamp,
        /// and the final "-report" or "-asm" suffix—the engine can safely skip
        /// this file, preventing data falsification or "filename snowballing".
        /// </remarks>
        /// <param name="fileNameWithoutExtension">The name of the file to check, without its extension.</param>
        /// <returns>True if the file contains the full archival signature; otherwise, False.</returns>
        public static bool IsArchivedBenchmarkFile(string fileNameWithoutExtension)
        {
            return ArchiveSignatureRegex.IsMatch(fileNameWithoutExtension);
        }

        /// <summary>
        /// Generates a new archival filename by appending the version, timestamp, and correct report suffix.
        /// </summary>
        /// <remarks>
        /// The goal is to safely track optimization progress over time by injecting the repository state
        /// and execution time into the filename. To ensure a consistent signature, we strip the native
        /// BenchmarkDotNet suffixes (like -report-github) and enforce our own "-report" or "-asm" suffix at the end.
        /// </remarks>
        /// <param name="originalNameWithoutExtension">The original file name generated by BDN, without the extension.</param>
        /// <param name="suffix">The archival suffix containing the repo version and run timestamp (e.g. "-0.10.26146.2-d5580f5-dev_2026-05-29_20-43-00").</param>
        /// <returns>The fully formatted archival filename without extension.</returns>
        public static string GetArchiveFileName(string originalNameWithoutExtension, string suffix)
        {
            string name = originalNameWithoutExtension;
            bool isAsm = name.EndsWith("-asm");

            // Strip any native BDN suffix starting with -report (e.g., -report, -report-default, -report-github)
            name = Regex.Replace(name, @"-report(-[a-zA-Z0-9\-]+)?$", "");

            // Strip -asm if it's there
            if (isAsm) name = name.Substring(0, name.Length - 4);

            if (isAsm)
                return name + suffix + "-asm";
            else
                return name + suffix + "-report";
        }

        public static string ArtifactsContent
        {
            get
            {
                if (_ArtifactsContentPath != null)
                {
                    return _ArtifactsContentPath;
                }
                else
                {
                    _ArtifactsContentPath = Path.Combine(ArtifactsPath, "content");
                    return _ArtifactsContentPath;
                }
            }
        }

        public static string ArtifactsDataPath
        {
            get
            {
                if (_ArtifactsDataPath != null)
                {
                    return _ArtifactsDataPath;
                }
                else
                {
                    _ArtifactsDataPath = Path.Combine(ArtifactsPath, "data");
                    return _ArtifactsDataPath;
                }
            }
        }

        public static string ArtifactsJsonPath
        {
            get
            {
                if (_ArtifactsJsonPath != null)
                {
                    return _ArtifactsJsonPath;
                }
                else
                {
                    _ArtifactsJsonPath = Path.Combine(ArtifactsPath, "json");
                    return _ArtifactsJsonPath;
                }
            }
        }

        public static void AssertBufferEqal(byte[] buffer, byte[] refBuffer)
        {
            Assert.True(refBuffer.Length == buffer.Length,
                $"Output length mismatch. Expected: {refBuffer.Length}, actual: {buffer.Length}");

            unsafe
            {
                fixed(byte* buf = buffer, refbuf = refBuffer)
                {
                    int* pbuf = (int*) buf;
                    int* pref = (int*)refbuf;

                    int* end = pbuf + refBuffer.Length;

                    int div = refBuffer.Length / sizeof(int);

                    for (int i = 0; i < div; i++)
                    {
                        if (*pbuf++ != *pref++)
                        {
                            Assert.True(false);
                        }
                    }

                    int rem = refBuffer.Length % sizeof(int);

                    if (rem > 0)
                    {
                        byte* bbuf = buf;
                        byte* bref = refbuf;
                        for (int i = 0; i < rem; i++)
                            Assert.Equal(*bbuf++, *bref++);
                    }
                }
            }
        }

        public static List<String> TestUnicodeStrings
        {
            get
            {
                List<String> retVal = new List<string>(new string[]
                {
                    "免去于革胜的全国社会保障基金理事会副理事长",
                    "재정난이 심해져 조직 내 구조조정과 임금 삭감이",
                    "وتهدف العملية إلى حماية المدنيين ومنع تحركات الحوثيين وقوات الرئيس المخلوع علي عبد الله صالح، وتوسيع وتوطيد التعاون",
                    "กรมศิลปากรได้พิจารณาแล้วเห็นว่า ตาม พ.ร.บ.โบราณสถาน โบราณวัตถุ ศิลปวัตถุ และพิพิธภัณฑสถานแห่งชาติ พ.ศ.2504 แก้ไขเพิ่มเติม",
                    "След като месеци наред отричаше да има подобно намерение, сега Мей сподели",
                    "Die Premierministerin Großbritanniens erhofft sich von Neuwahlen ein stärkeres Mandat für die Verhandlungen mit Brüssel",
                    "Παρά τις προβλέψεις για άμεσο οικονομικό κίνδυνο, μετά το δημοψήφισμα του περασμένου καλοκαιριού είδαμε ότι η εμπιστοσύνη",
                    "על-פי הערכות, נתניהו לא עודכן על קיום מסיבת העיתונאים של כחלון וגם לא על תוכן התוכנית שהוצגה",
                    "ホテル近くに横浜市の新市庁舎が移転することから「外国人観光客の増加も見込まれるが",
                    "В ходы войны город Идлиб переходил из рук в руки, но в итоге остался под контролем оппозиционеров",
                    "बयान में कहा गया, ‘एनएसए मैकमास्टर ने भारत-अमेरिका के सामरिक रिश्तों पर जोर दिया और भारत के एक",
                    "Mağazalarla ek 300 kişiye istihdam sağlayacaklarının altını çizen Serbes, dolaylı olarak da 1000 kişiye iş yaratılacağını belirtti",
                    "Cả hai đội đều có những thay đổi về đội hình ra sân. Bale vắng mặt nên Isco được đá chính trên hàng công",
                    "Nie wszystko dało się przewidzieć, stąd drobne opóźnienie – tłumaczy Sylwester Puczen, rzecznik Toru Służewiec",
                    "Guðlaugur Þór Þórðarson utanríkisráðherra átti í dag fund með Boris Johnson, utanríkisráðherra Bretlands í Lundúnum þar sem þeir ræddu útgöngu Breta úr Evrópusambandinu og leiðir til að efla samskipti Íslands og Bretlands",
                    "Konservatiivipuolueen kannattajilleen ja toimittajille lähettämässä kirjeessäkin puhutaan pelitermein \"vahvemmasta kädestä\" eli pääministerille halutaan paremmat kortit käteen kun hän lähtee EU",
                    "Det er mye som lykkes for Ap-leder Jonas Gahr Støre. På borgerlig side er samarbeidet gått surt, og kaoset truer. Meningsmålingene har gitt Ap",
                    "Em carta divulgada na segunda-feira (17), o ex-presidente da Câmara Eduardo Cunha rebatou as afirmações do presidente",
                });

                return retVal;
            }
        }

        private static bool IsImageBinaryComparable(Bitmap image1, Bitmap image2, out bool pixelFormatMismatch)
        {
            bool result = true;
            pixelFormatMismatch = false;

            if (image1 == null || image2 == null)
            {
                result = false;
            }
            else if (image1.PixelFormat != image2.PixelFormat || image1.PixelFormat != PixelFormat.Format24bppRgb)
            {
                pixelFormatMismatch = true;
            }
            else if (image1.Width != image2.Width || image1.Height != image2.Height)
            {
                result = false;
            }

            return result;
        }

        private static bool IsImageBinaryComparable(BitmapData image1, BitmapData image2)
        {
            bool result = true;

            if (image1 == null || image2 == null)
            {
                result = false;
            }
            else if (image1.PixelFormat != image2.PixelFormat)
            {
                result = false;
            }
            else if (image1.Width != image2.Width || image1.Height != image2.Height)
            {
                result = false;
            }

            return result;
        }

        public static bool CompareImagesForBinarySimilarity(Bitmap image1, Bitmap image2, double diffThreshold = 0.05, bool logDiff = false, string message = null)
        {
            double diff;
            bool result = CompareImagesForBinarySimilarity(image1, image2, out diff, diffThreshold);

            if (logDiff)
            {
                Console.WriteLine((message != null ? message : "") + $" Image diff: {diff:#0.0000}, passed: {result}");
            }

            return result;
        }

        public static bool CompareImagesForBinarySimilarity(Bitmap image1, Bitmap image2, out double diffValue, double diffThreshold = 0.05)
        {
            bool formatMismatch;
            bool result = IsImageBinaryComparable(image1, image2, out formatMismatch);

            diffValue = double.NaN;
            Bitmap bmp1 = null;
            Bitmap bmp2 = null;

            try
            {

                if (result && formatMismatch)
                {
                    if (image1.PixelFormat != PixelFormat.Format24bppRgb)
                    {
                        bmp1 = new Bitmap(image1.Width, image1.Height, PixelFormat.Format24bppRgb);
                        using var gfx = System.Drawing.Graphics.FromImage(bmp1);
                        gfx.DrawImage(image1, new Rectangle(0, 0, image1.Width, image1.Height));
                    }

                    if (image2.PixelFormat != PixelFormat.Format24bppRgb)
                    {
                        bmp2 = new Bitmap(image2.Width, image2.Height, PixelFormat.Format24bppRgb);
                        using var gfx = System.Drawing.Graphics.FromImage(bmp2);
                        gfx.DrawImage(image2, new Rectangle(0, 0, image2.Width, image2.Height));
                    }
                }

                if (result)
                {
                    Rectangle rect = new Rectangle(0, 0, image1.Width, image1.Height);
                    BitmapData img1 = bmp1?.LockBits(rect, ImageLockMode.ReadOnly, bmp1.PixelFormat) ?? image1.LockBits(rect, ImageLockMode.ReadOnly, image1.PixelFormat);
                    BitmapData img2 = bmp2?.LockBits(rect, ImageLockMode.ReadOnly, bmp2.PixelFormat) ?? image2.LockBits(rect, ImageLockMode.ReadOnly, image2.PixelFormat);

                    result = (diffValue = ImageBinarySimilarity(img1, img2)) <= diffThreshold;

                    if (bmp1 != null)
                    {
                        bmp1?.UnlockBits(img1);
                    }
                    else
                    {
                        image1.UnlockBits(img1);
                    }

                    if (bmp2 != null)
                    {
                        bmp2.UnlockBits(img2);
                    }
                    else
                    {
                        image2.UnlockBits(img2);
                    }
                }
            }
            finally
            {
                if (bmp1 != null)
                {
                    bmp1.Dispose();
                }

                if (bmp2 != null)
                {
                    bmp2.Dispose();
                }
            }

            return result;
        }

        /// <summary>
        /// Calculate average pixel binary diff between images
        /// </summary>
        /// <param name="imageData1"></param>
        /// <param name="imageData2"></param>
        /// <returns></returns>
        public static double ImageBinarySimilarity(BitmapData imageData1, BitmapData imageData2)
        {
            if (IsImageBinaryComparable(imageData1, imageData2))
            {
                return imageData1.PixelFormat switch
                {
                    PixelFormat.Format32bppArgb => ImageBinaryDiff(imageData1, imageData2, 32),
                    PixelFormat.Format24bppRgb => ImageBinaryDiff(imageData1, imageData2),
                    PixelFormat.Format8bppIndexed => ImageBinaryDiff(imageData1, imageData2, 8),
                    PixelFormat.Format16bppGrayScale => ImageBinaryDiff(imageData1, imageData2, 16, 16),
                    _ => throw new ArgumentException("Unsupported Image PixelFormat", nameof(imageData1.PixelFormat))
                };
            }
            else
            {
                return 1.0;
            }
        }

        /// <summary>
        /// Calculates the average absolute difference per channel per pixel across the whole image using raw pointers.
        /// It is the caller's responsibility to ensure that both image buffers share the exact same stride layout.
        /// </summary>
        /// <param name="ptr1">Pointer to the first image buffer.</param>
        /// <param name="ptr2">Pointer to the second image buffer.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="stride">The row stride (in bytes), including any padding and preserving the sign (for bottom-up images).</param>
        /// <param name="pixelSize">The size of a single pixel in bits (e.g., 24 for 24bpp RGB, 32 for ARGB).</param>
        /// <param name="channelSize">The size of a single color channel in bits (e.g., 8 for standard RGB channels).</param>
        /// <returns>A ratio between 0.0 (identical) and 1.0 (completely opposite) representing the average pixel difference.</returns>
        internal static unsafe double ImageBinaryDiff(byte* ptr1, byte* ptr2, int width, int height, int stride, int pixelSize = 24, int channelSize = 8)
        {
            if (channelSize % 8 != 0)
            {
                throw new ArgumentException("Method supports only multiple of 8 bits channel sizes");
            }
            return ImageBinaryDiffCore(ptr1, ptr2, (uint)width, (uint)height, stride, pixelSize, channelSize);
        }

        /// <summary>
        /// Calculates the average absolute difference per channel per pixel across the whole image using BitmapData.
        /// </summary>
        /// <param name="imageData1">The first image data object.</param>
        /// <param name="imageData2">The second image data object.</param>
        /// <param name="pixelSize">The size of a single pixel in bits (e.g., 24 for 24bpp RGB, 32 for ARGB).</param>
        /// <param name="channelSize">The size of a single color channel in bits (e.g., 8 for standard RGB channels).</param>
        /// <returns>A ratio between 0.0 (identical) and 1.0 (completely opposite) representing the average pixel difference.</returns>
        internal static unsafe double ImageBinaryDiff(BitmapData imageData1, BitmapData imageData2, int pixelSize = 24, int channelSize = 8)
        {
            if (channelSize % 8 != 0)
            {
                throw new ArgumentException("Method supports only multiple of 8 bits channel sizes");
            }

            if (imageData1.Stride != imageData2.Stride)
            {
                // We do not support comparing images with different strides.
                return 1.0;
            }

            uint width = (uint)imageData1.Width;
            uint height = (uint)imageData1.Height;
            int stride = imageData1.Stride;

            return ImageBinaryDiffCore((byte*)imageData1.Scan0, (byte*)imageData2.Scan0, width, height, stride, pixelSize, channelSize);
        }

        /// <summary>
        /// Calculates the aggregate average absolute difference per pixel across an entire image using SIMD hardware acceleration.
        /// </summary>
        /// <param name="scan0_1">Pointer to the start of the first image buffer in memory.</param>
        /// <param name="scan0_2">Pointer to the start of the second image buffer in memory.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="stride">The row stride in bytes, including any memory alignment padding.</param>
        /// <param name="pixelSize">The total size of a single pixel in bits (e.g., 24 for RGB).</param>
        /// <param name="channelSize">The size of a single color channel in bits (e.g., 8 for standard channels).</param>
        /// <returns>A <see cref="double"/> representing the average pixel difference across the image channels.</returns>
        /// <remarks>
        /// <para>
        /// It is strictly the caller's responsibility to ensure that both pointers reference buffers allocated with the
        /// exact same stride layout. The method natively calculates the visible byte width and uses linear byte processing
        /// to ensure memory stride padding is never read.
        /// </para>
        /// <para>
        /// When an image row's visible width is not perfectly aligned to a hardware vector boundary (32 bytes for AVX2,
        /// 16 bytes for Vector128), this method implements a tail-shift strategy to process the remainder bytes. The
        /// pointer for the final vector load is shifted backwards by a calculated offset, guaranteeing the load ends
        /// exactly on the last valid byte of the row without over-reading into the uninitialized padding.
        /// </para>
        /// <para>
        /// To prevent overlapping bytes (which were already processed in the previous SIMD iteration) from being double-counted
        /// in the accumulation, a dynamic bitmask is generated via a sequence comparison. This mask zeroes out the overlapping
        /// bytes using a bitwise AND operation, zerping them before the Sum of Absolute Differences (SAD) calculation occurs.
        /// </para>
        /// <para>
        /// The Vector128 fallback path optimizes pipeline throughput by accumulating differences into smaller lanes for up
        /// to 255 inner-loop iterations before flushing to larger row accumulators. This prevents accumulation overflow on
        /// large images while avoiding the latency of horizontal widening instructions in the hot path.
        /// </para>
        /// </remarks>
        internal static unsafe double ImageBinaryDiffCore(byte* scan0_1, byte* scan0_2, uint width, uint height, int stride, int pixelSize, int channelSize)
        {
            uint pixelSizeInBytes = (uint)pixelSize / 8;
            uint widthBytes = width * pixelSizeInBytes;
            double result = 0.0;

        #if NETCOREAPP
            if (Avx2.IsSupported && widthBytes >= 32)
            {
                int vectorBound = (int)widthBytes - 32;
                int tailShift = (int)((32 - (widthBytes % 32)) % 32);

                Vector256<double> mask = Vector256.Create((double)0x0010000000000000);
                Vector256<ulong> resultVecU = Vector256<ulong>.Zero;

                Vector256<sbyte> seq256 = Vector256.Create(
                    (sbyte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
                    16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31);
                Vector256<sbyte> threshold = Vector256.Create((sbyte)(tailShift - 1));
                Vector256<byte> tailMask = Avx2.CompareGreaterThan(seq256, threshold).AsByte();

                for (uint i = 0; i < height; i++)
                {
                    byte* p1 = scan0_1 + ((long)i * stride);
                    byte* p2 = scan0_2 + ((long)i * stride);
                    int x = 0;

                    while (x <= vectorBound - 96)
                    {
                        Vector256<byte> r11 = Avx2.LoadDquVector256(p1);
                        Vector256<byte> r21 = Avx2.LoadDquVector256(p2);
                        Vector256<byte> r12 = Avx2.LoadDquVector256(p1 + 32);
                        Vector256<byte> r22 = Avx2.LoadDquVector256(p2 + 32);
                        Vector256<ushort> diff1 = Avx2.SumAbsoluteDifferences(r11, r21);
                        Vector256<ushort> diff2 = Avx2.SumAbsoluteDifferences(r12, r22);

                        Vector256<byte> r13 = Avx2.LoadDquVector256(p1 + 64);
                        Vector256<byte> r23 = Avx2.LoadDquVector256(p2 + 64);
                        Vector256<byte> r14 = Avx2.LoadDquVector256(p1 + 96);
                        Vector256<byte> r24 = Avx2.LoadDquVector256(p2 + 96);
                        Vector256<ushort> diff3 = Avx2.SumAbsoluteDifferences(r13, r23);
                        Vector256<ushort> diff4 = Avx2.SumAbsoluteDifferences(r14, r24);

                        Vector256<ulong> diff12 = Avx2.Add(diff1.AsUInt64(), diff2.AsUInt64());
                        Vector256<ulong> diff34 = Avx2.Add(diff3.AsUInt64(), diff4.AsUInt64());
                        resultVecU = Avx2.Add(resultVecU, Avx2.Add(diff12, diff34));

                        p1 += 128;
                        p2 += 128;
                        x += 128;
                    }

                    while (x <= vectorBound)
                    {
                        Vector256<byte> r1 = Avx2.LoadDquVector256(p1);
                        Vector256<byte> r2 = Avx2.LoadDquVector256(p2);
                        Vector256<ushort> diff = Avx2.SumAbsoluteDifferences(r1, r2);

                        resultVecU = Avx2.Add(resultVecU, diff.AsUInt64());

                        p1 += 32;
                        p2 += 32;
                        x += 32;
                    }

                    if (x < widthBytes)
                    {
                        Vector256<byte> r1 = Avx2.LoadDquVector256(p1 - tailShift);
                        Vector256<byte> r2 = Avx2.LoadDquVector256(p2 - tailShift);

                        r1 = Avx2.And(r1, tailMask);
                        r2 = Avx2.And(r2, tailMask);

                        Vector256<ushort> diff = Avx2.SumAbsoluteDifferences(r1, r2);
                        resultVecU = Avx2.Add(resultVecU, diff.AsUInt64());
                    }
                }

                Vector256<ulong> tmpVec1 = Avx2.Or(resultVecU, mask.AsUInt64());
                Vector256<double> diff1Double = Avx2.Subtract(tmpVec1.AsDouble(), mask);

                Vector256<double> result1 = Avx2.HorizontalAdd(diff1Double, diff1Double);

                Vector128<double> lowVec = Avx2.ExtractVector128(result1, 0b0);
                Vector128<double> hiVec = Avx2.ExtractVector128(result1, 0b1);
                Vector128<double> resultVec = Avx2.AddScalar(lowVec, hiVec);

                result += resultVec.GetElement(0);
            }
            else if (Vector128.IsHardwareAccelerated && widthBytes >= 16)
            {
                int vectorBound = (int)widthBytes - 16;
                int tailShift = (int)((16 - (widthBytes % 16)) % 16);

                Vector128<ulong> imageAccum64L = Vector128<ulong>.Zero;
                Vector128<ulong> imageAccum64H = Vector128<ulong>.Zero;

                Vector128<sbyte> seq128 = Vector128.Create((sbyte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
                Vector128<sbyte> threshold = Vector128.Create((sbyte)(tailShift - 1));
                Vector128<byte> tailMask = Vector128.GreaterThan(seq128, threshold).AsByte();

                for (uint i = 0; i < height; i++)
                {
                    byte* p1 = scan0_1 + ((long)i * stride);
                    byte* p2 = scan0_2 + ((long)i * stride);
                    int x = 0;

                    while (x <= vectorBound)
                    {
                        int batchLimit = Math.Min(x + (255 * 16), vectorBound + 16);
                        if (batchLimit > vectorBound) batchLimit = vectorBound + 1; // Ensure we only run up to vectorBound

                        Vector128<ushort> batchAccum16L = Vector128<ushort>.Zero;
                        Vector128<ushort> batchAccum16H = Vector128<ushort>.Zero;

                        while (x < batchLimit)
                        {
                            Vector128<byte> r1 = Vector128.Load(p1);
                            Vector128<byte> r2 = Vector128.Load(p2);

                            Vector128<byte> diff = Vector128.Subtract(Vector128.Max(r1, r2), Vector128.Min(r1, r2));

                            batchAccum16L = Vector128.Add(batchAccum16L, Vector128.WidenLower(diff));
                            batchAccum16H = Vector128.Add(batchAccum16H, Vector128.WidenUpper(diff));

                            p1 += 16;
                            p2 += 16;
                            x += 16;
                        }

                        Vector128<uint> batch32L = Vector128.Add(Vector128.WidenLower(batchAccum16L), Vector128.WidenUpper(batchAccum16L));
                        Vector128<uint> batch32H = Vector128.Add(Vector128.WidenLower(batchAccum16H), Vector128.WidenUpper(batchAccum16H));

                        imageAccum64L = Vector128.Add(imageAccum64L, Vector128.Add(Vector128.WidenLower(batch32L), Vector128.WidenUpper(batch32L)));
                        imageAccum64H = Vector128.Add(imageAccum64H, Vector128.Add(Vector128.WidenLower(batch32H), Vector128.WidenUpper(batch32H)));
                    }

                    if (x < widthBytes)
                    {
                        Vector128<byte> r1 = Vector128.Load(p1 - tailShift);
                        Vector128<byte> r2 = Vector128.Load(p2 - tailShift);

                        r1 = Vector128.BitwiseAnd(r1, tailMask);
                        r2 = Vector128.BitwiseAnd(r2, tailMask);

                        Vector128<byte> diff = Vector128.Subtract(Vector128.Max(r1, r2), Vector128.Min(r1, r2));

                        Vector128<ushort> sum16L = Vector128.WidenLower(diff);
                        Vector128<ushort> sum16H = Vector128.WidenUpper(diff);

                        Vector128<uint> sum32L = Vector128.Add(Vector128.WidenLower(sum16L), Vector128.WidenUpper(sum16L));
                        Vector128<uint> sum32H = Vector128.Add(Vector128.WidenLower(sum16H), Vector128.WidenUpper(sum16H));

                        imageAccum64L = Vector128.Add(imageAccum64L, Vector128.Add(Vector128.WidenLower(sum32L), Vector128.WidenUpper(sum32L)));
                        imageAccum64H = Vector128.Add(imageAccum64H, Vector128.Add(Vector128.WidenLower(sum32H), Vector128.WidenUpper(sum32H)));
                    }
                }

                Vector128<ulong> finalSum64 = Vector128.Add(imageAccum64L, imageAccum64H);
                result += Vector128.Sum(finalSum64);
            }
            else
        #endif
            {
                result += ImageBinaryDiffScalar(scan0_1, scan0_2, widthBytes, height, stride);
            }

            double maxChannelValue = (1 << channelSize) - 1;
            return result / (width * height * ((double)pixelSize / channelSize) * maxChannelValue);
        }

        /// <summary>
        /// A highly optimized, branchless scalar fallback for calculating aggregate absolute image differences.
        /// </summary>
        /// <param name="scan0_1">Pointer to the start of the first image buffer in memory.</param>
        /// <param name="scan0_2">Pointer to the start of the second image buffer in memory.</param>
        /// <param name="widthBytes">The exact number of visible bytes per row, excluding any stride padding.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="stride">The row stride in bytes, including any memory alignment padding.</param>
        /// <returns>A <see cref="double"/> representing the raw sum of all absolute byte differences across the entire image.</returns>
        /// <remarks>
        /// <para>
        /// This method operates strictly on the calculated visible bytes rather than the full row stride to guarantee
        /// that memory row-padding bytes are never processed. It iterates over the continuous byte streams linearly,
        /// natively supporting various pixel formats without format-specific conditional branching.
        /// </para>
        /// <para>
        /// Floating-point conversions are deferred until the very end of the calculation. The inner loop uses an integer
        /// accumulator to track byte differences, avoiding the multi-cycle latency of floating-point addition in the
        /// tight iteration path.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe double ImageBinaryDiffScalar(byte* scan0_1, byte* scan0_2, uint widthBytes, uint height, int stride)
        {
            ulong result = 0;
            for (uint i = 0; i < height; i++)
            {
                byte* p1 = scan0_1 + ((long)i * stride);
                byte* p2 = scan0_2 + ((long)i * stride);

                for (uint wb = 0; wb < widthBytes; wb++)
                {
                    uint v1 = *p1;
                    uint v2 = *p2;
                    result += (v1 > v2) ? (v1 - v2) : (v2 - v1);
                    p1++;
                    p2++;
                }
            }
            return (double)result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe double GetPixelDiff(byte* pixel1, byte* pixel2)
        {
            float r1 = (float)(*pixel1);
            float g1 = (float)(*(++pixel1));
            float b1 = (float)(*(++pixel1));

            float r2 = (float)(*pixel2);
            float g2 = (float)(*(++pixel2));
            float b2 = (float)(*(++pixel2));

#if NETCOREAPP
            return MathF.Abs(r1 - r2) + MathF.Abs(g1 - g2) + MathF.Abs(b1 - b2);
#else
            return Math.Abs(r1 - r2) + Math.Abs(g1 - g2) + Math.Abs(b1 - b2);
#endif
        }

        public static bool CompareImages(Bitmap image1, Bitmap image2)
        {
            bool pixelFormatMismatch = false;
            bool result = IsImageBinaryComparable(image1, image2, out pixelFormatMismatch);

            if (result)
            {
                Rectangle rect = new Rectangle(0, 0, image1.Width, image1.Height);
                BitmapData img1 = image1.LockBits(rect, ImageLockMode.ReadOnly, image1.PixelFormat);
                BitmapData img2 = image2.LockBits(rect, ImageLockMode.ReadOnly, image1.PixelFormat);

                result = CompareImagesInternal(img1, img2);

                image1.UnlockBits(img1);
                image2.UnlockBits(img2);
            }

            return result;
        }

        public static bool CompareImages(BitmapData image1, BitmapData image2)
        {
            if (image1.PixelFormat != image2.PixelFormat)
            {
                return false;
            }

            if (image1.Width != image2.Width || image1.Height != image2.Height)
            {
                return false;
            }

            return CompareImagesInternal(image1, image2);
        }

        private static bool CompareImagesInternal(BitmapData image1, BitmapData image2)
        {
            if (Environment.Is64BitProcess)
            {
                return CompareImages64(image1, image2);
            }
            else
            {
                return CompareImages32(image1, image2);
            }
        }

        private static bool CompareImages64(BitmapData image1, BitmapData image2)
        {
            int pixelSize = Image.GetPixelFormatSize(image1.PixelFormat);

            unsafe
            {
                ulong rowSize = (ulong) ((pixelSize / 8) * image1.Width);
                ulong rowSizeWithPadding = (ulong) image1.Stride;
                ulong* longCheckSize;

                ulong* lp, lpRow = (ulong*)image1.Scan0;
                ulong* rp, rpRow = (ulong*)image2.Scan0;

                for (uint i = 0; i < image1.Height; i++)
                {
                    lp = (ulong*)(((byte*) lpRow) + (i * rowSizeWithPadding));
                    rp = (ulong*)(((byte*) rpRow) + (i * rowSizeWithPadding));
                    longCheckSize = (ulong*) (((byte*) lp) + rowSize);

                    for (; lp < longCheckSize; lp++, rp++)
                    {
                        if (*lp != *rp)
                        {
                            return false;
                        }
                    }

                    int remainder = 0;

                    if ((remainder = (int) (longCheckSize - lp)) > 0)
                    {
                        byte* lb = (byte*)lp;
                        byte* rb = (byte*)rp;

                        for (int ii = 0; ii < remainder; ii++, lb++, rb++)
                        {
                            if (*lb != *rb)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static bool CompareImages32(BitmapData image1, BitmapData image2)
        {
            int pixelSize = Image.GetPixelFormatSize(image1.PixelFormat);

            unsafe
            {
                uint rowSize = (uint)((pixelSize / 8) * image1.Width);
                uint rowSizeWithPadding = (uint)image1.Stride;
                uint* longCheckSize;

                uint* lp, lpRow = (uint*)image1.Scan0;
                uint* rp, rpRow = (uint*)image2.Scan0;

                for (uint i = 0; i < image1.Height; i++)
                {
                    lp = (uint*)(((byte*)lpRow) + (i * rowSizeWithPadding));
                    rp = (uint*)(((byte*)rpRow) + (i * rowSizeWithPadding));
                    longCheckSize = (uint*)(((byte*)lp) + rowSize);

                    for (; lp < longCheckSize; lp++, rp++)
                    {
                        if (*lp != *rp)
                        {
                            return false;
                        }
                    }

                    int remainder = 0;

                    if ((remainder = (int)(longCheckSize - lp)) > 0)
                    {
                        byte* lb = (byte*)lp;
                        byte* rb = (byte*)rp;

                        for (int ii = 0; ii < remainder; ii++, lb++, rb++)
                        {
                            if (*lb != *rb)
                            {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        public static Bitmap InvertColor(Bitmap source)
        {
            ColorMatrix colorMatrix = new ColorMatrix(
                new float[][]
                {
                    new float[] {-1, 0,  0,  0,  0},
                    new float[] {0, -1,  0,  0,  0},
                    new float[] {0,  0, -1,  0,  0},
                    new float[] {0,  0,  0,  1,  0},
                    new float[] {1,  1,  1,  0,  1}
                });

            return TransformBitmap(source, colorMatrix);
        }

        public static Bitmap TransformBitmap(Bitmap source, ColorMatrix colorMatrix)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, source.PixelFormat);

            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(result))
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
                    g.DrawImage(source, rect, 0, 0, source.Width, source.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
            return result;
        }
    }
}
