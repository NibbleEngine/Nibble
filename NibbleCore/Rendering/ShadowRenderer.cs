using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace NbCore
{
    public class ShadowRenderer : IDisposable
    {
        //Shadow Map dimensions
        private int SHADOW_WIDTH = 1024;
        private int SHADOW_HEIGHT = 1024;

        //Local depth framebuffer
        public FramebufferHandle depth_fbo;
        //Local depth texture id
        public TextureHandle depth_tex;

        //Default Constructor
        public ShadowRenderer()
        {
            //Setup framebuffer
            depth_fbo = GL.GenFramebuffer();
            depth_tex = GL.GenTexture();

            //Setup depth texture
            GL.BindTexture(TextureTarget.TextureCubeMap, depth_tex);
            for (int i = 0; i < 6; i++)
            {
                GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + (uint) i, 0, InternalFormat.DepthComponent,
                    SHADOW_WIDTH, SHADOW_HEIGHT, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            }

            GL.TexParameteri(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
            GL.TexParameteri(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
            GL.TexParameteri(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameteri(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameteri(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int) TextureWrapMode.ClampToEdge);


            //Bind Texture to the depth buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, depth_fbo);
            GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, depth_tex, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle.Zero);

        }

        

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    GL.DeleteTexture(depth_tex);
                    GL.DeleteFramebuffer(depth_fbo);
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
