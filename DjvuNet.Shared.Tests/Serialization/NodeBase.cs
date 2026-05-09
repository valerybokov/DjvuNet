using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace DjvuNet.Serialization
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "ID", IgnoreUnrecognizedTypeDiscriminators = false)]
    [JsonDerivedType(typeof(Anta), "ANTa")]
    [JsonDerivedType(typeof(Antz), "ANTz")]
    [JsonDerivedType(typeof(BG44), "BG44")]
    [JsonDerivedType(typeof(BM44), "BM44")]
    [JsonDerivedType(typeof(BM44Form), "FORM:BM44")]
    [JsonDerivedType(typeof(Cida), "CIDa")]
    [JsonDerivedType(typeof(Dirm), "DIRM")]
    [JsonDerivedType(typeof(Djbz), "Djbz")]
    [JsonDerivedType(typeof(DjviForm), "FORM:DJVI")]
    [JsonDerivedType(typeof(DjvmForm), "FORM:DJVM")]
    [JsonDerivedType(typeof(DjvuForm), "FORM:DJVU")]
    [JsonDerivedType(typeof(FG44), "FG44")]
    [JsonDerivedType(typeof(FGbz), "FGbz")]
    [JsonDerivedType(typeof(Incl), "INCL")]
    [JsonDerivedType(typeof(Info), "INFO")]
    [JsonDerivedType(typeof(Navm), "NAVM")]
    [JsonDerivedType(typeof(PM44), "PM44")]
    [JsonDerivedType(typeof(PM44Form), "FORM:PM44")]
    [JsonDerivedType(typeof(Sjbz), "Sjbz")]
    [JsonDerivedType(typeof(Smmr), "Smmr")]
    [JsonDerivedType(typeof(TH44), "TH44")]
    [JsonDerivedType(typeof(ThumForm), "FORM:THUM")]
    [JsonDerivedType(typeof(Txta), "TXTa")]
    [JsonDerivedType(typeof(Txtz), "TXTz")]
    [JsonDerivedType(typeof(Wmrm), "WMRM")]
    public class NodeBase
    {
        [JsonIgnore]
        public string ID
        {
            get
            {
                var attrs = Attribute.GetCustomAttributes(typeof(NodeBase), typeof(JsonDerivedTypeAttribute));
                foreach (JsonDerivedTypeAttribute attr in attrs)
                {
                    if (attr.DerivedType == this.GetType())
                        return attr.TypeDiscriminator?.ToString();
                }
                return null;
            }
            set { }
        }

        public int NodeOffset { get; set; }

        public int Size { get; set; }
    }

    public class NodeBaseDesc : NodeBase
    {
        public string Description { get; set; }
    }

    public class WaveletNodeBase : NodeBaseDesc
    {
        public int Slices { get; set; }

        public double Version { get; set; }

        public string Color { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
