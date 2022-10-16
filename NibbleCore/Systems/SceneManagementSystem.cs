using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Systems
{
    public class SceneManagementSystem : EngineSystem
    {
        private Dictionary<long, SceneGraph> _SceneGraphMap = new(); //Maps entities to scenes
        public List<SceneGraph> SceneGraphs = new();
        public SceneGraph ActiveSceneGraph = null;

        public SceneManagementSystem() : base(EngineSystemEnum.SCENE_MANAGEMENT_SYSTEM)
        {
            
        }

        public void CreateSceneGraph()
        {
            SceneGraph sceneGraph = new();
            sceneGraph.ID = SceneGraphs.Count;
            //Create Root
            SceneGraphNode root = EngineRef.CreateSceneNode("SceneRoot");
            EngineRef.RegisterEntity(root);
            
            sceneGraph.Root = root;
            SceneGraphs.Add(sceneGraph);
            _SceneGraphMap[sceneGraph.ID] = sceneGraph;
        }

        public void SetActiveScene(SceneGraph s)
        {
            if (SceneGraphs.Contains(s))
                ActiveSceneGraph = s;
        }

        public void UpdateActiveSceneGraph()
        {
            UpdateSceneGraph(ActiveSceneGraph);
        }

        public override void OnFrameUpdate(double dt)
        {
            
        }

        public override void OnRenderUpdate(double dt)
        {
            UpdateActiveSceneGraph();
        }

        private void UpdateMeshNodes(SceneGraph graph)
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in graph.MeshNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                MeshComponent mc = n.GetComponent<MeshComponent>() as MeshComponent;
                bool instance_updated = false;
                
                if (td.IsUpdated)
                {
                    if (td.IsOccluded && !td.WasOccluded)
                    {
                        //Remove Instance
                        Log($"Removing Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        //TODO: Maybe it is  a good idea to keep queues for 
                        //instances that will be removed and instance that will be added
                        //which will be passed per frame update to the rendering system
                        //which has direct access to the renderer
                        EngineRef.GetSystem<RenderingSystem>().Renderer.RemoveRenderInstance(ref mc.Mesh, mc);
                    }
                    else if (!td.IsOccluded && td.WasOccluded)
                    {
                        Log($"Adding Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        EngineRef.GetSystem<RenderingSystem>().Renderer.AddRenderInstance(ref mc, td);
                    }
                    else if (!td.IsOccluded)
                    {
                        instance_updated = true;
                        EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceWorldMat(mc.Mesh, mc.InstanceID, td.WorldTransformMat);
                        EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceWorldMatInv(mc.Mesh, mc.InstanceID, td.InverseTransformMat);
                    }

                    td.IsUpdated = false; //Reset updated status to prevent further updates on the same frame update
                }

                //Update Instance Data
                if (mc.IsUpdated && !td.IsOccluded)
                {
                    //Upload Uniforms
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 0, mc.InstanceUniforms[0].Values);
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 1, mc.InstanceUniforms[1].Values);
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 2, mc.InstanceUniforms[2].Values);
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 3, mc.InstanceUniforms[3].Values);
                    instance_updated = true;
                    mc.IsUpdated = false;
                }

                if (instance_updated)
                    EngineRef.GetSystem<RenderingSystem>().Renderer.UpdateInstance(mc.Mesh, mc.InstanceID);
            }
        }

        public void UpdateLightNodes(SceneGraph graph)
        {
            //Process Lights
            foreach (SceneGraphNode n in graph.LightNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                LightComponent lc = n.GetComponent<LightComponent>() as LightComponent;
                bool instance_updated = false;

                if (!lc.Data.IsRenderable && lc.InstanceID != -1)
                {
                    //Remove Instance
                    Log($"Removing Instance {n.Name}", LogVerbosityLevel.DEBUG);
                    //TODO: Maybe it is  a good idea to keep queues for 
                    //instances that will be removed and instance that will be added
                    //which will be passed per frame update to the rendering system
                    //which has direct access to the renderer
                    EngineRef.GetSystem<RenderingSystem>().Renderer.RemoveLightRenderInstance(ref lc.Mesh, lc);
                }
                else if (lc.Data.IsRenderable && lc.InstanceID == -1)
                {
                    Log($"Adding Light Volume Instance {n.Name}", LogVerbosityLevel.DEBUG);
                    EngineRef.GetSystem<RenderingSystem>().Renderer.AddLightRenderInstance(ref lc, td);
                }
                else if (lc.Data.IsRenderable)
                {
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetInstanceWorldMat(lc.Mesh, lc.InstanceID, td.WorldTransformMat);
                    instance_updated = true;
                }

                if (lc.Data.IsUpdated && lc.InstanceID != -1)
                {
                    EngineRef.GetSystem<RenderingSystem>().Renderer.SetLightInstanceData(lc);
                    instance_updated = true;
                    lc.Data.IsUpdated = false;
                }

                if (instance_updated)
                    EngineRef.GetSystem<RenderingSystem>().Renderer.UpdateInstance(lc.Mesh, lc.InstanceID);

            }
        }

        public void UpdateSceneGraph(SceneGraph graph)
        {
            UpdateMeshNodes(graph);
            UpdateLightNodes(graph);
        }

        public void ClearSceneGraph(SceneGraph graph)
        {
            var nodes = graph.Nodes.ToArray();
            for (int i = 0; i< nodes.Length; i++) 
                EngineRef.DisposeSceneGraphNode(nodes[i]);
            graph.Clear();
        }

        public void AddNode(SceneGraphNode node)
        {
            ActiveSceneGraph?.AddNode(node);
        }

        public override void CleanUp()
        {
            //TODO : Check if more has to be cleaned up or if the registry system will handle everything
            foreach (SceneGraph s in SceneGraphs)
                s.Clear();
            SceneGraphs.Clear();
            _SceneGraphMap.Clear();
        }
    }
}
