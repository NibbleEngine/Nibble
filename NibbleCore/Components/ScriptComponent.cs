using System;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class ScriptComponent : Component
    {
        public string SourcePath = "";
        public ulong ScriptHash = 0x0;

        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
        
        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Path");
            writer.WriteValue(SourcePath);
            writer.WriteEndObject();
        }

        public static ScriptComponent Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            ScriptComponent sc = new();
            sc.SourcePath = token.Value<string>("Path");
            sc.ScriptHash = NbHasher.Hash(sc.SourcePath);
            return sc;
        }
    }
}
