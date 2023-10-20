using NbCore.Common;
using NbCore;
using NbCore.Platform.Graphics;
using OpenTK.Mathematics;
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
                NbTransformData td = TransformationSystem.GetEntityTransformData(n);
                List<Component> meshComponents = n.GetComponents<MeshComponent>();

                foreach (Component c in meshComponents)
                {
                    MeshComponent mc = (MeshComponent)c;
                    bool mesh_instance_updated = false;

                    //Frustum Culling
                    bool renderable_status_updated = (n.IsRenderable != n.WasRenderable);
                    mc.WasOccluded = mc.IsOccluded;
                    mc.IsOccluded = n.IsRenderable ? FrustumCulling(mc.Mesh, td) : true;
                    mc.IsUpdated |= (mc.IsOccluded != mc.WasOccluded);

                    if (mc.IsUpdated || renderable_status_updated || td.IsUpdated)
                    {
                        if (mc.IsOccluded && !mc.WasOccluded)
                        {
                            //Remove Instance
                            Log($"Removing Instance {n.Name}", LogVerbosityLevel.HIDEBUG);
                            //TODO: Maybe it is  a good idea to keep queues for 
                            //instances that will be removed and instance that will be added
                            //which will be passed per frame update to the rendering system
                            //which has direct access to the renderer
                            GraphicsAPI.RemoveRenderInstance(ref mc.Mesh, mc);
                        }
                        else if (!mc.IsOccluded && mc.WasOccluded)
                        {
                            Log($"Adding Instance UpdateMesh {n.Name}", LogVerbosityLevel.HIDEBUG);
                            GraphicsAPI.AddRenderInstance(ref mc, td);
                        }
                        else if (!mc.IsOccluded && td.IsUpdated)
                        {
                            mesh_instance_updated = true;
                            GraphicsAPI.SetInstanceWorldMat(mc.Mesh, mc.InstanceID, td.WorldTransformMat);
                            GraphicsAPI.SetInstanceWorldMatInv(mc.Mesh, mc.InstanceID, td.InverseTransformMat);
                        }
                    }

                    //Update Instance Data
                    if (mc.IsUpdated && !mc.IsOccluded)
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
                    {
                        GraphicsAPI.UpdateInstance(mc.Mesh, mc.InstanceID);
                    }
                    
                }
            }
        }

        private void UpdateImposterNodes(SceneGraph graph)
        {
            //Add instances to all non occluded Nodes
            foreach (SceneGraphNode n in graph.ImposterNodes)
            {
                NbTransformData td = TransformationSystem.GetEntityTransformData(n);
                ImposterComponent ic = n.GetComponent<ImposterComponent>();
                bool instance_updated = false;

                if (td.IsUpdated)
                {
                    if (ic.IsOccluded && !ic.WasOccluded)
                    {
                        //Remove Instance
                        Log($"Removing Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        GraphicsAPI.RemoveRenderInstance(ref ic.Mesh, ic); //Remove Imposter Instance
                    }
                    else if (!ic.IsOccluded && ic.WasOccluded)
                    {
                        Log($"Adding Imposter Instance {n.Name}", LogVerbosityLevel.DEBUG);
                        GraphicsAPI.AddRenderInstance(ref ic, td);
                    }
                    else if (!ic.IsOccluded)
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
                NbTransformData td = TransformationSystem.GetEntityTransformData(n);
                LightComponent lc = n.GetComponent<LightComponent>();
                MeshComponent mc = n.GetComponent<MeshComponent>();
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
                else if (lc.Data.IsRenderable && td.IsUpdated)
                {
                    //Update transform data for the light volume
                    GraphicsAPI.SetInstanceWorldMat(lc.Mesh, lc.InstanceID, td.WorldTransformMat);
                    //Update light segment mesh
                    //mc.Mesh.Data.UpdateVertex(0, td.WorldPosition.Xyz);
                    NbMatrix4 rotX = NbMatrix4.CreateRotationX(Math.Radians(lc.Data.Direction.X));
                    NbMatrix4 rotY = NbMatrix4.CreateRotationY(Math.Radians(lc.Data.Direction.Y));
                    NbMatrix4 rotZ = NbMatrix4.CreateRotationZ(Math.Radians(lc.Data.Direction.Z));

                    NbVector4 endPoint = NbVector4.Transform(new NbVector4(1.0f, 0.0f, 0.0f, 0.0f), rotZ * rotX * rotY);
                    //NbVector4 endPoint = NbVector4.Transform(new NbVector4(1.0f, 0.0f, 0.0f, 0.0f), rot);
                    mc.Mesh.Data.UpdateVertex(1, endPoint.Xyz);
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

        private bool isPointCulled(NbVector4 vec)
        {
            vec = vec * NbRenderState.activeCam.viewMat; //Bring point into clip space
            return (-vec.W > vec.X || vec.X > vec.W) && (-vec.W > vec.Y || vec.Y > vec.W) && (-vec.W > vec.Z || vec.Z > vec.W);
        }

        private bool FrustumCullingMod(NbMesh mesh, NbTransformData td)
        {
            if (mesh.Hash.ToString() == "3881229220707198789")
            {
                Console.WriteLine($"{NbRenderState.activeCam.Position.X}, {NbRenderState.activeCam.Position.Y}, {NbRenderState.activeCam.Position.Z}");
                Console.WriteLine($"{NbRenderState.activeCam.Front.X}, {NbRenderState.activeCam.Front.Y}, {NbRenderState.activeCam.Front.Z}");
                for (int i = 0; i < 6; i++)
                    Console.WriteLine($"[{NbRenderState.activeCam.frPlanes[i].X},{NbRenderState.activeCam.frPlanes[i].Y},{NbRenderState.activeCam.frPlanes[i].Z},{NbRenderState.activeCam.frPlanes[i].W}]");

                Console.WriteLine($"{mesh.MetaData.AABBMIN.X}, {mesh.MetaData.AABBMIN.Y}, {mesh.MetaData.AABBMIN.Z}");
                Console.WriteLine($"{mesh.MetaData.AABBMAX.X}, {mesh.MetaData.AABBMAX.Y}, {mesh.MetaData.AABBMAX.Z}");
            }
            

            NbVector3 instancePos = td.WorldPosition.Xyz;
            // Loop through each frustum plane
            for (int planeID = 0; planeID < 6; ++planeID)
            {
                NbVector3 planeNormal = NbRenderState.activeCam.frPlanes[planeID].Xyz;
                float planeConstant = NbRenderState.activeCam.frPlanes[planeID].W;

                // Check each axis (x,y,z) to get the AABB vertex furthest away from the direction the plane is facing (plane normal)
                NbVector3 vec;

                //0:
                vec = new NbVector3(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMIN.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //1:
                vec = new NbVector3(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMAX.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //2:
                vec = new NbVector3(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMIN.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //3:
                vec = new NbVector3(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMAX.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //4:
                vec = new NbVector3(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMIN.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //5:
                vec = new NbVector3(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMAX.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //6:
                vec = new NbVector3(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMIN.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;

                //7:
                vec = new NbVector3(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMAX.Z);
                if (planeNormal.Dot(vec + instancePos) + planeConstant >= 0.0f)
                    return false;
            
            }
            return true;
        }

        private bool FrustumCullingSmart(NbMesh mesh, NbTransformData td)
        {
            if (mesh.Hash.ToString() == "3881229220707198789")
                Console.WriteLine('.');

            NbVector4 instancePos = td.WorldPosition;
            bool cull = false;
            // Loop through each frustum plane
            for (int planeID = 0; planeID < 6; ++planeID)
            {
                NbVector3 planeNormal = NbRenderState.activeCam.frPlanes[planeID].Xyz;
                float planeConstant = NbRenderState.activeCam.frPlanes[planeID].W;

                // Check each axis (x,y,z) to get the AABB vertex furthest away from the direction the plane is facing (plane normal)
                NbVector3 axisVert = new NbVector3();

                // x-axis
                if (planeNormal.X < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the x axis
                    axisVert.X = mesh.MetaData.AABBMIN.X + instancePos.X; // min x plus tree positions x
                else
                    axisVert.X = mesh.MetaData.AABBMAX.X + instancePos.X; // max x plus tree positions x

                // y-axis
                if (planeNormal.Y < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the y axis
                    axisVert.Y = mesh.MetaData.AABBMIN.Y + instancePos.Y; // min y plus tree positions y
                else
                    axisVert.Y = mesh.MetaData.AABBMAX.Y + instancePos.Y; // max y plus tree positions y

                // z-axis
                if (planeNormal.Z < 0.0f)    // Which AABB vertex is furthest down (plane normals direction) the z axis
                    axisVert.Z = mesh.MetaData.AABBMIN.Z + instancePos.Z; // min z plus tree positions z
                else
                    axisVert.Z = mesh.MetaData.AABBMAX.Z + instancePos.Z; // max z plus tree positions z

                // Now we get the signed distance from the AABB vertex that's furthest down the frustum planes normal,
                // and if the signed distance is negative, then the entire bounding box is behind the frustum plane, which means
                // that it should be culled
                if (planeNormal.Dot(axisVert) + planeConstant < 0.0f)
                {
                    cull = true;
                    // Skip remaining planes to check and move on to next tree
                    break;
                }
            }
            return cull;
        }

        private bool FrustumCulling(NbMesh mesh, NbTransformData td)
        {
            //Check corners
            NbVector4 vec = new NbVector4();

            //0:
            vec = new NbVector4(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMIN.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //1:
            vec = new NbVector4(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMAX.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //2:
            vec = new NbVector4(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMIN.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //3:
            vec = new NbVector4(mesh.MetaData.AABBMIN.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMAX.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //4:
            vec = new NbVector4(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMIN.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //5:
            vec = new NbVector4(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMIN.Y, mesh.MetaData.AABBMAX.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //6:
            vec = new NbVector4(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMIN.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            //7:
            vec = new NbVector4(mesh.MetaData.AABBMAX.X, mesh.MetaData.AABBMAX.Y, mesh.MetaData.AABBMAX.Z, 1.0f);
            if (!isPointCulled(vec * td.WorldTransformMat))
                return false;

            return true;
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
