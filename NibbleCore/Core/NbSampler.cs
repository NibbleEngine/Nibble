

namespace NbCore
{
    public class NbSampler
    {
        [NbSerializable]
        public string Name = "";
        [NbSerializable]
        public string Map = "";
        private NbTexture Tex = null;
        [NbSerializable]
        public bool IsCube = false;
        [NbSerializable]
        public bool IsSRGB = true;
        [NbSerializable]
        public bool UseCompression = false;
        [NbSerializable]
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
            State.TextureID = -1;
        }

        public NbSampler Clone()
        {
            NbSampler newsampler = new()
            {
                Name = Name,
                Map = Map,
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
                State.TextureID = tex.texID;
                State.Target = tex.Data.target;
                tex.Refs++;
            }
        }

        public NbTexture GetTexture()
        {
            return Tex;
        }
        
    }

    
}