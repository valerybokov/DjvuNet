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

                List<string> errorMessages = new List<string>();

                for (int i = 0; i < nativeCount; i++)
                {
                    var managedComponent = managedComponents[i];

                    // Extract Native Strings
                    string nativeId = GetNativeString(NativeMethods.GetDjvuDocumentDirmComponentId(nativeDoc.Document, i));
                    string nativeName = GetNativeString(NativeMethods.GetDjvuDocumentDirmComponentName(nativeDoc.Document, i));
                    string nativeTitle = GetNativeString(NativeMethods.GetDjvuDocumentDirmComponentTitle(nativeDoc.Document, i));

                    // Extract Native Value Types
                    NativeMethods.GetDjvuDocumentDirmComponentSize(nativeDoc.Document, i, out int nativeSize);
                    NativeMethods.GetDjvuDocumentDirmComponentFlags(nativeDoc.Document, i, out bool isPage, out bool isInclude, out bool isThumbnails, out bool isSharedAnno);

                    // Collect Mismatches
                    if (nativeId != (managedComponent.ID ?? "")) errorMessages.Add($"[Component {i}] ID Mismatch: Native '{nativeId}', Managed '{managedComponent.ID ?? ""}'");
                    if (nativeName != (managedComponent.Name ?? "")) errorMessages.Add($"[Component {i}] Name Mismatch: Native '{nativeName}', Managed '{managedComponent.Name ?? ""}'");
                    if (nativeTitle != (managedComponent.Title ?? "")) errorMessages.Add($"[Component {i}] Title Mismatch: Native '{nativeTitle}', Managed '{managedComponent.Title ?? ""}'");

                    if (nativeSize != managedComponent.Size) errorMessages.Add($"[Component {i}] Size Mismatch: Native '{nativeSize}', Managed '{managedComponent.Size}'");
                    if (isPage != managedComponent.IsPage) errorMessages.Add($"[Component {i}] IsPage Mismatch: Native '{isPage}', Managed '{managedComponent.IsPage}'");
                    if (isInclude != managedComponent.IsIncluded) errorMessages.Add($"[Component {i}] IsIncluded Mismatch: Native '{isInclude}', Managed '{managedComponent.IsIncluded}'");
                    if (isThumbnails != managedComponent.IsThumbnail) errorMessages.Add($"[Component {i}] IsThumbnail Mismatch: Native '{isThumbnails}', Managed '{managedComponent.IsThumbnail}'");
                    if (isSharedAnno != managedComponent.IsSharedAnno) errorMessages.Add($"[Component {i}] IsSharedAnno Mismatch: Native '{isSharedAnno}', Managed '{managedComponent.IsSharedAnno}'");
                }

                Assert.True(errorMessages.Count == 0, $"DIRM Property Mismatches Doc:{index}.\n" + string.Join("\n", errorMessages));
            }
        }

        [Fact]
        public void DirmChunkParsingTestIndex23()
        {
            DirmChunkParsingTheory(23);
        }

        private static string GetNativeString(IntPtr strPtr)
        {
            if (strPtr == IntPtr.Zero) return "";
            try
            {
                return Marshal.PtrToStringUTF8(strPtr) ?? "";
            }
            finally
            {
                DjvuMarshal.FreeHGlobal(strPtr);
            }
        }
    }
}
