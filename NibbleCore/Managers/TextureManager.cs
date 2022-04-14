using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Managers;

namespace NbCore
{
    public class TextureManager : ObjectManager<string, NbTexture>
    {
        public TextureManager()
        {

        }

        public bool AddTexture(NbTexture tex)
        {
            return Add(tex.Path, tex);
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
