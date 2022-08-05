using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    sealed public class MaterialManager : EntityManager<NbMaterial>
    {
        public readonly Dictionary<string, NbMaterial> MaterialNameMap = new();

        public bool AddMaterial(NbMaterial mat)
        {
            if (Add(mat))
            {
                MaterialNameMap[mat.Name] = mat;
                return true;
            }
            
            return false;
        }

        public NbMaterial GetByName(string name)
        {
            if (MaterialNameMap.ContainsKey(name))
                return MaterialNameMap[name];
            return null;
        }

    }
}
