using System;
using System.IO;
using NbCore.Platform.Graphics;

namespace NbCore
{
    public class NbTextureData
    {
        public NbTextureTarget target;
        public NbTextureInternalFormat pif;
        public NbTextureFilter MinFilter = NbTextureFilter.Linear;
        public NbTextureFilter MagFilter = NbTextureFilter.Linear;
        public NbTextureWrapMode WrapMode = NbTextureWrapMode.ClampToEdge;
        public byte[] Data;
        public int Width;
        public int Height;
        public int Depth;
        public int Faces;
        public int MipMapCount;
        
        public virtual MemoryStream Export()
        {
            throw new NotImplementedException();
        }

    }
}
