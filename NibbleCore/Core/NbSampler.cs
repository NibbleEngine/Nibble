using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class NbSampler
    {
        public string Name = "";
        public int SamplerID = -1;
        public bool IsCube = false;
        public bool IsSRGB = true;
        public bool UseCompression = false;
        public bool UseMipMaps = false;
        public string ShaderBinding;
        public int ShaderLocation;
        public NbTexture Texture;
        public bool isProcGen = false; //TODO : to be removed once we are done with the stupid proc gen texture parsing

        //Override Properties
        public NbSampler()
        {
            //Init State
            Name = "Sampler";
            SamplerID = -1;
            ShaderBinding = "";
            ShaderLocation = -1;
            Texture = null;
        }

        public NbSampler Clone()
        {
            NbSampler newsampler = new()
            {
                Name = Name,
                IsSRGB = IsSRGB,
                IsCube = IsCube,
                UseCompression = UseCompression,
                UseMipMaps = UseMipMaps
            };

            return newsampler;
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            writer.WritePropertyName("SamplerID");
            writer.WriteValue(SamplerID);
            writer.WritePropertyName("ShaderBinding");
            writer.WriteValue(ShaderBinding);
            writer.WritePropertyName("ShaderLocation");
            writer.WriteValue(ShaderLocation);
            writer.WritePropertyName("Texture");
            writer.WriteValue(Texture.Path);
            writer.WritePropertyName("IsSRGB");
            writer.WriteValue(IsSRGB);
            writer.WritePropertyName("IsCube");
            writer.WriteValue(IsCube);
            writer.WritePropertyName("UseCompression");
            writer.WriteValue(UseCompression);
            writer.WritePropertyName("UseMipMaps");
            writer.WriteValue(UseMipMaps);
            writer.WriteEndObject();
        }

        public static NbSampler Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbSampler sam = new NbSampler()
            {
                Name = token.Value<string>("Name"),
                SamplerID = token.Value<int>("SamplerID"),
                IsCube = token.Value<bool>("IsCube"),
                IsSRGB = token.Value<bool>("IsSRGB"),
                ShaderBinding = token.Value<string>("ShaderBinding"),
                ShaderLocation = token.Value<int>("ShaderLocation"),    
                Texture = Common.RenderState.engineRef.GetTexture(token.Value<string>("Texture")),
                UseCompression = token.Value<bool>("UseCompression"),
                UseMipMaps = token.Value<bool>("UseMipMaps")
            };

            return sam;
        }
        
    }

    
}