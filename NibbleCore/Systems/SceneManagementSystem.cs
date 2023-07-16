using NbCore.Platform.Graphics;
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

        public SceneGraph CreateSceneGraph()
        {
            SceneGraph sceneGraph = new();
            sceneGraph.ID = SceneGraphs.Count;
            
            SceneGraphs.Add(sceneGraph);
            _SceneGraphMap[sceneGraph.ID] = sceneGraph;
            
            return sceneGraph;
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
                List<Component> meshComponents = n.GetComponents<MeshComponent>();
                
                
                foreach (Component c in meshComponents)
                {
                    MeshComponent mc = (MeshComponent)c;
                    bool mesh_instance_updated = false;

                    if (td.IsUpdated || mc.IsUpdated)
                    {
                        if (td.IsOccluded && !td.WasOccluded)
                        {
                            //Remove Instance
                            Log($"Removing Instance {n.Name}", LogVerbosityLevel.DEBUG);
                            //TODO: Maybe it is  a good idea to keep queues for 
                            //instances that will be removed and instance that will be added
                            //which will be passed per frame update to the rendering system
                            //which has direct access to the renderer
                            GraphicsAPI.RemoveRenderInstance(ref mc.Mesh, mc);
                        }
                        else if (!td.IsOccluded && td.WasOccluded)
                        {
                            Log($"Adding Instance UpdateMesh {n.Name}", LogVerbosityLevel.DEBUG);
                            GraphicsAPI.AddRenderInstance(ref mc, td);
                        }
                        else if (!td.IsOccluded)
                        {
                            mesh_instance_updated = true;
                            GraphicsAPI.SetInstanceWorldMat(mc.Mesh, mc.InstanceID, td.WorldTransformMat);
                            GraphicsAPI.SetInstanceWorldMatInv(mc.Mesh, mc.InstanceID, td.InverseTransformMat);
                        }
                    }

                    //Update Instance Data
                    if (mc.IsUpdated && !td.IsOccluded)
                    {
                        //Upload Uniforms
                        GraphicsAPI.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 0, mc.InstanceUniforms[0].Values);
                        GraphicsAPI.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 1, mc.InstanceUniforms[1].Values);
                        GraphicsAPI.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 2, mc.InstanceUniforms[2].Values);
                        GraphicsAPI.SetInstanceUniform4(mc.Mesh, mc.InstanceID, 3, mc.InstanceUniforms[3].Values);
                        mesh_instance_updated = true;
                    }

                    //Update 
                    if (mesh_instance_updated)
                        GraphicsAPI.UpdateInstance(mc.Mesh, mc.InstanceID);
                }
            }
        }

        private void UpdateImposterNodes(SceneGraph graph)
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in graph.ImposterNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                ImposterComponent ic = n.GetComponent<ImposterComponent>();
                bool instance_updated = false;

                if (td.IsUpdated)
                {
                    if (td.IsOccluded && !td.WasOccluded)
                    {
                        //Remove Instance
                        Log($"Removing Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        GraphicsAPI.RemoveRenderInstance(ref ic.Mesh, ic); //Remove Imposter Instance
                    }
                    else if (!td.IsOccluded && td.WasOccluded)
                    {
                        Log($"Adding Imposter Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        GraphicsAPI.AddRenderInstance(ref ic, td);
                    }
                    else if (!td.IsOccluded)
                    {
                        instance_updated = true;
                        GraphicsAPI.SetInstanceWorldMat(ic.Mesh, ic.InstanceID, td.WorldTransformMat);
                        GraphicsAPI.SetInstanceWorldMatInv(ic.Mesh, ic.InstanceID, td.InverseTransformMat);
                    }
                }

                //Update Imposter Data
                if (ic.Data.IsUpdated)
                {
                    GraphicsAPI.SetImposterInstanceData(ic);
                }
                

                //Update 
                if (instance_updated)
                    GraphicsAPI.UpdateInstance(ic?.Mesh, ic.InstanceID);
            }
        }

        public void UpdateLightNodes(SceneGraph graph)
        {
            //Process Lights
            foreach (SceneGraphNode n in graph.LightNodes)
            {
                TransformData td = TransformationSystem.GetEntityTransformData(n);
                LightComponent lc = n.GetComponent<LightComponent>();
                bool light_instance_updated = false;

                if (!lc.Data.IsRenderable && lc.InstanceID != -1)
                {
                    //Remove Instance
                    Log($"Removing LightVolume Instance {n.Name}", LogVerbosityLevel.DEBUG);
                    //TODO: Maybe it is  a good idea to keep queues for 
                    //instances that will be removed and instance that will be added
                    //which will be passed per frame update to the rendering system
                    //which has direct access to the renderer
                    GraphicsAPI.RemoveLightRenderInstance(ref lc.Mesh, lc);
                }
                else if (lc.Data.IsRenderable && lc.InstanceID == -1)
                {
                    Log($"Adding Light Volume Instance {n.Name}", LogVerbosityLevel.DEBUG);
                    GraphicsAPI.AddLightRenderInstance(ref lc, td);
                }
                else if (lc.Data.IsRenderable)
                {
                    GraphicsAPI.SetInstanceWorldMat(lc.Mesh, lc.InstanceID, td.WorldTransformMat);
                    light_instance_updated = true;
                }

                if (lc.Data.IsUpdated && lc.InstanceID != -1)
                {
                    GraphicsAPI.SetLightInstanceData(lc);
                    light_instance_updated = true;
                }

                if (light_instance_updated)
                    GraphicsAPI.UpdateInstance(lc.Mesh, lc.InstanceID);

            }
        }

        private void ResetComponentUpdateStatus(SceneGraph graph)
        {
            foreach (SceneGraphNode n in graph.Nodes)
            {
                if (n.HasComponent<ImposterComponent>())
                {
                    ImposterComponent ic = n.GetComponent<ImposterComponent>();
                    ic.Data.IsUpdated = false;
                    ic.IsUpdated = false;
                }

                if (n.HasComponent<MeshComponent>())
                {
                    MeshComponent ic = n.GetComponent<MeshComponent>();
                    ic.IsUpdated = false;
                }

                if (n.HasComponent<LightComponent>())
                {
                    LightComponent ic = n.GetComponent<LightComponent>();
                    ic.IsUpdated = false;
                    ic.Data.IsUpdated = false;
                }

            }
        }


        public void UpdateSceneGraph(SceneGraph graph)
        {
            UpdateMeshNodes(graph);
            UpdateImposterNodes(graph);
            UpdateLightNodes(graph);

            //Reset Component Update Status
            ResetComponentUpdateStatus(graph);
        }

        public void ClearSceneGraph(SceneGraph graph)
        {
            var nodes = graph.Nodes.ToArray();
            for (int i = 0; i< nodes.Length; i++) 
                EngineRef.DisposeSceneGraphNode(nodes[i]);
            graph.Clear();
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
