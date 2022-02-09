using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Math;

namespace NbCore
{
    [Flags]
    public enum NbShaderMode
    {
        DEFAULT,
        DEFFERED,
        LIT,
        FORWARD,
        DECAL
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

    public struct NbSamplerState
    {
        public string ShaderBinding;
        public int ShaderLocation;
        public int TextureID;
        public int SamplerID; //Should be translated to a TexUnit on render
        public NbTextureTarget Target;
    }

    public struct NbUniformState
    {
        public string ShaderBinding;
        public int ShaderLocation;
        public NbUniformType Type;
    }

    public struct NbShaderState
    {
        //splitting per 2s,3s,4s is so fucking stupid. TODO: FIX it
        public Dictionary<string, NbVector2> Vec2s;
        public Dictionary<string, NbVector3> Vec3s;
        public Dictionary<string, NbVector4> Vec4s;
        public Dictionary<string, float> Floats;
        public Dictionary<string, NbSamplerState> Samplers;

        public static NbShaderState Create()
        {
            NbShaderState state;
            state.Vec2s = new();
            state.Vec3s = new();
            state.Vec4s = new();
            state.Floats = new();
            state.Samplers = new();

            return state;
        }

        public void AddUniform(string name, NbVector2 vec)
        {
            Vec2s[name] = vec;
        }

        public void AddUniform(string name, NbVector3 vec)
        {
            Vec3s[name] = vec;
        }

        public void AddUniform(string name, NbVector4 vec)
        {
            Vec4s[name] = vec;
        }

        public void AddUniform(string name, float val)
        {
            Floats[name] = val;
        }

        public void AddSampler(string name, NbSamplerState val)
        {
            Samplers[name] = val;
        }

        public void Clear()
        {
            Vec3s.Clear();
            Vec4s.Clear();
            Floats.Clear();
            Samplers.Clear();
        }

    }

    public enum NbUniformType
    {
        Bool,
        Sampler2D,
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
