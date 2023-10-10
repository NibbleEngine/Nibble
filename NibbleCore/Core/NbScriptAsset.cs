using NbCore.Common;
using Newtonsoft.Json;
using System;

namespace NbCore
{
    [NbSerializable]
    public class NbScriptAsset : Entity
    {
        public ulong Hash;
        
        public NbScriptAsset(string path) : base(EntityType.Script)
        {
            Path = path;
            Hash = NbHasher.Hash(path);
        }

        public override Entity Clone()
        {
            throw new NotImplementedException();
        }
        
        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Path");
            writer.WriteValue(Path);
            writer.WriteEndObject();
        }

        public static NbScriptAsset Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string script_path = token.Value<string>("Path");
            return new NbScriptAsset(script_path);
        }

    }
}
