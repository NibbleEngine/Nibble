using System;
using System.Collections.Generic;
using NbCore.Math;
using NbCore.Platform.Graphics;

#if OPENGL
    using OpenTK.Graphics.OpenGL;
    using OpenTK.Graphics;
#endif

namespace NbCore
{

    public enum NbFBOAttachment : uint
    {
        Attachment0,
        Attachment1,
        Attachment2,
        Attachment3,
        Attachment4,
        Depth
    }

    public struct FBOAttachmentDescription
    {
        public NbTextureTarget target;
        public NbTextureInternalFormat format;
    }

    [Flags]
    public enum FBOOptions
    {
        None = 0,
        MultiSample = 1
    }

    public class FBO : IDisposable
    {
#if OPENGL
        public FramebufferHandle fbo;
#else
        public int fbo = -1;
#endif
        private List<FBOAttachmentDescription> ColorAttachments;
        private FBOAttachmentDescription DepthAttachment;
        private FBOOptions options = FBOOptions.None;
        private Dictionary<NbFBOAttachment, NbTexture> textures;
        public int depth_channel = -1;
        
        //Buffer Specs
        public NbVector2i Size;
        public int msaa_samples = 8;

        public FBO(int x, int y, FBOOptions opts = FBOOptions.None)
        {
            //Setup properties
            Size = new(x, y);
            ColorAttachments = new();
            textures = new();
            options = opts;

            setup();
        }

        public NbTexture GetTexture(NbFBOAttachment attachment)
        {
            return textures[attachment];
        }

        public void setup()
        {
            //Init the main FBO
            FramebufferHandle handle = GL.GenFramebuffer();
            fbo = handle;

            //Main flags
            if (options.HasFlag(FBOOptions.MultiSample))
                GL.Enable(EnableCap.Multisample);
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferStatus.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle.Zero);
        }

        public void AddAttachment(NbTextureTarget target, NbTextureInternalFormat format, bool isDepth)
        {
            FBOAttachmentDescription attachment = new()
            {
                target = target,
                format = format
            };

            //TODO: Add check so that depth attachment is not overriden

            uint attachment_id = (uint) ColorAttachments.Count;
            
            if (!isDepth)
            {
                NbTexture tex = setup_texture(target, fbo, FramebufferAttachment.ColorAttachment0 + attachment_id, format);
                textures[NbFBOAttachment.Attachment0 + attachment_id] = tex;
                ColorAttachments.Add(attachment);
            } else
            {
                NbTexture tex = setup_texture(target, fbo, FramebufferAttachment.DepthAttachment, NbTextureInternalFormat.DEPTH);
                DepthAttachment = attachment;
                textures[NbFBOAttachment.Depth] = tex;
            }
        }

        public NbTexture setup_texture(NbTextureTarget target, FramebufferHandle attach_to_fbo, FramebufferAttachment attachment_id, NbTextureInternalFormat format)
        {
            TextureHandle handle = GL.GenTexture();
            InternalFormat pif = GraphicsAPI.InternalFormatMap[format];
            TextureTarget textarget = GraphicsAPI.TextureTargetMap[target];

            PixelType pixel_type = PixelType.Float;
            if (pif == InternalFormat.Rgba8)
                pixel_type = PixelType.UnsignedByte;


            if (textarget == TextureTarget.Texture2dMultisample)
            {
                GL.BindTexture(TextureTarget.Texture2dMultisample, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTarget.Texture2dMultisample, msaa_samples, pif, Size.X, Size.Y, true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, TextureTarget.Texture2dMultisample, handle, 0);

            }
            else if (textarget == TextureTarget.Texture2d)
            {
                GL.BindTexture(TextureTarget.Texture2d, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                //GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);

                switch (format)
                {
                    case NbTextureInternalFormat.DEPTH:
                        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.DepthComponent, Size.X, Size.Y, 0, PixelFormat.DepthComponent, pixel_type, IntPtr.Zero);
                        break;
                    default:
                        GL.TexImage2D(TextureTarget.Texture2d, 0, pif, Size.X, Size.Y, 0, PixelFormat.Rgba, pixel_type, IntPtr.Zero);
                        break;
                }

                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
                GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, TextureTarget.Texture2d, handle, 0);

            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferStatus.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer));


            //Create Texture
            NbTexture tex = new NbTexture()
            {
                Data = new NbTextureData()
                {
                    Width = Size.X,
                    Height = Size.Y,
                    Depth = 1,
                    MipMapCount = 1,
                    target = NbTextureTarget.Texture2D,
                    pif = format
                },
                texID = handle
            };
            
            return tex;
        }


        public void resize(int w, int h)
        {
            Size = new(w, h);
            Cleanup();
            
            setup();
            //Add color attachments
            NbTexture tex;
            for (int i = 0; i < ColorAttachments.Count; i++)
            {
                tex = setup_texture(ColorAttachments[i].target,
                                           fbo,
                                           FramebufferAttachment.ColorAttachment0 + (uint) i,
                                           ColorAttachments[i].format);
                textures[NbFBOAttachment.Attachment0 + (uint) i] = tex;
            }

            tex = setup_texture(DepthAttachment.target,
                                           fbo, FramebufferAttachment.DepthAttachment,
                                           DepthAttachment.format);
            textures[NbFBOAttachment.Depth] = tex;
        }

        public void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle.Zero);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            foreach (NbTexture tex in textures.Values)
                tex.Dispose();
            
            textures.Clear();
        }

        //STATIC HELPER METHODS

        public static void copyChannel(FramebufferHandle from_fbo, FramebufferHandle to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY,
            ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
        }

        public static void copyDepthChannel(FramebufferHandle from_fbo, FramebufferHandle to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        public static void copyChannel(FramebufferHandle fbo, int sourceSizeX, int sourceSizeY, ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            //Method 1: Use Blitbuffer
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, sourceSizeX, sourceSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }

        public static void dumpChannelToImage(FramebufferHandle fbo_id, ReadBufferMode from_channel, string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo_id);
            GL.ReadBuffer(from_channel);
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            NbTextureData tex = NbImagingAPI.Load(pixels, width, height, NbTextureInternalFormat.RGBA8);
            NbImagingAPI.ImageSave(tex, "Temp//framebuffer_raw_" + name + ".png");
        }

        //Disposable Stuff
        private bool disposedValue;
        
        private void Dispose(bool disposing)
        {
            if (disposedValue)
                return;

            if (disposing)
            {
                ColorAttachments.Clear();
            }
            
            //Free unmanaged resources
            GL.DeleteFramebuffer(fbo);
            foreach (NbTexture tex in textures.Values)
                tex.Dispose();

            disposedValue = true;
        }
        
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        
    }

