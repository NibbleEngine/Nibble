#if (OPENGL)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using NbCore;
using NbCore.Systems;
using NbCore.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.CodeAnalysis;

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
        public Vector2 frameDim; //Frame Dimensions
        [FieldOffset(24)]
        public float cameraNearPlane;
        [FieldOffset(28)]
        public float cameraFarPlane;
        [FieldOffset(32)]
        public Matrix4 rotMat;
        [FieldOffset(96)]
        public Matrix4 rotMatInv;
        [FieldOffset(160)]
        public Matrix4 projMat;
        [FieldOffset(224)]
        public Matrix4 projMatInv;
        [FieldOffset(288)]
        public Matrix4 lookMat;
        [FieldOffset(352)]
        public Matrix4 lookMatInv;
        [FieldOffset(416)]
        public Matrix4 cameraRotMat;
        [FieldOffset(480)]
        public Vector4 cameraPositionExposure; //Exposure is the W component
        [FieldOffset(496)]
        public Vector3 cameraDirection;
        [FieldOffset(508)]
        public int light_number;
        public static readonly int SizeInBytes = 512;
    };

    public enum NbBufferMask
    {
        Color,
        Depth,
        Stencil
    }

    public class GraphicsAPI
    {
        private const string RendererName = "OPENGL_RENDERER";
        private int activeProgramID = -1;
        public Dictionary<ulong, GLInstancedMesh> MeshMap = new();
        public Dictionary<ulong, GLVao> VaoMap = new();
        private readonly Dictionary<string, int> UBOs = new();
        private int SSBO_size = 2 * 1024 * 1024;
        private readonly Dictionary<string, int> SSBOs = new();

        private bool useMultiBuffers = false;
        private int multiBufferActiveId;
        private readonly List<int> multiBufferSSBOs = new(4);
        private readonly List<IntPtr> multiBufferSyncStatuses = new(4);


        public static readonly Dictionary<NbFBOAttachment, FramebufferAttachment> FramebufferAttachmentMap = new()
        {
            { NbFBOAttachment.Attachment0 , FramebufferAttachment.ColorAttachment0},
            { NbFBOAttachment.Attachment1 , FramebufferAttachment.ColorAttachment1},
            { NbFBOAttachment.Attachment2 , FramebufferAttachment.ColorAttachment2},
            { NbFBOAttachment.Attachment3 , FramebufferAttachment.ColorAttachment3},
            { NbFBOAttachment.Attachment4 , FramebufferAttachment.ColorAttachment4},
            { NbFBOAttachment.Attachment5 , FramebufferAttachment.ColorAttachment5},
            { NbFBOAttachment.Depth , FramebufferAttachment.DepthAttachment},
            { NbFBOAttachment.DepthStencil , FramebufferAttachment.DepthStencilAttachment}
    };

        public static readonly Dictionary<NbTextureTarget, TextureTarget> TextureTargetMap = new()
        {
            { NbTextureTarget.Texture1D , TextureTarget.Texture1D},
            { NbTextureTarget.Texture2D , TextureTarget.Texture2D},
            { NbTextureTarget.Texture3D, TextureTarget.Texture3D },
            { NbTextureTarget.Texture2DArray , TextureTarget.Texture2DArray},
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
            { NbTextureInternalFormat.DXT3, InternalFormat.CompressedSrgbAlphaS3tcDxt3Ext },
            { NbTextureInternalFormat.DXT5, InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext },
            { NbTextureInternalFormat.RGTC2, InternalFormat.CompressedRgRgtc2 },
            { NbTextureInternalFormat.BC7, InternalFormat.CompressedSrgbAlphaBptcUnorm },
            { NbTextureInternalFormat.RGBA8, InternalFormat.Rgba8},
            { NbTextureInternalFormat.SRGBA8, InternalFormat.Srgb8Alpha8},
            { NbTextureInternalFormat.SRGB8, InternalFormat.Srgb8},
            { NbTextureInternalFormat.BGRA8, InternalFormat.Rgba8},
            { NbTextureInternalFormat.RGBA16F, InternalFormat.Rgba16f},
            { NbTextureInternalFormat.RGBA32F, InternalFormat.Rgba32f},
            { NbTextureInternalFormat.DEPTH, InternalFormat.DepthComponent},
            { NbTextureInternalFormat.DEPTH24_STENCIL8, InternalFormat.Depth24Stencil8}

        };

        public static readonly Dictionary<NbTextureInternalFormat, PixelType> PixelTypeMap = new()
        {
            { NbTextureInternalFormat.RGBA8, PixelType.UnsignedByte},
            { NbTextureInternalFormat.SRGBA8, PixelType.UnsignedByte},
            { NbTextureInternalFormat.BGRA8, PixelType.UnsignedByte},
            { NbTextureInternalFormat.RGBA16F, PixelType.Float},
            { NbTextureInternalFormat.RGBA32F, PixelType.Float},
            { NbTextureInternalFormat.DEPTH, PixelType.UnsignedByte },
            { NbTextureInternalFormat.DEPTH24_STENCIL8, PixelType.UnsignedInt248 }
        };

        public static readonly Dictionary<NbTextureInternalFormat, PixelFormat> PixelFormatMap = new()
        {
            {NbTextureInternalFormat.RGBA8, PixelFormat.Rgba },
            {NbTextureInternalFormat.SRGBA8, PixelFormat.Rgba },
            {NbTextureInternalFormat.BGRA8, PixelFormat.Bgra },
            {NbTextureInternalFormat.RGBA16F, PixelFormat.Rgba },
            {NbTextureInternalFormat.DEPTH, PixelFormat.DepthComponent },
            {NbTextureInternalFormat.DEPTH24_STENCIL8, PixelFormat.DepthStencil }
        };
        
        //UBO structs
        CommonPerFrameUniforms cpfu;
        

        private const int MAX_NUMBER_OF_MESHES = 2000;
        private const int MULTI_BUFFER_COUNT = 3;

        private DebugProc GLDebug;

        private static void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(RendererName.ToUpper(), msg, lvl);
        }


        private void GLDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
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

