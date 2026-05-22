using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using DjvuNet;
using Xunit;

namespace DjvuNet.Tests
{
    public static partial class Util
    {
        public static DjvuDocument GetTestDocument(int index, out int pageCount)
        {
            pageCount = 0;

            if (index < 1)
                return null;

            pageCount = GetTestDocumentPageCount(index);
            return new DjvuDocument(GetTestFilePath(index));
        }

        public static void VerifyDjvuDocumentCtor(int pageCount, DjvuDocument document)
        {
            VerifyDjvuDocument(pageCount, document);
            Assert.False(document.IsDisposed);
            if (pageCount > 1)
                Assert.NotNull(document.Directory);
            Assert.NotNull(document.ActivePage);
            Assert.NotNull(document.RootForm);
            Assert.NotNull(document.Navigation);
        }

        public static void VerifyDjvuDocument(int pageCount, DjvuDocument document)
        {
            Assert.NotNull(document.FirstPage);
            Assert.NotNull(document.LastPage);
            if (pageCount > 0)
                Assert.Equal<int>(pageCount, document.Pages.Count);
        }

        public static IEnumerable<object[]> AllTestDocumentIndices
        {
            get
            {
                List<object[]> retVal = new List<object[]>();
                for (int i = 1; i <= 77; i++)
                {
                    retVal.Add(new object[] { i });
                }
                return retVal;
            }
        }

        /// <summary>
        /// Provides an enumeration of test document indices (1-77) with subtracted set of documents 
        /// which do not contain foreground data.
        /// 
        /// In the DjVu architecture, the Foreground Layer typically consists of two distinct components:
        /// 1. The Mask: Always encoded using the JB2 algorithm (Sjbz/FGbz chunks). This defines the bitonal shapes (e.g., text).
        /// 2. The Colors: Encoded using the IW44 Wavelet algorithm (FG44 chunks). This defines the color applied to the mask.
        /// 
        /// This data source can be safely used for testing both JB2 shape dictionaries and Foreground IW44 wavelets.
        /// </summary>
        public static IEnumerable<object[]> ForegroundImageSourceDocs
        {
            get
            {
                List<object[]> retVal = new List<object[]>();

                for (int i = 1; i <= 77; i++)
                {
                    // DjvuView does not use foreground/mask images for the following docs
                    if (i == 35 || i == 42 || i == 43 || i == 44 || i == 47 || i == 55 || i == 60 || i == 63 || i == 66 || i == 67 || i == 71)
                    {
                        continue;
                    }
                    retVal.Add(new object[] { i });
                }
                return retVal;
            }
        }
    }
}
