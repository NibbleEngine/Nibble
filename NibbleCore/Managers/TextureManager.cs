using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Managers;

namespace NbCore
{
    public class TextureManager : EntityManager<NbTexture>
    {
        Dictionary<string, NbTexture> TextureMap = new();
        
        public TextureManager()
        {

        }

        public override void CleanUp()
        {
            DeleteTextures();
            base.CleanUp();
        }

        public void DeleteTextures()
        {
            foreach (NbTexture p in Entities)
                p.Dispose();
        }

        public bool HasTexture(string name)
        {
            return TextureMap.ContainsKey(name);
        }

        public bool AddTexture(NbTexture t)
        {
            if (!HasTexture(t.Path))
            {
                TextureMap[t.Path] = t;
                return Add(t);
            }
            else
                return false;
            
        }

        public NbTexture Get(string name)
        {
            return TextureMap[name];
        }


    }
}
