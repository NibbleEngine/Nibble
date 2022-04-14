using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    public class GeometryManager :EntityManager<Entity>
    {
        public Dictionary<string, GeomObject> Geoms = new();
        
        public GeometryManager()
        {

        }

        #region GeomObjects
        public bool AddGeom(GeomObject o)
        {
            if (base.Add(o))
            {
                Geoms[o.Name] = o;
                return true;
            }
            return false;
        }

        public bool HasGeom(string name)
        {
            return Geoms.ContainsKey(name);
        }

        public GeomObject GetGeom(string name)
        {
            return Geoms[name];
        }

        #endregion

        public new void CleanUp()
        {
            Geoms.Clear();
            
            //I hope that the correct Dispose methods will be called and not just the default
            base.CleanUp();
        }
    }
}
