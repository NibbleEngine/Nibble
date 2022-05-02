using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Math;
using Newtonsoft.Json;

namespace NbCore
{
    [Flags]
    public enum NbShaderMode
    {
        DEFAULT,
        DEFFERED,
        LIT,
        FORWARD,
        DECAL,
        SKINNED
    }

    public enum NbShaderType
    {
        NULL_SHADER = 0x0,
        MESH_FORWARD_SHADER,
        MESH_DEFERRED_SHADER,
        DECAL_SHADER,
        DEBUG_MESH_SHADER,
        GIZMO_SHADER,
        PICKING_SHADER,
        BBOX_SHADER,
        LOCATOR_SHADER,
        JOINT_SHADER,
        CAMERA_SHADER,
        TEXTURE_MIX_SHADER,
        PASSTHROUGH_SHADER,
        RED_FILL_SHADER,
        LIGHT_SHADER,
        TEXT_SHADER,
        MATERIAL_SHADER,
        GBUFFER_SHADER,
        LIGHT_PASS_LIT_SHADER,
        LIGHT_PASS_UNLIT_SHADER, //Stupid but keeping that for testing...
        BRIGHTNESS_EXTRACT_SHADER,
        GAUSSIAN_HORIZONTAL_BLUR_SHADER,
        GAUSSIAN_VERTICAL_BLUR_SHADER,
        ADDITIVE_BLEND_SHADER,
        FXAA_SHADER,
        TONE_MAPPING,
        INV_TONE_MAPPING,
        BWOIT_COMPOSITE_SHADER
    }



    public enum NbShaderTextType
    {
        Static,
        Dynamic
    }

    public enum NbShaderSourceType
    {
        FragmentShader,
        VertexShader,
        GeometryShader,
        TessEvaluationShader,
        TessControlShader,
        ComputeShader,
        None
    }

    public struct NbUniformState
    {
        [NbSerializable]
        public string ShaderBinding;
        [NbSerializable]
        public int ShaderLocation;
        [NbSerializable]
        public NbUniformType Type;
    }

    public struct NbShaderState
    {
        public Dictionary<string, object> Data;
        
        public static NbShaderState Create()
        {
            NbShaderState state;
            state.Data = new();
            return state;
        }

        public void AddUniform(string name, NbVector2 vec)
        {
            Data["Vec2:" + name] = vec;
        }

        public void AddUniform(string name, NbVector3 vec)
        {
            Data["Vec3:" + name] = vec;
        }

        public void AddUniform(string name, NbVector4 vec)
        {
            Data["Vec4:" + name] = vec;
        }

        public void AddUniform(string name, float val)
        {
            Data["Float:" + name] = val;
        }

        public void AddSampler(string name, NbSampler val)
        {
            Data["Sampler:" + name] = val;
        }

        public void Clear()
        {
            Data.Clear();
        }

    }

    public enum NbUniformType
    {
        Bool,
        Sampler2D,
        Sampler3D,
        Sampler2DArray,
        Float,
        Vector2,
        Vector3,
        Vector4,
        Int,
        IVector2,
        IVector3,
        IVector4,
        Matrix3,
        Matrix4
    }

    public struct NbUniformFormat
    {
        public int loc;
        public string name;
        public NbUniformType type;
        public int count;
    }

    public class NbShaderCompilationRequest
    {
        public MeshMaterial mat;
        public NbShader shader;
        public GLSLShaderConfig config;
    }

}
