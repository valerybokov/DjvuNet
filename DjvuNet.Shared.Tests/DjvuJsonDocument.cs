using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DjvuNet.Tests
{

    public partial class DjvuJsonDocument
    {
        public class ChunkBase
        {
            [JsonPropertyName("ID")]
            public string ID { get; set; }

            [JsonPropertyName("Offset")]
            public int Offset { get; set; }

            [JsonPropertyName("Size")]
            public int Size { get; set; }
        }

        public class Info : ChunkBase
        {
            [JsonPropertyName("Width")]
            public int Width { get; set; }

            [JsonPropertyName("Height")]
            public int Height { get; set; }

            [JsonPropertyName("Version")]
            public int Version { get; set; }

            [JsonPropertyName("Dpi")]
            public int Dpi { get; set; }

            [JsonPropertyName("Gamma")]
            public double Gamma { get; set; }

            [JsonPropertyName("Orientation")]
            public int Orientation { get; set; }
        }

        public class Chunk : ChunkBase
        {

            [JsonPropertyName("Description")]
            public string Description { get; set; }

            [JsonPropertyName("Width")]
            public int? Width { get; set; }

            [JsonPropertyName("Height")]
            public int? Height { get; set; }

            [JsonPropertyName("Version")]
            public double? Version { get; set; }

            [JsonPropertyName("Dpi")]
            public int? Dpi { get; set; }

            [JsonPropertyName("Gamma")]
            public double? Gamma { get; set; }

            [JsonPropertyName("Name")]
            public string Name { get; set; }

            [JsonPropertyName("Colors")]
            public int? Colors { get; set; }

            [JsonPropertyName("Slices")]
            public int? Slices { get; set; }

            [JsonPropertyName("Color")]
            public string Color { get; set; }
        }
    }

    public partial class DjvuJsonDocument
    {
        public class RootChild : ChunkBase
        {

            [JsonPropertyName("Description")]
            public string Description { get; set; }

            [JsonPropertyName("DocumentType")]
            public string DocumentType { get; set; }

            [JsonPropertyName("FileCount")]
            public int FileCount { get; set; }

            [JsonPropertyName("PageCount")]
            public int PageCount { get; set; }

            [JsonPropertyName("Children")]
            public Chunk[] Children { get; set; }
        }
    }

    public partial class DjvuJsonDocument
    {
        public class Document
        {
            private RootChild[] _Pages;
            private RootChild[] _Files;
            private RootChild[] _Includes;
            private RootChild[] _Thumbnails;
            private RootChild _Dirm;
            private RootChild _Navm;

            [JsonPropertyName("ID")]
            public string ID { get; set; }

            [JsonPropertyName("Size")]
            public int Size { get; set; }

            [JsonPropertyName("Children")]
            public RootChild[] Children { get; set; }

            [JsonIgnore]
            public RootChild Navm
            {
                get
                {
                    if (_Navm != null)
                        return _Navm;
                    else
                    {
                        _Navm = Children.Where((x) => x.ID == "NAVM").FirstOrDefault();
                        return _Navm;
                    }
                }
            }

            [JsonIgnore]
            public RootChild Dirm
            {
                get
                {
                    if (_Dirm != null)
                        return _Dirm;
                    else
                    {
                        _Dirm = Children.Where((x) => x.ID == "DIRM").FirstOrDefault();
                        return _Dirm;
                    }
                }
            }

            [JsonIgnore]
            public RootChild[] Pages
            {
                get
                {
                    if (_Pages != null)
                        return _Pages;
                    else
                    {
                        _Pages = Children.Where((x) => x.ID == "FORM:DJVU").ToArray();
                        return _Pages;
                    }
                }
            }

            [JsonIgnore]
            public RootChild[] Files
            {
                get
                {
                    if (_Files != null)
                        return _Files;
                    else
                    {
                        _Files = Children.Where((x) => x.ID != "DIRM" && x.ID != "NAVM").ToArray();
                        return _Files;
                    }
                }
            }

            public RootChild[] Includes
            {
                get
                {
                    if (_Includes != null)
                        return _Includes;
                    else
                    {
                        _Includes = Children.Where((x) => x.ID == "FORM:DJVI").ToArray();
                        return _Includes;
                    }
                }
            }

            public RootChild[] Thumbnails
            {
                get
                {
                    if (_Thumbnails != null)
                        return _Thumbnails;
                    else
                    {
                        _Thumbnails = Children.Where((x) => x.ID == "FORM:THUM").ToArray();
                        return _Thumbnails;
                    }
                }
            }
        }

    }

    public partial class DjvuJsonDocument
    {

        [JsonPropertyName("DjvuData")]
        public Document Data { get; set; }

        [JsonIgnore]
        public string DocumentFile { get; set; }

        public override string ToString()
        {
            return String.IsNullOrWhiteSpace(DocumentFile) ? base.ToString() : DocumentFile ;
        }

        public override bool Equals(object obj)
        {
            DjvuJsonDocument doc = obj as DjvuJsonDocument;
            if (doc == null)
                return false;

            if (!String.IsNullOrWhiteSpace(DocumentFile))
                return DocumentFile == doc.DocumentFile;
            else
                return base.Equals(doc); 
        }

        public override int GetHashCode()
        {
            if (!String.IsNullOrWhiteSpace(DocumentFile))
                return DocumentFile.GetHashCode();

            return base.GetHashCode();
        }
    }

}
