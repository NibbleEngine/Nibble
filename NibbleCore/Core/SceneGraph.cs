﻿using NbCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;

namespace NbCore
{
    public class SceneGraph
    {
        public int ID;
        public SceneGraphNode Root = null;
        public readonly List<SceneGraphNode> Nodes = new();
        public readonly List<SceneGraphNode> MeshNodes = new();
        public readonly List<SceneGraphNode> JointNodes = new();
        public readonly List<SceneGraphNode> SceneNodes = new();
        public readonly List<SceneGraphNode> LightNodes = new();

        public SceneGraph()
        {
            Nodes = new();
            MeshNodes = new();
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

            if (n.HasComponent<JointComponent>())
                JointNodes.Remove(n);

            if (n.HasComponent<SceneComponent>())
                SceneNodes.Remove(n);

        }

        public void AddNode(SceneGraphNode n)
        {
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
                JointNodes.Add(n);

            if (n.HasComponent<SceneComponent>())
                SceneNodes.Add(n);

            foreach (SceneGraphNode child in n.Children)
                AddNode(child);
        }

        public void Clear()
        {
            Root.Children.Clear();
            Nodes.Clear();
            MeshNodes.Clear();
            SceneNodes.Clear();
            LightNodes.Clear();
            JointNodes.Clear();
        }

    }
}
