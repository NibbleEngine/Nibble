﻿using System;
using System.Collections.Generic;
using NbCore.Systems;
using Newtonsoft.Json;
using OpenTK.Mathematics;
using NbCore.Utils;
using Newtonsoft.Json.Serialization;

namespace NbCore
{
    public enum SceneNodeType
    {
        MODEL=0x0,
        LOCATOR,
        JOINT,
        MESH,
        LIGHT,
        LIGHTVOLUME,
        EMITTER,
        COLLISION,
        REFERENCE,
        DECAL,
        GIZMO,
        GIZMOPART,
        TEXT,
        UNKNOWN
    }

    
    public class SceneGraphNode : Entity
    {
        [NbSerializable]
        public new SceneNodeType Type = SceneNodeType.UNKNOWN;
        public bool IsSelected = false;
        [NbSerializable]
        public string Name = "";
        public bool IsRenderable = true;
        public bool IsOpen = false;
        //public SceneGraphNode ParentScene = null; //Is this useful at all?
        public List<float> LODDistances = new();
        public Dictionary<string, string> Attributes = new();
        public SceneGraphNode Root = null;
        public SceneGraphNode Parent = null;
        [NbSerializable]
        public List<SceneGraphNode> Children = new();
        
        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public SceneGraphNode(SceneNodeType type) : base(EntityType.SceneNode)
        {
            Type = type;
            switch (type)
            {
                case SceneNodeType.MESH:
                    base.Type = EntityType.SceneNodeMesh;
                    break;
                case SceneNodeType.MODEL:
                    base.Type = EntityType.SceneNodeModel;
                    break;
                case SceneNodeType.LOCATOR:
                    base.Type = EntityType.SceneNode; // Not sure if this should be any different
                    break;
                case SceneNodeType.JOINT:
                    base.Type = EntityType.SceneNodeJoint;
                    break;
                case SceneNodeType.LIGHT:
                    base.Type = EntityType.SceneNodeLight;
                    break;
                case SceneNodeType.COLLISION:
                    base.Type = EntityType.SceneNodeCollision;
                    break;
                case SceneNodeType.REFERENCE:
                    base.Type = EntityType.SceneNodeReference;
                    break;
                default:
                    throw new Exception("make sure to property initialize base type");
            }
        }

        public bool IsDisposed()
        {
            return disposed;
        }

        public void SetRenderableStatusRec(bool status)
        {
            IsRenderable = status;
            TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
            tc.Data.IsActive = status;
            
            foreach (SceneGraphNode child in Children)
                child.SetRenderableStatusRec(status);
        }
        
        public void RemoveChild(SceneGraphNode m)
        {
            if (Children.Contains(m))
            {
                Children.Remove(m);
                m.Parent = null;
            }
        }

        public void AddChild(SceneGraphNode e)
        {
            e.SetParent(this);
        }

        public void SetRootScene(SceneGraphNode e)
        {
            if (e.HasComponent<SceneComponent>())
                Root = e;
        }

        public void SetParent(SceneGraphNode e)
        {
            Parent = e;
            Parent.Children.Add(this);

            //Connect TransformComponents if both have
            if (e.HasComponent<TransformComponent>() && HasComponent<TransformComponent>())
            {
                TransformComponent tc = GetComponent<TransformComponent>() as TransformComponent;
                TransformComponent parent_tc = Parent.GetComponent<TransformComponent>() as TransformComponent;
                tc.Data.SetParentData(parent_tc.Data);
            }
        }

        public void findNodeByID(long id, ref SceneGraphNode m)
        {
            GUIDComponent gc = m.GetComponent<GUIDComponent>() as GUIDComponent;
            if (gc.ID == id)
            {
                m = this;
                return;
            }

            foreach (SceneGraphNode child in Children)
            {
                child.findNodeByID(id, ref m);
            }
        }

        public void findNodeByName(string name, ref SceneGraphNode m)
        {
            if (Name == name)
            {
                m = this;
                return;
            }

            foreach (SceneGraphNode child in Children)
            {
                child.findNodeByName(name, ref m);
            }
        }

        public void resetTransform()
        {
            TransformData td = TransformationSystem.GetEntityTransformData(this);
            td.ResetTransform();
        }

        public override Entity Clone()
        {
            throw new NotImplementedException();
        }

        //WARNING: This should NOT be called directly, object disposals should happen via the engine
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Children.Clear();
            }


            if (HasComponent<MeshComponent>())
            {
                MeshComponent mc = GetComponent<MeshComponent>() as MeshComponent;
                //TODO: Remove mc from the corresponding mesh instanceRefs
            
            };



            //Free unmanaged resources
            disposed = true;
            base.Dispose(disposing);
        }

    }

}
