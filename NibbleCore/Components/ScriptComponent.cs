using System;
using NbCore.Common;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class ScriptComponent : Component
    {
        [JsonIgnore]
        public NbScript Script; //Private Implementation
        public NbScriptAsset Asset; //Reference for the script code
        
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
            writer.WritePropertyName("ScriptHash");
            writer.WriteValue(Asset.Hash);
            writer.WriteEndObject();
        }

        public static ScriptComponent Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            ScriptComponent sc = new();
            ulong script_hash = token.Value<ulong>("ScriptHash");

            //Load Script Asset from the engine
            sc.Asset = RenderState.engineRef.GetScriptAssetByHash(script_hash);

            //NOTE: Script Compilation should happen at the end of the serialization
            
            return sc;
        }
    }
}
