using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    sealed public class MaterialManager : EntityManager<MeshMaterial>
    {
        public readonly Dictionary<string, MeshMaterial> MaterialNameMap = new();
        
        public bool AddMaterial(MeshMaterial mat) {
            
            if (Add(mat))
            {
                GUIDComponent gc = mat.GetComponent<GUIDComponent>() as GUIDComponent;
                MaterialNameMap[mat.Name] = mat;
                return true;
            }
            return false;
        }

        public MeshMaterial GetByName(string name)
        {
            if (MaterialNameMap.ContainsKey(name))
                return MaterialNameMap[name];
            return null;
        }

    }
}
