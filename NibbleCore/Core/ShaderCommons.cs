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
        DEFAULT = 1 << 0,
        DEFFERED = 1 << 1,
        LIGHTING = 1 << 2,
        FORWARD = 1 << 3,
        DECAL = 1 << 4,
        SKINNED = 1 << 5,
        NO_TRANSFORM = 1 << 6,
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

        public void SetUniform(string name, float val)
        {
            string uniform_name = "Float: " + name;
            if (Data.ContainsKey(uniform_name))
            {
                Data[uniform_name] = val;
            }
        }

        public void SetUniform(string name, int val)
        {
            string uniform_name = "Int: " + name;
            if (Data.ContainsKey(uniform_name))
            {
                Data[uniform_name] = val;
            }
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

}
