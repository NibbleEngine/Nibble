#if (OPENGL)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using NbCore;
using NbCore.Systems;
using NbCore.Math;
using NbCore.Common;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System.Linq;

namespace NbCore.Platform.Graphics
{
    //Framebuffer Structs
    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameSamplers
    {
        [FieldOffset(0)]
        public int depthMap; //Depth Map Sampler ID
        public static readonly int SizeInBytes = 12;
    };

    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameUniforms
    {
        [FieldOffset(0)]
        public float diffuseFlag; //Enable Textures
        [FieldOffset(4)]
        public float use_lighting; //Enable lighting
        [FieldOffset(8)]
        public float gfTime; //Fractional Time
        [FieldOffset(12)]
        public float MSAA_SAMPLES; //MSAA Samples
        [FieldOffset(16)]
        public OpenTK.Mathematics.Vector2 frameDim; //Frame Dimensions
        [FieldOffset(24)]
        public float cameraNearPlane;
        [FieldOffset(28)]
        public float cameraFarPlane;
        [FieldOffset(32)]
        public OpenTK.Mathematics.Matrix4 rotMat;
        [FieldOffset(96)]
        public OpenTK.Mathematics.Matrix4 rotMatInv;
        [FieldOffset(160)]
        public OpenTK.Mathematics.Matrix4 mvp;
        [FieldOffset(224)]
        public OpenTK.Mathematics.Matrix4 lookMatInv;
        [FieldOffset(288)]
        public OpenTK.Mathematics.Matrix4 projMatInv;
        [FieldOffset(352)]
        public OpenTK.Mathematics.Vector4 cameraPositionExposure; //Exposure is the W component
        [FieldOffset(368)]
        public int light_number;
        [FieldOffset(384)]
        public OpenTK.Mathematics.Vector3 cameraDirection;
        [FieldOffset(400)]
        public unsafe fixed float lights[32 * 64];
        //[FieldOffset(400), MarshalAs(UnmanagedType.LPArray, SizeConst=32*64)]
        //public float[] lights;
        public static readonly int SizeInBytes = 8592;
    };

    public enum NbBufferMask
    {
        Color,
        Depth
    }

    public class GraphicsAPI
    {
        private const string RendererName = "OPENGL_RENDERER";
        private ProgramHandle activeProgramID = ProgramHandle.Zero;
        public Dictionary<ulong, GLInstancedMesh> MeshMap = new();
        public Dictionary<ulong, GLVao> VaoMap = new();
        private readonly Dictionary<string, BufferHandle> UBOs = new();
        private int SSBO_size = 2 * 1024 * 1024;
        private readonly Dictionary<string, BufferHandle> SSBOs = new();

        private bool useMultiBuffers = false;
        private int multiBufferActiveId;
        private readonly List<BufferHandle> multiBufferSSBOs = new(4);
        private readonly List<GLSync> multiBufferSyncStatuses = new(4);

        public static readonly Dictionary<NbTextureTarget, TextureTarget> TextureTargetMap = new()
        {
            { NbTextureTarget.Texture1D , TextureTarget.Texture1d},
            { NbTextureTarget.Texture2D , TextureTarget.Texture2d},
            { NbTextureTarget.Texture3D, TextureTarget.Texture3d },
            { NbTextureTarget.Texture2DArray , TextureTarget.Texture2dArray},
            { NbTextureTarget.TextureCubeMap, TextureTarget.TextureCubeMap}
        };

        public static readonly Dictionary<NbTextureFilter, int> TextureFilterMap = new()
        {
            { NbTextureFilter.Nearest , (int) TextureMagFilter.Nearest}, //Nearest enum for Mag and Min filters are the same
            { NbTextureFilter.Linear , (int) TextureMagFilter.Linear}, //Same
            { NbTextureFilter.NearestMipmapLinear , (int) TextureMinFilter.NearestMipmapLinear}, //Only min filter
            { NbTextureFilter.LinearMipmapNearest , (int) TextureMinFilter.LinearMipmapNearest}, //only min filter
            { NbTextureFilter.LinearMipmapLinear , (int) TextureMinFilter.LinearMipmapLinear}, //only min filter
        };

        public static readonly Dictionary<NbTextureWrapMode, int> TextureWrapMap = new()
        {
            { NbTextureWrapMode.ClampToEdge , (int) TextureWrapMode.ClampToEdge},
            { NbTextureWrapMode.ClampToBorder , (int) TextureWrapMode.ClampToBorder},
            { NbTextureWrapMode.Repeat , (int) TextureWrapMode.Repeat},
            { NbTextureWrapMode.MirroredRepeat , (int) TextureWrapMode.MirroredRepeat}
        };

        public static readonly Dictionary<NbTextureInternalFormat, InternalFormat> InternalFormatMap = new()
        {
            { NbTextureInternalFormat.DXT1, InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext },
            { NbTextureInternalFormat.DXT3, InternalFormat.CompressedRgbaS3tcDxt3Ext },
            { NbTextureInternalFormat.DXT5, InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext },
            { NbTextureInternalFormat.RGTC2, InternalFormat.CompressedRgRgtc2 },
            { NbTextureInternalFormat.BC7, InternalFormat.CompressedSrgbAlphaBptcUnorm },
            { NbTextureInternalFormat.RGBA8, InternalFormat.Rgba8},
            { NbTextureInternalFormat.BGRA8, InternalFormat.Rgba8},
            { NbTextureInternalFormat.RGBA16F, InternalFormat.Rgba16f},
            { NbTextureInternalFormat.DEPTH, InternalFormat.DepthComponent},
        };

        public static readonly Dictionary<NbTextureInternalFormat, PixelFormat> PixelFormatMap = new()
        {
            {NbTextureInternalFormat.RGBA8, PixelFormat.Rgba },
            {NbTextureInternalFormat.BGRA8, PixelFormat.Bgra }
        };

        //UBO structs
        CommonPerFrameUniforms cpfu;
        

        private const int MAX_NUMBER_OF_MESHES = 2000;
        private const int MULTI_BUFFER_COUNT = 3;

        private GLDebugProc GLDebug;

        private static void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(RendererName.ToUpper(), msg, lvl);
        }


        private void GLDebugMessage(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            bool report = false;
            switch (severity)
            {
                case DebugSeverity.DebugSeverityHigh:
                    report = true;
                    break;
            }

            if (report)
            {
                string msg = source == DebugSource.DebugSourceApplication ?
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}" :
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}\n\tid:{id} severity:{severity} type:{type} source:{source}\n";

