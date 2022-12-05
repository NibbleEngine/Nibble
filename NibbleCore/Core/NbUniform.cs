using NbCore.IO;
using NbCore.Math;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.CodeDom;
using System.Runtime.CompilerServices;

namespace NbCore
{
    //Not needed for now
    public class DataRef<T>
    {
        public T Data;

		public DataRef()
		{
		}

		public DataRef(T ob)
        {
            Data = ob;
		}
    }
    
    public class NbUniform
    {
        public string Name = "Uniform"; //Uniform custom name
        public NbUniformType Type;
        public string ShaderBinding;
        public int ShaderLocation;
        public NbVector4 Values;

        public NbUniform()
        {
            
        }

        public NbUniform(NbUniformType type, string name = "", float x = 0.0f, float y = 0.0f, float z = 0.0f, float w = 0.0f)
        {
            Name = name;
            ShaderBinding = "";
            ShaderLocation = -1;
            Type = type;
            Values = new(x, y, z, w);
        }

        public bool HasInitializedState
        {
            get
            {
                bool status = true;
                status &= ShaderLocation != -1;
                status &= ShaderBinding != "";
                return status;
            }
        }

        public void SetState(string binding, int loc)
        {
            ShaderBinding = binding;
            ShaderLocation = loc;
        }
        
        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            writer.WritePropertyName("Type");
            writer.WriteValue(Type);
            writer.WritePropertyName("ShaderBinding");
            writer.WriteValue(ShaderBinding);
            writer.WritePropertyName("ShaderLocation");
            writer.WriteValue(ShaderLocation);
            writer.WritePropertyName("Values");
            NbSerializer.Serialize(Values, writer);
            writer.WriteEndObject();
        }

        public static NbUniform Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            return new()
            {
                Name = (string)token.Value<Newtonsoft.Json.Linq.JToken>("Name"),
                Type = (NbUniformType)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Type")),
                ShaderBinding = (string)token.Value<Newtonsoft.Json.Linq.JToken>("ShaderBinding"),
                ShaderLocation = (int)token.Value<Newtonsoft.Json.Linq.JToken>("ShaderLocation"),
                Values = (NbVector4)IO.NbDeserializer.Deserialize(token),
            };
        }

        

    }

}