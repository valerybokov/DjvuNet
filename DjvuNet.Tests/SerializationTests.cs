using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DjvuNet.Serialization;
using DjvuNet.Tests.Xunit;
using System.Text.Json;
using Xunit;

namespace DjvuNet.Tests
{
    public class SerializationTests
    {
        public static IEnumerable<object[]> DeserializeTestData
        {
            get
            {
                List<object[]> retVal = new List<object[]>();

                string[] files = Directory.GetFiles(
                    Util.ArtifactsJsonPath, "*.json");

                foreach(string f in files)
                {
                    retVal.Add(new object[]
                    {
                        Path.GetFileName(f),
                        f
                    });
                }

                return retVal;
            }
        }

        [Theory]
        [MemberData(nameof(DeserializeTestData))]
#if NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void Deserialize_Theory(string fileName, string filePath)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            string json = File.ReadAllText(filePath, new UTF8Encoding(false));
            DjvuDoc doc = JsonSerializer.Deserialize<DjvuNet.Serialization.DjvuDoc>(json, options);

            Assert.NotNull(doc);
            Assert.NotNull(doc.File);
            Assert.NotNull(doc.DjvuData);
        }

        [Theory]
        [InlineData(new object[] { 1, 2, 3})]
        [InlineData(new object[] { 3, 2, 5})]
        public void Test_Theory(int var1, int var2, int result)
        {
            Assert.Equal(result, var1 + var2);
        }
    }
}
