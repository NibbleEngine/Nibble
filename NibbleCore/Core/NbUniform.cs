using NbCore.Math;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class NbUniform
    {
        public string Name = "Uniform"; //Uniform custom name
        public NbVector4 Values = new(0.0f);
        public NbUniformState State;

        public NbUniform() { }

        public NbUniform(string name, NbVector4 values)
        {
            Name = name;
            Values = values;
            State = new()
            {
                ShaderBinding = "",
                ShaderLocation = -1,
                Type = NbUniformType.Float
            };
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            writer.WritePropertyName("State");
            IO.NbSerializer.Serialize(State, writer);
            writer.WritePropertyName("Values");
            IO.NbSerializer.Serialize(Values, writer);

            writer.WriteEndObject();
        }

        public static NbUniform Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbUniform uf = new NbUniform()
            {
                Name = token.Value<string>("Name"),
                State = (NbUniformState)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("State")),
                Values = (NbVector4)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Values"))
            };

            return uf;
        }

        

    }

}