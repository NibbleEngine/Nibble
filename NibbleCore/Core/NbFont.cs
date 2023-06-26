using Newtonsoft.Json;
using System;

namespace NbCore
{
    [NbSerializable]
    public class NbFont
    {
        public string fontPath;
        public string atlasPath;
        
        public NbFont(string font, string atlas)
        {
            fontPath = font;
            atlasPath = atlas;
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("FontPath");
            writer.WriteValue(fontPath);
            writer.WritePropertyName("AtlasPath");
            writer.WriteValue(atlasPath);
            writer.WriteEndObject();
        }

        public static NbFont Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbFont fnt = new(token.Value<string>("FontPath"),
                              token.Value<string>("AtlasPath"));
            return fnt;
        }


    }
}
