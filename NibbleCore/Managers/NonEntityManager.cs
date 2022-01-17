using System.Collections.Generic;

namespace NbCore.Managers
{
    public class ObjectManager<T>
    {
        public int ObjectCount = 0;
        public List<T> Objects = new();
        public Dictionary<ulong, T> ObjectMap = new();

        //All objects added to the manager are Entities, I can disable sanity checks
        public virtual bool Contains(ulong id)
        {
            return ObjectMap.ContainsKey(id);
        }

        public virtual bool Contains(T item)
        {
            return Objects.Contains(item);
        }

        public virtual bool Add(ulong id, T item)
        {
            if (!Contains(id))
            {
                Objects.Add(item);
                ObjectMap[id] = item;
                ObjectCount++;
                return true;
            }
            return false;
        }

        public virtual bool Remove(ulong id)
        {
            if (Contains(id))
            {
                T item = Get(id);
                Objects.Remove(item);
                ObjectMap.Remove(id);
                ObjectCount--;
                return true;
            }
            return false;
        }

        public T Get(ulong id)
        {
            if (Contains(id))
                return ObjectMap[id];
            return default;
        }

        public virtual void CleanUp()
        {
            ObjectMap.Clear();
            Objects.Clear();
        }

    }
}
