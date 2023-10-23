using NbCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NbCore
{
    public class SceneGraph
    {
        public int ID;
        public SceneGraphNode Root = null;
        public readonly List<SceneGraphNode> Nodes = new();
        public readonly List<SceneGraphNode> MeshNodes = new();
        public readonly List<SceneGraphNode> ImposterNodes = new();
        public readonly List<SceneGraphNode> JointNodes = new();
        public readonly List<SceneGraphNode> SceneNodes = new();
        public readonly List<SceneGraphNode> LightNodes = new();

        public SceneGraph()
        {
            Nodes = new();
            MeshNodes = new();
            ImposterNodes = new();
            SceneNodes = new();
            LightNodes = new();
            JointNodes = new();
        }

        public void SetID(int id)
        {
            ID = id;
        }

        public bool HasNode(SceneGraphNode n)
        {
            return Nodes.Contains(n);
        }

        public SceneGraphNode GetNodeByName(string name)
        {
            return Nodes.Find(x => x.Name == name);
        }

        public SceneGraphNode GetNodeByID(uint id)
        {
            return Nodes.Find(x => x.ID == id);
        }

        public SceneGraphNode GetJointNodeByJointID(int jointID)
        {
            foreach (SceneGraphNode node in JointNodes)
            {
                JointComponent jc = node.GetComponent<JointComponent>();
                if (jc.JointIndex == jointID)
                    return node;
            }
            return null;
        }

        public void RemoveNode(SceneGraphNode n)
        {
            if (!HasNode(n))
            {
                string msg = string.Format("Node {0} does not belongs to this scene", n.Name);
                Callbacks.Log(this, msg, LogVerbosityLevel.WARNING);
                return;
            }

            //Handle orphans
            if (n.Parent != null)
            {
                //Connect node's parent to node's children
                //Temporarily cache the node's children because it could be modified
                List<SceneGraphNode> _children = new List<SceneGraphNode>(n.Children);
                foreach (SceneGraphNode child in _children)
                    child.SetParent(n.Parent);

                //Disconnect node from parent
                n.Parent.RemoveChild(n);
            }
            
            Nodes.Remove(n);

            if (n.HasComponent<MeshComponent>())
                MeshNodes.Remove(n);

            if (n.HasComponent<ImposterComponent>())
                ImposterNodes.Remove(n);

            if (n.HasComponent<LightComponent>())
                LightNodes.Remove(n);

            if (n.HasComponent<JointComponent>())
                JointNodes.Remove(n);

            if (n.HasComponent<SceneComponent>())
                SceneNodes.Remove(n);

        }

        public void AddNode(SceneGraphNode n)
        {
            if (HasNode(n))
            {
                string msg = string.Format("Node {0} already belongs to scene", n.Name);
                Callbacks.Log(this, msg, LogVerbosityLevel.WARNING);
                return;
            }

            Nodes.Add(n);

            if (n.HasComponent<MeshComponent>())
                MeshNodes.Add(n);

            if (n.HasComponent<ImposterComponent>())
                ImposterNodes.Add(n);

            if (n.HasComponent<LightComponent>())
                LightNodes.Add(n);

            if (n.HasComponent<JointComponent>())
                JointNodes.Add(n);

            if (n.HasComponent<SceneComponent>())
                SceneNodes.Add(n);
        }

        public void Clear()
        {
            Root.Children.Clear();
            Nodes.Clear();
            MeshNodes.Clear();
            ImposterNodes.Clear();
            SceneNodes.Clear();
            LightNodes.Clear();
            JointNodes.Clear();
        }

    }

}
