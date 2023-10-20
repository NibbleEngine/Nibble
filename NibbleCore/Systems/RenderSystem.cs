using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Common;
using NbCore.Platform.Graphics;
using NbCore.Managers;
using OpenTK.Graphics.OpenGL4;
using NbCore.Platform.Windowing;
using System.Reflection.Metadata;

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
        public readonly NbObjectManager<ulong, NbMeshData> MeshDataMgr = new();
        public readonly NbMaterialManager MaterialMgr = new();
        public readonly GeometryManager GeometryMgr = new();
        public readonly TextureManager TextureMgr = new();
        public readonly ShaderManager ShaderMgr = new();
        public readonly FontManager FontMgr = new();

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        public int last_text_height;
        
        private NbVector2i ViewportSize = new NbVector2i(1024, 768);
        private double gfTime = 0.0f;

        //Render Buffers
        private FBO gBuffer;
        private FBO renderBuffer;
        private FBO bloomBuffer;
        
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

        public void init()
        {
            //Identify System
            Log(string.Format("Renderer {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("Vendor {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("OpenGL Version {0}", GL.GetString(StringName.Version)), LogVerbosityLevel.INFO);
            Log(string.Format("Shading Language Version {0}", GL.GetString(StringName.ShadingLanguageVersion)), LogVerbosityLevel.INFO);

            //Initialize API
            Renderer = new GraphicsAPI(); //Use OpenGL by default
            Renderer.Init();

            //Create Default MeshGroup
            NbMeshGroup group = new()
            {
                ID = 0,
                GroupTBO1Data = new NbMatrix4[512],
                PrevFrameJointData = new NbMatrix4[512],
                NextFrameJointData = new NbMatrix4[512],
                GroupTBO1 = Renderer.CreateGroupBuffer()
            };

            MeshGroupDict[0] = group;
            MeshGroups.Add(group);

            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);

            //Initialize Gbuffer
            setupFBOs();

            Log("Resource Manager Initialized", LogVerbosityLevel.INFO);
        }

        public void setupFBOs()
        {
            //Create gbuffer
            gBuffer = Renderer.CreateFrameBuffer(ViewportSize.X, ViewportSize.Y, FBOOptions.None);
            gBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment0, ViewportSize); //albedo
            gBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment1, ViewportSize); //normals
            gBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment2, ViewportSize); //info1
            gBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment3, ViewportSize); //info2
            gBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.DEPTH, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Depth, ViewportSize); //depth
            
            //Create renderbuffer
            renderBuffer = Renderer.CreateFrameBuffer(ViewportSize.X, ViewportSize.Y, FBOOptions.None);
            renderBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment0, ViewportSize); //HDR Channel 0
            renderBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA16F, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment1, ViewportSize); //HDR Channel 1
            renderBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.RGBA8, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.Attachment2, ViewportSize);   //RGB Channel
            renderBuffer.AddAttachment(NbTextureTarget.Texture2D, NbTextureInternalFormat.DEPTH24_STENCIL8, NbTextureFilter.Nearest, NbTextureFilter.Nearest, NbFBOAttachment.DepthStencil, ViewportSize);  //depth

            //Create bloombuffer
            bloomBuffer = Renderer.CreateFrameBuffer(ViewportSize.X, ViewportSize.Y, FBOOptions.None);

            NbVector2i bloomMipSize = new(ViewportSize.X, ViewportSize.Y);
            for (int i = 0; i < 6; i++)
            {
                bloomBuffer.AddAttachment(NbTextureTarget.Texture2D,
                NbTextureInternalFormat.R11FG11FB10F, NbTextureFilter.Linear, NbTextureFilter.Linear, (NbFBOAttachment) i, bloomMipSize, false);
                bloomMipSize.X /= 2;
                bloomMipSize.Y /= 2;
            }
            
            //Rebind the default framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Log("FBOs Initialized", LogVerbosityLevel.INFO);
        }

        public void deleteFBOs()
        {
            gBuffer.Dispose();
            renderBuffer.Dispose();
            bloomBuffer.Dispose();
        }

        public FBO getRenderFBO()
        {
            return renderBuffer;
        }

        public FBO getGeometryFBO()
        {
            return gBuffer;
        }

        //public void getMousePosInfo(int x, int y, ref NbVector4[] arr)
        //{
        //    //Fetch Depth
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
        //    GL.ReadPixels(x, y, 1, 1, 
        //        PixelFormat.DepthComponent, PixelType.Float, arr);
        //    //Fetch color from UI Fbo
        //}

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

        public void RegisterEntity(NbShader shader)
        {
            ShaderMgr.AddShader(shader);
        }

        public void RegisterEntity(NbMaterial mat)
        {
            MaterialMgr.AddMaterial(mat);
        }

        public void RegisterEntity(NbMesh mesh)
        {
            if (mesh == null)
                return;

            if (mesh.Group != null)
                AddNewMeshGroup(mesh.Group);

            Renderer.AddMesh(mesh);
            //Store Mesh Data Here
            MeshDataMgr.Add(mesh.Data.Hash, mesh.Data);

            bool save_to_group = false;
            //Explicitly handle locator, scenes and collision meshes
            switch (mesh.Type)
            {
                case (NbMeshType.Mesh):
                    {
                        save_to_group = true;
                        break;
                    }
                case (NbMeshType.Locator):
                    {
                        if (!locatorMeshList.Contains(mesh))
                            locatorMeshList.Add(mesh);
                        break;
                    }
                case (NbMeshType.Collision):
                    collisionMeshList.Add(mesh);
                    break;
                case (NbMeshType.Joint):
                    jointMeshList.Add(mesh);
                    break;
                case (NbMeshType.Light):
                    lightMeshList.Add(mesh);
                    break;
                case (NbMeshType.LightVolume):
                    {
                        //Do nothing
                        break;
                    }

            }

            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(mesh))
                globalMeshList.Add(mesh);

            //Add mesh to the default mesh group if it is not assigned to any group
            if (save_to_group)
            {
                if (mesh.Group == null)
                {
                    //Add to default mesh group
                    MeshGroupDict[0].AddMesh(mesh);
                } else
                {
                    //No Need to do anything mesh already has a group
                }
            }
        }

        //TODO: Do we need that?

        public void RegisterEntity(MeshComponent mc)
        {
            if (mc == null)
                return;

            if (mc.Mesh.Group != null)
                AddNewMeshGroup(mc.Mesh.Group);
        }   

        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //FrameData
            Renderer.SetCameraData(NbRenderState.activeCam);
            Renderer.SetCommonDataPerFrame(gBuffer, gfTime);
            Renderer.SetRenderSettings(NbRenderState.settings.RenderSettings);
            Renderer.UploadFrameData();
        }

        public void Resize(int x, int y)
        {
            ViewportSize.X = x;
            ViewportSize.Y = y;
            deleteFBOs();
            setupFBOs();
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
            
            Log($"Opening MeshGroup with ID {mg.ID}", LogVerbosityLevel.DEBUG);
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
            NbMatrix4 temp_matrix = NbMatrix4.Identity();
            if (mg.JointBindingDataList.Count > 1)
            {
                for (int i = 0; i < mg.JointBindingDataList.Count; i++)
                {
                    mg.GroupTBO1Data[i] = temp_matrix;
                    mg.PrevFrameJointData[i] = temp_matrix;
                    mg.NextFrameJointData[i] = temp_matrix;
                    //This does not work for all models (e.g. the astronaut)
                    //I suspect that its just an issue with the model, this matrix multiplication should bring the model to its binding pose
                }
            } else
            {
                mg.GroupTBO1Data[0] = temp_matrix;
                mg.PrevFrameJointData[0] = temp_matrix;
                mg.NextFrameJointData[0] = temp_matrix;
            }
            
            MeshGroups.Add(mg);
            MeshGroupDict[mg.ID] = mg;
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
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, 
                    mg.GroupTBO1Data.Length * sizeof(NbMatrix4), mg.GroupTBO1Data);
            }
        }

        #endregion


        #region SortingProcedures
        private void SortMeshGroup(NbMeshGroup group)
        {
            group.Meshes.Sort((NbMesh a, NbMesh b) =>
            {
                NbMaterial ma = MaterialMgr.Get(a.Material.ID);
                NbMaterial mb = MaterialMgr.Get(b.Material.ID);
                return ma.Shader.ID.CompareTo(mb.Shader.ID);
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
                    
                    float d1 = (TransformationSystem.GetEntityWorldPosition(l1).Xyz - NbRenderState.activeCam.Position).Length;
                    float d2 = (TransformationSystem.GetEntityWorldPosition(l2).Xyz - NbRenderState.activeCam.Position).Length;

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
            Renderer.SetCullFace(false);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //Collisions
            if (NbRenderState.settings.ViewSettings.ViewCollisions)
            {
                NbMaterial mat = EngineRef.GetMaterialByName("collisionMat");

                //Render static meshes
                foreach (NbMesh m in collisionMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;
                    
                    Renderer.RenderCollision(m, mat);
                }
            }

            //Lights
            if (NbRenderState.settings.ViewSettings.ViewLights)
            {
                NbMaterial mat = EngineRef.GetMaterialByName("lightMat");

                //Render static meshes 
                foreach (NbMesh m in lightMeshList)
                {
                    if (m.InstanceCount == 0) continue;
                    Renderer.RenderLight(m, mat);
                }
            }

            //Light Volumes
            if (NbRenderState.settings.ViewSettings.ViewLightVolumes)
            {
                NbMaterial mat = EngineRef.GetMaterialByName("collisionMat");

                //Render static meshes
                NbMesh light_sphere = EngineRef.GetMesh(NbHasher.Hash("default_light_sphere"));
                
                if (light_sphere.InstanceCount > 0)
                    Renderer.RenderMesh(light_sphere, mat);
            }

            //Mesh Bounding Volumes
            if (NbRenderState.settings.ViewSettings.ViewBoundHulls)
            {
                //Bind BBox Shader
                NbShader bbox_shader = ShaderMgr.GetShaderByType(NbShaderType.BBOX_SHADER);
                Renderer.EnableShaderProgram(bbox_shader);
                
                //Render static meshes
                foreach (NbMeshGroup mg in MeshGroups)
                {
                    Renderer.BindGroupBuffer(mg);
                    foreach (NbMesh mesh in mg.OpaqueMeshes)
                    {
                        if (mesh.InstanceCount == 0)
                            continue;
                        
                        if (mesh.InstanceCount > 0)
                            Renderer.RenderBBoxes(mesh);
                    }
                }

            }

            //Joints
            if (NbRenderState.settings.ViewSettings.ViewJoints)
            {
                NbMaterial mat = EngineRef.GetMaterialByName("jointMat");

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
            if (NbRenderState.settings.ViewSettings.ViewLocators)
            {
                NbMaterial mat = EngineRef.GetMaterialByName("crossMat");
                
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

            Renderer.SetCullFace(true);
        }

        private void renderTestQuad()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            //Set Test Program


            NbMaterial mat = MaterialMgr.GetByName("redMat");
            NbShader shader = mat.Shader;
            Renderer.SetProgram(mat.Shader.ProgramID);
            
            Renderer.RenderQuad(EngineRef.GetMesh(NbHasher.Hash("default_renderquad")),
                shader, shader.CurrentState);
        }

        private void renderStaticMeshes()
        {
            //Set polygon mode
            Renderer.SetPolygonMode(NbRenderState.settings.RenderSettings.RenderMode);
            
            foreach (NbMeshGroup mg in MeshGroups)
            {
                Renderer.BindGroupBuffer(mg);
                foreach (NbMesh mesh in mg.OpaqueMeshes)
                {
                    if (mesh.InstanceCount == 0)
                        continue;

                    NbMaterial mat = MaterialMgr.Get(mesh.Material.ID);
                    Renderer.RenderMesh(mesh, mat);
                    frameStats.RenderedVerts += mesh.InstanceCount * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics);
                    frameStats.RenderedIndices += mesh.InstanceCount * mesh.MetaData.BatchCount;
                }
            }

            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }
        
        private void defferedShading()
        {
            //DEFERRED STAGE
            Renderer.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            Renderer.BindDrawFrameBuffer(gBuffer, new int[] { 0, 1, 2, 3 });
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);
            //Renderer.SetDepthTest(true);
            //GL.DepthMask(true);

            renderGeometry(); //Renders main geometry

            //Copy depth buffer to the render Buffer
            Renderer.CopyDepthChannel(gBuffer, renderBuffer);

            if (NbRenderState.settings.RenderSettings.UseLighting)
                renderDeferredLightPass(); //Deferred Lighting Pass to pbuf
            else
            {
                //Pass albedo to render buffer
                pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment0, gBuffer.GetTexture(NbFBOAttachment.Attachment0));
            }

            //FORWARD STAGE 

            //Draw immediately on the render buffer overwriting the depth buffer
            //renderDecalMeshes(); //Render Decals
            //renderTransparent(); //TODO: Bring back

            //Default Meshes
            renderDefaultMeshes(); //Collisions, Locators, Joints
            

        }

        private void forwardShading()
        {
            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the color channel only
            
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);

            renderGeometry();

            //GL.BindTexture(TextureTarget.Texture2D, renderBuffer.GetTexture(NbFBOAttachment.Attachment0).texID);
            //GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        private void renderGeometry()
        {
            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Prepare Mesh UBO
            Renderer.PrepareMeshBuffers();

            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            //renderTestQuad();
            renderStaticMeshes(); //Deferred Rendered MESHES
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

            //    foreach (NbMaterial mat in ShaderMgr.GetShaderMaterials(shader))
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
            Renderer.CopyDepthChannel(gBuffer, renderBuffer);
            
            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(renderBuffer.GetTexture(NbFBOAttachment.Attachment1).GpuID, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(renderBuffer.GetTexture(NbFBOAttachment.Attachment2).GpuID, 0, PixelFormat.Rgba, PixelType.Float, new float[] { 1.0f, 1.0f ,1.0f, 1.0f});

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
            Renderer.SetPolygonMode(NbRenderState.settings.RenderSettings.RenderMode);

            foreach (NbMeshGroup mg in MeshGroups)
            {
                Renderer.BindGroupBuffer(mg);
                foreach (NbMesh mesh in mg.TransparentMeshes)
                {
                    if (mesh.InstanceCount == 0)
                        continue;

                    NbMaterial mat = MaterialMgr.Get(mesh.Material.ID);
                    Renderer.SetProgram(mat.Shader.ProgramID);
                    Renderer.RenderMesh(mesh, mat);
                    frameStats.RenderedVerts += mesh.InstanceCount * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics);
                    frameStats.RenderedIndices += mesh.InstanceCount * mesh.MetaData.BatchCount;
                }
            }
            
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
                    Texture = renderBuffer.GetTexture(NbFBOAttachment.Attachment1)
                }
            );

            bwoit_composite_shader.CurrentState.AddSampler("in2Tex",
                new()
                {
                    Texture = renderBuffer.GetTexture(NbFBOAttachment.Attachment2)
                }
            );

            Renderer.RenderQuad(EngineRef.GetMesh(NbHasher.Hash("default_renderquad")), 
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
            //GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, renderBuffer.Size.X / 8, 0,
                                     2 * renderBuffer.Size.X / 8, renderBuffer.Size.Y / 8,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            //B: Params01
            //GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment2);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 2 * renderBuffer.Size.X / 8, 0,
                                     3 * renderBuffer.Size.X / 8, renderBuffer.Size.Y / 8,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            //C: Params02
            //GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment3);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 3 * renderBuffer.Size.X / 8, 0,
                                     4 * renderBuffer.Size.X / 8, renderBuffer.Size.Y / 8,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }
        
        private void renderShadows()
        {

        }

        //Rendering Mechanism
        public void render(double dt)
        {
            frameStats.Clear();
            //Previous frame time but makes sense
            frameStats.Frametime = (float) dt; 
            gfTime += dt; //Update render time

            //Log("Rendering Frame");
            

            //Deferred Shading
            defferedShading();

            

            //Forward Shading
            //forwardShading();

            Renderer.PostRendering();

            //POST-PROCESSING
            post_process();
            
            //Pass results to Screen
            //renderFinalPass();

            //Pass Result to Render FBO
            //Render to render_fbo
            //GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, render_fbo.fbo);
            //GL.Viewport(0, 0, ViewportSize.X, ViewportSize.Y);
            //render_quad(Array.Empty<string>(), Array.Empty<float>(), Array.Empty<string>(), Array.Empty<TextureTarget>(), Array.Empty<int>(), resMgr.GLShaders[NbShaderType.RED_FILL_SHADER]);

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

        


        private void pass_tex(int to_fbo, DrawBufferMode to_channel, NbTexture InTex, ClearBufferMask mask = ClearBufferMask.ColorBufferBit)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(mask);
            NbShader shader = ShaderMgr.GetShaderByType(NbShaderType.PASSTHROUGH_SHADER);
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("InTex", new NbSampler()
            {
                SamplerID = 0,
                Texture = InTex
            });

            Renderer.RenderQuad(EngineRef.GetMesh(NbHasher.Hash("default_renderquad")),
                        shader, shader.CurrentState);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }


        private void bloom()
        {
            //Load Programs
            NbShader downsampling_program = ShaderMgr.GetShaderByType(NbShaderType.DOWNSAMPLING_SHADER);
            NbShader upsampling_program = ShaderMgr.GetShaderByType(NbShaderType.UPSAMPLING_SHADER);
            NbShader mix_proram = ShaderMgr.GetShaderByType(NbShaderType.MIX_SHADER);
            NbMesh render_quad = EngineRef.GetMesh(NbHasher.Hash("default_renderquad"));
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);

            //Step A: Downsampling the current renderbuffer results
            downsampling_program.ClearCurrentState();
            downsampling_program.CurrentState.AddSampler("srcTexture", new NbSampler()
            {
                SamplerID = 0,
                Texture = renderBuffer.GetTexture(0),
            });
            downsampling_program.CurrentState.AddUniform("srcResolution",
                    new NbVector2(renderBuffer.Size.X, renderBuffer.Size.Y));


            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, bloomBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            NbTexture mip_lvl;
            for (int i = 0; i < 6; i++)
            {
                //Update shader state
                
                if (i == 0)
                {
                    downsampling_program.CurrentState.AddUniform("mipLevel", 0);
                }
                else
                {
                    downsampling_program.CurrentState.AddUniform("mipLevel", 1);
                }

                //Get bloom level texture (to be written)
                mip_lvl = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0 + i);
                
                GL.Viewport(0, 0, mip_lvl.Data.Width, mip_lvl.Data.Height);

                //Attach texture to framebuffer
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, 
                    FramebufferAttachment.ColorAttachment0, 
                    GraphicsAPI.TextureTargetMap[mip_lvl.Data.target], 
                    mip_lvl.GpuID, 0);
                
                GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);
                Renderer.RenderQuad(render_quad, downsampling_program, downsampling_program.CurrentState);
                downsampling_program.CurrentState.RemoveUniform("srcResolution");
                downsampling_program.CurrentState.RemoveUniform("mipLevel");
                downsampling_program.CurrentState.RemoveUniform("srcTexture");

                downsampling_program.CurrentState.AddSampler("srcTexture", new NbSampler()
                {
                    SamplerID = 0,
                    Texture = mip_lvl
                });

                downsampling_program.CurrentState.AddUniform("srcResolution",
                    new NbVector2(mip_lvl.Data.Width, mip_lvl.Data.Height));

            }


            //mip_lvl = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0);
            //Attach texture to framebuffer
            //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
            //    FramebufferAttachment.ColorAttachment0,
            //    GraphicsAPI.TextureTargetMap[mip_lvl.Data.target],
            //    mip_lvl.GpuID, 0);

            
            //Step B: Upsampling
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            upsampling_program.ClearCurrentState();
            upsampling_program.CurrentState.AddUniform("filterRadius", NbRenderState.settings.RenderSettings.BloomFilterRadius);
            
            for (int i = 5; i > 0; i--)
            {
                upsampling_program.CurrentState.AddSampler("srcTexture", new NbSampler()
                {
                    SamplerID = 0,
                    Texture = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0 + i)
                });

                NbTexture prev_lvl = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0 + i - 1);

                //Attach texture to framebuffer

                GL.Viewport(0, 0, prev_lvl.Data.Width, prev_lvl.Data.Height);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    GraphicsAPI.TextureTargetMap[prev_lvl.Data.target],
                    prev_lvl.GpuID, 0);
                
                //GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);
                Renderer.RenderQuad(render_quad, upsampling_program, upsampling_program.CurrentState);
                upsampling_program.CurrentState.RemoveUniform("srcTexture");
            }

            //mip_lvl = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0);
            //Attach texture to framebuffer
            //GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
            //    FramebufferAttachment.ColorAttachment0,
            //    GraphicsAPI.TextureTargetMap[mip_lvl.Data.target],
            //    mip_lvl.GpuID, 0);


            //Step C: Blend results

            mix_proram.ClearCurrentState();
            mix_proram.CurrentState.AddUniform("mix_factor", 
                NbRenderState.settings.RenderSettings.BloomIntensity);
            mix_proram.CurrentState.AddSampler("inTex1", new NbSampler()
            {
                SamplerID = 0,
                Texture = renderBuffer.GetTexture(NbFBOAttachment.Attachment0)
            });

            mix_proram.CurrentState.AddSampler("inTex2", new NbSampler()
            {
                SamplerID = 1,
                Texture = bloomBuffer.GetTexture(NbFBOAttachment.Attachment0)
            });

            GL.Disable(EnableCap.Blend);
            Renderer.BindDrawFrameBuffer(renderBuffer, NbFBOAttachment.Attachment1);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Renderer.RenderQuad(render_quad, mix_proram, mix_proram.CurrentState);

            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);
        }



        private void fxaa()
        {
            //inv_tone_mapping(); //Apply tone mapping pbuf.color shoud be ready

            //Load Programs
            NbShader shader = ShaderMgr.GetShaderByType(NbShaderType.FXAA_SHADER);

            //Apply FXAA
            Renderer.BindDrawFrameBuffer(renderBuffer, NbFBOAttachment.Attachment0);
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);
            
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("diffuseTex", new NbSampler()
            {
                SamplerID = 0,
                Texture = renderBuffer.GetTexture(NbFBOAttachment.Attachment1)
            });

            Renderer.RenderQuad(EngineRef.GetMesh(NbHasher.Hash("default_renderquad")),
                        shader, shader.CurrentState);
            
        }

        private void tone_mapping()
        {
            //Load Programs
            NbShader shader = ShaderMgr.GetShaderByType(NbShaderType.TONE_MAPPING);

            //Apply Tone Mapping
            //Draw to the RGB channel
            Renderer.BindDrawFrameBuffer(renderBuffer, NbFBOAttachment.Attachment1);
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);

            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("inTex", new NbSampler()
            {
                SamplerID = 0,
                Texture = renderBuffer.GetTexture(NbFBOAttachment.Attachment0) //Load HDR channel
            });
            
            Renderer.RenderQuad(EngineRef.GetMesh(NbHasher.Hash("default_renderquad")),
                        shader, shader.CurrentState);
        }

        private void composite()
        {
            //Draw to the composite RGB channel
            Renderer.BindDrawFrameBuffer(renderBuffer, new int[] { 2 });
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);
            Renderer.ClearColor(NbRenderState.settings.RenderSettings.BackgroundColor);

            //The following settings are the defaults
            //GL.Enable(EnableCap.Blend);
            //GL.BlendEquation(BlendEquationMode.FuncAdd);
            //GL.BlendFunc(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment2, renderBuffer.GetTexture(NbFBOAttachment.Attachment1));


            /*
             * Keep that for testing
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, bloomBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment2);
            GL.BlitFramebuffer(0, 0, bloomBuffer.Size.X, bloomBuffer.Size.Y, 0, 0,
                                     400, 250,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            */
            //GL.Disable(EnableCap.Blend);

        }

        private void post_process()
        {
            //Actuall Post Process effects in AA space without tone mapping
            //TODO: Bring that back

            if (NbRenderState.settings.RenderSettings.UseBLOOM)
                bloom(); //BLOOM
            else
            {
                //Pass channel 0 to 1 since tone mapping is expecting the results there
                pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment1, renderBuffer.GetTexture(NbFBOAttachment.Attachment0), ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            //FXAA
            if (NbRenderState.settings.RenderSettings.UseFXAA)
                fxaa();
            else
            {
                //Pass channel 0 to 1 since tone mapping is expecting the results there
                pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment0, renderBuffer.GetTexture(NbFBOAttachment.Attachment1), ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            //TONE MAPPING
            if (NbRenderState.settings.RenderSettings.UseToneMapping)
                tone_mapping(); 
            else
            {
                //Pass channel 1 to 0 since tone mapping is expecting the results there
                pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment1, renderBuffer.GetTexture(NbFBOAttachment.Attachment0), ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            //Composite 
            
            composite();


        }

        

        private void renderDeferredLightPass()
        {
            NbMaterial mat = EngineRef.GetMaterialByName("lightMat");
            NbMesh mesh = EngineRef.GetMesh(NbHasher.Hash("default_light_sphere"));

            if (mesh.InstanceCount == 0)
                return;

            NbShader stencil_shader = ShaderMgr.GetShaderByType(NbShaderType.LIGHT_PASS_STENCIL_SHADER);
            NbShader light_shader = ShaderMgr.GetShaderByType(NbShaderType.LIGHT_PASS_LIT_SHADER);

            //Setup Light Shader
            Renderer.EnableShaderProgram(light_shader);
            
            //Upload samplers
            string[] sampler_names = new string[] { "albedoTex", "normalTex", "parameterTex01", "parameterTex02", "depthTex" };
            int[] texture_ids = new int[] { gBuffer.GetTexture(NbFBOAttachment.Attachment0).GpuID,
                                            gBuffer.GetTexture(NbFBOAttachment.Attachment1).GpuID,
                                            gBuffer.GetTexture(NbFBOAttachment.Attachment2).GpuID,
                                            gBuffer.GetTexture(NbFBOAttachment.Attachment3).GpuID,
                                            gBuffer.GetTexture(NbFBOAttachment.Depth).GpuID};
            TextureTarget[] sampler_targets = new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D, TextureTarget.Texture2D };
            for (int i = 0; i < sampler_names.Length; i++)
            {
                if (light_shader.uniformLocations.ContainsKey(sampler_names[i]))
                {
                    GL.Uniform1(light_shader.uniformLocations[sampler_names[i]].loc, i);
                    GL.ActiveTexture(TextureUnit.Texture0 + i);
                    GL.BindTexture(sampler_targets[i], texture_ids[i]);
                }
            }

            //Drawing on the render buffer's first channel
            Renderer.BindDrawFrameBuffer(renderBuffer, NbFBOAttachment.Attachment0);
            GraphicsAPI.ClearDrawBuffer(NbBufferMask.Color);

            for (int i = 0; i < mesh.InstanceCount; i++)
            {
                //Stencil Pass
                GL.DrawBuffer(DrawBufferMode.None);
                GraphicsAPI.ClearDrawBuffer(NbBufferMask.Stencil);

                //Setup stencil ops
                GL.Enable(EnableCap.StencilTest);
                GL.Enable(EnableCap.DepthTest);
                GL.DepthMask(false); //Disable writing to depth buffer
                GL.Disable(EnableCap.CullFace); //Render front/back faces
                GL.StencilFunc(StencilFunction.Always, 0x0, 0x0); //With always all fragments pass
                GL.StencilOpSeparate(StencilFace.Back, StencilOp.Keep, StencilOp.IncrWrap, StencilOp.Keep);
                GL.StencilOpSeparate(StencilFace.Front, StencilOp.Keep, StencilOp.DecrWrap, StencilOp.Keep);

                Renderer.EnableShaderProgram(stencil_shader);
                //Render the light volumes
                //Thus we directly render the mesh using the simple RenderMesh method
                Renderer.RenderMesh(mesh, i);

                //Light Pass
                
                //Bind the color channel of the pbuf for drawing
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                Renderer.EnableShaderProgram(light_shader);

                GL.StencilFunc(StencilFunction.Notequal, 0x0, 0xFF);
                GL.Disable(EnableCap.DepthTest);
                //GL.Enable(EnableCap.Blend);
                //GL.BlendEquation(BlendEquationMode.FuncAdd);
                GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
                GL.Enable(EnableCap.CullFace);
                GL.CullFace(CullFaceMode.Front);

                //Note: we do not draw the light volumes for preview.
                //Thus we directly render the mesh using the simple RenderMesh method
                Renderer.RenderMesh(mesh, i);
            }

            //Cleanup
            GL.DepthMask(true);
            Renderer.SetDepthTest(true);
            GL.Disable(EnableCap.StencilTest);
            Renderer.SetCullFace(true);
            GL.CullFace(CullFaceMode.Back);


            //Copy Channels
            //Blend lighting and albedo info on the main channel of the renderbuffer

            //GL.BlendEquation(BlendEquationMode.FuncAdd);
            Renderer.SetBlend(true);
            GL.BlendFunc(0, BlendingFactorSrc.OneMinusDstAlpha, BlendingFactorDest.DstAlpha);

            //Copy the albedo info to the accumulated lighting info on channel 0
            pass_tex(renderBuffer.fbo, DrawBufferMode.ColorAttachment0, gBuffer.GetTexture(0), ClearBufferMask.None);

            //Renderer.SetBlend(false);
            //Restore default blendfunc
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
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
            Camera.UpdateCameraDirectionalVectors(NbRenderState.activeCam);

            //Re-upload meshgroup buffers
            foreach(NbMeshGroup mg in MeshGroups)
                UpdateMeshGroupData(mg);
            
            //Re-Compile requested shaders
            while (ShaderMgr.ShaderCompilationQueue.Count > 0)
            {
                NbShader shader = ShaderMgr.ShaderCompilationQueue.Dequeue();
                EngineRef.CompileShader(shader);
            }

            //Render Scene
            render(dt); //Render Everything
        }

        public override void OnFrameUpdate(double dt)
        {
            throw new NotImplementedException();
        }
        #endregion

    }

}