//    public class PBuffer
//    {
//        public int fbo = -1;
        
//        //Pixel Bufffer Textures
//        public int color = -1;
//        public int blur1 = -1;
//        public int blur2 = -1;
//        public int composite = -1;
//        public int depth = -1;

//        //Buffer Specs
//        public int[] size;
//        public int msaa_samples = 8;


//        public PBuffer(int x, int y)
//        {
//            size = new int[] { x, y };
//            setup();
//        }

//        public void setup()
//        {
//            //Init the main FBO
//            fbo = GL.GenFramebuffer();

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Setup color texture
//            setup_texture(ref color, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(color, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
//            //Setup blur1 texture
//            setup_texture(ref blur1, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(blur1, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
//            //Setup blur2 texture
//            setup_texture(ref blur2, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(blur2, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
//            //Setup composite texture
//            setup_texture(ref composite, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(composite, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment3);
            
//            //Setup depth texture
//            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true);
//            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Revert Back the default fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//        }


//        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth)
//        {
//            handle = GL.GenTexture();
//            GL.BindTexture(textarget, handle);

//            if (textarget == TextureTarget.Texture2DMultisample)
//            {
//                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

//            }
//            else if (textarget == TextureTarget.Texture2D)
//            {
//                if (isDepth)
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                else
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);


//                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
//                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

//            }
//            else
//            {
//                throw new Exception("Unsupported texture target " + textarget);
//            }
//        }

//        //TODO: Organize this function a bit
//        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
//        {
//            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
//            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
//        }

//        public void Cleanup()
//        {

//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//            //Delete Buffer
//            GL.DeleteFramebuffer(fbo);

//            //Delete textures
//            GL.DeleteTexture(color);
//            GL.DeleteTexture(blur1);
//            GL.DeleteTexture(blur2);
//            GL.DeleteTexture(composite);
//            GL.DeleteTexture(depth);

//        }

//        public void resize(int w, int h)
//        {
//            size[0] = w;
//            size[1] = h;

//            Cleanup();
//            setup();
//        }


        

//    }

//    public class GBuffer : IDisposable
//    {
//        public int fbo = -1;

//        //Textures
//        public int albedo = -1;
//        public int normals = -1;
//        public int info = -1;
//        public int depth = -1;
//        public int depth_dump = -1;

//        //Buffer Specs
//        public int[] size;
//        public int msaa_samples = 4;

//        public GBuffer(int x, int y)
//        {
//            //Setup all stuff
//            //Init size to the current GLcontrol size
//            size = new int[] { x, y };

//            init();
//            setup();
//        }

//        public void setup()
//        {
//            //Init the main FBO
//            fbo = GL.GenFramebuffer();
            
//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Setup diffuse texture
//            setup_texture(ref albedo, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(albedo, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
//            //Setup normals texture
//            setup_texture(ref normals, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(normals, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
//            //Setup info texture 
//            setup_texture(ref info, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(info, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
//            //Setup Depth texture
//            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true, TextureMagFilter.Nearest, TextureMinFilter.Nearest);
//            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);
            
