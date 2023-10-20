using NbCore.Common;
using NbCore.Platform.Graphics;
using System.Collections;
using System;
using System.Collections.Generic;

namespace NbCore
{
    public enum NbFBOAttachment
    {
        Attachment0,
        Attachment1,
        Attachment2,
        Attachment3,
        Attachment4,
        Attachment5,
        Attachment6,
        Attachment7,
        Attachment8,
        Attachment9,
        Attachment10,
        Depth,
        DepthStencil
    }

    public struct FBOAttachmentDescription
    {
        public NbTextureTarget target;
        public NbTextureInternalFormat format;
        public NbTextureFilter magFilter;
        public NbTextureFilter minFilter;
    }

    [Flags]
    public enum FBOOptions
    {
        None = 0,
        MultiSample = 1
    }

    public class FBO : IDisposable
    {
        public int fbo = -1;
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
        }

        public NbTexture GetTexture(NbFBOAttachment attachment)
        {
            return textures[attachment];
        }

        public void AddAttachment(NbTextureTarget target, NbTextureInternalFormat format,
            NbTextureFilter mag_filter, NbTextureFilter min_filter, NbFBOAttachment attachment_type, 
            NbVector2i texture_size, bool attach = true)
        {
            FBOAttachmentDescription attachment = new()
            {
                target = target,
                format = format,
                magFilter = mag_filter,
                minFilter = min_filter
            };

            GraphicsAPI renderer = NbRenderState.engineRef.GetRenderer();

            //Create Texture
            NbTexture tex = new NbTexture()
            {
                Data = new NbTextureData()
                {
                    Width = texture_size.X,
                    Height = texture_size.Y,
                    Depth = 1,
                    MipMapCount = 1,
                    target = target,
                    pif = format,
                    MagFilter = mag_filter,
                    MinFilter = min_filter,
                }
            };

            renderer.AddFrameBufferAttachment(this, tex, attachment_type, attach);
            textures[attachment_type] = tex;

            switch (attachment_type)
            {
                case NbFBOAttachment.Depth:
                case NbFBOAttachment.DepthStencil:
                    DepthAttachment = attachment;
                    break;
                default:
                    ColorAttachments.Add(attachment);
                    break;
            }
        }

        //STATIC HELPER METHODS

        public static void copyDepthChannel(FBO from, FBO to)
        {

            GraphicsAPI renderer = NbRenderState.engineRef.GetRenderer();
            renderer.CopyDepthChannel(from, to);
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

            GraphicsAPI renderer = NbRenderState.engineRef.GetRenderer();
            renderer.DeleteFrameBuffer(this);

            foreach (NbTexture tex in textures.Values)
            {
                renderer.DeleteTexture(tex);
                tex.Dispose();
            }

            textures.Clear();
            disposedValue = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }

}
