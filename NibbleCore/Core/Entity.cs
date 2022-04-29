using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NbCore
{
    public enum EntityType
    {
        SceneNode,
        SceneNodeLight,
        SceneNodeJoint,
        SceneNodeMesh,
        SceneNodeLocator,
        SceneNodeModel,
        SceneNodeReference,
        SceneNodeCollision,
        SceneNodeEmmiter,
        MeshComponent,
        AnimationComponent,
        SceneComponent,
        LightComponent,
        Material,
        Texture,
        GeometryObject,
        Camera,
        Script,
        Asset,
        ShaderSource,
        ShaderConfig,
        Shader,
        Mesh,
        Animation,
        InstancedMesh,
        LightInstancedMesh
    }

    [NbSerializable]
    public abstract class Entity : IDisposable
    {
        //Public
        public ulong NameHash;
        public EntityType Type;
        public ulong ID = 0xFFFFFFFF;
        public long testID = 0; //TODO: remove that when we're happy with memory disposal
        public bool Initialized = false;
        public static long test_counter = 1;

        //Private
        private readonly Dictionary<Type, Component> _componentMap = new();

        [NbSerializable]
        public readonly List<Component> Components = new();

        //Disposable Stuff
        private bool disposedValue;

        public Entity(EntityType typ)
        {
            Type = typ;
            testID = test_counter++;
        }

        public void Init(ulong id)
        {
            if (Initialized)
                return;
            ID = id;
            Initialized = true;
        }

        public bool HasComponent(Component comp)
        {
            return Components.Contains(comp);
        }

        public bool HasComponent<T>()
        {
            return _componentMap.ContainsKey(typeof(T));
        }

        public bool HasComponent(Type t)
        {
            return _componentMap.ContainsKey(t);
        }

        public Component GetComponent<T>()
        {
            if (HasComponent<T>())
                return _componentMap[typeof(T)];
            return null;
        }

        public void AddComponent<T>(Component comp)
        {
            AddComponent(typeof(T), comp);
        }

        public virtual void AddComponent(Type t, Component comp)
        {
            if (HasComponent(t))
                return;
            _componentMap[t] = comp;
            Components.Add(comp);
            comp.RefEntity = this;
        }

        public void RemoveComponent<T>()
        {
            //Orphan Component will be collected from the GC
            if (HasComponent<T>())
                _componentMap.Remove(typeof(T));
        }

        public abstract Entity Clone();

        public void CopyFrom(Entity e)
        {
            //Copy data from e
            Type = e.Type;
            
            //Clone components
            
            foreach (KeyValuePair<Type, Component> kp in _componentMap)
            {
                Component c = kp.Value.Clone();
                AddComponent(c.GetType(), c);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    foreach (var kp in _componentMap){
                        _componentMap[kp.Key].Dispose();
                    }
                    Components.Clear();
                    _componentMap.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

#if DEBUG
        ~Entity()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            Common.Callbacks.Logger.Log(this, $"Undisposed lock. Object Type {GetType()}, Entity ID: {ID}", LogVerbosityLevel.WARNING);
        }
#endif


    }

}
