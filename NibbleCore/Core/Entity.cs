using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NbCore.Common;
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
        LightInstancedMesh,
        Font
    }

    [NbSerializable]
    public abstract class Entity : IDisposable
    {
        //Public
        public ulong NameHash;
        public string Path = ""; //Entity path (if any)
        public EntityType Type;
        public ulong ID = 0xFFFFFFFF;
        public long testID = 0; //TODO: remove that when we're happy with memory disposal
        public bool Initialized = false;
        public static long test_counter = 1;

        //Private
        private readonly Dictionary<Type, List<Component>> _componentMap = new();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent(Component c)
        {
            return Components.Contains(c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent<T>()
        {
            return _componentMap.ContainsKey(typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetComponent<T>() where T: Component
        {
            return GetComponent<T>(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<Component> GetComponents<T>() where T : Component
        {
            return _componentMap[typeof(T)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetComponentCount<T>() where T : Component
        {
            return _componentMap[typeof(T)].Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetComponent<T>(int index) where T : Component
        {
            if (HasComponent<T>())
                return (T)_componentMap[typeof(T)][index];
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(Component comp)
        {
            AddComponent(typeof(T), comp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void AddComponent(Type t, Component comp)
        {
            if (HasComponent(comp))
            {
                Callbacks.Log(this, "Entity already contains component", LogVerbosityLevel.WARNING);
                return;
            }
            
            if (!_componentMap.ContainsKey(t))
            {
                _componentMap[t] = new();
            }
            
            _componentMap[t].Add(comp);
            Components.Add(comp);
            comp.RefEntity = this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveComponent<T>()
        {
            //Orphan Component will be collected from the GC
            if (HasComponent<T>())
            {
                foreach (Component comp in _componentMap[typeof(T)])
                {
                    comp.RefEntity = null;
                    Components.Remove(comp);
                }
                _componentMap[typeof(T)].Clear();
                _componentMap.Remove(typeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveComponent(Component comp)
        {
            //Orphan Component will be collected from the GC
            if (HasComponent(comp))
            {
                comp.RefEntity = null;
                Components.Remove(comp);
                _componentMap[comp.GetType()].Remove(comp);
            }
        }

        public abstract Entity Clone();

        public void CopyFrom(Entity e)
        {
            //Copy data from e
            Type = e.Type;
            
            //Clone components
            foreach (Component c in Components)
            {
                Component c_clone = c.Clone();
                AddComponent(c.GetType(), c_clone);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (Component c in Components)
                        c.Dispose();

                    //Clear structures
                    foreach (var kp in _componentMap)
                        _componentMap[kp.Key].Clear();
                    
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
            Callbacks.Log(this, $"Undisposed lock. Object Type {GetType()}, Entity ID: {ID}", LogVerbosityLevel.WARNING);
        }
#endif


    }

}
