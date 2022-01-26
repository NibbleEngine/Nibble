using System;
using System.Collections.Generic;
using System.Text;
using NbCore;
using NbCore.Systems;
using NbCore.Common;

namespace NbCore
{
    public class SceneComponent : Component
    {
        public int activeLOD = 0;
        public int NumLods;
        public readonly List<float> LODDistances = new();

        public readonly List<SceneGraphNode> Nodes = new();
        public readonly List<SceneGraphNode> MeshNodes = new();
        public readonly List<SceneGraphNode> LightNodes = new();
        public readonly Dictionary<string, SceneGraphNode> JointNodes = new();
        
        public SceneComponent()
        {
            
        }
        
        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

        public bool HasNode(SceneGraphNode n)
        {
            return Nodes.Contains(n);
        }

        public SceneGraphNode GetNodeByName(string name)
        {
            return Nodes.Find(x => x.Name == name);
        }

        public void RemoveNode(SceneGraphNode n)
        {
            if (!HasNode(n))
            {
                Callbacks.Log(string.Format("Node {0} does not belongs to this scene", n.Name),
                    LogVerbosityLevel.WARNING);
                return;
            }

            //Handle orphans
            if (n.Parent != null)
                n.Parent.RemoveChild(n);

            if (n.HasComponent<MeshComponent>())
                MeshNodes.Remove(n);

            if (n.HasComponent<LightComponent>())
                LightNodes.Remove(n);

        }

        public void AddNode(SceneGraphNode n)
        {
            //I should not chekck for registration status of n here
            //This should allow for node generation from the plugins
            //And then try to register the entire scene once its ready
            //to the entity registry

            if (HasNode(n))
            {
                Callbacks.Log(string.Format("Node {0} already belongs to scene", n.Name),
                    LogVerbosityLevel.WARNING);
                return;
            }

            Nodes.Add(n);
            
            if (n.HasComponent<MeshComponent>())
                MeshNodes.Add(n);

            if (n.HasComponent<LightComponent>())
                LightNodes.Add(n);

            if (n.HasComponent<JointComponent>())
                JointNodes[n.Name] = n;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                
                MeshNodes.Clear();
                JointNodes.Clear();
                LightNodes.Clear();
                Nodes.Clear();

                //Free other resources here
                base.Dispose(disposing);
            }

        }
    }
}
