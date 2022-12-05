using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace NbCore.UI.ImGui
{
    static class ImGuiUtil
    {
        [Pure]
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        [Conditional("DEBUG")]
        public static void CheckGLError(string title)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Debug.Print($"{title}: {error}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LabelObject(ObjectIdentifier objLabelIdent, uint glObject, string name)
        {
            GL.ObjectLabel(objLabelIdent, glObject, name.Length, name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateTexture(TextureTarget target, string Name, out TextureHandle Texture)
        {
            Texture = GL.CreateTexture(target);
            LabelObject(ObjectIdentifier.Texture, (uint) Texture.Handle, $"Texture: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateProgram(string Name, out ProgramHandle Program)
        {
            Program = GL.CreateProgram();
            LabelObject(ObjectIdentifier.Program, (uint) Program.Handle, $"Program: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateShader(ShaderType type, string Name, out ShaderHandle Shader)
        {
            Shader = GL.CreateShader(type);
            LabelObject(ObjectIdentifier.Shader, (uint) Shader.Handle, $"Shader: {type}: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateBuffer(string Name, out BufferHandle Buffer)
        {
            Buffer = GL.CreateBuffer();
            LabelObject(ObjectIdentifier.Buffer, (uint) Buffer.Handle, $"Buffer: {Name}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateVertexBuffer(string Name, out BufferHandle Buffer) => CreateBuffer($"VBO: {Name}", out Buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateElementBuffer(string Name, out BufferHandle Buffer) => CreateBuffer($"EBO: {Name}", out Buffer);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CreateVertexArray(string Name, out VertexArrayHandle VAO)
        {
            VAO = GL.CreateVertexArray();
            LabelObject(ObjectIdentifier.VertexArray, (uint) VAO.Handle, $"VAO: {Name}");
        }
    }
}