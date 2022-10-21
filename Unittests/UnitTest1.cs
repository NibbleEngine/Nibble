using System;
using System.Diagnostics;
using NbCore;
using NbCore.Math;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace Unittests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void ImageSharpTest()
        {
            byte[] pixel_data = new byte[1024 * 1024 * 4];
            Image<Rgba32> image = new Image<Rgba32>(1024, 1024);
            image.CopyPixelDataTo(pixel_data);
            Assert.Pass();
        }

        [Test]
        public void ImageLoadFromFileTest()
        {
            Engine e = new Engine(); //The engine attaches the assembly loader
            NbTextureData texture = NbImagingAPI.Load("Default_albedo.jpg");
            NbImagingAPI.ImageSave(texture, "text_tex.png");
            Assert.Pass();
        }

        [Test]
        public void PropertySetting()
        {
            NbUniform uniform = new();
            NbVector4 vec = new(2.0f);

            //uniform._LocalValues.X = 1.0f;
            //uniform._LocalValues.Y = 1.0f;
            
            Debug.WriteLine($"Uniform Values {uniform.Values.X} {uniform.Values.Y} {uniform.Values.Z} {uniform.Values.W}");
            uniform.Bind(ref vec);
            Debug.WriteLine($"Uniform Values {uniform.Values.X} {uniform.Values.Y} {uniform.Values.Z} {uniform.Values.W}");
            vec.X = 15.0f;
            Debug.WriteLine($"Uniform Values {uniform.Values.X} {uniform.Values.Y} {uniform.Values.Z} {uniform.Values.W}");
            uniform.SetX(5.0f);
            Debug.WriteLine($"Uniform Values {uniform.Values.X} {uniform.Values.Y} {uniform.Values.Z} {uniform.Values.W}");
            uniform.UnBind();
            Debug.WriteLine($"Uniform Values {uniform.Values.X} {uniform.Values.Y} {uniform.Values.Z} {uniform.Values.W}");
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