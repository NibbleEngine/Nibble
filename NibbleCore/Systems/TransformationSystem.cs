using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Common;
using NbCore;

namespace NbCore.Systems
{
    public unsafe class TransformationSystem : EngineSystem
    {
        private readonly List<NbTransformData> _Data;
        private readonly Dictionary<ulong, TransformController> EntityControllerMap;
        private readonly Dictionary<ulong, TransformComponent> EntityDataMap;
        private readonly Queue<Entity> UpdatedEntities; //Entities to update on demand
        private readonly List<Entity> DynamicEntities; //Dynamic entities that need to be constantly updated
        
        //Properties
        public static double updateInterval = 1.0 / 60; //Default Update interval of 60hz

        public TransformationSystem (): base(EngineSystemEnum.TRANSFORMATION_SYSTEM)
        {
            EntityControllerMap = new();
            EntityDataMap = new();
            DynamicEntities = new();
            UpdatedEntities = new Queue<Entity> ();
            _Data = new();
        }

        public void SetInterval(double interval)
        {
            updateInterval = interval;
        }

        public void RegisterEntity(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.ID))
            {
                Log("Entity Already Registered", LogVerbosityLevel.INFO);
                return;
            }

            if (!e.HasComponent<TransformComponent>())
            {
                Log(string.Format("Entity {0} should have a transform component", e.ID), LogVerbosityLevel.INFO);
                return;
            }
            
            TransformComponent tc = e.GetComponent<TransformComponent>();
            
            //Insert to Maps
            EntityDataMap[e.ID] = tc;
            _Data.Add(tc.Data); //Add ref to TransformData list
            
            if (tc.IsControllable)
            {
                EntityControllerMap[e.ID] = new TransformController(tc.Data);
                AddDynamicEntity(e);
            }
    
        }

        public void DeleteEntity(Entity e)
        {
            if (!e.HasComponent<TransformComponent>())
            {
                Log($"Entity {e.ID} should has no transform component. Nothing to do...", LogVerbosityLevel.INFO);
                return;
            }

            if (!EntityDataMap.ContainsKey(e.ID))
            {
                Log("Entity not Registered", LogVerbosityLevel.INFO);
                return;
            }

            TransformComponent tc = e.GetComponent<TransformComponent>();

            //Remove from maps
            EntityDataMap.Remove(e.ID);
            _Data.Remove(tc.Data);

            if (tc.IsControllable)
            {
                EntityControllerMap.Remove(e.ID);
                RemoveDynamicEntity(e);
            }
                
        }

        public override void OnRenderUpdate(double dt)
        {
            //Dynamic entities that have transform controllers should be updated per frame

            //Calculate Current Transform states
            foreach (Entity e in DynamicEntities)
            {
                TransformController tc = GetEntityTransformController(e);
                tc.Update(dt); //Recalculate state
            }

        }

        public override void OnFrameUpdate(double dt)
        {
            foreach (Entity e in DynamicEntities)
            {
                TransformController tc = GetEntityTransformController(e);
                tc.Advance();
            }

            //Update On Demand Entities
            while (UpdatedEntities.Count > 0)
            {
                Entity e = UpdatedEntities.Dequeue();

                //Immediately calculate new transforms
                NbTransformData td = GetEntityTransformData(e);
                td.RecalculateTransformMatrices();
            }
        }

        public void AddDynamicEntity(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.ID) && !DynamicEntities.Contains(e))
                DynamicEntities.Add(e);
        }

        public void RequestEntityUpdate(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.ID))
                UpdatedEntities.Enqueue(e);
            else
                Log("Entity not registered to the transformation system", 
                    LogVerbosityLevel.WARNING);
        }

        public void RemoveDynamicEntity(Entity e)
        {
            DynamicEntities.Remove(e);
        }

        public TransformController GetEntityTransformController(Entity e)
        {
            return EntityControllerMap[e.ID];
        }

        public static void AddTransformComponentToEntity(Entity e)
        {
            NbTransformData td = new();
            TransformComponent tc = new(td);
            e.AddComponent<TransformComponent>(tc);
        }

        public static void SetEntityLocation(Entity e, NbVector3 loc)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            td.localTranslation = loc;
        }

        public static void SetEntityRotation(Entity e, NbQuaternion rot)
        {
            NbTransformData td = (e.GetComponent<TransformComponent>()).Data;
            td.localRotation = rot;
        }

        public static void SetEntityScale(Entity e, NbVector3 scale)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            td.localScale = scale;
        }

        public static NbMatrix4 GetEntityLocalMat(Entity e)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            return td.LocalTransformMat;
        }

        public static NbQuaternion GetEntityLocalRotation(Entity e)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            return td.localRotation;
        }

        public static NbMatrix4 GetEntityWorldMat(Entity e)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            return td.WorldTransformMat;
        }

        public static NbVector4 GetEntityWorldPosition(Entity e)
        {
            NbTransformData td = e.GetComponent<TransformComponent>().Data;
            return td.WorldPosition;
        }

        public static NbTransformData GetEntityTransformData(Entity e)
        {
            return e.GetComponent<TransformComponent>().Data;
        }

        public override void CleanUp()
        {
            _Data.Clear();
            EntityControllerMap.Clear();
            EntityDataMap.Clear();
            UpdatedEntities.Clear();
            DynamicEntities.Clear();
        }        
    }
}
