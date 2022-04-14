using System.Collections.Generic;

namespace NbCore.Managers
{
    public class ObjectManager<TKey, TValue>
    {
        public int ObjectCount = 0;
        public List<TValue> Objects = new();
        public Dictionary<TKey, TValue> ObjectMap = new();

        //All objects added to the manager are Entities, I can disable sanity checks
        public virtual bool Contains(TKey id)
        {
            return ObjectMap.ContainsKey(id);
        }

        public virtual bool Contains(TValue item)
        {
            return Objects.Contains(item);
        }

        public virtual bool Add(TKey id, TValue item)
        {
            Common.Callbacks.Assert(id != null, "NULL Ids are not allowed");
            if (!Contains(id))
            {
                Objects.Add(item);
                ObjectMap[id] = item;
                ObjectCount++;
                return true;
            }
            return false;
        }

        public virtual bool Remove(TKey id)
        {
            if (Contains(id))
            {
                TValue item = Get(id);
                Objects.Remove(item);
                ObjectMap.Remove(id);
                ObjectCount--;
                return true;
            }
            return false;
        }

        public TValue Get(TKey id)
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
