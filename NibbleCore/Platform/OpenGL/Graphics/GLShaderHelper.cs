using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Generic;
using System.Linq;
using NbCore;
using NbCore.Math;
using NbCore.Common;
using System.Windows;
using NbCore.Utils;


namespace NbCore.Platform.Graphics.OpenGL { 

    [Flags]
    public enum SHADER_MODE
    {
        DEFAULT,
        DEFFERED,
        LIT,
        FORWARD,
        DECAL
    }
    
    public enum SHADER_TYPE
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

    public enum NbShaderSourceType
    {
        Static,
        Dynamic
    }

    public enum NbShaderType
    {
        FragmentShader,
        VertexShader,
        GeometryShader,
        TessEvaluationShader,
        TessControlShader,
        ComputeShader,
        None
    }

    public struct GLSLSamplerState
    {
        public int TextureID;
        public NbTextureTarget Target;

        public GLSLSamplerState(int id, NbTextureTarget target)
        {
            TextureID = id;
            Target = target;
        }
    }

    public struct GLSLShaderState
    {
        //splitting per 2s,3s,4s is so fucking stupid. TODO: FIX it
        public Dictionary<string, NbVector2> Vec2s;
        public Dictionary<string, NbVector3> Vec3s;
        public Dictionary<string, NbVector4> Vec4s;
        public Dictionary<string, float> Floats;
        public Dictionary<string, GLSLSamplerState> Samplers;

        public static GLSLShaderState Create()
        {
            GLSLShaderState state;
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

        public void AddSampler(string name, GLSLSamplerState val)
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


    public static class GLShaderHelper
    {
        static public string NumberLines(string s)
        {
            if (s == "")
                return s;
                
            string n_s = "";
            string[] split = s.Split('\n');

            for (int i = 0; i < split.Length; i++)
                n_s += (i + 1).ToString() + ": " + split[i] + "\n";
            
            return n_s;
        }

        //Shader Compilation

        public static void reportUBOs(GLSLShaderConfig shader_conf)
        {
            //Print Debug Information for the UBO
            // Get named blocks info
            int test_program = shader_conf.ProgramID;
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

        public static void throwCompilationError(string log)
        {
            //Lock execution until the file is available
            string log_file = "shader_compilation_log.out";

            if (!File.Exists(log_file))
                File.Create(log_file);

            while (!FileUtils.IsFileReady(log_file))
            {
                Console.WriteLine("Log File not ready yet");
            };
            
            StreamWriter sr = new StreamWriter(log_file);
            sr.Write(log);
            sr.Close();
            Console.WriteLine(log);
            Callbacks.Assert(false, "Shader Compilation Failed. Check Log");
        }
    }
}


