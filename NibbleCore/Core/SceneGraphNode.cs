using System;
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

    [NbSerializable]
    public class SceneGraphNode : Entity
    {
        public new SceneNodeType Type = SceneNodeType.UNKNOWN;
        public bool IsSelected = false;
        public string Name = "";
        public bool IsRenderable = true;
        public bool IsOpen = false;
        //public SceneGraphNode ParentScene = null; //Is this useful at all?
        public List<float> LODDistances = new();
        public Dictionary<string, string> Attributes = new();
        public SceneGraphNode Root = null;
        public SceneGraphNode Parent = null;
        public List<SceneGraphNode> Children = new();
        
        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        private static Dictionary<SceneNodeType, EntityType> entityTypeMap = new()
        {
            {SceneNodeType.MESH, EntityType.SceneNodeMesh},
            {SceneNodeType.LIGHT, EntityType.SceneNodeLight},
            {SceneNodeType.MODEL, EntityType.SceneNodeModel},
            {SceneNodeType.LOCATOR, EntityType.SceneNodeLocator},
            {SceneNodeType.JOINT, EntityType.SceneNodeJoint},
            {SceneNodeType.COLLISION, EntityType.SceneNodeCollision},
            {SceneNodeType.REFERENCE, EntityType.SceneNodeReference},
            {SceneNodeType.EMITTER, EntityType.SceneNodeEmmiter}
        };

        public SceneGraphNode(SceneNodeType type) : base(entityTypeMap[type])
        {
            Type = type;
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

        public void findNodeByID(ulong id, ref SceneGraphNode m)
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


        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Type");
            writer.WriteValue(Type);
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            //Serialize Components
            writer.WritePropertyName("Components");
            writer.WriteStartArray();
            foreach (Component c in Components)
                IO.NbSerializer.Serialize(c, writer);
            writer.WriteEndArray();

            //Serialize Children
            writer.WritePropertyName("Children");
            writer.WriteStartArray();
            foreach (SceneGraphNode c in Children)
                IO.NbSerializer.Serialize(c, writer);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static SceneGraphNode Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            SceneNodeType type = (SceneNodeType)token.Value<int>("Type");
            SceneGraphNode node = new SceneGraphNode(type);
            node.Name = token.Value<string>("Name");

            //Deserialize Components
            Newtonsoft.Json.Linq.JToken complist_tkn = token.Value<Newtonsoft.Json.Linq.JToken>("Components");

            foreach (Newtonsoft.Json.Linq.JToken tkn in complist_tkn.Children())
            {
                Component c = IO.NbDeserializer.Deserialize(tkn) as Component;
                node.AddComponent(c.GetType(), c);
            }
            
            

            return node;
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
