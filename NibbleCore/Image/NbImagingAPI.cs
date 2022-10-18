using NbCore.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using OpenTK.Graphics.ES11;
using SixLabors.ImageSharp.PixelFormats;

namespace NbCore
{
    public enum NbUncompressedImageFormat
    {
        PNG,
        JPEG,
        BMP
    }

    public static class NbImagingAPI
    {
        private static bool Initialized = false;
        
        private static Image ImageLoad(byte[] data)
        {
            return Image.Load(data);
        }

        private static Image GetImageFromTetureData(NbTextureData tex)
        {
            switch (tex.pif)
            {
                case NbTextureInternalFormat.RGBA8:
                    return Image.LoadPixelData<Rgba32>(tex.Data, tex.Width, tex.Height);
                case NbTextureInternalFormat.BGRA8:
                    return Image.LoadPixelData<Bgra32>(tex.Data, tex.Width, tex.Height);
            }
            return null;
        }
        
        private static NbTextureData GetTextureDataFromImage(Image<Rgba32> image)
        {
            byte[] pixel_data = new byte[image.Width * image.Height * Marshal.SizeOf(image.GetType().GenericTypeArguments[0])];
            image.CopyPixelDataTo(pixel_data);
            return new()
            {
                Width = image.Width,
                Height = image.Height,
                Data = pixel_data,
                MipMapCount = 1,
                Depth = 1,
                target = NbTextureTarget.Texture2D,
                pif = NbTextureInternalFormat.RGBA8
            };
        }
        
        //Public Methods
        public static NbTextureData Load(byte[] pixeldata, int width, int height, NbTextureInternalFormat fmt)
        {
            switch (fmt)
            {
                case NbTextureInternalFormat.RGBA8:
                    {
                        Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixeldata, width, height);
                        return GetTextureDataFromImage(image);
                    }
                case NbTextureInternalFormat.BGRA8:
                    {
                        Image<Bgra32> image = Image.LoadPixelData<Bgra32>(pixeldata, width, height);
                        return GetTextureDataFromImage(image.CloneAs<Rgba32>());
                    }
            }
            return null;
        }

        public static NbTextureData Load(byte[] image_data)
        {
            Image image = Image.Load(image_data);
            Image<Rgba32> new_image = image.CloneAs<Rgba32>();
            return GetTextureDataFromImage(new_image);
        }

        public static NbTextureData Load(string filepath)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return Load(data);
        }

        public static void ImageSave(NbTextureData tex, string filepath)
        {
            Image image = GetImageFromTetureData(tex);
            image.Save(filepath);
        }

    }
}
