using System;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace NbCore.UI.ImGui
{
    public enum TextureCoordinate
    {
        S = (int) TextureParameterName.TextureWrapS,
        T = (int) TextureParameterName.TextureWrapT,
        R = (int) TextureParameterName.TextureWrapR
    }

    class ImGuiTexture : IDisposable
    {
        public const SizedInternalFormat Srgb8Alpha8 = (SizedInternalFormat)All.Srgb8Alpha8;
        public const SizedInternalFormat RGB32F = (SizedInternalFormat)All.Rgb32f;

        public const GetPName MAX_TEXTURE_MAX_ANISOTROPY = (GetPName)0x84FF;

        public static readonly float MaxAniso;

        static ImGuiTexture()
        {
            GL.GetFloat(MAX_TEXTURE_MAX_ANISOTROPY, ref MaxAniso);
        }

        public readonly string Name;
        public readonly TextureHandle GLTexture;
        public readonly int Width, Height;
        public readonly int MipmapLevels;
        public readonly SizedInternalFormat InternalFormat;

        public ImGuiTexture(string name, NbTextureData texture, bool generateMipmaps, bool srgb)
        {
            Name = name;
            Width = texture.Width;
            Height = texture.Height;
            InternalFormat = srgb ? Srgb8Alpha8 : SizedInternalFormat.Rgba8;

            if (generateMipmaps)
            {
                // Calculate how many levels to generate for this texture
                MipmapLevels = (int) System.Math.Floor(System.Math.Log(System.Math.Max(Width, Height), 2));
            }
            else
            {
                // There is only one level
                MipmapLevels = 1;
            }

            ImGuiUtil.CheckGLError("Clear");

            ImGuiUtil.CreateTexture(TextureTarget.Texture2d, Name, out GLTexture);
            GL.TextureStorage2D(GLTexture, MipmapLevels, InternalFormat, Width, Height);
            ImGuiUtil.CheckGLError("Storage2d");

            unsafe
            {
                fixed(byte* ptr = texture.Data) 
                {
                    GL.TextureSubImage2D(GLTexture, 0, 0, 0, Width, Height, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
                }
            }

            ImGuiUtil.CheckGLError("SubImage");
            if (generateMipmaps) GL.GenerateTextureMipmap(GLTexture);

            GL.TextureParameteri(GLTexture, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            ImGuiUtil.CheckGLError("WrapS");
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            ImGuiUtil.CheckGLError("WrapT");

            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMinFilter, (int)(generateMipmaps ? TextureMinFilter.Linear : TextureMinFilter.LinearMipmapLinear));
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            ImGuiUtil.CheckGLError("Filtering");

            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMaxLevel, MipmapLevels - 1);

        }

        public ImGuiTexture(string name, TextureHandle GLTex, int width, int height, int mipmaplevels, SizedInternalFormat internalFormat)
        {
            Name = name;
            GLTexture = GLTex;
            Width = width;
            Height = height;
            MipmapLevels = mipmaplevels;
            InternalFormat = internalFormat;
        }

        public ImGuiTexture(string name, int width, int height, IntPtr data, bool generateMipmaps = false, bool srgb = false)
        {
            Name = name;
            Width = width;
            Height = height;
            InternalFormat = srgb ? Srgb8Alpha8 : SizedInternalFormat.Rgba8;
            MipmapLevels = generateMipmaps == false ? 1 : (int)System.Math.Floor(System.Math.Log(System.Math.Max(Width, Height), 2));

            ImGuiUtil.CreateTexture(TextureTarget.Texture2d, Name, out GLTexture);
            GL.TextureStorage2D(GLTexture, MipmapLevels, InternalFormat, Width, Height);

            GL.TextureSubImage2D(GLTexture, 0, 0, 0, Width, Height, PixelFormat.Bgra, PixelType.UnsignedByte, data);

            if (generateMipmaps) GL.GenerateTextureMipmap(GLTexture);

            SetWrap(TextureCoordinate.S, TextureWrapMode.Repeat);
            SetWrap(TextureCoordinate.T, TextureWrapMode.Repeat);

            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMaxLevel, MipmapLevels - 1);
        }

        public void SetMinFilter(TextureMinFilter filter)
        {
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMinFilter, (int)filter);
        }

        public void SetMagFilter(TextureMagFilter filter)
        {
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMagFilter, (int)filter);
        }

        public void SetAnisotropy(float level)
        {
            const TextureParameterName TEXTURE_MAX_ANISOTROPY = (TextureParameterName)0x84FE;
            GL.TextureParameteri(GLTexture, TEXTURE_MAX_ANISOTROPY, (int) ImGuiUtil.Clamp(level, 1, MaxAniso));
        }

        public void SetLod(int @base, int min, int max)
        {
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureLodBias, @base);
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMinLod, min);
            GL.TextureParameteri(GLTexture, TextureParameterName.TextureMaxLod, max);
        }

        public void SetWrap(TextureCoordinate coord, TextureWrapMode mode)
        {
            GL.TextureParameteri(GLTexture, (TextureParameterName)coord, (int)mode);
        }

        public void Dispose()
        {
            GL.DeleteTexture(GLTexture);
        }
    }
}
