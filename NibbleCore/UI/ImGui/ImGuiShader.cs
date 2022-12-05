using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System.Transactions;
using OpenTK.Compute.OpenCL;

namespace NbCore.UI.ImGui
{
    struct UniformFieldInfo
    {
        public int Location;
        public string Name;
        public int Size;
        public UniformType Type;
    }

    class ImGuiShader
    {
        public readonly string Name;
        public ProgramHandle Program { get; private set; }
        private readonly Dictionary<string, int> UniformToLocation = new Dictionary<string, int>();
        private bool Initialized = false;

        private readonly (ShaderType Type, string Path)[] Files;

        public ImGuiShader(string name, string vertexShader, string fragmentShader)
        {
            Name = name;
            Files = new[]{
                (ShaderType.VertexShader, vertexShader),
                (ShaderType.FragmentShader, fragmentShader),
            };
            Program = CreateProgram(name, Files);
        }
        public void UseShader()
        {
            GL.UseProgram(Program);
        }

        public void Dispose()
        {
            if (Initialized)
            {
                GL.DeleteProgram(Program);
                Initialized = false;
            }
        }

        public UniformFieldInfo[] GetUniforms()
        {
            int UniformCount = 0;
            GL.GetProgrami(Program, ProgramPropertyARB.ActiveUniforms, ref UniformCount);

            UniformFieldInfo[] Uniforms = new UniformFieldInfo[UniformCount];

            for (int i = 0; i < UniformCount; i++)
            {
                int name_length = 0;
                int size = 0;
                UniformType type = UniformType.Int;
                string name = "";

                GL.GetActiveUniform(Program, (uint)i, 100, ref name_length, ref size, ref type, out name);

                UniformFieldInfo FieldInfo;
                FieldInfo.Location = GetUniformLocation(name);
                FieldInfo.Name = name;
                FieldInfo.Size = size;
                FieldInfo.Type = type;

                Uniforms[i] = FieldInfo;
            }

            return Uniforms;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetUniformLocation(string uniform)
        {
            if (UniformToLocation.TryGetValue(uniform, out int location) == false)
            {
                location = GL.GetUniformLocation(Program, uniform);
                UniformToLocation.Add(uniform, location);

                if (location == -1)
                {
                    Debug.Print($"The uniform '{uniform}' does not exist in the shader '{Name}'!");
                }
            }

            return location;
        }

        private ProgramHandle CreateProgram(string name, params (ShaderType Type, string source)[] shaderPaths)
        {
            ImGuiUtil.CreateProgram(name, out ProgramHandle Program);

            ShaderHandle[] Shaders = new ShaderHandle[shaderPaths.Length];
            for (int i = 0; i < shaderPaths.Length; i++)
            {
                Shaders[i] = CompileShader(name, shaderPaths[i].Type, shaderPaths[i].source);
            }

            foreach (var shader in Shaders)
                GL.AttachShader(Program, shader);

            GL.LinkProgram(Program);

            int Success = -1;
            GL.GetProgrami(Program, ProgramPropertyARB.LinkStatus, ref Success);
            if (Success == 0)
            {
                GL.GetProgramInfoLog(Program, out string Info);
                Debug.WriteLine($"GL.LinkProgram had info log [{name}]:\n{Info}");
            }

            foreach (var Shader in Shaders)
            {
                GL.DetachShader(Program, Shader);
                GL.DeleteShader(Shader);
            }

            Initialized = true;

            return Program;
        }

        private ShaderHandle CompileShader(string name, ShaderType type, string source)
        {
            ImGuiUtil.CreateShader(type, name, out ShaderHandle Shader);
            GL.ShaderSource(Shader, source);
            GL.CompileShader(Shader);

            int success = -1;
            GL.GetShaderi(Shader, ShaderParameterName.CompileStatus, ref success);
            if (success == 0)
            {
                GL.GetShaderInfoLog(Shader, out string Info);
                Debug.WriteLine($"GL.CompileShader for shader '{Name}' [{type}] had info log:\n{Info}");
            }

            return Shader;
        }
    }
}
