using System.Collections.Generic;
using NbCore.Common;

namespace NbCore.Managers
{
    public class EntityManager<T>
    {
        public int EntityCount = 0;
        public List<T> Entities = new();
        public Dictionary<long, T> EntityMap = new();

        private Entity _CheckEntity(T item)
        {
            Entity e = (Entity)(object)item;
            Callbacks.Assert(e != null, "Null entity, is item an entity??");
            return e;
        }

        //All objects added to the manager are Entities, I can disable sanity checks
        public virtual bool Contains(long id)
        {
            return EntityMap.ContainsKey(id);
        }

        private bool Contains(Entity e)
        {
            GUIDComponent gc = e.GetComponent<GUIDComponent>() as GUIDComponent;
            return Contains(gc.ID);
        }

        public virtual bool Contains(T item)
        {
            Entity e = (Entity)(object)item;
            if (e != null)
                return Contains(e);
            return false;
        }

        private bool _IsEntityRegistered(Entity e)
        {
            GUIDComponent gc = e.GetComponent<GUIDComponent>() as GUIDComponent;
            return gc.Initialized;
        }
            
        public virtual bool Add(T item)
        {
            //Check if Item is Entity
            Entity e = _CheckEntity(item);

            if (e == null)
                return false;

            if (!_IsEntityRegistered(e))
                return false;
            
            if (!Contains(e))
            {
                Entities.Add(item);
                EntityMap[(e.GetComponent<GUIDComponent>() as GUIDComponent).ID] = item;
                EntityCount++;
                return true;
            }
            return false;
        }

        public virtual bool Remove(T item)
        {
            if (Contains(item))
            {
                Entity e = (Entity) (object) item;
                Entities.Remove(item);
                EntityMap.Remove((e.GetComponent<GUIDComponent>() as GUIDComponent).ID);
                EntityCount--;
                return true;
            }
            return false;
        }

        public T Get(long id)
        {
            if (Contains(id))
                return EntityMap[id];
            return default;
        }

        public virtual void CleanUp()
        {
            EntityMap.Clear();
            Entities.Clear();
        }

    }
}
