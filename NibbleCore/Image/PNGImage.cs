using System;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NbCore
{
    public class PNGImage : NbTextureData
    {
        public PNGImage(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);

            //Load the image from file
            Image<Bgra32> bmpTexture = Image.Load<Bgra32>(ms);
#if DEBUG
            bmpTexture.Save("test_image.bmp");
#endif
            //TODO: Check if we need to keep pixels at this level
            Memory<Bgra32> pixels;
            bmpTexture.DangerousTryGetSinglePixelMemory(out pixels);
            
            unsafe 
            {
                var bytes = MemoryMarshal.AsBytes(pixels.Span);
                Data = new byte[bytes.Length];
                using (ms = new MemoryStream(Data))
                {
                    ms.Write(bytes);
                }
            }
            
            Width = bmpTexture.Width;
            Height = bmpTexture.Height;
            MipMapCount = 1;
            Depth = 1;
            target = NbTextureTarget.Texture2D;
            pif = NbTextureInternalFormat.RGBA8;

            ms.Close();
        }

        public override MemoryStream Export()
        {
            throw new NotImplementedException();
        }
    }
}
