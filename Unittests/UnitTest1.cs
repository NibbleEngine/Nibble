using NbCore;
using NbCore.Common;
using NbCore.Image;
using NbCore.Platform.Windowing;
using NUnit.Framework;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.IO;

namespace Unittests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ImageLoadFromFileTest()
        {
            Engine e = new Engine(); //The engine attaches the assembly loader
            NbTextureData texture = NbImagingAPI.Load("pinarello-logo.png");
            NbImagingAPI.ImageSave(texture, "pinarello_conv.png");
            Assert.Pass();
        }

        [Test]
        public void CreateImageFromRawPixelDataTest()
        {
            Engine e = new Engine(); //The engine attaches the assembly loader
            int width = 128;
            int height = 128;
            byte[] data = new byte[width * height * 4];

            //Make image red
            for (int i = 0; i < width * height; i++)
            {
                data[4 * i] = 255;
            }

            NbTextureData texture = NbImagingAPI.Load(data, width, height, NbTextureInternalFormat.RGBA8);
            NbImagingAPI.ImageSave(texture, "test.bmp");
            Assert.Pass();
        }

    }
}