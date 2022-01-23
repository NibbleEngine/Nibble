using System.Collections.Generic;
using NbCore.Common;
using NbCore.Math;
using NbCore.Platform.Graphics.OpenGL;

namespace NbCore.Platform.Graphics
{
    public enum NbBufferMask
    {
        Color,
        Depth
    }

    public interface IGraphicsApi
    {
        public void Init();
        public void SetProgram(int progra_id);
        public void ResizeViewport(int width, int height);
        public void SetCameraData(Camera cam);
        public void SetRenderSettings(RenderSettings settings);
        public void SetCommonDataPerFrame(FBO gBuffer, NbMatrix4 rotMat, double time);
        public void UploadFrameData();
        public int CreateGroupBuffer();
        public void DestroyGroupBuffer(int id);

        //Shader Compilation
        public void EnableMaterialProgram(MeshMaterial mat);
        public void EnableShaderProgram(NbShader shader);
        public void CompileShader(ref NbShader shader, GLSLShaderConfig config, MeshMaterial mat);
        public void CompileShader(ref NbShader shader, GLSLShaderConfig config);
        public void CompileShader(MeshMaterial mat);
        public void AttachUBOToShaderBindingPoint(NbShader shader_conf, string block_name, int binding_point);
        public void AttachSSBOToShaderBindingPoint(NbShader shader_conf, string block_name, int binding_point);
        
        
        //Mesh Buffer Methods
        public void PrepareMeshBuffers();
        public void UnbindMeshBuffers();

        //Render Instance Manipulation
        public void AddMesh(NbMesh mesh);
        public void AddRenderInstance(ref MeshComponent mc, TransformData td);
        public void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc);
        public void AddLightRenderInstance(ref LightComponent mc, TransformData td);
        public void RemoveLightRenderInstance(ref NbMesh mesh, LightComponent mc);
        public void SetLightInstanceData(LightComponent lc);
        public void SetInstanceWorldMat(NbMesh mesh, int instanceID, NbMatrix4 mat);
        public void SetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID, NbVector4 uf);
        public void SetInstanceWorldMatInv(NbMesh mesh, int instanceId, NbMatrix4 mat);
        public NbVector4 GetInstanceUniform4(NbMesh mesh, int instanceID, int uniformID);

        //Rendering Methods
        public void RenderQuad(NbMesh quadMesh, NbShader shader, NbShaderState state);
        public void RenderMesh(NbMesh mesh); //Direct mesh rendering, without any shader, uniform uploads
        public void RenderMesh(NbMesh mesh, MeshMaterial mat);
        public void RenderLocator(NbMesh mesh, MeshMaterial mat);
        public void RenderJoint(NbMesh mesh, MeshMaterial mat);
        public void RenderCollision(NbMesh mesh, MeshMaterial mat);
        public void RenderLight(NbMesh mesh, MeshMaterial mat);
        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat);

        //Framebuffer Methods
        public void ClearDrawBuffer(NbBufferMask mask);
        public void BindDrawFrameBuffer(FBO framebuffer, int[] drawBuffers);
        public FBO CreateFrameBuffer(int width, int height);

        //Viewport
        public void Viewport(int x, int y);
            
        //Misc
        public void SyncGPUCommands();
        public void ClearColor(NbVector4 col);
        public void EnableBlend();
    }


}