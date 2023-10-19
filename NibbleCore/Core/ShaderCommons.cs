using System;
using System.Collections.Generic;
using System.Text;
using NbCore;
using Newtonsoft.Json;

namespace NbCore
{
    [Flags]
    public enum NbShaderMode
    {
        DEFAULT = 1,
        DEFFERED = 2,
        LIT = 4,
        FORWARD = 8,
        DECAL = 16,
        SKINNED = 32
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
        LIGHT_PASS_LIT_SHADER, //18
        LIGHT_PASS_STENCIL_SHADER,
        LIGHT_PASS_UNLIT_SHADER, //20: Stupid but keeping that for testing...
        BRIGHTNESS_EXTRACT_SHADER, 
        DOWNSAMPLING_SHADER, //22 //Used for bloom effect
        UPSAMPLING_SHADER, //Used for bloom effect
        GAUSSIAN_HORIZONTAL_BLUR_SHADER, //24
        GAUSSIAN_VERTICAL_BLUR_SHADER,
        ADDITIVE_BLEND_SHADER, //26
        FXAA_SHADER, 
        TONE_MAPPING,//28 
        INV_TONE_MAPPING,
        BWOIT_COMPOSITE_SHADER,//30
        MIX_SHADER
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

        public void AddUniform(string name, int val)
        {
            Data["Int:" + name] = val;
        }

        public void AddSampler(string name, NbSampler val)
        {
            Data["Sampler:" + name] = val;
        }

        public void RemoveUniform(string name)
        {
            foreach (string key in Data.Keys)
            {
                if (key.Contains(name))
                {
                    Data.Remove(name);
                    break;
                }
            }
        }

        public void Clear()
        {
            Data.Clear();
        }

    }

    public enum NbUniformType
    {
        Bool = 0x0,
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
        public NbMaterial mat;
        public NbShader shader;
        public NbShaderConfig config;
    }

}
