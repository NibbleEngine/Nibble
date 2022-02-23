using NbCore.Systems;
using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Common;
using Newtonsoft.Json;

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

            Nodes.Remove(n);

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

        public void Export(string output_file)
        {
            System.IO.StreamWriter sw = new(output_file);
            JsonTextWriter writer = new JsonTextWriter(sw);
            JsonSerializer serializer = new JsonSerializer();
            writer.Formatting = Formatting.Indented;

            writer.WriteStartObject();
            
            //Step A: Export ShaderSource Objects
            List<GLSLShaderSource> sources = new();
            List<GLSLShaderConfig> configs = new();
            foreach (SceneGraphNode node in Nodes)
            {
                if (node.HasComponent<MeshComponent>())
                {
                    MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;

                    if (mc.Mesh == null)
                        continue;
                    else if (mc.Mesh.Material == null)
                        continue;

                    var shader_config = mc.Mesh.Material.ShaderConfig;
                    if (!configs.Contains(shader_config))
                        configs.Add(shader_config);

                    var shader_sources = mc.Mesh.Material.ShaderConfig.Sources;
                    foreach (GLSLShaderSource s in shader_sources.Values)
                    {
                        if (s.SourceFilePath == "")
                            continue;
                        if (!sources.Contains(s))
                            sources.Add(s);
                    }
                }
            }

            writer.WritePropertyName("SHADER_SOURCES");
            writer.WriteStartArray();
            foreach (GLSLShaderSource source in sources)
                serializer.Serialize(writer, source);
            writer.WriteEndArray();

            //Step B: Export Shader Configurations
            writer.WritePropertyName("SHADER_CONFIGS");
            writer.WriteStartArray();
            foreach (GLSLShaderConfig conf in configs)
                serializer.Serialize(writer, conf);
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.Close();
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