//            //Setup depth backup  texture
//            setup_texture(ref depth_dump, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true, TextureMagFilter.Nearest, TextureMinFilter.Nearest);

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Revert Back the default fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//        }

//        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth, TextureMagFilter texMagFilter, TextureMinFilter texMinFilter)
//        {
//            handle = GL.GenTexture();
//            GL.BindTexture(textarget, handle);

//            if (textarget == TextureTarget.Texture2DMultisample)
//            {
//                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
//            }
//            else if (textarget == TextureTarget.Texture2D)
//            {
//                if (isDepth)
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                else
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

//                //Setup Texture Parameters
//                GL.TexParameter(textarget, TextureParameterName.TextureMagFilter, (int)texMagFilter);
//                GL.TexParameter(textarget, TextureParameterName.TextureMinFilter, (int)texMinFilter);
//                //GL.TexParameter(textarget, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(textarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
//            }
//            else
//            {
//                throw new Exception("Unsupported texture target " + textarget);
//            }
//        }

//        //TODO: Organize this function a bit
//        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
//        {
//            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
//            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
//        }

        
//        public void init()
//        {
//            GL.Viewport(0, 0, size[0], size[1]);
//            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
//            //Main flags
//            GL.Enable(EnableCap.Multisample);
            
//            //Geometry Shader Parameters
//            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
//            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
//            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
//        }
        
//        public void bind()
//        {
//            //Bind Gbuffer fbo
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
//            GL.Viewport(0, 0, size[0], size[1]);
//            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0,
//                                                      DrawBuffersEnum.ColorAttachment1,
//                                                      DrawBuffersEnum.ColorAttachment2} );
//        }

//        public void clearColor(Vector4 col)
//        {
//            GL.ClearColor(col.X, col.Y, col.Z, col.W);
//        }

//        public void clear(ClearBufferMask mask)
//        {
//            GL.Clear(mask);
//        }

//        public void stop()
//        {
//            //Blit can replace the render & stop funtions
//            //Simply resolves and copies the ms offscreen fbo to the default framebuffer without any need to render the textures and to any other post proc effects
//            //I guess that I don't need the textures as well, when I'm rendering like this
//            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
//            //Blit
//            GL.BlitFramebuffer(0, 0, size[0], size[1],
//                                   0, 0, size[0], size[1],
//                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
//                                   BlitFramebufferFilter.Nearest);
//        }

//        public void dump()
//        {
//            //Bind Buffers
//            //Resolving Buffers
//            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

//            byte[] pixels = new byte[16 * size[0] * size[1]];
//            //pixels = new byte[4 * size[0] * size[1]];
//            //Console.WriteLine("Dumping Framebuffer textures " + size[0] + " " + size[1]);

//#if false
//            //Save Depth Texture
//            GL.BindTexture(TextureTarget.Texture2D, dump_depth);
//            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, pixels);

//            File.WriteAllBytes("dump.depth", pixels);
//#endif

//#if false
//            //Read Color0
//            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
//            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
//            GL.BlitFramebuffer(0, 0, size[0], size[1],
//                                   0, 0, size[0], size[1],
//                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
//                                   BlitFramebufferFilter.Nearest);


//            //Save Diffuse Color
//            GL.BindTexture(TextureTarget.Texture2D, dump_diff);
//            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

//            //File.WriteAllBytes("dump.color0", pixels);
//#endif


//            //Rebind Gbuffer fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
//            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
//            //GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

//        }

//        public void Cleanup()
//        {

//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//            //Delete Buffer
//            GL.DeleteFramebuffer(fbo);

//            //Delete textures
//            GL.DeleteTexture(albedo);
//            GL.DeleteTexture(normals);
//            GL.DeleteTexture(info);
//            GL.DeleteTexture(depth);
//            GL.DeleteTexture(depth_dump);
//        }

//        public void resize(int w, int h)
//        {
//            size[0] = w;
//            size[1] = h;
            
//            Cleanup();
//            setup();
//        }


        

//        #region IDisposable Support
//        private bool disposedValue = false; // To detect redundant calls

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!disposedValue)
//            {
//                if (disposing)
//                {
//                    // TODO: dispose managed state (managed objects).
//                    Cleanup();
//                }

//                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
//                // TODO: set large fields to null.

//                disposedValue = true;
//            }
//        }

//        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
//        //~GBuffer()
//        //{
//        //    Cleanup();
//        //    GL.DeleteBuffer(quad_vbo);
//        //    GL.DeleteBuffer(quad_ebo);
//        //}

//        // This code added to correctly implement the disposable pattern.
//        public void Dispose()
//        {
//            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
//            Dispose(true);
//            // TODO: uncomment the following line if the finalizer is overridden above.
//            // GC.SuppressFinalize(this);
//        }
//        #endregion


//    }

}
