using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NbCore
{
    public class PNGImage : NbTextureData
    {
        public PNGImage(byte[] data)
        {
            MemoryStream ms = new MemoryStream(Data);

            //Load the image from file
            Image<Bgra32> bmpTexture = Image.Load<Bgra32>(ms);
#if DEBUG
            bmpTexture.Save("test_image.bmp");
#endif
            //TODO: Check if we need to keep pixels at this level
            Span<Bgra32> pixels;
            bmpTexture.TryGetSinglePixelSpan(out pixels);
            
            unsafe
            {
                Data = new byte[pixels.Length * sizeof(Bgra32)];
                Buffer.BlockCopy(pixels.ToArray(), 0, Data, 0, Data.Length);
            }
            
            Width = bmpTexture.Width;
            Height = bmpTexture.Height;
            MipMapCount = 1;
            Depth = 1;
            target = NbTextureTarget.Texture2D;
            pif = NbTextureInternalFormat.RGBA;

            ms.Close();
        }

        public override MemoryStream Export()
        {
            throw new NotImplementedException();
        }
    }
}
