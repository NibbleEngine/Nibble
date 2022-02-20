using System;
using System.Collections.Generic;

#if OPENGL
using NbCore.Platform.Graphics.OpenGL; 
#endif

using NbCore;
using NbCore.Common;
using NbCore.Platform.Graphics;
using NbCore.Managers;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;

namespace NbCore.Systems
{
    public struct RenderFrameStats
    {
        public float Frametime;
        public int RenderedVerts;
        public int RenderedIndices;

        public void Clear()
        {
            Frametime = 0;
            RenderedVerts = 0;
            RenderedIndices = 0;
        }
    }

    public class RenderingSystem : EngineSystem, IDisposable
    {
        //Constants
        private const ulong MAX_OCTREE_WIDTH = 256;

        //Mesh Management
        readonly List<NbMesh> globalMeshList = new();
        readonly List<NbMesh> collisionMeshList = new();
        readonly List<NbMesh> locatorMeshList = new();
        readonly List<NbMesh> jointMeshList = new();
        readonly List<NbMesh> lightMeshList = new();
        readonly Dictionary<int, NbMeshGroup> MeshGroupDict = new() { };
        readonly List<NbMeshGroup> MeshGroups = new();
        //This is used during group population
        readonly Dictionary<int, NbMeshGroup> OpenMeshGroups = new(); 

        public GraphicsAPI Renderer;

        //Entity Managers used by the rendering system
        public readonly ObjectManager<NbMeshData> MeshDataMgr = new();
        public readonly MaterialManager MaterialMgr = new();
        public readonly GeometryManager GeometryMgr = new();
        public readonly TextureManager TextureMgr = new();
        public readonly ShaderManager ShaderMgr = new();
        public readonly FontManager FontMgr = new();

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        public int last_text_height;
        
        private NbVector2i ViewportSize;
        private const int blur_fbo_scale = 2;
        private double gfTime = 0.0f;

        //Render Buffers
        private FBO gBuffer;
        private FBO renderBuffer;
        
        //Octree Structure
        private Octree octree;

        //RenderFrame Stats
        public RenderFrameStats frameStats;

        public RenderingSystem() : base(EngineSystemEnum.RENDERING_SYSTEM)
        {
            
        }

        public NbVector2i GetViewportSize()
        {
            return ViewportSize;
        }

        public void init(int width, int height)
        {
            //Identify System
            Log(string.Format("Renderer {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("Vendor {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("OpenGL Version {0}", GL.GetString(StringName.Version)), LogVerbosityLevel.INFO);
            Log(string.Format("Shading Language Version {0}", GL.GetString(StringName.ShadingLanguageVersion)), LogVerbosityLevel.INFO);

            ViewportSize = new NbVector2i(width, height);

            //Initialize API
            Renderer = new GraphicsAPI(); //Use OpenGL by default
            Renderer.Init();

            //Create Default MeshGroup
            NbMeshGroup group = new()
            {
                ID = 0,
                GroupTBO1Data = new NbMatrix4[256],
                PrevFrameJointData = new NbMatrix4[256],
                NextFrameJointData = new NbMatrix4[256],
                GroupTBO1 = Renderer.CreateGroupBuffer(),
                boneRemapIndices = new int[1],
                Meshes = new()
            };
            MeshGroupDict[0] = group;
            MeshGroups.Add(group);

            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);

            //Initialize Gbuffer
            setupGBuffer(width, height);

            Log("Resource Manager Initialized", LogVerbosityLevel.INFO);
        }

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gBuffer = Renderer.CreateFrameBuffer(width, height);
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //albedo
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //normals
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //info
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true); //depth
            
            renderBuffer = Renderer.CreateFrameBuffer(width, height);
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //final pass
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //color 0 - blur 0
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //color 1 - blur 1
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //composite
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true); //depth