                Log(msg, LogVerbosityLevel.DEBUG);
            }
        }

        public void Init()
        {
#if (DEBUG)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GLDebug = new GLDebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            
            GL.DebugMessageControl(DebugSource.DontCare, DebugType.DontCare,
            DebugSeverity.DontCare, new uint[] { 0 }, true);
            
            GL.DebugMessageInsert(DebugSource.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif

#if (DEBUG)
            //Query GL Extensions
            Log("OPENGL AVAILABLE EXTENSIONS:", LogVerbosityLevel.INFO);
            var test = GL.GetString(StringName.Extensions);
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Log(s, LogVerbosityLevel.INFO);
                if (s.Contains("texture"))
                    Log(s, LogVerbosityLevel.INFO);
                if (s.Contains("16"))
                    Log(s, LogVerbosityLevel.INFO);
            }

            //Query maximum buffer sizes
            int maxUniformBlockSize = 0;
            GL.GetInteger(GetPName.MaxUniformBlockSize, ref maxUniformBlockSize);
            Log($"MaxUniformBlock Size {maxUniformBlockSize}", LogVerbosityLevel.INFO);
#endif
            
            //Default Enables
            EnableBlend();
            
            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs

            setupSSBOs(SSBO_size); //Init SSBOs to 2MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];

        }

        public void SetProgram(ProgramHandle program_id)
        {
            if (activeProgramID != program_id)
                GL.UseProgram(program_id); //Set Program if not already active
        }

        private void setupSSBOs(int size)
        {
            //Allocate space for lights in the framebuffer. TODO: Remove that shit
            //cpfu.lights = new float[32 * 64];

            //Allocate atlas
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes;
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes; //256 MB just to play safe
            //OpenGL Spec max size for the SSBO is 128 MB, lets stick to that
            
            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
            {
                BufferHandle ssbo = GL.GenBuffer();
                GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, ssbo);
                if (useMultiBuffers)
                    GL.BufferStorage(BufferStorageTarget.ShaderStorageBuffer, size,
                        IntPtr.Zero, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit);
                else
                    GL.BufferData(BufferTargetARB.ShaderStorageBuffer, size, IntPtr.Zero, BufferUsageARB.StreamDraw); //FOR OLD METHOD
                multiBufferSSBOs.Add(ssbo);
                multiBufferSyncStatuses.Add(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0));
            }

            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, BufferHandle.Zero);

            GL.Flush();
        }

        private void deleteSSBOs()
        {
            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, BufferHandle.Zero);
            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
                GL.DeleteBuffer(multiBufferSSBOs[i]);
        }

        private void resizeSSBOs(int size)
        {
            deleteSSBOs();
            setupSSBOs(size);
        }

        private void setupFrameUBO()
        {
            BufferHandle ubo = GL.GenBuffer();
            GL.BindBuffer(BufferTargetARB.UniformBuffer, ubo);
            GL.BufferData(BufferTargetARB.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageARB.StreamRead);
            GL.BindBuffer(BufferTargetARB.UniformBuffer, BufferHandle.Zero);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo;

            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferTargetARB.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }

        public BufferHandle CreateGroupBuffer()
        {
            int size = 512 * 16 * 4; //FIXED SIZE FOR NOW
            BufferHandle ssbo = GL.GenBuffer();
            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, ssbo);
            GL.BufferStorage(BufferStorageTarget.ShaderStorageBuffer, size,
                IntPtr.Zero, BufferStorageMask.DynamicStorageBit);
            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, BufferHandle.Zero);

            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, ssbo);

            return ssbo;
        }

        public void DestroyBuffer(BufferHandle handle)
        {
            GL.DeleteBuffer(handle);
        }

        public void PrepareMeshBuffers()
        {
            if (useMultiBuffers)
                PrepareMeshBuffersMultiBuffer();
            else
                PrepareMeshBuffersSingleBuffer();
        }

        public void PrepareMeshBuffersMultiBuffer()
        {
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;

            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            SyncStatus result = SyncStatus.WaitFailed;
#if DEBUG
            System.Diagnostics.Stopwatch timer = new();
            timer.Start();
#endif
            while (result == SyncStatus.TimeoutExpired || result == SyncStatus.WaitFailed)
            {
                //Callbacks.Logger.Log(result.ToString());
                //Console.WriteLine("Gamithike o dias");
                result = GL.ClientWaitSync(multiBufferSyncStatuses[multiBufferActiveId], 0, 10);
            }
#if DEBUG
            timer.Stop();
            Log($"Elapsed time for Sync Wait {timer.ElapsedMilliseconds}", LogVerbosityLevel.HIDEBUG);
#endif
            GL.DeleteSync(multiBufferSyncStatuses[multiBufferActiveId]);

            PrepareMeshBuffersSingleBuffer();
        }

        

        public void PrepareMeshBuffersSingleBuffer()
        {
            //Upload atlas UBO data
            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);

            //Prepare UBO data
            int max_ubo_offset = GLMeshInstanceManager.GetAtlasSize();

            if (SSBO_size < GLMeshInstanceManager.GetAtlasSize())
            {
                //Unmap and unbind buffer
                GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, BufferHandle.Zero);

                resizeSSBOs(max_ubo_offset);

                //Rebind buffer
                GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);
            }

            //Upload Data
            

            unsafe
            {
                GCHandle handle = GCHandle.Alloc(GLMeshInstanceManager.atlas_cpmu, GCHandleType.Pinned);
                //IntPtr handlePtr = handle.AddrOfPinnedObject();
                //System.Buffer.MemoryCopy(handlePtr.ToPointer(), ptr.ToPointer(), max_ubo_offset, max_ubo_offset);
                GL.BufferSubData(BufferTargetARB.ShaderStorageBuffer, IntPtr.Zero, 
                    Marshal.SizeOf<MeshInstance>() * GLMeshInstanceManager.atlas_cpmu.Length, handle.AddrOfPinnedObject());
            }

            GL.BindBuffer(BufferTargetARB.ShaderStorageBuffer, BufferHandle.Zero);

        }

        public void UploadFrameData()
        {
            GL.BindBuffer(BufferTargetARB.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTargetARB.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, cpfu);
            GL.BindBuffer(BufferTargetARB.UniformBuffer, BufferHandle.Zero);
        }

        public void SetRenderSettings(RenderSettings settings)
        {
            //Prepare Struct
            cpfu.diffuseFlag = (RenderState.settings.RenderSettings.UseTextures) ? 1.0f : 0.0f;
            cpfu.use_lighting = (RenderState.settings.RenderSettings.UseLighting) ? 1.0f : 0.0f;
            cpfu.cameraPositionExposure.W = RenderState.settings.RenderSettings.HDRExposure;
        }

        public void SetCameraData(Camera cam)
        {
            cpfu.mvp = RenderState.activeCam.viewMat._Value;
            cpfu.lookMatInv = RenderState.activeCam.lookMatInv._Value;
            cpfu.projMatInv = RenderState.activeCam.projMatInv._Value;
            cpfu.cameraPositionExposure.Xyz = RenderState.activeCam.Position._Value;
            cpfu.cameraDirection = RenderState.activeCam.Front._Value;
            cpfu.cameraNearPlane = RenderState.settings.CamSettings.zNear;
            cpfu.cameraFarPlane = RenderState.settings.CamSettings.zFar;
        }

        public void SetCommonDataPerFrame(FBO gBuffer, NbMatrix4 rotMat, double time)
        {
            cpfu.frameDim.X = gBuffer.Size.X;
            cpfu.frameDim.Y = gBuffer.Size.Y;
            cpfu.rotMat = RenderState.rotMat._Value;
            cpfu.rotMatInv = RenderState.rotMat._Value.Inverted();
            cpfu.gfTime = (float)time;
            cpfu.MSAA_SAMPLES = gBuffer.msaa_samples;
        }

        public void ResizeViewport(int w, int h)
        {
            
        }

        public void Viewport(int x, int y)
        {
            GL.Viewport(0, 0, x, y);
        }

        public void EnableBlend()
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        public void ClearColor(NbVector4 vec)
        {
            GL.ClearColor(vec.X, vec.Y, vec.Z, vec.W);
        }

        public bool AddMesh(NbMesh mesh)
        {
            Callbacks.Assert(mesh.Hash != 0x0, 
                "Default mesh hash. Something went wrong during mesh generation");

            Callbacks.Assert(mesh.ID != 0x0,
                "WARNING. Mesh Object probably not registered");

            if (MeshMap.ContainsKey(mesh.ID))
            {
                Log($"Mesh Hash already exists in map. Entity ID: {mesh.ID}. Mesh Hash {mesh.Hash}", LogVerbosityLevel.WARNING);
                return false;
            }
            
            //Generate instanced mesh
            GLInstancedMesh imesh = GenerateAPIMesh(mesh);
            MeshMap[mesh.ID] = imesh;


            
            var err = GL.GetError();

            if (err != ErrorCode.NoError)
                Log("GL ERROR", LogVerbosityLevel.WARNING);

            Log($"Mesh was successfully registered to the Renderer. Entity ID: {mesh.ID}. Mesh Hash {mesh.Hash}", LogVerbosityLevel.DEBUG);
            return true;
        }

        private GLInstancedMesh GenerateAPIMesh(NbMesh mesh)
        {
            GLVao gl_vao;
            if (VaoMap.ContainsKey(mesh.Hash))
                gl_vao = VaoMap[mesh.Hash];
            else
            {
                gl_vao = generateVAO(mesh);
                VaoMap[mesh.Hash] = gl_vao;
            }
            
            GLInstancedMesh imesh = new()
            {
                vao = gl_vao,
                Mesh = mesh
            };
            
            return imesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableMaterialProgram(NbMaterial mat)
        {
            NbShader shader = mat.Shader;
            GL.UseProgram(shader.ProgramID); //Set Program
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableShaderProgram(NbShader shader)
        {
            GL.UseProgram(shader.ProgramID); //Set Program
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnbindMeshBuffers()
        {
            GL.BindBuffer(BufferTargetARB.UniformBuffer, BufferHandle.Zero); //Unbind UBOs
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindGroupBuffer(NbMeshGroup mg)
        {
            //GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mg.GroupTBO1);
            GL.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 2, mg.GroupTBO1);
        }

        #region RENDERING

        public void RenderQuad(NbMesh quadMesh, NbShader shader, NbShaderState state)
        {
            GLInstancedMesh glmesh = MeshMap[quadMesh.ID];

            GL.UseProgram(shader.ProgramID);
            GL.BindVertexArray(glmesh.vao.vao);

            //Filter State
            shader.FilterState(ref state);
            
            int sampler_id = 0;
            foreach (KeyValuePair<string, object> sstate in state.Data)
            {
                string prefix = sstate.Key.Split(":")[0];
                string key = sstate.Key.Split(":")[1];

                switch (prefix)
                {
                    case "Sampler":
                        {
                            //Upload sampler
                            NbSampler nbSampler = (NbSampler) sstate.Value;
                            GL.Uniform1i(shader.uniformLocations[key].loc, nbSampler.SamplerID);
                            GL.ActiveTexture((TextureUnit) ((int) TextureUnit.Texture0 + sampler_id));
                            GL.BindTexture(TextureTargetMap[nbSampler.Texture.Data.target], nbSampler.Texture.texID);
                            sampler_id++;
                            break;
                        }
                    case "Float":
                        {
                            float val = (float) sstate.Value;
                            GL.Uniform1f(shader.uniformLocations[key].loc, val);
                            break;
                        }
                    case "Vec2":
                        {
                            NbVector2 vec = (NbVector2) sstate.Value;
                            GL.Uniform2f(shader.uniformLocations[key].loc, vec.X, vec.Y);
                            break;
                        }
                    case "Vec3":
                        {
                            NbVector3 vec = (NbVector3) sstate.Value;
                            GL.Uniform3f(shader.uniformLocations[key].loc, vec.X, vec.Y, vec.Z);
                            break;
                        }
                    case "Vec4":
                        {
                            NbVector4 vec = (NbVector4) sstate.Value;
                            GL.Uniform4f(shader.uniformLocations[key].loc, vec.X, vec.Y, vec.Z, vec.W);
                            break;
                        }

                }

            }

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, (IntPtr)0);
            GL.BindVertexArray(VertexArrayHandle.Zero);
            GL.Enable(EnableCap.DepthTest);

        }

        public void RenderMesh(NbMesh mesh)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            GL.BindVertexArray(glmesh.vao.vao);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount);
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderMesh(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat); //Set Shader and material uniforms

            GL.BindVertexArray(glmesh.vao.vao);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                glmesh.Mesh.InstanceCount);
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderLocator(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            GL.BindVertexArray(glmesh.vao.vao);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount); //Use Instancing
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderJoint(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            GL.BindVertexArray(glmesh.vao.vao);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.MetaData.BatchCount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderCollision(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(glmesh.vao.vao);

            //TODO: make sure that primitive collisions have the vertrstartphysics set to 0

            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, glmesh.Mesh.MetaData.BatchCount,
                DrawElementsType.UnsignedShort, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartGraphics);
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount,
                DrawElementsType.UnsignedShort, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartGraphics);

            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderLight(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.InstanceIndexBuffer[0] * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferTargetARB.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            GL.BindVertexArray(glmesh.vao.vao);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.InstanceCount);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.InstanceCount); //Draw both points
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }

        public void RenderLightVolume(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];
            RenderMesh(glmesh.Mesh, mat);
        }


        public void renderBBoxes(GLInstancedMesh mesh, int pass)
        {
            for (int i = 0; i > mesh.Mesh.InstanceCount; i++)
            {
                renderBbox(mesh.Mesh, i);
            }
        }

        public void renderBbox(NbMesh mesh, int instanceID)
        {
            if (mesh == null)
                return;

            NbVector4[] tr_AABB = new NbVector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new NbVector4(mesh.MetaData.AABBMIN, 1.0f);
            tr_AABB[1] = new NbVector4(mesh.MetaData.AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            BufferHandle vb_bbox = GL.GenBuffer();
            BufferHandle eb_bbox = GL.GenBuffer();

            //Upload vertex buffer
            GL.BindBuffer(BufferTargetARB.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTargetARB.ArrayBuffer, (IntPtr)(2 * arraysize), IntPtr.Zero, BufferUsageARB.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTargetARB.ArrayBuffer, IntPtr.Zero, verts1);

            ////Upload index buffer
            GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

            //Render
            GL.BindBuffer(BufferTargetARB.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, (uint) verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }

        public static void renderBHull(GLInstancedMesh mesh)
        {
            if (mesh.bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.bHullVao.vao);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength,
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength,
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.BindVertexArray(VertexArrayHandle.Zero);
        }


        #endregion


        #region TextureMethods

        public static NbTexture CreateTexture(InternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, bool generate_mipmaps)
        {
            NbTexture tex = new();
            tex.texID = GL.GenTexture();
            tex.Data = new NbTextureData()
            {
                Width = w,
                Height = h,
                target = NbTextureTarget.Texture2D
            };

            GL.BindTexture(TextureTargetMap[tex.Data.target], tex.texID);
            GL.TexImage2D(TextureTargetMap[tex.Data.target], 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);

            if (generate_mipmaps)
                GL.GenerateMipmap(TextureTarget.Texture2d);

            return tex;
        }

        public static NbTexture CreateTexture(InternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, bool generate_mipmaps)
        {
            NbTexture tex = new();
            tex.texID = GL.GenTexture();
            tex.Data.target = NbTextureTarget.Texture2DArray;
            GL.BindTexture(TextureTargetMap[tex.Data.target], tex.texID);
            GL.TexImage3D(TextureTargetMap[tex.Data.target], 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);

            if (generate_mipmaps)
                GL.GenerateMipmap(TextureTarget.Texture2dArray);

            return tex;
        }

        public static void setupTextureParameters(NbTexture tex, NbTextureWrapMode wrapMode, NbTextureFilter magFilter, NbTextureFilter minFilter, float af_amount)
        {
            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            GL.BindTexture(gl_target, tex.texID);
            GL.TexParameteri(gl_target, TextureParameterName.TextureWrapS, TextureWrapMap[wrapMode]);
            GL.TexParameteri(gl_target, TextureParameterName.TextureWrapT, TextureWrapMap[wrapMode]);
            
            if (magFilter == NbTextureFilter.NearestMipmapLinear || magFilter == NbTextureFilter.LinearMipmapNearest)
                Log($"Non compatible mag filter {magFilter}. No Mag filter used", LogVerbosityLevel.WARNING);
            else
                GL.TexParameteri(gl_target, TextureParameterName.TextureMagFilter, TextureFilterMap[magFilter]);
            
            GL.TexParameteri(gl_target, TextureParameterName.TextureMinFilter, TextureFilterMap[minFilter]);

            //Use anisotropic filtering

            float temp_af_amount = 0.0f;
            GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy, ref temp_af_amount);
            af_amount = System.Math.Max(temp_af_amount, af_amount);
            
            GL.TexParameterf(gl_target, (TextureParameterName)0x84FE, af_amount);
        }

        public static void DumpTexture(NbTexture tex, string name)
        {
            var pixels = new byte[4 * tex.Data.Width * tex.Data.Height];
            GL.BindTexture(TextureTargetMap[tex.Data.target], tex.texID);
            GL.GetTexImage(TextureTargetMap[tex.Data.target], 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            NbImagingAPI.ImageSave(tex.Data, "Temp//framebuffer_raw_" + name + ".png");
        }

        public static void GenerateTexture(NbTexture tex)
        {
            //Upload to GPU
            tex.texID = GL.GenTexture();

            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            InternalFormat gl_pif = InternalFormatMap[tex.Data.pif];

            int mm_count = System.Math.Max(tex.Data.MipMapCount, 1);

            GL.BindTexture(gl_target, tex.texID);
            //TODO: Remove all parameter settings from here, and make it possible to set them using other API calls
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameteri(gl_target, TextureParameterName.TextureBaseLevel, 0);


            //Use anisotropic filtering
            float af_amount = 0.0f;
            GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy, ref af_amount);
            af_amount = (float)System.Math.Max(af_amount, 4.0f);

            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            int max_level = 0;
            int base_level = 0;
            GL.GetTexParameteri(gl_target, GetTextureParameter.TextureMaxLevelSgis, ref max_level);
            GL.GetTexParameteri(gl_target, GetTextureParameter.TextureBaseLevelSgis, ref base_level);

            int maxsize = System.Math.Max(tex.Data.Height, tex.Data.Width);
            int p = (int)System.Math.Floor(System.Math.Log(maxsize, 2)) + base_level;
            int q = System.Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
        }

        private static void UploadTextureData(TextureHandle tex_id, NbTextureData tex_data)
        {
            TextureTarget gl_target = TextureTargetMap[tex_data.target];
            InternalFormat gl_pif = InternalFormatMap[tex_data.pif];
            
            GL.BindTexture(gl_target, tex_id);
            GL.TexImage2D(gl_target, 0, gl_pif, tex_data.Width, tex_data.Height, 0,
                    PixelFormatMap[tex_data.pif], PixelType.UnsignedByte, tex_data.Data);
            GL.GenerateTextureMipmap(tex_id);

            //Cleanup
            GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, new BufferHandle(0)); //Unbind texture PBO
        }


        private static void UploadTextureData(TextureHandle tex_id, DDSImage tex_data)
        {
            //Temp Variables
            int w = tex_data.Width;
            int h = tex_data.Height;
            int d = System.Math.Max(1, tex_data.Depth);
            int mm_count = System.Math.Max(1, tex_data.MipMapCount); //Fix the counter to 1 to handle textures with single mipmaps
            int face_count = tex_data.Faces;

            TextureTarget gl_target = TextureTargetMap[tex_data.target];
            InternalFormat gl_pif = InternalFormatMap[tex_data.pif];
            GL.BindTexture(gl_target, tex_id);
            
            int offset = 0;
            for (int i = 0; i < 1; i++)
            {
                int temp_size = 0;


                if (tex_data.header.ddspf.dwFlags.HasFlag(DDS_PIXELFORMAT_DWFLAGS.DDPF_RGB))
                {
                    //Calculate size of the top layer
                    temp_size = w * h * d * 4;
                    byte[] temp_data = new byte[temp_size];
                    System.Buffer.BlockCopy(tex_data.Data, offset, temp_data, 0, temp_size);
                        
                    //Figure out channel ordering
                    PixelFormat pf = PixelFormat.Rgba;
                    InternalFormat pif = InternalFormat.Rgba8;
                    if (tex_data.header.ddspf.dwRBitMask == 0x000000FF &&
                        tex_data.header.ddspf.dwGBitMask == 0x0000FF00 &&
                        tex_data.header.ddspf.dwBBitMask == 0x00FF0000 &&
                        tex_data.header.ddspf.dwABitMask == 0xFF000000)
                    {
                        pf = PixelFormat.Rgba;
                    }
                    else if (tex_data.header.ddspf.dwRBitMask == 0x00FF0000 &&
                        tex_data.header.ddspf.dwGBitMask == 0x0000FF00 &&
                        tex_data.header.ddspf.dwBBitMask == 0x000000FF &&
                        tex_data.header.ddspf.dwABitMask == 0xFF000000)
                    {
                        pf = PixelFormat.Bgra;
                    }
                    
                    switch (gl_target)
                    {
                        case TextureTarget.Texture2d:
                            GL.TexImage2D(gl_target, i, pif, w, h, 0, pf, PixelType.UnsignedByte, temp_data);
                            break;
                        case TextureTarget.Texture2dArray:
                            GL.TexImage3D(gl_target, i, pif, w, h, d, 0, pf, PixelType.UnsignedByte, temp_data);
                            break;
                    }


                } else 
                {
                    //Calculate size of the top layer
                    temp_size = w * h * d * face_count * tex_data.blockSize / 16;
                    byte[] temp_data = new byte[temp_size];
                    System.Buffer.BlockCopy(tex_data.Data, offset, temp_data, 0, temp_size);
                    switch (gl_target)
                    {
                        case TextureTarget.Texture3d:
                            //GL.CompressedTexImage3D(gl_target, i, InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext, w, h, d, 0, temp_size, temp_data);
                            break;
                        case TextureTarget.Texture2dArray:
                            GL.CompressedTexImage3D(gl_target, i, gl_pif, w, h, d * face_count, 0, temp_data);
                            break;
                        default:
                            GL.CompressedTexImage2D(gl_target, i, gl_pif, w, h, 0, temp_data);
                            break;
                    }
                }

                offset += temp_size;

                w = System.Math.Max(w >> 1, 1);
                h = System.Math.Max(h >> 1, 1);
                d = System.Math.Max(d >> 1, 1);

                temp_size = face_count * d * System.Math.Max(1, (w + 3) / 4) * 
                            System.Math.Max(1, (h + 3) / 4) * tex_data.blockSize;

            }

            //GL.TexParameter(gl_target, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.NearestMipmapLinear);
            //GL.TexParameter(gl_target, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);

            GL.GenerateTextureMipmap(tex_id);

            //GL.GetTexParameter(gl_target, GetTextureParameter.TextureMaxLod, out int out_mm);
            //GL.TexParameter(gl_target, TextureParameterName.TextureMaxLevel, mm_count - 1);
            //GL.TextureParameter(tex_id, TextureParameterName.TextureMaxLod, mm_count - 1);

            //GL.TexParameter(gl_target, TextureParameterName.TextureMaxLevel, 1.0f);
            //This works only for square textures
            //temp_size = Math.Max(temp_size/4, blocksize);

        }

        public static void UploadTexture(NbTexture tex)
        {
            Callbacks.Assert(tex.texID.Handle >= 0, "Invalid texture ID");
            if (tex.Data is DDSImage) 
                UploadTextureData(tex.texID, tex.Data as DDSImage);
            else
                UploadTextureData(tex.texID, tex.Data);
        }

        #endregion

        //Fetch main VAO
        public static GLVao generateVAO(NbMesh mesh)
        {
            //Generate VAO
            GLVao vao = new();
            vao.vao = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao);

            //Generate VBOs
            BufferHandle[] vbo_buffers = new BufferHandle[2];
            GL.GenBuffers(vbo_buffers);
            
            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            //Bind vertex buffer
            int size = 0;
            GL.BindBuffer(BufferTargetARB.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTargetARB.ArrayBuffer, mesh.Data.VertexBuffer, BufferUsageARB.StaticDraw);
            GL.GetBufferParameteri(BufferTargetARB.ArrayBuffer, BufferPNameARB.BufferSize, ref size);

            Callbacks.Assert(size == mesh.Data.VertexBufferStride * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics + 1),
                "Mesh metadata does not match the vertex buffer size from the geometry file");

            //Assign VertexAttribPointers
            for (int i = 0; i < mesh.Data.buffers.Length; i++)
            {
                NbMeshBufferInfo buf = mesh.Data.buffers[i];
                VertexAttribPointerType buftype = VertexAttribPointerType.Float; //default
                switch (buf.type)
                {
                    case NbPrimitiveDataType.Double:
                        buftype = VertexAttribPointerType.Double;
                        break;
                    case NbPrimitiveDataType.UnsignedByte:
                        buftype = VertexAttribPointerType.UnsignedByte;
                        break;
                    case NbPrimitiveDataType.Float:
                        buftype = VertexAttribPointerType.Float;
                        break;
                    case NbPrimitiveDataType.HalfFloat:
                        buftype = VertexAttribPointerType.HalfFloat;
                        break;
                    case NbPrimitiveDataType.Int2101010Rev:
                        buftype = VertexAttribPointerType.Int2101010Rev;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                GL.VertexAttribPointer(buf.semantic, buf.count, buftype, buf.normalize, buf.stride, buf.offset);
                GL.EnableVertexArrayAttrib(vao.vao, buf.semantic);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTargetARB.ElementArrayBuffer, mesh.Data.IndexBuffer, BufferUsageARB.StaticDraw);
            
            GL.GetBufferParameteri(BufferTargetARB.ElementArrayBuffer, BufferPNameARB.BufferSize, ref size);
            Callbacks.Assert(size == mesh.Data.IndexBuffer.Length,
                "Mesh metadata does not match the index buffer size from the geometry file");

            //Unbind
            GL.BindVertexArray(VertexArrayHandle.Zero);
            //for (int i = 0; i < mesh.Data.buffers.Length; i++)
            //    GL.DisableVertexAttribArray(mesh.Data.buffers[i].semantic);
            GL.BindBuffer(BufferTargetARB.ArrayBuffer, BufferHandle.Zero);
            GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, BufferHandle.Zero);
            
            return vao;
        }

        private void UploadUniform(NbUniform uf)
        {
            switch (uf.Type)
            {
                case (NbUniformType.Float):
                    GL.Uniform1f(uf.ShaderLocation, uf.Values.X);
                    break;
                case (NbUniformType.Vector2):
                    GL.Uniform2f(uf.ShaderLocation, uf.Values._Value.Xy);
                    break;
                case (NbUniformType.Vector3):
                    GL.Uniform3f(uf.ShaderLocation, uf.Values._Value.Xyz);
                    break;
                case (NbUniformType.Vector4):
                    GL.Uniform4f(uf.ShaderLocation, uf.Values._Value);
                    break;
                default:
                    Console.WriteLine($"Unsupported Uniform {uf.Type}");
                    break;
            }
        }

        private void SetShaderAndUniforms(NbMaterial Material)
        {
            //Upload Material Information

            //Upload Custom Per Material Uniforms
            foreach (NbUniform un in Material.ActiveUniforms)
                UploadUniform(un);
                
            //BIND TEXTURES
            foreach (NbSampler s in Material.ActiveSamplers)
            {
                GL.Uniform1i(Material.Shader.uniformLocations[s.ShaderBinding].loc, s.SamplerID);
                GL.ActiveTexture(TextureUnit.Texture0 + (uint) s.SamplerID);
                GL.BindTexture(TextureTargetMap[s.Texture.Data.target], s.Texture.texID);
            }
        }
        
        public void PostRendering()
        {
            if (useMultiBuffers)
                SyncGPUCommands();
        }

        public void SyncGPUCommands()
        {
            //Setup FENCE AFTER ALL THE MAIN GEOMETRY DRAWCALLS ARE ISSUED
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
        }

        public void ClearDrawBuffer(NbBufferMask mask)
        {
            if (mask.HasFlag(NbBufferMask.Color) && mask.HasFlag(NbBufferMask.Depth))
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            else if (mask.HasFlag(NbBufferMask.Color))
                GL.Clear(ClearBufferMask.ColorBufferBit);
            else if (mask.HasFlag(NbBufferMask.Depth))
                GL.Clear(ClearBufferMask.DepthBufferBit);
        }

        public void BindDrawFrameBuffer(FBO fbo, int[] drawBuffers)
        {
            BindDrawFrameBuffer(fbo.fbo, fbo.Size.X, fbo.Size.Y, drawBuffers);
        }

        public void BindDrawFrameBuffer(FramebufferHandle fbo, int size_x, int size_y, int[] drawBuffers)
        {
            //Bind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.Viewport(0, 0, size_x, size_y);

            DrawBufferMode[] bufferEnums = new DrawBufferMode[drawBuffers.Length];

            for (int i = 0; i < drawBuffers.Length; i++)
                bufferEnums[i] = (DrawBufferMode) ((int) DrawBufferMode.ColorAttachment0 + drawBuffers[i]);

            GL.DrawBuffers(bufferEnums);
        }

        
        public void BindFrameBuffer(FramebufferHandle fbo_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo_id);
        }

        public void SetViewPort(int x, int y, int w, int h)
        {
            GL.Viewport(x, y, w, h);
        }


        public FBO CreateFrameBuffer(int w, int h)
        {
            FBO fbo = new(w,h);

            return fbo;
        }

        #region ShaderMethods

        public static Dictionary<NbShaderSourceType, ShaderType> ShaderTypeMap = new()
        {
            { NbShaderSourceType.VertexShader, ShaderType.VertexShader },
            { NbShaderSourceType.FragmentShader, ShaderType.FragmentShader },
            { NbShaderSourceType.GeometryShader, ShaderType.GeometryShader },
            { NbShaderSourceType.TessControlShader, ShaderType.TessControlShader },
            { NbShaderSourceType.TessEvaluationShader, ShaderType.TessEvaluationShader },
            { NbShaderSourceType.ComputeShader, ShaderType.ComputeShader },
        };

        

        //This method attaches UBOs to shader binding points
        public static void AttachUBOToShaderBindingPoint(NbShader shader, string var_name, uint binding_point)
        {
            //int shdr_program_id = shader.ProgramID;
            uint ubo_index = GL.GetUniformBlockIndex(shader.ProgramID, var_name);
            if (ubo_index != 0)
                GL.UniformBlockBinding(shader.ProgramID, ubo_index, binding_point);
        }

        public static void AttachSSBOToShaderBindingPoint(NbShader shader, string var_name, uint binding_point)
        {
            //Binding Position 0 - Matrices UBO
            //int shdr_program_id = shader.ProgramID;
            uint ssbo_index = GL.GetProgramResourceIndex(shader.ProgramID, ProgramInterface.ShaderStorageBlock, var_name);
            if (ssbo_index != 0)
                GL.ShaderStorageBlockBinding(shader.ProgramID, ssbo_index, binding_point);
        }

        private static string NumberLines(string s)
        {
            if (s == "")
                return s;

            string n_s = "";
            string[] split = s.Split('\n');

            for (int i = 0; i < split.Length; i++)
                n_s += (i + 1).ToString() + ": " + split[i] + "\n";

            return n_s;
        }

        public static bool CompileShaderSource(NbShader shader, 
            NbShaderSourceType type, ref ShaderHandle object_id, ref string temp_log, string append_text = "")
        {
            GLSLShaderSource _source = shader.GetShaderConfig().Sources[type];

            if (_source == null)
                return false;

            if (!_source.Resolved)
                _source.Resolve();

            ShaderHandle shader_object_id;
            string ActualShaderSource;
            shader_object_id = GL.CreateShader(ShaderTypeMap[type]);
            
            //Compile Shader
            GL.ShaderSource(shader_object_id, GLSLShaderConfig.version + "\n" + append_text + "\n" + _source.ResolvedText);

            //Get resolved shader text
            int actual_shader_length = 0;
            GL.GetShaderSource(shader_object_id, 32768, ref actual_shader_length, out ActualShaderSource);
            
            GL.CompileShader(shader_object_id);
            GL.GetShaderInfoLog(shader_object_id, out string info);

            int status_code = -1;
            GL.GetShaderi(shader_object_id, ShaderParameterName.CompileStatus, ref status_code);
            
            temp_log += NumberLines(ActualShaderSource) + "\n";
            temp_log += info + "\n";

            if (status_code != 1)
            {
                object_id.Handle = -1;
                return false;
            }
            
            object_id = shader_object_id;
            return true;
        }

        //Shader Creation
        public static bool CompileShader(NbShader shader)
        {
            GLSLShaderConfig conf = shader.GetShaderConfig();
            bool gsflag = conf.Sources.ContainsKey(NbShaderSourceType.GeometryShader);

            bool tsflag = conf.Sources.ContainsKey(NbShaderSourceType.TessControlShader) |
                     conf.Sources.ContainsKey(NbShaderSourceType.TessControlShader);

            List<string> finaldirectives = new();
            finaldirectives.AddRange(RenderState.engineRef.CreateShaderDirectivesFromMode(conf.ShaderMode));
            finaldirectives.AddRange(shader.directives);
            
            //Generate DirectiveString
            string directivestring = "";
            foreach (string dir in finaldirectives)
                directivestring += "#define " + dir + '\n'; //Configuration Directives (through the mode)

            //Try to compile all attachments
            Dictionary<NbShaderSourceType, ShaderHandle> temp_objects = new();
            string temp_log = "";
            foreach (var pair in conf.Sources)
            {
                ShaderHandle object_id = ShaderHandle.Zero;
                bool status = CompileShaderSource(shader, pair.Key, ref object_id, ref temp_log, directivestring);
                if (!status)
                {
                    Log("Error During Shader Compilation", 
                        LogVerbosityLevel.ERROR);
                    Log(temp_log, LogVerbosityLevel.ERROR); //Show only in console
                    return false;
                }
                
                temp_objects[pair.Key] = object_id;
            }

            shader.CompilationLog = temp_log;
            
            //Save Objects to shader
            foreach (var pair in conf.Sources)
            {
                //TODO: Try to first compile the shader and return if there are problems
                if (shader.SourceObjects.ContainsKey(pair.Key))
                    if (shader.SourceObjects[pair.Key].Handle != 0)
                        GL.DeleteShader(shader.SourceObjects[pair.Key]);

                shader.SourceObjects[pair.Key] = temp_objects[pair.Key];
            }

            if (shader.ProgramID.Handle != -1)
                GL.DeleteProgram(shader.ProgramID);

            //Create new program
            shader.ProgramID = GL.CreateProgram();

            //Attach shaders to program
            foreach (var pair in shader.SourceObjects)
                GL.AttachShader(shader.ProgramID, pair.Value);
            
            GL.LinkProgram(shader.ProgramID);
            
            //Check Linking
            GL.GetProgramInfoLog(shader.ProgramID, out string info);
            shader.CompilationLog += info + "\n";

            int status_code = -1;
            GL.GetProgrami(shader.ProgramID, ProgramPropertyARB.LinkStatus, ref status_code);
            if (status_code != 1)
            {
                Log(shader.CompilationLog, LogVerbosityLevel.ERROR);
                Callbacks.Assert(false, "Shader Compilation Error");
            }

            ShaderCompilationLog(shader);
            loadActiveUniforms(shader);

            //Attach UBO binding Points
            AttachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            AttachUBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            AttachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESHGROUP", 2);

            return true;
        }

        public static void ShaderCompilationLog(NbShader shader)
        {
            string log_file = "shader_compilation_log.out";

            if (!System.IO.File.Exists(log_file))
                System.IO.File.Create(log_file).Close();

            while (!Utils.FileUtils.IsFileReady(log_file))
            {
                Log("Log File not ready yet", LogVerbosityLevel.WARNING);
            };

            System.IO.StreamWriter sr = new System.IO.StreamWriter(log_file, true);
            sr.WriteLine("### COMPILING SHADER " + shader.Hash + "###");
            sr.Write(shader.CompilationLog);
            sr.Close();
            //Console.WriteLine(conf.log);
        }

        private static void loadActiveUniforms(NbShader shader)
        {
            int active_uniforms_count = 0;
            GL.GetProgrami(shader.ProgramID, ProgramPropertyARB.ActiveUniforms, ref active_uniforms_count);
            
            shader.uniformLocations.Clear(); //Reset locataions
            shader.CompilationLog += "Active Uniforms: " + active_uniforms_count.ToString() + "\n";
            for (int i = 0; i < active_uniforms_count; i++)
            {
                int bufSize = 64;
                int loc;
                int size = 0;
                int length = 0;
                UniformType type = UniformType.Int;

                string name = GL.GetActiveUniform(shader.ProgramID, (uint) i, bufSize, ref size, ref length, ref type);
                string basename = name.Split("[0]")[0];
                for (int j = 0; j < length; j++)
                {
                    if (j > 0)
                        name = basename + "[" + j + "]";
                    
                    NbUniformFormat fmt = new();
                    fmt.name = name;
                    fmt.loc = GL.GetUniformLocation(shader.ProgramID, name);
                    
                    switch (type)
                    {
                        case UniformType.Float:
                            fmt.type = NbUniformType.Float;
                            break;
                        case UniformType.Bool:
                            fmt.type = NbUniformType.Bool;
                            break;
                        case UniformType.Int:
                            fmt.type = NbUniformType.Int;
                            break;
                        case UniformType.FloatMat3:
                            fmt.type = NbUniformType.Matrix3;
                            break;
                        case UniformType.FloatMat4:
                            fmt.type = NbUniformType.Matrix4;
                            break;
                        case UniformType.FloatVec2:
                            fmt.type = NbUniformType.Vector2;
                            break;
                        case UniformType.FloatVec3:
                            fmt.type = NbUniformType.Vector3;
                            break;
                        case UniformType.FloatVec4:
                            fmt.type = NbUniformType.Vector4;
                            break;
                        case UniformType.Sampler2d:
                            fmt.type = NbUniformType.Sampler2D;
                            break;
                        case UniformType.Sampler3d:
                            fmt.type = NbUniformType.Sampler3D;
                            break;
                        case UniformType.Sampler2dArray:
                            fmt.type = NbUniformType.Sampler2DArray;
                            break;
                        default:
                            throw new Exception("Unidentified uniform format. Inform dev");
                    }

                    fmt.count = 1;
                    shader.uniformLocations[name] = fmt; //Store location

                    if (RenderState.enableShaderCompilationLog)
                    {
                        string info_string = $"Uniform # {i} Location: {fmt.loc} Type: {type.ToString()} Name: {name}";
                        shader.CompilationLog += info_string + "\n";
                    }
                }
            }
        }

        public void ShaderReport(NbShader shader)
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int block_count = 0;
            GL.GetProgrami(shader.ProgramID, ProgramPropertyARB.ActiveUniformBlocks, ref block_count);

            for (int i = 0; i < block_count; ++i)
            {
                // Get blocks name
                int length = 0;
                string block_name = "";
                int block_size = 0;
                int block_bind_index = 0;
                int block_active_uniforms = 0;
                
                block_name = GL.GetActiveUniformBlockName(shader.ProgramID, (uint) i, 256, ref length);
                GL.GetActiveUniformBlocki(shader.ProgramID, (uint)i, UniformBlockPName.UniformBlockDataSize, ref block_size);
                Console.WriteLine("Block {0} Data Size {1}", block_name, block_size);
                
                GL.GetActiveUniformBlocki(shader.ProgramID, (uint) i, UniformBlockPName.UniformBlockBinding, ref block_bind_index);
                Console.WriteLine("    Block Binding Point {0}", block_bind_index);


                int info = -1;
                GL.GetInteger(GetPName.UniformBufferBinding, (uint) block_bind_index, ref info);
                Console.WriteLine("    Block Bound to Binding Point: {0} {{", info);

                GL.GetActiveUniformBlocki(shader.ProgramID, (uint)i, UniformBlockPName.UniformBlockActiveUniforms, ref block_active_uniforms);
                int[] uniform_indices = new int[block_active_uniforms];
                GL.GetActiveUniformBlocki(shader.ProgramID, (uint)i, UniformBlockPName.UniformBlockActiveUniformIndices, uniform_indices);

                int[] uniform_types = new int[block_active_uniforms];
                int[] uniform_offsets = new int[block_active_uniforms];
                int[] uniform_sizes = new int[block_active_uniforms];

                //Fetch Parameters for all active Uniforms
                GL.GetActiveUniformsi(shader.ProgramID, (uint[]) (object) uniform_indices, UniformPName.UniformType, uniform_types);
                GL.GetActiveUniformsi(shader.ProgramID, (uint[]) (object) uniform_indices, UniformPName.UniformOffset, uniform_offsets);
                GL.GetActiveUniformsi(shader.ProgramID, (uint[]) (object) uniform_indices, UniformPName.UniformSize, uniform_sizes);

                for (int k = 0; k < block_active_uniforms; ++k)
                {
                    int actual_name_length = 0;
                    string name = GL.GetActiveUniformName(shader.ProgramID, (uint) uniform_indices[k], 256, ref actual_name_length);
                    
                    Console.WriteLine("\t{0}", name);
                    Console.WriteLine("\t\t    type: {0}", uniform_types[k]);
                    Console.WriteLine("\t\t    offset: {0}", uniform_offsets[k]);
                    Console.WriteLine("\t\t    size: {0}", uniform_sizes[k]);

                    /*
                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformArrayStride, out uniArrayStride);
                    Console.WriteLine("\t\t    array stride: {0}", uniArrayStride);

                    GL.GetActiveUniforms(test_program, i, ref uniform_indices[k], ActiveUniformParameter.UniformMatrixStride, out uniMatStride);
                    Console.WriteLine("\t\t    matrix stride: {0}", uniMatStride);
                    */
                }
                Console.WriteLine("}}");
            }

        }

        public void AddRenderInstance(ref MeshComponent mc, TransformData td)
        {
            GLMeshBufferManager.AddRenderInstance(ref mc, td);
            GLMeshInstanceManager.AddMeshInstance(ref mc.Mesh, mc.InstanceID);
        }

        public void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            GLMeshInstanceManager.RemoveMeshInstance(ref mesh, mc.InstanceID);
            GLMeshBufferManager.RemoveRenderInstance(ref mesh, mc);
            mc.InstanceID = -1;
        }

        public void UpdateInstance(NbMesh mesh, int index)
        {
            GLMeshInstanceManager.UpdateMeshInstance(ref mesh, index);
        }

        public void AddLightRenderInstance(ref LightComponent lc, TransformData td)
        {
            GLLightBufferManager.AddRenderInstance(ref lc, td);
            GLMeshInstanceManager.AddMeshInstance(ref lc.Mesh, lc.InstanceID);
        }

        public void SetLightInstanceData(LightComponent lc)
        {
            GLLightBufferManager.SetLightInstanceData(lc);
        }

        public void RemoveLightRenderInstance(ref NbMesh mesh, LightComponent lc)
        {
            GLMeshInstanceManager.RemoveMeshInstance(ref mesh, lc.InstanceID);
            GLLightBufferManager.RemoveRenderInstance(ref mesh, lc);
            lc.InstanceID = -1;
        }

        public void SetInstanceWorldMat(NbMesh mesh, int instanceID, NbMatrix4 mat)
        {
            GLMeshBufferManager.SetInstanceWorldMat(mesh, instanceID, mat);
        }

        public void SetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID, NbVector4 uf)
        {
            GLMeshBufferManager.SetInstanceUniform4(mesh, instanceID, uniformID, uf);
        }

        public NbVector4 GetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID)
        {
            return GLMeshBufferManager.GetInstanceUniform4(mesh, instanceID, uniformID);
        }

        public void SetInstanceWorldMatInv(NbMesh mesh, int instanceID, NbMatrix4 mat)
        {
            GLMeshBufferManager.SetInstanceWorldMatInv(mesh, instanceID, mat);
        }

        #endregion


        #region TextureMethods

        
        #endregion

    }
}

#endif