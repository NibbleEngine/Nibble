using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Managers;

namespace NbCore
{
    public class TextureManager : EntityManager<NbTexture>
    {
        public Dictionary<string, NbTexture> TextureMap = new();
        
        public TextureManager()
        {

        }

        public void DeleteTextures()
        {
            foreach (NbTexture p in TextureMap.Values)
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

        public override void CleanUp()
        {
            TextureMap.Clear();
            base.CleanUp();
        }


    }
}