            //Rebind the default framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Log("FBOs Initialized", LogVerbosityLevel.INFO);
        }

        public FBO getRenderFBO()
        {
            return renderBuffer;
        }

        public FBO getGeometryFBO()
        {
            return gBuffer;
        }

        public void getMousePosInfo(int x, int y, ref NbVector4[] arr)
        {
            //Fetch Depth
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadPixels(x, y, 1, 1, 
                PixelFormat.DepthComponent, PixelType.Float, arr);
            //Fetch color from UI Fbo
        }

        public void progressTime(double dt)
        {
            gfTime += dt;
        }

        private void CleanUpGeometry()
        {
            globalMeshList.Clear();
            collisionMeshList.Clear();
            locatorMeshList.Clear();
            jointMeshList.Clear();
            lightMeshList.Clear();
            octree.clear();

            DeleteMeshGroups(); //This frees TBOs as well
        }

        public override void CleanUp()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit
            //TODO: No proper disposal of vao's at the moment
            CleanUpGeometry();
            
            //Manager Cleanups
            TextureMgr.CleanUp();
            MaterialMgr.CleanUp();
            ShaderMgr.CleanUp();
            
        }

        public NbShader GetMaterialShader(MeshMaterial mat)
        {
            //Calculate Shader hash
            List<string> directives = new();
            directives = EngineRef.CombineShaderDirectives(directives, mat.ShaderConfig.ShaderMode);
            directives.AddRange(EngineRef.GetMaterialShaderDirectives(mat));
            int shader_hash = EngineRef.CalculateShaderHash(directives);

            NbShader shader = null;

            if (ShaderMgr.ShaderHashExists(shader_hash))
            {
                shader =  ShaderMgr.GetShaderByHash(shader_hash);
            } else
            {
                //Compile Material Shader
                shader =  EngineRef.CompileMaterialShader(mat);
            }

            EngineRef.AttachShaderToMaterial(mat, shader);
            return shader;
        }

        public void RegisterEntity(Entity e)
        {
            if (!e.HasComponent<MeshComponent>())
            {
                Log(string.Format("Entity {0} should have a mesh component", e.GetID()), Common.LogVerbosityLevel.INFO);
                return;
            }

            MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;

            Renderer.AddMesh(mc.Mesh);
            MaterialMgr.AddMaterial(mc.Mesh.Material);

            //Store Mesh Data Here
            MeshDataMgr.Add(mc.Mesh.Data.Hash, mc.Mesh.Data);

            //Add to MeshGroup
            if (mc.Mesh.Group != null)
                if (!OpenMeshGroups.ContainsKey(mc.Mesh.Group.ID))
                    AddNewMeshGroup(mc.Mesh.Group);
            
            process_model(mc);
        }   

        private void process_model(MeshComponent m)
        {
            if (m == null)
                return;
            
            bool connect_material_to_shader = false;
            bool save_to_group = false;
            //Explicitly handle locator, scenes and collision meshes
            switch (m.Mesh.Type)
            {
                case (NbMeshType.Mesh):
                    {
                        connect_material_to_shader = true;
                        save_to_group = true;
                        break;
                    }
                case (NbMeshType.Locator):  
                    {
                        if (!locatorMeshList.Contains(m.Mesh))
                            locatorMeshList.Add(m.Mesh);
                        break;
                    }
                case (NbMeshType.Collision):
                    collisionMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.Joint):
                    jointMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.Light):
                    lightMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.LightVolume):
                    {
                        //Do nothing
                        break;
                    }
            }

            //Check if the shader has been registered to the rendering system
            if (!ShaderMgr.ShaderIDExists(m.Mesh.Material.Shader.GetID()))
            {
                ShaderMgr.AddShader(m.Mesh.Material.Shader);
            }

            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m.Mesh))
                globalMeshList.Add(m.Mesh);

            //Organize Meshes to Meshgroup
            if (m.Mesh.Group == null && save_to_group)
            {
                if (!MeshGroupDict[0].Meshes.Contains(m.Mesh))
                {
                    MeshGroupDict[0].Meshes.Add(m.Mesh);
                    m.Mesh.Group = MeshGroupDict[0];
                }
                    
            }
            
        }

        private void process_models(SceneGraphNode root)
        {
            MeshComponent mc = root.GetComponent<MeshComponent>() as MeshComponent;
            process_model(mc);
            
            //Repeat process with children
            foreach (SceneGraphNode child in root.Children)
            {
                process_models(child);
            }
        }

        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //FrameData
            Renderer.SetCameraData(RenderState.activeCam);
            Renderer.SetCommonDataPerFrame(gBuffer, RenderState.rotMat, gfTime);
            Renderer.SetRenderSettings(RenderState.settings.renderSettings);
            Renderer.UploadFrameData();
        }

        public void Resize(int x, int y)
        {
            ViewportSize.X = x;
            ViewportSize.Y = y;
            //Renderer.ResizeViewport(x, y);
            gBuffer?.resize(x, y);
            renderBuffer?.resize(x, y);
        }


        #region MeshGroupActions
        //MeshGroup Actions
        public void AddNewMeshGroup(NbMeshGroup mg)
        {
            if (OpenMeshGroups.ContainsKey(mg.ID))
            {
                Log("MeshGroup Already Open!", LogVerbosityLevel.WARNING);
                return;
            }
            
            OpenMeshGroups[mg.ID] = mg;
        }

        public void DeleteOpenMeshGroup(int id)
        {
            if (!OpenMeshGroups.ContainsKey(id))
            {
                Log($"There is no MeshGroup with ID {id}!", LogVerbosityLevel.WARNING);
                return;
            }
            OpenMeshGroups.Remove(id);
        }

        public void DeleteMeshGroup(NbMeshGroup mg)
        {
            Renderer.DestroyGroupBuffer(mg.GroupTBO1);
        }

        public void DeleteMeshGroups()
        {
            foreach (NbMeshGroup mg in MeshGroups)
            {
                DeleteMeshGroup(mg);
            }
            
            MeshGroups.Clear();
            MeshGroupDict.Clear();
        }

        public void SubmitMeshGroup(NbMeshGroup mg)
        {
            mg.ID = MeshGroups.Count;
            mg.GroupTBO1 = Renderer.CreateGroupBuffer();
            
            mg.GroupTBO1Data = new NbMatrix4[System.Math.Max(1,mg.JointBindingDataList.Count)];
            mg.PrevFrameJointData = new NbMatrix4[System.Math.Max(1, mg.JointBindingDataList.Count)];
            mg.NextFrameJointData = new NbMatrix4[System.Math.Max(1, mg.JointBindingDataList.Count)];

            //Copy any existing data to the bytearray
            for (int i = 0; i < mg.JointBindingDataList.Count; i++)
            {
                NbMatrix4 temp_matrix = mg.JointBindingDataList[i].invBindMatrix;
                mg.GroupTBO1Data[i] = temp_matrix;
                mg.PrevFrameJointData[i] = temp_matrix;
                mg.NextFrameJointData[i] = temp_matrix;
                //This does not work for all models (e.g. the astronaut)
                //I suspect that its just an issue with the model, this matrix multiplication should bring the model to its binding pose
            }
            
            MeshGroups.Add(mg);
            MeshGroupDict[mg.ID] = mg;
        }

        public void AddMeshToOpenGroup(int id, NbMesh mesh)
        {
            if (!OpenMeshGroups.ContainsKey(id))
            {
                Log($"There is no open MeshGroup with ID {id}!", LogVerbosityLevel.WARNING);
                return;
            }

            NbMeshGroup mg = OpenMeshGroups[id];
            mg.Meshes.Add(mesh);
        }

        public void SubmitOpenMeshGroups()
        {
            foreach (var pair in OpenMeshGroups)
            {
                SubmitMeshGroup(pair.Value);
                SortMeshGroup(pair.Value);
                OpenMeshGroups.Remove(pair.Key);
            }
        }

        public void UpdateMeshGroupData(NbMeshGroup mg)
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mg.GroupTBO1);
            
            //Upload skinning data
            unsafe
            {
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, mg.GroupTBO1Data.Length * sizeof(NbMatrix4), mg.GroupTBO1Data);
            }
        }

        #endregion


        #region SortingProcedures
        private void SortMeshGroup(NbMeshGroup group)
        {
            group.Meshes.Sort((NbMesh a, NbMesh b) =>
            {
                MeshMaterial ma = MaterialMgr.Get(a.Material.GetID());
                MeshMaterial mb = MaterialMgr.Get(b.Material.GetID());
                return ma.Shader.GetID().CompareTo(mb.Shader.GetID());
            });
        }

        #endregion

        #region Rendering Methods

        private void sortLights()
        {
            List<Entity> lights = EngineRef.GetEntityTypeList(EntityType.SceneNodeLight);
            SceneGraphNode mainLight = (SceneGraphNode) lights[0];

            lights.RemoveAt(0);
            
            lights.Sort(
                delegate (Entity e1, Entity e2)
                {
                    SceneGraphNode l1 = (SceneGraphNode) e1;
                    SceneGraphNode l2 = (SceneGraphNode) e2;
                    
                    float d1 = (TransformationSystem.GetEntityWorldPosition(l1).Xyz - RenderState.activeCam.Position).Length;
                    float d2 = (TransformationSystem.GetEntityWorldPosition(l2).Xyz - RenderState.activeCam.Position).Length;

                    return d1.CompareTo(d2);
                }
            );

            lights.Insert(0, mainLight);
        }


        private void LOD_filtering(List<GLInstancedMesh> model_list)
        {
            /* TODO : REplace this shit with occlusion based on the instance_ids
            foreach (GLMeshVao m in model_list)
            {
                int i = 0;
                int occluded_instances = 0;
                while (i < m.instance_count)
                {
                    //Skip non LODed meshes
                    if (!m.name.Contains("LOD"))
                    {
                        i++;
                        continue;
                    }

                    //Calculate distance from camera
                    Vector3 bsh_center = m.Bbox[0] + 0.5f * (m.Bbox[1] - m.Bbox[0]);

                    //Move sphere to object's root position
                    Matrix4 mat = m.getInstanceWorldMat(i);
                    bsh_center = (new Vector4(bsh_center, 1.0f) * mat).Xyz;

                    double distance = (bsh_center - Common.RenderState.activeCam.Position).Length;

                    //Find active LOD
                    int active_lod = m.parent.LODNum - 1;
                    for (int j = 0; j < m.parentScene.LODNum - 1; j++)
                    {
                        if (distance < m.parentScene.LODDistances[j])
                        {
                            active_lod = j;
                            break;
                        }
                    }

                    //occlude the other LOD levels
                    for (int j = 0; j < m.parentScene.LODNum; j++)
                    {
                        if (j == active_lod)
                            continue;
                        
                        string lod_text = "LOD" + j;
                        if (m.name.Contains(lod_text))
                        {
                            m.setInstanceOccludedStatus(i, true);
                            occluded_instances++;
                        }
                    }
                    
                    i++;
                }

                if (m.instance_count == occluded_instances)
                    m.occluded = true;
            }
            */
        }

        /* NOT USED
        private void frustum_occlusion(List<GLMeshVao> model_list)
        {
            foreach (GLMeshVao m in model_list)
            {
                int occluded_instances = 0;
                for (int i = 0; i < m.instance_count; i++)
                {
                    if (m.getInstanceOccludedStatus(i))
                        continue;
                    
                    if (!RenderState.activeCam.frustum_occlude(m, i))
                    {
                        occludedNum++;
                        occluded_instances++;
                        m.setInstanceOccludedStatus(i, false);
                    }
                }
            }
        }
        */

        private void renderDefaultMeshes()
        {
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //Collisions
            if (RenderState.settings.viewSettings.ViewCollisions)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("collisionMat");
                Renderer.SetProgram(mat.Shader.ProgramID);

                //Render static meshes
                foreach (NbMesh m in collisionMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;
                    
                    Renderer.RenderCollision(m, mat);
                }
            }

            //Lights
            if (RenderState.settings.viewSettings.ViewLights)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                Renderer.SetProgram(mat.Shader.ProgramID);

                //Render static meshes
                foreach (NbMesh m in lightMeshList)
                {
                    if (m.InstanceCount == 0) continue;
                    Renderer.RenderLight(m, mat);
                }
            }

            //Light Volumes
            if (RenderState.settings.viewSettings.ViewLightVolumes)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                Renderer.SetProgram(mat.Shader.ProgramID);

                //Render static meshes
                NbMesh light_sphere = EngineRef.GetPrimitiveMesh((ulong) "default_light_sphere".GetHashCode());

                if (light_sphere.InstanceCount > 0)
                    Renderer.RenderMesh(light_sphere, mat);
            }

            //Joints
            if (RenderState.settings.viewSettings.ViewJoints)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("jointMat");
                Renderer.SetProgram(mat.Shader.ProgramID);

                //Render static meshes
                foreach (NbMesh m in jointMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;

                    Renderer.RenderJoint(m, mat);
                }
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            //Locators
            if (RenderState.settings.viewSettings.ViewLocators)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("crossMat");
                Renderer.SetProgram(mat.Shader.ProgramID);
                
                //Render static meshes
                foreach (NbMesh mesh in locatorMeshList)
                {
                    if (mesh.InstanceCount == 0)
                        continue;

                    //Renderer.RenderLocator(m, mat);
                    Renderer.RenderMesh(mesh, mat);
                    frameStats.RenderedVerts += mesh.InstanceCount * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics);
                    frameStats.RenderedIndices += mesh.InstanceCount * mesh.MetaData.BatchCount;
                }
            }

            

            GL.Enable(EnableCap.CullFace);
        }

        private void renderTestQuad()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            //Set Test Program


            MeshMaterial mat = MaterialMgr.GetByName("redMat");
            NbShader shader = mat.Shader;
            Renderer.SetProgram(mat.Shader.ProgramID);

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode()),
                shader, shader.CurrentState);
        }

        private void renderStaticMeshes()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);
            
            foreach(NbMeshGroup mg in MeshGroups)
            {
                //Bind Group Data Buffer
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mg.GroupTBO1);

                foreach (NbMesh mesh in mg.Meshes)
                {
                    if (mesh.InstanceCount == 0)
                        continue;

                    MeshMaterial mat = MaterialMgr.Get(mesh.Material.GetID());
                    Renderer.SetProgram(mat.Shader.ProgramID);
                    Renderer.RenderMesh(mesh, mat);
                    frameStats.RenderedVerts += mesh.InstanceCount * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics);
                    frameStats.RenderedIndices += mesh.InstanceCount * mesh.MetaData.BatchCount;
                }
            }
            
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void renderGeometry()
        {
            //DEFERRED STAGE - STATIC MESHES

            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            //DEFERRED STAGE
            GL.ClearColor(new OpenTK.Mathematics.Color4(0.0f, 0, 0, 1.0f));
            Renderer.BindDrawFrameBuffer(gBuffer, new int[] {0, 1, 2});
            Renderer.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);

            //renderTestQuad();
            renderStaticMeshes(); //Deferred Rendered MESHES
            //renderDecalMeshes(); //Render Decals
            renderDefaultMeshes(); //Collisions, Locators, Joints
            
            renderDeferredLightPass(); //Deferred Lighting Pass to pbuf

            //FORWARD STAGE - TRANSPARENT MESHES
            //renderTransparent(); //Directly to Pbuf

        }
        
        private void renderDecalMeshes()
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            //REWRITE
            //foreach (GLSLShaderConfig shader in ShaderMgr.GLDeferredDecalShaders)
            //{
            //    GL.UseProgram(shader.ProgramID);
            //    //Upload depth texture to the shader

            //    //Bind Depth Buffer
            //    GL.Uniform1(shader.uniformLocations["mpCommonPerFrameSamplers.depthMap"].loc, 6);
            //    GL.ActiveTexture(TextureUnit.Texture6);
            //    GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetChannel(4));

            //    foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
            //    {
            //        
            //        //foreach (NbMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
            //        //{
            //        //    if (mesh.InstanceCount == 0)
            //        //        continue;

            //        //    Renderer.RenderMesh(mesh, mat);
            //        //}
            //    }
            //}

            GL.Disable(EnableCap.Blend);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        private void renderTransparent()
        {
            //Copy depth channel from gbuf to pbuf
            FBO.copyDepthChannel(gBuffer.fbo, renderBuffer.fbo, 
                                 gBuffer.Size.X, gBuffer.Size.Y,
                                 renderBuffer.Size.X, renderBuffer.Size.Y);
            
            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(renderBuffer.GetChannel(1), 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(renderBuffer.GetChannel(2), 0, PixelFormat.Rgba, PixelType.Float, new float[] { 1.0f, 1.0f ,1.0f, 1.0f});

            //Enable writing to both channels after clearing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment1,
                                          DrawBuffersEnum.ColorAttachment2});
            
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest); //Enable depth test
            //Set BlendFuncs for the 2 drawbuffers
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.Zero, BlendingFactorDest.OneMinusSrcAlpha);

            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);

            //REWRITE
            //foreach (GLSLShaderConfig shader in ShaderMgr.GLForwardTransparentShaders)
            //{
            //    Renderer.EnableShaderProgram(shader); //Set Program
                
            //    foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
            //    {
                    
            //        //foreach (NbMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
            //        //{
            //        //    if (mesh.InstanceCount == 0)
            //        //        continue;
                    
            //        //    Renderer.RenderMesh(mesh, mat);
            //        //}
                    
            //    }
            //}

            Renderer.UnbindMeshBuffers();
            GL.DepthMask(true); //Re-enable depth buffer
            
            //Composite Step
            NbShader bwoit_composite_shader = ShaderMgr.GetShaderByType(NbShaderType.BWOIT_COMPOSITE_SHADER);
            
            //Draw to main color channel
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha); //Set compositing blend func
                                                                                    //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); //Set compositing blend func

            //Prepare Shader State
            bwoit_composite_shader.ClearCurrentState();
            bwoit_composite_shader.CurrentState.AddSampler("in1Tex",
                new()
                {
                    Target = NbTextureTarget.Texture2D,
                    TextureID = renderBuffer.GetChannel(1)
                }
            );

            bwoit_composite_shader.CurrentState.AddSampler("in2Tex",
                new()
                {
                    Target = NbTextureTarget.Texture2D,
                    TextureID = renderBuffer.GetChannel(2)
                }
            );

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode()), 
                bwoit_composite_shader, bwoit_composite_shader.CurrentState);
            
            GL.Disable(EnableCap.Blend);
        }

        private void renderFinalPass()
        {
            //Blit albedo, normals and depth to the final render buffer
            //A: albedo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, 
                                     renderBuffer.Size.X/8, renderBuffer.Size.Y / 8, 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            //B: Normals
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, renderBuffer.Size.X / 8, 0,
                                     2 * renderBuffer.Size.X / 8, renderBuffer.Size.Y / 8,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            //B: Params
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment2);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 2 * renderBuffer.Size.X / 8, 0,
                                     3 * renderBuffer.Size.X / 8, renderBuffer.Size.Y / 8,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        }
        
        private void renderShadows()
        {

        }

        //Rendering Mechanism
        public void testrender(double dt)
        {
            frameStats.Clear();
            //Previous frame time but makes sense
            frameStats.Frametime = (float) dt; 
            gfTime += dt; //Update render time
            
            //Log("Rendering Frame");
            
            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Prepare Mesh UBO
            Renderer.PrepareMeshBuffers();
            
            //Render Geometry
            renderGeometry();

            Renderer.SyncGPUCommands();

            //POST-PROCESSING
            post_process();

            //Pass result to Render FBO
            renderFinalPass();
            
            
            //Pass Result to Render FBO
            //Render to render_fbo
            //GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, render_fbo.fbo);
            //GL.Viewport(0, 0, ViewportSize.X, ViewportSize.Y);
            //render_quad(Array.Empty<string>(), Array.Empty<float>(), Array.Empty<string>(), Array.Empty<TextureTarget>(), Array.Empty<int>(), resMgr.GLShaders[NbShaderType.RED_FILL_SHADER]);

        }

        public void render()
        {
            //Prepare UBOs
            prepareCommonPerFrameUBO();
            
            //Render Shadows
            renderShadows();

            //Sort Lights
            sortLights();
            
            //Sort Transparent Objects
            //sortTransparent(); //NOT NEEDED ANYMORE
            
            //LOD filtering
            if (RenderState.settings.renderSettings.LODFiltering)
            {
                //LOD_filtering(staticMeshQueue); TODO: FIX
                //LOD_filtering(transparentMeshQueue); TODO: FIX
            }

            //Prepare Mesh UBOs
            Renderer.PrepareMeshBuffers();

            //Render octree
            //octree.render(resMgr.GLShaders[GLSLHelper.NbShaderType.BBOX_SHADER].program_id);

            //Render Geometry
            renderGeometry();

            //Light Pass


            //POST-PROCESSING
            post_process();

            //Final Pass
            renderFinalPass();

            //Render UI();
            //UI Rendering is handled for now by the Window. We'll see if this has to be brought back
            
        }

        /*
        private void render_cameras()
        {
            int active_program = resMgr.GLShaders[GLSLHelper.NbShaderType.BBOX_SHADER].program_id;

            GL.UseProgram(active_program);
            int loc;
            //Send object world Matrix to all shaders


            foreach (Camera cam in resMgr.GLCameras)
            {
                //Old rendering the inverse clip space
                //Upload uniforms
                //loc = GL.GetUniformLocation(active_program, "self_mvp");
                //Matrix4 self_mvp = cam.viewMat;
                //GL.UniformMatrix4(loc, false, ref self_mvp);

                //New rendering the exact frustum plane
                loc = GL.GetUniformLocation(active_program, "worldMat");
                Matrix4 test = Matrix4.Identity;
                test[0, 0] = -1.0f;
                test[1, 1] = -1.0f;
                test[2, 2] = -1.0f;
                GL.UniformMatrix4(loc, false, ref test);

                //Render all inactive cameras
                if (!cam.isActive) cam.render();
            
            }

        }
        */

        private void pass_tex(int to_fbo, DrawBufferMode to_channel, int InTex)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(ClearBufferMask.ColorBufferBit);
            NbShader shader = ShaderMgr.GetShaderByType(NbShaderType.PASSTHROUGH_SHADER);
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("InTex", new NbSamplerState()
            {
                Target = NbTextureTarget.Texture2D,
                TextureID = InTex
            });

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong) "default_renderquad".GetHashCode()),
                        shader, shader.CurrentState);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }


        //private void bloom()
        //{
        //    //Load Programs
        //    GLSLShaderConfig gs_horizontal_blur_program = EngineRef.GetShaderByType(NbShaderType.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
        //    GLSLShaderConfig gs_vertical_blur_program = EngineRef.GetShaderByType(NbShaderType.GAUSSIAN_VERTICAL_BLUR_SHADER);
        //    GLSLShaderConfig br_extract_program = EngineRef.GetShaderByType(NbShaderType.BRIGHTNESS_EXTRACT_SHADER) ;
        //    GLSLShaderConfig add_program = EngineRef.GetShaderByType(NbShaderType.ADDITIVE_BLEND_SHADER);

        //    GL.Disable(EnableCap.DepthTest);

        //    //Copy Color to blur fbo channel 1
        //    FBO.copyChannel(pbuf.fbo, blur_fbo.fbo, gbuf.size[0], gbuf.size[1], blur_fbo.size_x, blur_fbo.size_y,
        //        ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
        //    //pass_tex(blur_fbo.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, new int[] { blur_fbo.size_x, blur_fbo.size_y });

        //    //Extract Brightness on the blur buffer and write it to channel 0
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Write to blur1

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, br_extract_program);



        //    //Copy Color to blur fbo channel 1
        //    //FBO.copyChannel(blur_fbo.fbo, pbuf.fbo, blur_fbo.size_x, blur_fbo.size_y, gbuf.size[0], gbuf.size[1],
        //    //    ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

        //    //return;

        //    //Log(GL.GetError()); 

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
        //    GL.Viewport(0, 0, blur_fbo.size_x, blur_fbo.size_y); //Change the viewport
        //    int blur_amount = 2;
        //    for (int i=0; i < blur_amount; i++)
        //    {
        //        //Step 1- Apply horizontal blur
        //        GL.DrawBuffer(DrawBufferMode.ColorAttachment1); //blur2
        //        GL.Clear(ClearBufferMask.ColorBufferBit);

        //        render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[0]}, gs_horizontal_blur_program);

        //        //Step 2- Apply horizontal blur
        //        GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //blur2
        //        GL.Clear(ClearBufferMask.ColorBufferBit);

        //        render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, gs_vertical_blur_program);
        //    }

        //    //Blit to screen
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
        //    GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
        //    GL.Clear(ClearBufferMask.ColorBufferBit); //Clear Screen

        //    GL.BlitFramebuffer(0, 0, blur_fbo.size_x, blur_fbo.size_y, 0, 0, pbuf.size[0], pbuf.size[1],
        //    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        //    GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]); //Restore viewport

        //    //Save Color to blur2 so that we can composite on the main channel
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.ReadBuffer(ReadBufferMode.ColorAttachment0); //color
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment2); //blur2
        //    GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
        //    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "in1Tex", "in2Tex" }, new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D }, new int[] { pbuf.blur2, pbuf.blur1 }, add_program);
        //    //render_quad(new string[] { }, new float[] { }, new string[] { "blurTex" }, new int[] { pbuf.blur1 }, gs_bloom_program);

        //}

        //private void fxaa()
        //{
        //    //inv_tone_mapping(); //Apply tone mapping pbuf.color shoud be ready

        //    //Load Programs
        //    GLSLShaderConfig fxaa_program = ShaderMgr.GetGenericShader(NbShaderType.FXAA_SHADER);

        //    //Copy Color to first channel
        //    FBO.copyChannel(pbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], pbuf.size[0], pbuf.size[1],
        //        ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
        //    //pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size);

        //    //Apply FXAA
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, fxaa_program);

        //    //tone_mapping(); //Invert Tone Mapping

        //}

        private void tone_mapping()
        {
            //Load Programs
            NbShader shader = ShaderMgr.GetShaderByType(NbShaderType.TONE_MAPPING);

            //Copy Color to first channel
            pass_tex(renderBuffer.fbo, 
                DrawBufferMode.ColorAttachment1, 
                renderBuffer.GetChannel(0)); //LOOKS OK!

            //Apply Tone Mapping
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("inTex", new NbSamplerState()
            {
                SamplerID = 0,
                Target = NbTextureTarget.Texture2D,
                TextureID = renderBuffer.GetChannel(1)
            });
            
            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode()),
                        shader, shader.CurrentState);
        }

        //private void inv_tone_mapping()
        //{
        //    //Load Programs
        //    GLSLShaderConfig inv_tone_mapping_program = ShaderMgr.GetGenericShader(NbShaderType.INV_TONE_MAPPING);

        //    //Copy Color to first channel
        //    pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

        //    //Apply Tone Mapping
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, inv_tone_mapping_program);

        //}

        private void post_process()
        {
            //Actuall Post Process effects in AA space without tone mapping
            //TODO: Bring that back

            //    if (RenderState.settings.renderSettings.UseBLOOM)
            //        bloom(); //BLOOM

            tone_mapping(); //FINAL TONE MAPPING, INCLUDES GAMMA CORRECTION

            //if (RenderState.settings.renderSettings.UseFXAA)
            //    fxaa(); //FXAA (INCLUDING TONE/UNTONE)
        }

        private void backupDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, gBuffer.Size.X, gBuffer.Size.Y,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
        }

        private void restoreDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, gBuffer.Size.X, gBuffer.Size.Y,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        private void renderDeferredLightPass()
        {
            
            /*
            GLSLShaderConfig shader_conf = resMgr.GLShaders[NbShaderType.GBUFFER_SHADER];

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            //TEST DRAW TO SCREEN
            //GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            //GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex"},
                                                            new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D},
                                                            new int[] { gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info}, shader_conf);
            */

            //Render Light volume
            NbShader shader_conf = ShaderMgr.GetShaderByType(NbShaderType.LIGHT_PASS_LIT_SHADER);
            
            MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
            Renderer.EnableMaterialProgram(mat);

            //At first blit the albedo (gbuf 0) -> channel 0 of the pbuf
            FBO.copyChannel(gBuffer.fbo, renderBuffer.fbo,
                            gBuffer.Size.X, gBuffer.Size.Y,
                            renderBuffer.Size.X, renderBuffer.Size.Y,
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            GL.Clear(ClearBufferMask.DepthBufferBit);
            
            //Enable Blend
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            

            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);

            //Disable DepthTest and Depth Write
            GL.DepthMask(false);
            GL.Disable(EnableCap.DepthTest);

            NbMesh mesh = GeometryMgr.GetPrimitiveMesh((ulong) "default_light_sphere".GetHashCode());
            
            GL.UseProgram(shader_conf.ProgramID);

            //Upload samplers
            string[] sampler_names = new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex" };
            int[] texture_ids = new int[] { gBuffer.GetChannel(0),
                                            gBuffer.GetChannel(3),
                                            gBuffer.GetChannel(1),
                                            gBuffer.GetChannel(2) };
            TextureTarget[] sampler_targets = new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D };
            for (int i = 0; i < sampler_names.Length; i++)
            {
                if (shader_conf.uniformLocations.ContainsKey(sampler_names[i]))
                {
                    GL.Uniform1(shader_conf.uniformLocations[sampler_names[i]].loc, i);
                    GL.ActiveTexture(TextureUnit.Texture0 + i);
                    GL.BindTexture(sampler_targets[i], texture_ids[i]);
                }
            }
            
            //Note: we do not draw the light volumes for preview.
            //Thus we directly render the mesh using the simple RenderMesh method
            if (mesh.InstanceCount > 0) 
                Renderer.RenderMesh(mesh);

            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

        }

#endregion Rendering Methods

#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CleanUp(); //Clean local resources
                    gBuffer.Dispose(); //Dispose gbuffer
                    shdwRenderer.Dispose(); //Dispose shadowRenderer
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public override void OnRenderUpdate(double dt)
        {
            Camera.UpdateCameraDirectionalVectors(RenderState.activeCam);

            //Re-upload meshgroup buffers
            foreach(NbMeshGroup mg in MeshGroups)
                UpdateMeshGroupData(mg);
            
            //Re-Compile requested shaders
            while (ShaderMgr.CompilationQueue.Count > 0)
            {
                NbShader shader = ShaderMgr.CompilationQueue.Dequeue();
                //TODO: FIX
                if (shader.RefMaterial is null)
                    Renderer.CompileShader(ref shader, shader.RefConfig);
                else
                    Renderer.CompileShader(ref shader, shader.RefConfig, shader.RefMaterial);

                shader.IsUpdated?.Invoke();
            }
            
            //Render Scene
            testrender(dt); //Render Everything
        }

        public override void OnFrameUpdate(double dt)
        {
            throw new NotImplementedException();
        }
        #endregion

    }

}
