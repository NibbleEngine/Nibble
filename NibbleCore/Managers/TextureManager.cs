using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Managers;
using NbCore.Platform.Graphics;

namespace NbCore
{
    public class TextureManager : ObjectManager<string, NbTexture>
    {
        public TextureManager()
        {

        }

        public override bool Add(string path, NbTexture tex)
        {
            if (tex.GpuID < 0)
            {
                //Upload texture to the GPU
                GraphicsAPI.GenerateTexture(tex);
                GraphicsAPI.UploadTexture(tex);
                GraphicsAPI.setupTextureParameters(tex, tex.Data.WrapMode, tex.Data.MagFilter, tex.Data.MinFilter, 8.0f);

                if (!tex.KeepDataBufferAfterUpload)
                    tex.Data.DataBuffer = null;
            }

            return base.Add(path, tex);
        }

        public void DeleteTextures()
        {
            foreach (NbTexture p in Objects)
                p.Dispose();
        }

        public override void CleanUp()
        {
            base.CleanUp();
        }


    }
}
