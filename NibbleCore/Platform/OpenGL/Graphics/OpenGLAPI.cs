using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using NbCore;
using NbCore.Systems;
using NbCore.Math;
using NbCore.Common;
using NbCore.Platform.Graphics;
using OpenTK.Graphics.OpenGL4;
using System.Linq;

namespace NbCore.Platform.Graphics.OpenGL
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

    public class GraphicsAPI : IGraphicsApi
    {
        private const string RendererName = "OPENGL_RENDERER";
        private int activeProgramID = -1;
        public Dictionary<ulong, GLInstancedMesh> MeshMap = new();
        private readonly Dictionary<string, int> UBOs = new();
        private readonly Dictionary<string, int> SSBOs = new();

        private int multiBufferActiveId;
        private readonly List<int> multiBufferSSBOs = new(4);
        private readonly List<IntPtr> multiBufferSyncStatuses = new(4);

        public static readonly Dictionary<NbTextureTarget, TextureTarget> TextureTargetMap = new()
        {
            {NbTextureTarget.Texture1D , TextureTarget.Texture1D},
            {NbTextureTarget.Texture2D , TextureTarget.Texture2D},
            {NbTextureTarget.Texture2DArray , TextureTarget.Texture2DArray }
        };

        public static readonly Dictionary<NbTextureInternalFormat, InternalFormat> InternalFormatMap = new()
        {
            { NbTextureInternalFormat.DXT1, InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext },
            { NbTextureInternalFormat.DXT5, InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext },
            { NbTextureInternalFormat.RGTC2, InternalFormat.CompressedRgRgtc2 },
            { NbTextureInternalFormat.BC7, InternalFormat.CompressedSrgbAlphaBptcUnorm },
        };

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private MeshInstance[] atlas_cpmu;

        private const int MAX_NUMBER_OF_MESHES = 2000;
        private const int MULTI_BUFFER_COUNT = 3;

        private DebugProc GLDebug;

        private void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(string.Format("* {0} : {1}", RendererName, msg), lvl);
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
#if (DEBUG)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GLDebug = new DebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DontCare, 0, new int[] { 0 }, true);

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif
            //Setup per Frame UBOs
            setupFrameUBO();


            //Setup SSBOs
            setupSSBOs(2 * 1024 * 1024); //Init SSBOs to 2MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];

            //Setup Atlas Instance array
            atlas_cpmu = new MeshInstance[1024]; //Support up to 1024 instance for now by default
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
            int size = (256 * 16 + 128) * 4; //FIXED SIZE FOR NOW
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
            
        private bool prepareCommonPermeshSSBO(GLInstancedMesh m, uint max_buffer_size, ref int UBO_Offset, ref int instance_counter)
        {
            //if (m.instance_count == 0 || m.visible_instances == 0) //use the visible_instance if we maintain an occluded status
            if (m.Mesh.InstanceCount == 0)
                return true;

            m.UBO_aligned_size = 0;


            //Calculate aligned size
            int newsize = m.Mesh.InstanceCount * Marshal.SizeOf(typeof(MeshInstance));
            

            if (newsize + UBO_Offset > max_buffer_size)
            {
#if DEBUG
                Log("Mesh overload skipping...", LogVerbosityLevel.WARNING);
#endif
                return false;
            }

            m.UBO_aligned_size = newsize; //Save new size

            //if (m.Mesh.Type == NbMeshType.LightVolume)
            //{
            //    ((GLInstancedLightMesh)m).uploadData();
            //}

            for (int i = 0; i < m.Mesh.InstanceCount; i++)
                atlas_cpmu[instance_counter++] = m.Mesh.InstanceDataBuffer[i];
            
            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += m.UBO_aligned_size; //Increase the offset

            return true;
        }

        public void PrepareMeshBuffers()
        {
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;

            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            WaitSyncStatus result = WaitSyncStatus.WaitFailed;
            while (result == WaitSyncStatus.TimeoutExpired || result == WaitSyncStatus.WaitFailed)
            {
                //Callbacks.Log(result.ToString());
                //Console.WriteLine("Gamithike o dias");
                result = GL.ClientWaitSync(multiBufferSyncStatuses[multiBufferActiveId], 0, 10);
            }

            GL.DeleteSync(multiBufferSyncStatuses[multiBufferActiveId]);

            //Upload atlas UBO data
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);

            //Prepare UBO data
            int ubo_offset = 0;
            int instance_counter = 0;
            int max_instance_count = atlas_cpmu.Length;
            int max_ubo_offset = max_instance_count * Marshal.SizeOf(typeof(MeshInstance));

            //METHOD 2: Use MAP Buffer
            IntPtr ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                 max_ubo_offset, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            //Upload Meshes
            bool atlas_fine = true;
            foreach (GLInstancedMesh m in MeshMap.Values)
            {
                atlas_fine &= prepareCommonPermeshSSBO(m, (uint) max_ubo_offset, ref ubo_offset, ref instance_counter);
            }

            //Console.WriteLine("ATLAS SIZE ORIGINAL: " +  atlas_cpmu.Length + " vs  OFFSET " + ubo_offset);

            if (instance_counter > 0.9 * max_instance_count)
            {
                int new_instance_counter = max_instance_count + (int)(0.25 * max_instance_count);
                atlas_cpmu = new MeshInstance[new_instance_counter];

                int new_size = new_instance_counter * Marshal.SizeOf(typeof(MeshInstance));

                //Unmap and unbind buffer
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

                resizeSSBOs(new_size);

                //Remap and rebind buffer at the current index
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);
                ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                new_size, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            }

            if (ubo_offset != 0)
            {
#if (DEBUG)
                if (ubo_offset > max_ubo_offset)
                    Log("GAMITHIKE O DIAS", LogVerbosityLevel.WARNING);
#endif
                //at this point the ubo_offset is the actual size of the atlas buffer

                unsafe
                {

                    GCHandle handle = GCHandle.Alloc(atlas_cpmu, GCHandleType.Pinned);
                    IntPtr handlePtr = handle.AddrOfPinnedObject();
                    System.Buffer.MemoryCopy(handlePtr.ToPointer(), ptr.ToPointer(), max_ubo_offset, max_ubo_offset);
                }
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
            cpfu.diffuseFlag = (RenderState.settings.renderSettings.UseTextures) ? 1.0f : 0.0f;
            cpfu.use_lighting = (RenderState.settings.renderSettings.UseLighting) ? 1.0f : 0.0f;
            cpfu.cameraPositionExposure.W = RenderState.settings.renderSettings.HDRExposure;
        }

        public void SetCameraData(Camera cam)
        {
            cpfu.mvp = RenderState.activeCam.viewMat._Value;
            cpfu.lookMatInv = RenderState.activeCam.lookMatInv._Value;
            cpfu.projMatInv = RenderState.activeCam.projMatInv._Value;
            cpfu.cameraPositionExposure.Xyz = RenderState.activeCam.Position._Value;
            cpfu.cameraDirection = RenderState.activeCam.Front._Value;
            cpfu.cameraNearPlane = RenderState.activeCam.zNear;
            cpfu.cameraFarPlane = RenderState.activeCam.zFar;
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

        public void AddMesh(NbMesh mesh)
        {

            if (mesh.Hash == 0x0)
            {
                Log("Default mesh hash. Something went wrong during mesh generation", LogVerbosityLevel.WARNING);
                return;
            }

            if (MeshMap.ContainsKey(mesh.Hash))
            {
                Log("Mesh Hash already exists in map", LogVerbosityLevel.WARNING);
            }

            //Generate instanced mesh
            GLInstancedMesh imesh = GenerateAPIMesh(mesh);
            MeshMap[mesh.Hash] = imesh;
        }

        public void RenderMesh()
        {
            throw new NotImplementedException();
        }

        private GLInstancedMesh GenerateAPIMesh(NbMesh mesh)
        {
            GLInstancedMesh imesh = new(mesh);
            return imesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableMaterialProgram(MeshMaterial mat)
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

        public void RenderQuad(NbMesh quadMesh, NbShader shader, NbShaderState state)
        {
            GLInstancedMesh glmesh = MeshMap[quadMesh.Hash];
            
            GL.UseProgram(shader.ProgramID);
            GL.BindVertexArray(glmesh.vao.vao_id);

            //Filter State
            shader.FilterState(ref state);

            //Upload samplers
            int i = 0;
            foreach (KeyValuePair<string, NbSamplerState> sstate in state.Samplers)
            {
                GL.Uniform1(shader.uniformLocations[sstate.Key].loc , sstate.Value.SamplerID);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(TextureTargetMap[sstate.Value.Target], sstate.Value.TextureID);
            }

            //Floats
            foreach (KeyValuePair<string, float> pair in state.Floats)
            {
                GL.Uniform1(shader.uniformLocations[pair.Key].loc, pair.Value);
            }

            //Vec2s
            foreach (KeyValuePair<string, NbVector2> pair in state.Vec2s)
            {
                GL.Uniform2(shader.uniformLocations[pair.Key].loc,
                    pair.Value.X, pair.Value.Y);
            }

            //Vec3s
            foreach (KeyValuePair<string, NbVector3> pair in state.Vec3s)
            {
                GL.Uniform3(shader.uniformLocations[pair.Key].loc,
                    pair.Value.X, pair.Value.Y, pair.Value.Z);
            }

            //Vec4s
            foreach (KeyValuePair<string, NbVector4> pair in state.Vec4s)
            {
                GL.Uniform4(shader.uniformLocations[pair.Key].loc,
                    pair.Value.X, pair.Value.Y, pair.Value.Z, pair.Value.W);
            }

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, (IntPtr)0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);
            
        }

        public void RenderMesh(NbMesh mesh)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash]; //Fetch GL Mesh

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

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

            Common.Callbacks.Assert(size == mesh.Data.VertexBufferStride * (mesh.MetaData.VertrEndGraphics - mesh.MetaData.VertrStartGraphics + 1),
                "Mesh metadata does not match the vertex buffer size from the geometry file");

            //Assign VertexAttribPointers
            for (int i = 0; i < mesh.Data.buffers.Length; i++)
            {
                bufInfo buf = mesh.Data.buffers[i];
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

                GL.VertexAttribPointer(buf.semantic, buf.count, buftype, buf.normalize, (int) buf.stride, buf.offset);
                GL.EnableVertexAttribArray(buf.semantic);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)mesh.Data.IndexBuffer.Length,
                mesh.Data.IndexBuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            Common.Callbacks.Assert(size == mesh.Data.IndexBuffer.Length,
                "Mesh metadata does not match the index buffer size from the geometry file");

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < mesh.Data.buffers.Length; i++)
                GL.DisableVertexAttribArray(mesh.Data.buffers[i].semantic);

            return vao;
        }

        public void RenderMesh(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash]; //Fetch GL Mesh

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr) glmesh.UBO_offset, glmesh.UBO_aligned_size);

            SetShaderAndUniforms(mat); //Set Shader and material uniforms
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero, 
                glmesh.Mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

        public void RenderLocator(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount); //Use Instancing
            GL.BindVertexArray(0);
        }

        public void RenderJoint(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.MetaData.BatchCount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public void RenderCollision(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

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

        public void RenderLight(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.InstanceCount);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.InstanceCount); //Draw both points
            GL.BindVertexArray(0);
        }

        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash] as GLInstancedMesh;
            RenderMesh(glmesh.Mesh, mat);
        }


        public void renderBBoxes(GLInstancedMesh mesh, int pass)
        {
            for (int i = 0; i > mesh.Mesh.InstanceCount; i++)
            {
                renderBbox(mesh.Mesh.instanceRefs[i]);
            }
        }

        public void renderBbox(MeshComponent mc)
        {
            if (mc == null)
                return;

            NbVector4[] tr_AABB = new NbVector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new NbVector4(mc.Mesh.MetaData.AABBMIN, 1.0f);
            tr_AABB[1] = new NbVector4(mc.Mesh.MetaData.AABBMAX, 1.0f);

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

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
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
            GL.BindVertexArray(mesh.bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.BindVertexArray(0);
        }


        private void UploadUniform(NbUniform uf)
        {
            switch (uf.Format.type)
            {
                case (NbUniformType.Float):
                    GL.Uniform1(uf.Format.loc, uf.Values._Value.X);
                    break;
                case (NbUniformType.Vector2):
                    GL.Uniform2(uf.Format.loc, uf.Values._Value.Xy);
                    break;
                case (NbUniformType.Vector3):
                    GL.Uniform3(uf.Format.loc, uf.Values._Value.Xyz);
                    break;
                case (NbUniformType.Vector4):
                    GL.Uniform4(uf.Format.loc, uf.Values._Value);
                    break;
                default:
                    Console.WriteLine($"Unsupported Uniform {uf.Format.type}");
                    break;
            }
        }

        private void SetShaderAndUniforms(MeshMaterial Material)
        {
            //Upload Material Information

            //Upload Custom Per Material Uniforms
            foreach (NbUniform un in Material.ActiveUniforms)
                UploadUniform(un);
                
            //BIND TEXTURES
            //Diffuse Texture
            foreach (NbSampler s in Material.ActiveSamplers)
            {
                GL.Uniform1(s.State.ShaderLocation, s.State.SamplerID);
                GL.ActiveTexture(TextureUnit.Texture0 + s.State.SamplerID);
                GL.BindTexture(TextureTargetMap[s.State.Target], s.State.TextureID);
            }
        }
        
        public void SyncGPUCommands()
        {
            //Setup FENCE AFTER ALL THE MAIN GEOMETRY DRAWCALLS ARE ISSUED
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        }

        public void ClearDrawBuffer(NbBufferMask mask)
        {
            ClearBufferMask glmask = ClearBufferMask.None;

            if (mask.HasFlag(NbBufferMask.Color))
                glmask |= ClearBufferMask.ColorBufferBit;
            
            if (mask.HasFlag(NbBufferMask.Color))
                glmask |= ClearBufferMask.DepthBufferBit;

            GL.Clear(glmask);
        }

        public void BindDrawFrameBuffer(FBO fbo, int[] drawBuffers)
        {
            //Bind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo.fbo);
            GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y);

            DrawBuffersEnum[] bufferEnums = new DrawBuffersEnum[drawBuffers.Length];

            for (int i = 0; i < drawBuffers.Length; i++)
                bufferEnums[i] = DrawBuffersEnum.ColorAttachment0 + drawBuffers[i];

            GL.DrawBuffers(bufferEnums.Length, bufferEnums);
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
        public void AttachUBOToShaderBindingPoint(NbShader shader, string var_name, int binding_point)
        {
            int shdr_program_id = shader.ProgramID;
            int ubo_index = GL.GetUniformBlockIndex(shdr_program_id, var_name);
            GL.UniformBlockBinding(shdr_program_id, ubo_index, binding_point);
        }

        public void AttachSSBOToShaderBindingPoint(NbShader shader, string var_name, int binding_point)
        {
            //Binding Position 0 - Matrices UBO
            int shdr_program_id = shader.ProgramID;
            int ssbo_index = GL.GetProgramResourceIndex(shdr_program_id, ProgramInterface.ShaderStorageBlock, var_name);
            GL.ShaderStorageBlockBinding(shader.ProgramID, ssbo_index, binding_point);
        }

        public void CompileShaderSource(NbShader shader, GLSLShaderConfig shaderConf, 
            NbShaderSourceType type, string append_text = "")
        {
            GLSLShaderSource _source = shaderConf.Sources[type];

            if (_source == null)
                return;

            if (!_source.Resolved)
                _source.Resolve();


            //TODO: Try to first compile the shader and return if there are problems

            if (shader.SourceObjects.ContainsKey(type))
                if (shader.SourceObjects[type] != -1)
                    GL.DeleteShader(shader.SourceObjects[type]);

            int shader_object_id;
            string ActualShaderSource;
            shader_object_id = GL.CreateShader(ShaderTypeMap[type]);
            
            //Compile Shader
            GL.ShaderSource(shader_object_id, GLSLShaderConfig.version + "\n" + append_text + "\n" + _source.ResolvedText);

            //Get resolved shader text
            GL.GetShaderSource(shader_object_id, 32768, out int actual_shader_length, out ActualShaderSource);

            GL.CompileShader(shader_object_id);
            GL.GetShaderInfoLog(shader_object_id, out string info);

            shader.CompilationLog += GLShaderHelper.NumberLines(ActualShaderSource) + "\n";
            shader.CompilationLog += info + "\n";

            GL.GetShader(shader_object_id, ShaderParameter.CompileStatus, out int status_code);
            if (status_code != 1)
            {
                Log(GLShaderHelper.NumberLines(ActualShaderSource), LogVerbosityLevel.ERROR);

                Log("Failed to compile shader for the model. Contact Dev",
                    LogVerbosityLevel.ERROR);
                Callbacks.Assert(status_code == 1, "Shader Compilation Error");
            }

            shader.SourceObjects[type] = shader_object_id;
        }

        public void CompileShader(MeshMaterial mat)
        {
            NbShader shader = new();
            CompileShader(ref shader, mat.ShaderConfig, mat);
        }

        public void CompileShader(ref NbShader shader, GLSLShaderConfig config)
        {
            CompileShader(ref shader, config, "");
        }

        public void CompileShader(ref NbShader shader, GLSLShaderConfig config, MeshMaterial mat)
        {
            string extradirectives = "";
            for (int i = 0; i < mat.Flags.Count; i++)
            {
                if (MeshMaterial.supported_flags.Contains(mat.Flags[i]))
                    extradirectives += "#define " + mat.Flags[i].ToString().Split(".")[^1] + '\n';
            }

            CompileShader(ref shader, config, extradirectives);
        }

        //Shader Creation
        public void CompileShader(ref NbShader shader, GLSLShaderConfig config, string extradirectives = "")
        {
            if (shader.ProgramID != -1)
                GL.DeleteProgram(shader.ProgramID);

            bool gsflag = false;
            bool tsflag = false;


            gsflag = config.Sources.ContainsKey(NbShaderSourceType.GeometryShader);

            tsflag = config.Sources.ContainsKey(NbShaderSourceType.TessControlShader) |
                     config.Sources.ContainsKey(NbShaderSourceType.TessControlShader);

            //Convert directives array to string
            string directivestring = "";
            //Add General directives
            //General Directives are provided here
            if (config.ShaderMode.HasFlag(NbShaderMode.DEFFERED))
                directivestring += "#define _D_DEFERRED_RENDERING" + '\n';

            if (config.ShaderMode.HasFlag(NbShaderMode.LIT))
                directivestring += "#define _D_LIGHTING" + '\n';

            directivestring += extradirectives;

            //Generate DirectiveString
            foreach (string dir in config.directives)
                directivestring += "#define " + dir + '\n';

            foreach (var pair in config.Sources)
                CompileShaderSource(shader, config, pair.Key, directivestring);
            
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
                Callbacks.Log(shader.CompilationLog, LogVerbosityLevel.ERROR);
                Callbacks.Assert(false, "Shader Compilation Error");
            }
            
            ShaderCompilationLog(shader);
            loadActiveUniforms(shader);
        }

        public void ShaderCompilationLog(NbShader shader)
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

        private void loadActiveUniforms(NbShader shader)
        {
            GL.GetProgram(shader.ProgramID, GetProgramParameterName.ActiveUniforms, out int active_uniforms_count);

            shader.uniformLocations.Clear(); //Reset locataions
            shader.CompilationLog += "Active Uniforms: " + active_uniforms_count.ToString() + "\n";
            for (int i = 0; i < active_uniforms_count; i++)
            {
                int bufSize = 64;
                int loc;

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

        public static void reportUBOs(NbShader shader)
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






        public void AddRenderInstance(ref MeshComponent mc, TransformData td)
        {
            GLMeshBufferManager.AddRenderInstance(ref mc, td);
        }

        public void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            GLMeshBufferManager.RemoveRenderInstance(ref mesh, mc);
        }

        public void AddLightRenderInstance(ref LightComponent lc, TransformData td)
        {
            GLLightBufferManager.AddRenderInstance(ref lc, td);
        }

        public void SetLightInstanceData(LightComponent lc)
        {
            GLLightBufferManager.SetLightInstanceData(lc);
        }

        public void RemoveLightRenderInstance(ref NbMesh mesh, LightComponent lc)
        {
            GLLightBufferManager.RemoveRenderInstance(ref mesh, lc);
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

        public static Texture CreateTexture(PixelInternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, bool generate_mipmaps)
        {
            Texture tex = new();
            tex.texID = GL.GenTexture();
            tex.target = NbTextureTarget.Texture2D;
            tex.Width = w;
            tex.Height = h;
            GL.BindTexture(TextureTargetMap[tex.target], tex.texID);
            GL.TexImage2D(TextureTargetMap[tex.target], 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);

            if (generate_mipmaps)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            return tex;
        }

        public static Texture CreateTexture(PixelInternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, bool generate_mipmaps)
        {
            Texture tex = new();
            tex.texID = GL.GenTexture();
            tex.target = NbTextureTarget.Texture2DArray;
            GL.BindTexture(TextureTargetMap[tex.target], tex.texID);
            GL.TexImage3D(TextureTargetMap[tex.target], 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);

            if (generate_mipmaps)
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);

            return tex;
        }

        public static void setupTextureParameters(Texture tex, int wrapMode, int magFilter, int minFilter, float af_amount)
        {

            GL.BindTexture(TextureTargetMap[tex.target], tex.texID);
            GL.TexParameter(TextureTargetMap[tex.target], TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(TextureTargetMap[tex.target], TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(TextureTargetMap[tex.target], TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(TextureTargetMap[tex.target], TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = System.Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(TextureTargetMap[tex.target], (TextureParameterName)0x84FE, af_amount);
        }

        #endregion

    }
}