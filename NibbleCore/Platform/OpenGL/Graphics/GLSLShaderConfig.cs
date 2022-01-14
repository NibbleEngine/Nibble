using System;
using System.Collections.Generic;
using System.Linq;

namespace NbCore.Platform.Graphics.OpenGL
{
    public class GLSLShaderConfig : Entity
    {
        public string Name = "";

        public Dictionary<NbShaderType, GLSLShaderSource> Sources = new();
        public Dictionary<NbShaderType, int> SourceObjects = new();

        public List<string> directives = new();

        //Store the raw shader text temporarily
        public SHADER_TYPE shader_type = SHADER_TYPE.NULL_SHADER;

        public SHADER_MODE ShaderMode = SHADER_MODE.DEFAULT;
        
        //Program ID
        public int ProgramID = -1;
        public int Hash = -1; //Should contain the hashcode of all the material related preprocessor flags (is set externally)
        //Shader Compilation log
        public string CompilationLog = "";

        public GLSLShaderState CurrentState = GLSLShaderState.Create(); //Empty state

        //Keep active uniforms
        public Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        //Default shader versions
        public const string version = "#version 450\n #extension GL_ARB_explicit_uniform_location : enable\n" +
                                       "#extension GL_ARB_separate_shader_objects : enable\n" +
                                       "#extension GL_ARB_texture_query_lod : enable\n" +
                                       "#extension GL_ARB_gpu_shader5 : enable\n";

        public GLSLShaderConfig() : base(EntityType.Shader)
        {

        }

        public GLSLShaderConfig(SHADER_TYPE type, GLSLShaderSource vvs,
            GLSLShaderSource ffs, GLSLShaderSource ggs,
            GLSLShaderSource ttcs, GLSLShaderSource ttes,
            List<string> directives, SHADER_MODE mode) : base(EntityType.Shader)
        {
            shader_type = type; //Set my custom shader type for recognition
            ShaderMode = mode;

            //Store objects
            AddSource(NbShaderType.VertexShader, vvs);
            AddSource(NbShaderType.FragmentShader, ffs);
            AddSource(NbShaderType.GeometryShader, ggs);
            AddSource(NbShaderType.TessControlShader, ttcs);
            AddSource(NbShaderType.TessEvaluationShader, ttes);
                
            foreach (string d in directives)
                this.directives.Add(d);

        }

        public void AddSource(NbShaderType t, GLSLShaderSource s)
        {
            if (s == null)
                return;
            Sources[t] = s;
            SourceObjects[t] = -1;

            //Add shader reference to source object
            if (!s.ReferencedByShaders.Contains(this))
                s.ReferencedByShaders.Add(this);
        }

        public void ClearCurrentState()
        {
            CurrentState.Clear();
        }

        public void FilterState(ref GLSLShaderState state)
        {
            //Floats;
            var arr = state.Floats.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr[i].Key))
                    state.Floats.Remove(arr[i].Key);
            }

            //Vec2s
            var arr1 = state.Vec2s.ToArray();
            for (int i = 0; i < arr1.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr1[i].Key))
                    state.Vec2s.Remove(arr1[i].Key);
            }

            //Vec3s
            var arr3 = state.Vec3s.ToArray();
            for (int i = 0; i < arr3.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr3[i].Key))
                    state.Vec3s.Remove(arr3[i].Key);
            }

            //Vec4s
            var arr4 = state.Vec4s.ToArray();
            for (int i = 0; i < arr4.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr4[i].Key))
                    state.Vec4s.Remove(arr4[i].Key);
            }

        }
    }


}
