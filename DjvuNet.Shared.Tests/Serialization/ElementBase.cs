using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace DjvuNet.Serialization
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "ID", IgnoreUnrecognizedTypeDiscriminators = true)]
    [JsonDerivedType(typeof(DjvmForm), "FORM:DJVM")]
    [JsonDerivedType(typeof(DjvuForm), "FORM:DJVU")]
    [JsonDerivedType(typeof(ThumForm), "FORM:THUM")]
    [JsonDerivedType(typeof(PM44Form), "FORM:PM44")]
    [JsonDerivedType(typeof(BM44Form), "FORM:BM44")]
    public class ElementBase : NodeBase
    {
        public NodeBase[] Children { get; set; }
    }
}
