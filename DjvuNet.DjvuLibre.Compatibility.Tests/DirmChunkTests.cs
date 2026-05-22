using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DjvuNet;
using DjvuNet.DataChunks;
using DjvuNet.DjvuLibre;
using Xunit;
using DjvuNet.Tests;

namespace DjvuNet.DjvuLibre.Compatibility.Tests
{
#if DJVUNET_ALL_TESTS
    [Collection("NativeAndManagedDocCollection")]
#endif
    public class DirmChunkTests
    {
#if DJVUNET_ALL_TESTS
        private readonly DjvuDocFixture _managedFixture;
        private readonly NativeDjvuDocFixture _nativeFixture;

        public DirmChunkTests(DjvuDocFixture managedFixture, NativeDjvuDocFixture nativeFixture)
        {
            _managedFixture = managedFixture;
            _nativeFixture = nativeFixture;
        }
#endif

        [Theory]
        [MemberData(nameof(Util.AllTestDocumentIndices), MemberType = typeof(Util))]
        public void DirmChunkParsingTheory(int index)
        {
            string filePath = Util.GetTestFilePath(index);
#if DJVUNET_ALL_TESTS
            DjvuDocument managedDoc = _managedFixture.GetDocument(index);
            DjvuDocumentInfo nativeDoc = _nativeFixture.GetDocument(index);
#else
            using (var managedDoc = new DjvuDocument(filePath))
            using (var nativeDoc = DjvuDocumentInfo.CreateDjvuDocumentInfo(filePath))
#endif
            {
                var root = managedDoc.RootForm as DjvmChunk;
                if (root?.Dirm == null) return;

                var managedComponents = root.Dirm.Components;
                int nativeCount = NativeMethods.GetDjvuDocumentDirmComponentCount(nativeDoc.Document);

                Assert.Equal(nativeCount, managedComponents.Count);

                List<string> nativeIds = new List<string>();
                for (int i = 0; i < nativeCount; i++)
                {
                    IntPtr strPtr = NativeMethods.GetDjvuDocumentDirmComponentId(nativeDoc.Document, i);
                    if (strPtr == IntPtr.Zero) 
                    { 
                        nativeIds.Add(""); 
                        continue; 
                    }
                    try 
                    { 
                        string s = Marshal.PtrToStringUTF8(strPtr);
                        nativeIds.Add(s ?? ""); 
                    }
                    finally 
                    { 
                        DjvuMarshal.FreeHGlobal(strPtr); 
                    }
                }

                string nativeFull = string.Join(", ", nativeIds.Select(s => $"'{s}'"));
                string managedFull = string.Join(", ", managedComponents.Select(x => $"'{x.ID ?? ""}'"));
                
                Assert.True(nativeFull == managedFull, 
                    $"DIRM Arrays Mismatch Doc:{index}.\n  Native Array:  [{nativeFull}]\n  Managed Array: [{managedFull}]");
            }
        }

        [Fact]
        public void DirmChunkParsingTestIndex23()
        {
            DirmChunkParsingTheory(23);
        }
    }
}
