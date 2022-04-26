using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class NbSampler
    {
        public string Name = "";
        private NbTexture Tex = null;
        public bool IsCube = false;
        public bool IsSRGB = true;
        public bool UseCompression = false;
        public bool UseMipMaps = false;
        public NbSamplerState State;
        public bool isProcGen = false; //TODO : to be removed once we are done with the stupid proc gen texture parsing

        //Override Properties
        public NbSampler()
        {
            //Init State
            State.SamplerID = -1;
            State.ShaderBinding = "";
            State.ShaderLocation = -1;
            State.Texture = null;
        }

        public NbSampler Clone()
        {
            NbSampler newsampler = new()
            {
                Name = Name,
                IsSRGB = IsSRGB,
                IsCube = IsCube,
                UseCompression = UseCompression,
                UseMipMaps = UseMipMaps,
                Tex = Tex,
                State = State
            };

            return newsampler;
        }

        public void SetTexture(NbTexture tex)
        {
            if (Tex == null || Tex != tex)
            {
                Tex = tex;
                State.Texture = tex;
                tex.Refs++;
            }
        }

        public NbTexture GetTexture()
        {
            return Tex;
        }


        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            writer.WritePropertyName("Path");
            writer.WriteValue(Tex != null ? Tex.Path : "");
            writer.WritePropertyName("State");
            IO.NbSerializer.Serialize(State, writer);
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
            var test = token.Value<Newtonsoft.Json.Linq.JToken>("State");
            NbSampler sam = new NbSampler()
            {
                Name = token.Value<string>("Name"),
                IsCube = token.Value<bool>("IsCube"),
                IsSRGB = token.Value<bool>("IsSRGB"),
                State = (NbSamplerState) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("State")),
                UseCompression = token.Value<bool>("UseCompression"),
                UseMipMaps = token.Value<bool>("UseMipMaps")
            };

            //Try to load texture
            string tex_path = token.Value<string>("Path");
            if (tex_path != "")
            {
                NbTexture tex = Common.RenderState.engineRef.GetTexture(tex_path);
                if (tex == null)
                    tex = Common.RenderState.engineRef.CreateTexture(tex_path, false);
                sam.SetTexture(tex);
            }
            
            return sam;
        }
        
    }

    
}