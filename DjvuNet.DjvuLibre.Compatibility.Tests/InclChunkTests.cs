using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DjvuNet;
using DjvuNet.DjvuLibre;
using Xunit;
using DjvuNet.Tests;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DjvuNet.DjvuLibre.Compatibility.Tests
{
#if DJVUNET_ALL_TESTS
    [Collection("NativeAndManagedDocCollection")]
#endif
    public class InclChunkTests
    {
#if DJVUNET_ALL_TESTS
        private readonly DjvuDocFixture _managedFixture;
        private readonly NativeDjvuDocFixture _nativeFixture;

        public InclChunkTests(DjvuDocFixture managedFixture, NativeDjvuDocFixture nativeFixture)
        {
            _managedFixture = managedFixture;
            _nativeFixture = nativeFixture;
        }
#endif

        [Theory]
        [MemberData(nameof(Util.ForegroundImageSourceDocs), MemberType = typeof(Util))]
        public void InclChunkParsingTheory(int index)
        {
            string filePath = Util.GetTestFilePath(index);
            Assert.True(File.Exists(filePath), $"Test file not found: {filePath}");

#if DJVUNET_ALL_TESTS
            DjvuDocument managedDoc = _managedFixture.GetDocument(index);
            DjvuDocumentInfo nativeDoc = _nativeFixture.GetDocument(index);
#else
            using (var managedDoc = new DjvuDocument(filePath))
            using (var nativeDoc = DjvuDocumentInfo.CreateDjvuDocumentInfo(filePath))
#endif
            {
                var managedPage = managedDoc.Pages[0];
                var managedIncludes = managedPage.PageForm?.IncludedItems;
                if (managedIncludes == null)
                {
                    managedIncludes = new List<DjvuNet.DataChunks.InclChunk>();
                }

                int nativeCount = NativeMethods.GetDjvuDocumentInclCount(nativeDoc.Document, 0);

                Assert.Equal(nativeCount, managedIncludes.Count);

                for (int i = 0; i < nativeCount; i++)
                {
                    IntPtr strPtr = NativeMethods.GetDjvuDocumentInclId(nativeDoc.Document, 0, i);
                    if (strPtr == IntPtr.Zero)
                    {
                        Assert.Fail($"Native API returned null pointer for Doc:{index} Index:{i}");
                        continue;
                    }

                    string nativeId;
                    try
                    {
                        nativeId = Marshal.PtrToStringUTF8(strPtr);
                    }
                    finally
                    {
                        DjvuMarshal.FreeHGlobal(strPtr);
                    }

                    var chunk = managedIncludes[i];
                    string managedId = chunk.IncludeID;
                    bool success = nativeId == managedId;

                    if (!success)
                    {
                        long prevPos = managedDoc.RootForm.Reader.Position;
                        managedDoc.RootForm.Reader.Position = chunk.DataOffset;
                        byte[] rawData = managedDoc.RootForm.Reader.ReadBytes((int)chunk.Length);
                        managedDoc.RootForm.Reader.Position = prevPos;

                        string hexData = BitConverter.ToString(rawData);
                        string asciiData = Encoding.ASCII.GetString(rawData);

                        Assert.True(success,
                            $"INCL Mismatch Doc:{index} Index:{i}.\n" +
                            $"  Native:  '{nativeId}'\n" +
                            $"  Managed: '{managedId}'\n" +
                            $"  Raw Hex (Length {chunk.Length}): {hexData}\n" +
                            $"  ASCII:   '{asciiData}'"
                        );
                    }
                }
            }
        }
    }
}

// "e:\src\.net\DjvuNet\build\bin\Windows.x64.Release/binaries/net10.0/win-x64/publish/DjvuNet.DjvuLibre.Compatibility.Tests.exe" -trait- "Category=skip-netcoreapp" -trait- "Category=Skip" -nologo -nocolor "-xml" "TestResults/net10.0/DjvuNet.DjvuLibre.Compatibility.Tests.xml"