using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;

namespace NbCore.Systems
{
    public class EntityRegistrySystem : EngineSystem
    {
        private long itemCounter = 0;
        private ulong NextID = 1;
        private Dictionary<ulong, Entity> EntityMap = new();
        private Dictionary<EntityType, List<Entity>> EntityTypeList = new();
        public EntityRegistrySystem() : base(EngineSystemEnum.REGISTRY_SYSTEM)
        {
            //Initialize EntityTypeList
            foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
            {
                EntityTypeList[t] = new List<Entity>();
            }
        }

        public Entity GetEntity(ulong ID)
        {
            return EntityMap[ID];
        }

        public Entity GetEntity(EntityType type, ulong ID)
        {
            return EntityTypeList[type].Find(x=> x.ID == ID);
        }

        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return EntityTypeList[type];
        }
        
        public bool RegisterEntity(Entity e)
        {
            if (e.ID != (0xFFFFFFFF))
            {
                Log($"Entity of type {e.Type} has no default ID, probably already registered", LogVerbosityLevel.INFO);
                return false;
            }

            if (IsRegistered(e))
            {
                Log($"Entity already registered. ID: {e.ID}", LogVerbosityLevel.DEBUG);
                return false;
            }
            
            e.ID = NextID++;
            e.Initialized = true;
            EntityMap[e.ID] = e;
            EntityTypeList[e.Type].Add(e);

            //Explicitly handle ScenenodeTypes
            switch (e.Type)
            {
                case EntityType.SceneNodeLight:
                case EntityType.SceneNodeJoint:
                case EntityType.SceneNodeMesh:
                case EntityType.SceneNodeModel:
                    EntityTypeList[EntityType.SceneNode].Add(e);
                    break;
            }


            Log($"Entity of Type {e.Type} was successfully registered. ID: {e.ID}", LogVerbosityLevel.DEBUG);

            return true;
        }

        public bool DeleteEntity(Entity e)
        {
            if (!IsRegistered(e))
            {
                Log("Entity not registered. Nothing to do", LogVerbosityLevel.INFO);
                return false;
            }
            EntityMap.Remove(e.ID);
            EntityTypeList[e.Type].Remove(e);

            //Explicitly handle ScenenodeTypes
            switch (e.Type)
            {
                case EntityType.SceneNodeLight:
                case EntityType.SceneNodeJoint:
                case EntityType.SceneNodeMesh:
                case EntityType.SceneNodeModel:
                    EntityTypeList[EntityType.SceneNode].Remove(e);
                    break;
            }

            return true;
        }

        //This clears the registry, other systems are responsible for disposing all generated components
        public void Clear()
        {
            itemCounter = 0;
            EntityMap.Clear();
            foreach (EntityType t in Enum.GetValues(typeof(EntityType)))
                EntityTypeList[t].Clear();
        }

        public bool IsRegistered(Entity e)
        {
            
            if (EntityMap.ContainsKey(e.ID))
                return true;
            return false;
        }

        
        public override void OnRenderUpdate(double dt)
        {
            //Won't be used
            throw new NotImplementedException();
        }

        public override void OnFrameUpdate(double dt)
        {
            //Won't be used
            throw new NotImplementedException();
        }

        public override void CleanUp()
        {
            EntityMap.Clear();
            EntityTypeList.Clear();
        }
    }
}
