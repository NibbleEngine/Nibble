using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace NbCore
{
    [NbSerializable]
    class NbConfig<T> where T : Entity
    {
        public T Data;

        public NbConfig(T data)
        {
            Data = data;
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("Path");
            writer.WriteValue(Data.Path);
            
            writer.WriteEndObject();
        }

        public static T Deserialize(JToken token)
        {
            string data_path = (string) token["Path"];
            if (!File.Exists(data_path))
                return null;
            
            JObject job = IO.NbDeserializer.DeserializeToToken(data_path);
            T ob = (T) IO.NbDeserializer.Deserialize(job);
            ob.Path = data_path; //Set path for entity
            return ob;
        }
    }
}
