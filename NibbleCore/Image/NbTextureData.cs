using Newtonsoft.Json;
using System;
using System.IO;

namespace NbCore
{

    [NbSerializable]
    public class NbTextureData
    {
        public NbTextureTarget target;
        public NbTextureInternalFormat pif;
        public NbTextureFilter MinFilter = NbTextureFilter.Linear;
        public NbTextureFilter MagFilter = NbTextureFilter.Linear;
        public NbTextureWrapMode WrapMode = NbTextureWrapMode.ClampToEdge;
        public byte[] DataBuffer;
        public int Width;
        public int Height;
        public int Depth;
        public int ArraySize;
        public int MipMapCount;

        public virtual MemoryStream Export()
        {
            throw new NotImplementedException();
        }

        public virtual void Serialize(JsonWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
