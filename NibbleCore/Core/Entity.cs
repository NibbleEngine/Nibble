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

        //Private
        private readonly Dictionary<Type, Component> _componentMap = new();

        [NbSerializable]
        public readonly List<Component> Components = new();

        //Disposable Stuff
        private bool disposedValue;

        public Entity(EntityType typ)
        {
            Type = typ;
            
            //Add GUID Component by default to all entities
            GUIDComponent c = new GUIDComponent();
            AddComponent<GUIDComponent>(c);
        }

        public long GetID()
        {
            GUIDComponent gc = GetComponent<GUIDComponent>() as GUIDComponent;
            return gc.ID;
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

        public void AddComponent(Type t, Component comp)
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
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + GetType().ToString());
        }
#endif



    }

}
