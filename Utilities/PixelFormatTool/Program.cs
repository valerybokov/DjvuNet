using System;
using System.CommandLine;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DjvuNet.DjvuLibre;

namespace DjvuNet.Utils.PixelFormatTool
{
    class Program
    {
        static int Main(string[] args)
        {
            var inputOption = new Option<FileInfo>("--input")
            {
                Description = "The path to the input RGB image file.",
                Required = true
            };
            inputOption.Aliases.Add("-i");

            var formatOption = new Option<string>("--format")
            {
                Description = "The target pixel format.",
                DefaultValueFactory = _ => "ycbcr"
            };
            formatOption.Aliases.Add("-f");

            var outputOption = new Option<FileInfo>("--output")
            {
                Description = "The specific path for the generated output file."
            };
            outputOption.Aliases.Add("-o");

            var rootCommand = new RootCommand("Pixel Format Tool");
            rootCommand.Options.Add(inputOption);
            rootCommand.Options.Add(formatOption);
            rootCommand.Options.Add(outputOption);

            rootCommand.SetAction((ParseResult parseResult) =>
            {
                FileInfo input = parseResult.GetValue(inputOption);
                string format = parseResult.GetValue(formatOption);
                FileInfo output = parseResult.GetValue(outputOption);

                if (input == null || !input.Exists)
                {
                    Console.WriteLine($"File not found: {input?.FullName}");
                    return 1;
                }

                if (format.Equals("ycbcr", StringComparison.OrdinalIgnoreCase))
                {
                    GenerateYCbCrArtifact(input.FullName, output?.FullName);
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Unsupported format: {format}");
                    return 1;
                }
            });

            return rootCommand.Parse(args).Invoke();
        }

        private static unsafe void GenerateYCbCrArtifact(string inPath, string requestedOutPath)
        {
            using (Bitmap bmp = new Bitmap(inPath))
            {
                int w = bmp.Width;
                int h = bmp.Height;
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                
                int stride = data.Stride;
                int totalBytes = stride * h;
                
                string outPath = requestedOutPath;
                if (string.IsNullOrWhiteSpace(outPath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(inPath);
                    baseName = baseName.Replace("-24bgr", "", StringComparison.OrdinalIgnoreCase)
                                       .Replace("-24rgb", "", StringComparison.OrdinalIgnoreCase);
                                       
                    outPath = Path.Combine(Path.GetDirectoryName(inPath), baseName + $"-{w}x{h}-24bpp-YCbCr.bin");
                }
                
                if (stride % 3 != 0)
                {
                    throw new NotSupportedException($"DjVuLibre NativeMethods used for YCbCr transform requires the byte stride ({stride}) to be a perfect multiple of 3. Please use an image width that satisfies this condition.");
                }

                int gpRowSize = stride / 3;
                int totalGPixels = h * gpRowSize;

                IntPtr nativeYPtr = DjvuMarshal.AllocHGlobal((uint)totalGPixels);
                IntPtr nativeCbPtr = DjvuMarshal.AllocHGlobal((uint)totalGPixels);
                IntPtr nativeCrPtr = DjvuMarshal.AllocHGlobal((uint)totalGPixels);

                try
                {
                    bool success = NativeMethods.RgbToYCbCr(data.Scan0, w, h, gpRowSize, nativeYPtr, nativeCbPtr, nativeCrPtr, gpRowSize);
                    if (!success)
                    {
                        Console.WriteLine("NativeMethods.RgbToYCbCr failed.");
                        return;
                    }

                    byte[] outBuffer = new byte[totalBytes];
                    byte* pY = (byte*)nativeYPtr.ToPointer();
                    byte* pCb = (byte*)nativeCbPtr.ToPointer();
                    byte* pCr = (byte*)nativeCrPtr.ToPointer();
                    
                    fixed (byte* pDstFixed = outBuffer)
                    {
                        byte* pDst = pDstFixed;
                        for (int i = 0; i < totalGPixels; i++)
                        {
                            *pDst++ = *pY++;
                            *pDst++ = *pCb++;
                            *pDst++ = *pCr++;
                        }
                    }

                    File.WriteAllBytes(outPath, outBuffer);
                    Console.WriteLine($"Generated: {outPath}");
                }
                finally
                {
                    DjvuMarshal.FreeHGlobal(nativeYPtr);
                    DjvuMarshal.FreeHGlobal(nativeCbPtr);
                    DjvuMarshal.FreeHGlobal(nativeCrPtr);
                    bmp.UnlockBits(data);
                }
            }
        }
    }
}