#if (OPENGL)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GLDebug = new DebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DontCare, 0, new int[] { 0 }, true);

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");

            //Query GL Extensions
            Log("OPENGL AVAILABLE EXTENSIONS:", LogVerbosityLevel.INFO);
            string[] ext = GL.GetString(StringNameIndexed.Extensions, 0).Split(' ');
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
            Log($"MaxUniformBlock Size {GL.GetInteger(GetPName.MaxUniformBlockSize)}", LogVerbosityLevel.INFO);
#endif

            //Default Enables
            SetBlend(true);
            
            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs

            setupSSBOs(SSBO_size); //Init SSBOs to 2MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];

        }

        public void SetProgram(int program_id)
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
                int ssbo_id = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
                GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size,
                    IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit);
                //GL.BufferData(BufferTarget.UniformBuffer, atlas_ubo_buffer_size, IntPtr.Zero, BufferUsageHint.StreamDraw); //FOR OLD METHOD
                multiBufferSSBOs.Add(ssbo_id);
                multiBufferSyncStatuses.Add(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0));
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            GL.Flush();
        }

        private void deleteSSBOs()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
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
            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo_id;

            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }

        public int CreateGroupBuffer()
        {
            int size = 512 * 16 * 4; //FIXED SIZE FOR NOW
            int ssbo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
            GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size,
                IntPtr.Zero, BufferStorageFlags.DynamicStorageBit);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, ssbo_id);

            return ssbo_id;
        }

        public void DestroyGroupBuffer(int id)
        {
            GL.DeleteBuffer(id);
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

            WaitSyncStatus result = WaitSyncStatus.WaitFailed;
#if DEBUG
            System.Diagnostics.Stopwatch timer = new();
            timer.Start();
#endif
            while (result == WaitSyncStatus.TimeoutExpired || result == WaitSyncStatus.WaitFailed)
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
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);

            //Prepare UBO data
            int max_ubo_offset = NbMeshInstanceManager.GetAtlasSize();

            //METHOD 2: Use MAP Buffer
            IntPtr ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                 max_ubo_offset, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            if (SSBO_size < NbMeshInstanceManager.GetAtlasSize())
            {
                int new_size = NbMeshInstanceManager.GetAtlasSize();

                //Unmap and unbind buffer
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

                resizeSSBOs(new_size);

                //Remap and rebind buffer at the current index
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);
                ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                new_size, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);
            }
            
            unsafe
            {
                GCHandle handle = GCHandle.Alloc(NbMeshInstanceManager.atlas_cpmu, GCHandleType.Pinned);
                IntPtr handlePtr = handle.AddrOfPinnedObject();
                System.Buffer.MemoryCopy(handlePtr.ToPointer(), ptr.ToPointer(), max_ubo_offset, max_ubo_offset);
            }

            GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        }

        public void UploadFrameData()
        {
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        public void SetRenderSettings(RenderSettings settings)
        {
            //Prepare Struct
            cpfu.diffuseFlag = (settings.UseTextures) ? 1.0f : 0.0f;
            cpfu.use_lighting = (settings.UseLighting) ? 1.0f : 0.0f;
            cpfu.cameraPositionExposure.W = settings.HDRExposure;
        }

        public void SetCameraData(Camera cam)
        {
            cpfu.projMat = cam.projMat._Value;
            cpfu.projMatInv = cam.projMatInv._Value;
            cpfu.lookMat = cam.lookMat._Value;
            cpfu.lookMatInv = cam.lookMatInv._Value;
            cpfu.cameraRotMat = cam.cameraRotMat._Value;
            cpfu.cameraPositionExposure.Xyz = cam.Position._Value;
            cpfu.cameraDirection = cam.Front._Value;
            cpfu.cameraNearPlane = RenderState.settings.CamSettings.zNear;
            cpfu.cameraFarPlane = RenderState.settings.CamSettings.zFar;
        }

        public void SetCommonDataPerFrame(FBO gBuffer, NbMatrix4 rotMat, double time)
        {
            cpfu.frameDim.X = gBuffer.Size.X;
            cpfu.frameDim.Y = gBuffer.Size.Y;
            cpfu.rotMat = rotMat._Value;
            cpfu.rotMatInv = Matrix4.Transpose(rotMat._Value.Inverted());
            cpfu.gfTime = (float)time;
            cpfu.MSAA_SAMPLES = gBuffer.msaa_samples;
        }

        public void SetCullFace(bool status)
        {
            if (status)
                GL.Enable(EnableCap.CullFace);
            else
                GL.Disable(EnableCap.CullFace);
        }

        public void SetDepthTest(bool status)
        {
            if (status)
                GL.Enable(EnableCap.DepthTest);
            else
                GL.Disable(EnableCap.DepthTest);
        }

        public void SetBlend(bool status)
        {
            if (status)
            {
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            } else
            {
                GL.Disable(EnableCap.Blend);
            }
        }

        public void ClearColor(NbVector4 vec)
        {
            GL.ClearColor(vec.X, vec.Y, vec.Z, vec.W);
        }

        public void ClearColor(NbVector3 vec)
        {
            GL.ClearColor(vec.X, vec.Y, vec.Z, 1.0f);
        }

        public void ClearColor(float x, float y, float z, float w)
        {
            GL.ClearColor(x, y, z, w);
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
            GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindGroupBuffer(NbMeshGroup mg)
        {
            //GL.BindBuffer(BufferTarget.ShaderStorageBuffer, mg.GroupTBO1);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, mg.GroupTBO1);
        }

        #region RENDERING

        public void RenderQuad(NbMesh quadMesh, NbShader shader, NbShaderState state)
        {
            GLInstancedMesh glmesh = MeshMap[quadMesh.ID];

            GL.UseProgram(shader.ProgramID);
            GL.BindVertexArray(glmesh.vao.vao_id);

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
                            GL.Uniform1(shader.uniformLocations[key].loc, sampler_id);
                            GL.ActiveTexture(TextureUnit.Texture0 + sampler_id);
                            GL.BindTexture(TextureTargetMap[nbSampler.Texture.Data.target], nbSampler.Texture.texID);
                            sampler_id++;
                            break;
                        }
                    case "Float":
                        {
                            float val = (float) sstate.Value;
                            GL.Uniform1(shader.uniformLocations[key].loc, val);
                            break;
                        }
                    case "Vec2":
                        {
                            NbVector2 vec = (NbVector2) sstate.Value;
                            GL.Uniform2(shader.uniformLocations[key].loc, vec.X, vec.Y);
                            break;
                        }
                    case "Vec3":
                        {
                            NbVector3 vec = (NbVector3) sstate.Value;
                            GL.Uniform3(shader.uniformLocations[key].loc, vec.X, vec.Y, vec.Z);
                            break;
                        }
                    case "Vec4":
                        {
                            NbVector4 vec = (NbVector4) sstate.Value;
                            GL.Uniform4(shader.uniformLocations[key].loc, vec.X, vec.Y, vec.Z, vec.W);
                            break;
                        }

                }

            }

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, (IntPtr)0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);

        }
        /// <summary>
        /// Uses instanced rendering to draw all instances of a mesh in a single drawcall
        /// </summary>
        /// <param name="mesh"></param>
        public void RenderMeshInstanced(NbMesh mesh)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

        public void RenderMesh(NbMesh mesh)
        {
            RenderMesh(mesh, 0);
        }

        public void RenderMesh(NbMesh mesh, int instanceId)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            int instanceBufferSize = Marshal.SizeOf<MeshInstance>();
            uint offset = (uint)((mesh.AtlasBufferOffset + instanceId) * instanceBufferSize);
             
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), instanceBufferSize);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Uses instanced rendering to render all instance of a mesh in a single drawcall
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="mat"></param>
        public void RenderMeshInstanced(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat); //Set Shader and material uniforms

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                glmesh.Mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Renders the indicated instance of a mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="mat"></param>
        /// <param name="instanceId"></param>
        public void RenderMesh(NbMesh mesh, NbMaterial mat, int instanceId)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID]; //Fetch GL Mesh

            //Bind Mesh Buffer
            int instanceBufferSize = Marshal.SizeOf<MeshInstance>();
            uint offset = (uint)((mesh.AtlasBufferOffset + instanceId) * instanceBufferSize);

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), instanceBufferSize);

            SetShaderAndUniforms(mat); //Set Shader and material uniforms

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElements(glmesh.RenderPrimitive,
                glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Renders the first instance of a mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="mat"></param>
        public void RenderMesh(NbMesh mesh, NbMaterial mat)
        {
            RenderMesh(mesh, mat, 0);
        }

        public void RenderLocator(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount); //Use Instancing
            GL.BindVertexArray(0);
        }

        public void RenderJoint(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.MetaData.BatchCount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public void RenderCollision(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), size);

            SetShaderAndUniforms(mat);

            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(glmesh.vao.vao_id);

            //TODO: make sure that primitive collisions have the vertrstartphysics set to 0

            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, glmesh.Mesh.MetaData.BatchCount,
                DrawElementsType.UnsignedShort, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartGraphics);
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount,
                DrawElementsType.UnsignedShort, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartGraphics);

            GL.BindVertexArray(0);
        }

        public void RenderLight(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];

            //Re-Upload vertex buffer for the line segment
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.BindBuffer(BufferTarget.ArrayBuffer, glmesh.vao.vertex_buffer_object);
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr) mesh.Data.VertexBuffer.Length, mesh.Data.VertexBuffer);
            
            //Bind Mesh Buffer
            uint offset = (uint)(mesh.AtlasBufferOffset * Marshal.SizeOf<MeshInstance>());
            int size = mesh.InstanceCount * Marshal.SizeOf<MeshInstance>();

            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr) offset, size);

            SetShaderAndUniforms(mat);

            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.InstanceCount);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.InstanceCount); //Draw both points
            GL.BindVertexArray(0);
        }

        public void RenderLightVolume(NbMesh mesh, NbMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.ID];
            RenderMesh(glmesh.Mesh, mat);
        }


        public void RenderBBoxes(NbMesh mesh)
        {
            for (int i = 0; i < mesh.InstanceCount; i++)
            {
                RenderBbox(mesh, i);
            }
        }

        public void RenderBbox(NbMesh mesh, int instanceID)
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
            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);
            GL.GenBuffers(1, out int vb_bbox);
            GL.GenBuffers(1, out int eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Bind Mesh Buffer
            int instanceBufferSize = Marshal.SizeOf<MeshInstance>();
            uint offset = (uint)((mesh.AtlasBufferOffset + instanceID) * instanceBufferSize);

            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(offset), instanceBufferSize);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);
            GL.BindVertexArray(0);

            //Cleanup
            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);
            GL.DeleteVertexArray(vao);
        }

        public static void renderBHull(GLInstancedMesh mesh)
        {
            if (mesh.bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength,
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength,
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.BindVertexArray(0);
        }


        #endregion


        #region TextureMethods

        public static void queryTextureParameters(NbTexture tex)
        {
            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            GL.BindTexture(gl_target, tex.texID);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureMagFilter, out int mag_filter);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureMinFilter, out int min_filter);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureWrapS, out int wrapmode);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureBaseLevel, out int baselevel);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureMaxLevel, out int maxlevel);
            
            Log($"{(TextureMagFilter) mag_filter} {(TextureMinFilter)min_filter} {(TextureWrapMode)wrapmode} {baselevel} {maxlevel}", LogVerbosityLevel.INFO);
        }
        
        public static void setupTextureMagFilter(NbTexture tex, NbTextureFilter magFilter)
        {
            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            GL.BindTexture(gl_target, tex.texID);
            if (magFilter == NbTextureFilter.NearestMipmapLinear || magFilter == NbTextureFilter.LinearMipmapNearest)
                Log($"Non compatible mag filter {magFilter}. No Mag filter used", LogVerbosityLevel.WARNING);
            else
            {
                GL.TexParameter(gl_target, TextureParameterName.TextureMagFilter, TextureFilterMap[magFilter]);
                tex.Data.MagFilter = magFilter;
            }
            GL.BindTexture(gl_target, 0); //Unbind
        }

        public static void setupTextureMinFilter(NbTexture tex, NbTextureFilter minFilter)
        {
            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            GL.BindTexture(gl_target, tex.texID);
            GL.TexParameter(gl_target, TextureParameterName.TextureMinFilter, TextureFilterMap[minFilter]);
            GL.BindTexture(gl_target, 0); //Unbind
        }

        public static void setupTextureParameters(NbTexture tex, NbTextureWrapMode wrapMode, NbTextureFilter magFilter, NbTextureFilter minFilter, float af_amount)
        {
            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            GL.BindTexture(gl_target, tex.texID);
            GL.TexParameter(gl_target, TextureParameterName.TextureWrapS, TextureWrapMap[wrapMode]);
            GL.TexParameter(gl_target, TextureParameterName.TextureWrapT, TextureWrapMap[wrapMode]);
            
            if (magFilter == NbTextureFilter.NearestMipmapLinear || magFilter == NbTextureFilter.LinearMipmapNearest)
                Log($"Non compatible mag filter {magFilter}. Switching to Default (Linear)", LogVerbosityLevel.WARNING);
            else
            {
                GL.TexParameter(gl_target, TextureParameterName.TextureMagFilter, TextureFilterMap[magFilter]);
                tex.Data.MagFilter = NbTextureFilter.Linear;
            }
            
            GL.TexParameter(gl_target, TextureParameterName.TextureMinFilter, TextureFilterMap[minFilter]);
            
            //Use anisotropic filtering
            //af_amount = System.Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(gl_target, (TextureParameterName)0x84FE, 4.0f);
        }



        public static void DumpTexture(NbTexture tex, string name)
        {
            PixelType pxtype;
            int pix_size;

            if (tex.Data.pif == NbTextureInternalFormat.RGBA16F)
            {
                pxtype = PixelType.Float;
                pix_size = 16;
            }
            else
            {
                pxtype = PixelType.UnsignedByte;
                pix_size = 4;
            }
                
            var pixels = new byte[pix_size * tex.Data.Width * tex.Data.Height];
            GL.BindTexture(TextureTargetMap[tex.Data.target], tex.texID);
            GL.GetTexImage(TextureTargetMap[tex.Data.target], 0, 
                PixelFormat.Rgba, pxtype, pixels);
            
            NbImagingAPI.ImageSave(tex.Data, pixels, "Temp//framebuffer_raw_" + name + ".png");
        }


        public void DeleteTexture(NbTexture tex)
        {
            if (tex.texID > 0)
                GL.DeleteTexture(tex.texID);
        }

        public static void GenerateTexture(NbTexture tex)
        {
            //Upload to GPU
            tex.texID = GL.GenTexture();

            TextureTarget gl_target = TextureTargetMap[tex.Data.target];
            //InternalFormat gl_pif = InternalFormatMap[tex.Data.pif];

            int mm_count = System.Math.Max(tex.Data.MipMapCount, 1);

            GL.BindTexture(gl_target, tex.texID);
            //TODO: Remove all parameter settings from here, and make it possible to set them using other API calls
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(gl_target, TextureParameterName.TextureBaseLevel, 0);
            
            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float)System.Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureMaxLevel, out int max_level);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureBaseLevel, out int base_level);

            int maxsize = System.Math.Max(tex.Data.Height, tex.Data.Width);
            int p = (int)System.Math.Floor(System.Math.Log(maxsize, 2)) + base_level;
            int q = System.Math.Min(p, max_level);
            GL.TexParameter(gl_target, TextureParameterName.TextureMaxLevel, q);

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

        private static void UploadTextureData(int tex_id, NbTextureData tex_data, bool upload_data=true)
        {
            TextureTarget gl_target = TextureTargetMap[tex_data.target];
            InternalFormat gl_pif = InternalFormatMap[tex_data.pif];
            PixelType gl_pxtype = PixelTypeMap[tex_data.pif];
            
            GL.BindTexture(gl_target, tex_id);
            
            if (upload_data)
                GL.TexImage2D(gl_target, 0, (PixelInternalFormat)gl_pif, tex_data.Width, tex_data.Height, 0,
                    PixelFormatMap[tex_data.pif], gl_pxtype, tex_data.Data);
            else
                GL.TexImage2D(gl_target, 0, (PixelInternalFormat)gl_pif, tex_data.Width, tex_data.Height, 0,
                    PixelFormatMap[tex_data.pif], gl_pxtype, IntPtr.Zero);
            
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(gl_target, 0);
        }


        private static void UploadTextureData(int tex_id, DDSImage tex_data)
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
                    PixelInternalFormat pif = PixelInternalFormat.Rgba8;
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
                        case TextureTarget.Texture2D:
                            GL.TexImage2D(gl_target, i, pif, w, h, 0, pf, PixelType.UnsignedByte, temp_data);
                            break;
                        case TextureTarget.Texture2DArray:
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
                        case TextureTarget.Texture3D:
                            //GL.CompressedTexImage3D(gl_target, i, InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext, w, h, d, 0, temp_size, temp_data);
                            break;
                        case TextureTarget.Texture2DArray:
                            GL.CompressedTexImage3D(gl_target, i, gl_pif, w, h, d * face_count, 0, temp_size, temp_data);
                            break;
                        default:
                            GL.CompressedTexImage2D(gl_target, i, gl_pif, w, h, 0, temp_size, temp_data);
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
            Callbacks.Assert(tex.texID >= 0, "Invalid texture ID");
            if (tex.Data is DDSImage) 
                UploadTextureData(tex.texID, tex.Data as DDSImage);
            else if (tex.Data != null)
                UploadTextureData(tex.texID, tex.Data);
            else
                UploadTextureData(tex.texID, tex.Data, false);
        }

        #endregion

        //Fetch main VAO
        public static GLVao generateVAO(NbMesh mesh)
        {
            //Generate VAO
            GLVao vao = new();
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);

            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            //Bind vertex buffer
            int size;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)mesh.Data.VertexBuffer.Length,
                mesh.Data.VertexBuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);

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
                    case NbPrimitiveDataType.Byte:
                        buftype = VertexAttribPointerType.Byte;
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

                GL.VertexAttribPointer((int) buf.semantic, buf.count, buftype, buf.normalize, buf.stride, buf.offset);
                GL.EnableVertexArrayAttrib(vao.vao_id, (int) buf.semantic);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)mesh.Data.IndexBuffer.Length,
                mesh.Data.IndexBuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            Callbacks.Assert(size == mesh.Data.IndexBuffer.Length,
                "Mesh metadata does not match the index buffer size from the geometry file");

            //Unbind
            GL.BindVertexArray(0);
            //for (int i = 0; i < mesh.Data.buffers.Length; i++)
            //    GL.DisableVertexAttribArray(mesh.Data.buffers[i].semantic);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            
            return vao;
        }

        
        private void UploadUniform(NbUniform uf)
        {
            switch (uf.Type)
            {
                case (NbUniformType.Float):
                    GL.Uniform1(uf.ShaderLocation, uf.Values._Value.X);
                    break;
                case (NbUniformType.Vector2):
                    GL.Uniform2(uf.ShaderLocation, uf.Values._Value.Xy);
                    break;
                case (NbUniformType.Vector3):
                    GL.Uniform3(uf.ShaderLocation, uf.Values._Value.Xyz);
                    break;
                case (NbUniformType.Vector4):
                    GL.Uniform4(uf.ShaderLocation, uf.Values._Value);
                    break;
                default:
                    Console.WriteLine($"Unsupported Uniform {uf.Type}");
                    break;
            }
        }

        private void SetShaderAndUniforms(NbMaterial Material)
        {
            //Upload Material Information
            EnableMaterialProgram(Material);

            //Upload Custom Per Material Uniforms
            foreach (NbUniform un in Material.ActiveUniforms)
                UploadUniform(un);
                
            //BIND TEXTURES
            foreach (NbSampler s in Material.ActiveSamplers)
            {
                GL.Uniform1(Material.Shader.uniformLocations[s.ShaderBinding].loc, s.SamplerID);
                GL.ActiveTexture(TextureUnit.Texture0 + s.SamplerID);
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
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        }

        public static void ClearDrawBuffer(NbBufferMask mask)
        {
            ClearBufferMask glmask = ClearBufferMask.None;
            
            if (mask.HasFlag(NbBufferMask.Color))
                glmask |= ClearBufferMask.ColorBufferBit;
            
            if (mask.HasFlag(NbBufferMask.Depth))
                glmask |= ClearBufferMask.DepthBufferBit;

            if (mask.HasFlag(NbBufferMask.Stencil))
                glmask |= ClearBufferMask.StencilBufferBit;

            GL.Clear(glmask);
        }
        

        public FBO CreateFrameBuffer(int w, int h, FBOOptions options)
        {
            GL.CreateFramebuffers(1, out int fbo_id);

            //Main flags
            if (options.HasFlag(FBOOptions.MultiSample))
                GL.Enable(EnableCap.Multisample);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer " + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0); //Bind default framebuffer
            
            return new FBO(w, h)
            {
                fbo = fbo_id
            };
        }

        public void ActivateFrameBuffer(FBO fbo)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.fbo);
            GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y);
        }

        public void CreateFrameBuffer(FBO fbo, FBOOptions options)
        {
            GL.CreateFramebuffers(1, out int fbo_id);
            fbo.fbo = fbo_id;

            //Main flags
            if (options.HasFlag(FBOOptions.MultiSample))
                GL.Enable(EnableCap.Multisample);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer " + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0); //Bind default framebuffer
        }

        public void DeleteFrameBuffer(FBO fbo)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo.fbo);
            fbo.fbo = -1;
        }

        public void CopyDepthChannel(FBO from, FBO to)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to.fbo);

            GL.BlitFramebuffer(0, 0, from.Size.X, from.Size.Y, 0, 0, to.Size.X, to.Size.Y,
            ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        public void CopyFrameBufferChannelToTexture(NbFBOAttachment fbo_attachment, NbTexture tex)
        {
            //Copies the attachment of the currently bound framebuffer to the texture
            
            //Copy the read buffers to the  
            GL.BindTexture(TextureTargetMap[tex.Data.target], tex.texID);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Callbacks.Assert(tex.Data.target == NbTextureTarget.Texture2D,
                            "fbo texture target is not correct");
            GL.CopyTexSubImage2D(TextureTargetMap[tex.Data.target], 0, 0, 0, 0, 0, tex.Data.Width, tex.Data.Height);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }


        public void AddFrameBufferAttachment(FBO fbo, NbTexture tex, NbFBOAttachment attachment_id)
        {
            int handle = GL.GenTexture();
            tex.texID = handle;
            PixelInternalFormat pif = (PixelInternalFormat)InternalFormatMap[tex.Data.pif];
            PixelType pixel_type = PixelTypeMap[tex.Data.pif];
            TextureTarget textarget = TextureTargetMap[tex.Data.target];

            //PixelFormat fmt = PixelFormat.Rgba;
            PixelFormat fmt = PixelFormatMap[tex.Data.pif];

            if (textarget == TextureTarget.Texture2DMultisample)
            {
                GL.BindTexture(TextureTarget.Texture2DMultisample, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, fbo.msaa_samples, pif, fbo.Size.X, fbo.Size.Y, true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachmentMap[attachment_id], TextureTarget.Texture2DMultisample, handle, 0);
            }
            else if (textarget == TextureTarget.Texture2D)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);
                GL.TexImage2D(TextureTarget.Texture2D, 0, pif, fbo.Size.X, fbo.Size.Y, 0, fmt, pixel_type, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, TextureFilterMap[tex.Data.MagFilter]);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, TextureFilterMap[tex.Data.MinFilter]);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachmentMap[attachment_id], TextureTarget.Texture2D, handle, 0);

            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Log("MALAKIES STO FRAMEBUFFER tou GBuffer during texture setup " + GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer), LogVerbosityLevel.ERROR);
            
        }

        public void BindDrawFrameBuffer(FBO fbo)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo.fbo);
            GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        }

        public void BindDrawFrameBuffer(FBO fbo, int[] drawBuffers)
        {
            BindDrawFrameBuffer(fbo.fbo, fbo.Size.X, fbo.Size.Y, drawBuffers);
        }

        public void BindDrawFrameBuffer(int fbo_id, int size_x, int size_y, int[] drawBuffers)
        {
            //Bind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo_id);
            GL.Viewport(0, 0, size_x, size_y);

            DrawBuffersEnum[] bufferEnums = new DrawBuffersEnum[drawBuffers.Length];

            for (int i = 0; i < drawBuffers.Length; i++)
                bufferEnums[i] = DrawBuffersEnum.ColorAttachment0 + drawBuffers[i];

            GL.DrawBuffers(bufferEnums.Length, bufferEnums);
        }

        public static void BindFrameBuffer(FBO fbo)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.fbo);
        }

        public void BindFrameBuffer(int fbo_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo_id);
        }

        public static void BindDefaultFrameBuffer()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public static void SetViewPortSize(int x, int y, int w, int h)
        {
            GL.Viewport(x, y, w, h);
        }

        public static void GetViewPortSize(ref int X, ref int Y)
        {
            int[] size = new int[2];
            GL.GetInteger(GetPName.Viewport, size);
            X = size[0];
            Y = size[1];
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
        public static void AttachUBOToShaderBindingPoint(NbShader shader, string var_name, int binding_point)
        {
            int shdr_program_id = shader.ProgramID;
            int ubo_index = GL.GetUniformBlockIndex(shdr_program_id, var_name);
            if (ubo_index != -1)
                GL.UniformBlockBinding(shdr_program_id, ubo_index, binding_point);
        }

        public static void AttachSSBOToShaderBindingPoint(NbShader shader, string var_name, int binding_point)
        {
            //Binding Position 0 - Matrices UBO
            int shdr_program_id = shader.ProgramID;
            int ssbo_index = GL.GetProgramResourceIndex(shdr_program_id, ProgramInterface.ShaderStorageBlock, var_name);
            if (ssbo_index != -1)
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
            NbShaderSourceType type, ref int object_id, ref string temp_log, string append_text = "")
        {
            NbShaderSource _source = shader.GetShaderConfig().Sources[type];

            if (_source == null)
                return false;

            if (!_source.Resolved)
                _source.Resolve();


            int shader_object_id;
            string ActualShaderSource;
            shader_object_id = GL.CreateShader(ShaderTypeMap[type]);
            
            //Compile Shader
            GL.ShaderSource(shader_object_id, NbShaderConfig.version + "\n" + append_text + "\n" + _source.ResolvedText);

            //Get resolved shader text
            GL.GetShaderSource(shader_object_id, 32768, out int actual_shader_length, out ActualShaderSource);

            GL.CompileShader(shader_object_id);
            GL.GetShaderInfoLog(shader_object_id, out string info);

            GL.GetShader(shader_object_id, ShaderParameter.CompileStatus, out int status_code);

            temp_log += NumberLines(ActualShaderSource) + "\n";
            temp_log += info + "\n";

            if (status_code != 1)
            {
                object_id = -1;
                return false;
            }
            
            object_id = shader_object_id;
            return true;
        }

        //Shader Creation
        public static bool CompileShader(NbShader shader)
        {
            NbShaderConfig conf = shader.GetShaderConfig();
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
            Dictionary<NbShaderSourceType, int> temp_objects = new();
            string temp_log = "";
            foreach (var pair in conf.Sources)
            {
                int object_id = -1;
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
                    if (shader.SourceObjects[pair.Key] != -1)
                        GL.DeleteShader(shader.SourceObjects[pair.Key]);

                shader.SourceObjects[pair.Key] = temp_objects[pair.Key];
            }

            if (shader.ProgramID != -1)
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

            GL.GetProgram(shader.ProgramID, GetProgramParameterName.LinkStatus, out int status_code);
            if (status_code != 1)
            {
                Log(shader.CompilationLog, LogVerbosityLevel.ERROR);
                Callbacks.Assert(false, "Shader Compilation Error");
            }

            //ShaderCompilationLog(shader);
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
            GL.GetProgram(shader.ProgramID, GetProgramParameterName.ActiveUniforms, out int active_uniforms_count);

            shader.uniformLocations.Clear(); //Reset locataions
            shader.CompilationLog += "Active Uniforms: " + active_uniforms_count.ToString() + "\n";
            for (int i = 0; i < active_uniforms_count; i++)
            {
                int bufSize = 64;

                GL.GetActiveUniform(shader.ProgramID, i, bufSize, out int size, out int length, out ActiveUniformType type, out string name);
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
                        case ActiveUniformType.Float:
                            fmt.type = NbUniformType.Float;
                            break;
                        case ActiveUniformType.Bool:
                            fmt.type = NbUniformType.Bool;
                            break;
                        case ActiveUniformType.Int:
                            fmt.type = NbUniformType.Int;
                            break;
                        case ActiveUniformType.FloatMat3:
                            fmt.type = NbUniformType.Matrix3;
                            break;
                        case ActiveUniformType.FloatMat4:
                            fmt.type = NbUniformType.Matrix4;
                            break;
                        case ActiveUniformType.FloatVec2:
                            fmt.type = NbUniformType.Vector2;
                            break;
                        case ActiveUniformType.FloatVec3:
                            fmt.type = NbUniformType.Vector3;
                            break;
                        case ActiveUniformType.FloatVec4:
                            fmt.type = NbUniformType.Vector4;
                            break;
                        case ActiveUniformType.Sampler2D:
                            fmt.type = NbUniformType.Sampler2D;
                            break;
                        case ActiveUniformType.Sampler3D:
                            fmt.type = NbUniformType.Sampler3D;
                            break;
                        case ActiveUniformType.Sampler2DArray:
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

        public static void ShaderReport(NbShader shader)
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int test_program = shader.ProgramID;
            GL.GetProgram(test_program, GetProgramParameterName.ActiveUniformBlocks, out int count);

            for (int i = 0; i < count; ++i)
            {
                // Get blocks name
                GL.GetActiveUniformBlockName(test_program, i, 256, out int length, out string block_name);
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockDataSize, out int block_size);
                Console.WriteLine("Block {0} Data Size {1}", block_name, block_size);

                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockBinding, out int block_bind_index);
                Console.WriteLine("    Block Binding Point {0}", block_bind_index);

                GL.GetInteger(GetIndexedPName.UniformBufferBinding, block_bind_index, out int info);
                Console.WriteLine("    Block Bound to Binding Point: {0} {{", info);

                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniforms, out int block_active_uniforms);
                int[] uniform_indices = new int[block_active_uniforms];
                GL.GetActiveUniformBlock(test_program, i, ActiveUniformBlockParameter.UniformBlockActiveUniformIndices, uniform_indices);


                int[] uniform_types = new int[block_active_uniforms];
                int[] uniform_offsets = new int[block_active_uniforms];
                int[] uniform_sizes = new int[block_active_uniforms];

                //Fetch Parameters for all active Uniforms
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformType, uniform_types);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformOffset, uniform_offsets);
                GL.GetActiveUniforms(test_program, block_active_uniforms, uniform_indices, ActiveUniformParameter.UniformSize, uniform_sizes);

                for (int k = 0; k < block_active_uniforms; ++k)
                {
                    GL.GetActiveUniformName(test_program, uniform_indices[k], 256, out int actual_name_length, out string name);
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

        public static void AddRenderInstance(ref MeshComponent mc, TransformData td)
        {
            NbMeshBufferManager.AddRenderInstance(ref mc, td);
            NbMeshInstanceManager.AddMeshInstance(ref mc.Mesh);
        }

        public static void AddRenderInstance(ref ImposterComponent ic, TransformData td)
        {
            NbImposterBufferManager.AddRenderInstance(ref ic, td);
            NbMeshInstanceManager.AddMeshInstance(ref ic.Mesh);
        }

        public static void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            NbMeshInstanceManager.RemoveMeshInstance(ref mesh);
            NbMeshBufferManager.RemoveRenderInstance(ref mesh, mc);
            mc.InstanceID = -1;
        }

        public static void UpdateInstance(NbMesh mesh, int index)
        {
            NbMeshInstanceManager.UpdateMeshInstance(ref mesh, index);
        }

        public static void AddLightRenderInstance(ref LightComponent lc, TransformData td)
        {
            NbLightBufferManager.AddRenderInstance(ref lc, td);
            NbMeshInstanceManager.AddMeshInstance(ref lc.Mesh);
        }

        public static void SetLightInstanceData(LightComponent lc)
        {
            NbLightBufferManager.SetInstanceData(lc);
        }

        public static void SetImposterInstanceData(ImposterComponent ic)
        {
            NbImposterBufferManager.SetInstanceData(ic);
        }

        public static void RemoveLightRenderInstance(ref NbMesh mesh, LightComponent lc)
        {
            NbMeshInstanceManager.RemoveMeshInstance(ref mesh);
            NbLightBufferManager.RemoveRenderInstance(ref mesh, lc);
            lc.InstanceID = -1;
        }

        public static void SetInstanceWorldMat(NbMesh mesh, int instanceID, NbMatrix4 mat)
        {
            NbMeshBufferManager.SetInstanceWorldMat(mesh, instanceID, mat);
        }

        public static void SetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID, NbVector4 uf)
        {
            NbMeshBufferManager.SetInstanceUniform4(mesh, instanceID, uniformID, uf);
        }

        public static NbVector4 GetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID)
        {
            return NbMeshBufferManager.GetInstanceUniform4(mesh, instanceID, uniformID);
        }

        public static void SetInstanceWorldMatInv(NbMesh mesh, int instanceID, NbMatrix4 mat)
        {
            NbMeshBufferManager.SetInstanceWorldMatInv(mesh, instanceID, mat);
        }

        #endregion


        #region TextureMethods

        
        #endregion

    }
}

#endif