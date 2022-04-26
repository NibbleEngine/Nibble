
using OpenTK.Graphics.OpenGL4; //TODO : REMOVE 
using System;
using NbCore.Math;
using System.IO;
using NbCore.Common;
using Newtonsoft.Json;

namespace NbCore
{
    public enum NbTextureTarget
    {
        Texture1D,
        Texture2D,
        Texture3D,
        Texture2DArray,
        TextureCubeMap
    }

    public enum NbTextureInternalFormat
    {
        DXT1,
        DXT3,
        DXT5,
        RGTC2,
        BC7,
        DX10,
        RGBA8,
        RGBA16F,
        DEPTH
    }

    [NbSerializable]
    public class NbTexture : Entity
    {
        public int texID = -1;
        private bool disposed = false;
        public string Path = "";
        public PaletteOpt palOpt;
        public NbVector4 procColor;
        public NbVector3 avgColor;
        public NbTextureData Data;
        public int Refs = 0;
        
        //Empty Initializer
        public NbTexture() :base(EntityType.Texture) { }

        public NbTexture(string path) :base(EntityType.Texture)
        {
            Path = path;

            byte[] byte_data = File.ReadAllBytes(Path);
            string ext = System.IO.Path.GetExtension(path).ToUpper();
            Data = textureInit(byte_data, ext);
        }

        public NbTexture(string path, byte[] data) : base(EntityType.Texture)
        {
            Path = path;
            string ext = System.IO.Path.GetExtension(path).ToUpper();
            Data = textureInit(data, ext);
        }

        private NbTextureData textureInit(byte[] imageData, string ext)
        {
            switch (ext)
            {
                case ".DDS":
                    {
                        return new DDSImage(imageData);
                    }
                case ".PNG":
                    {
                        return new PNGImage(imageData);
                    }
                default:
                    {
                        Console.WriteLine("Unsupported Texture Extension");
                        return null;
                    }
            }
        }

        public override NbTexture Clone()
        {
            throw new NotImplementedException();
        }

        public void Export(string name)
        {
            if (Data is null)
            {
                string msg = "No Texture Data in memory";
                Callbacks.Logger.Log(this, msg, LogVerbosityLevel.ERROR);
                return;
            }

            MemoryStream ms = Data.Export();
            ms.Seek(0, SeekOrigin.Begin);
            FileStream fs = new FileStream(name, FileMode.Create);
            ms.WriteTo(fs);
            ms.Close();
            fs.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (texID != -1) GL.DeleteTexture(texID);

                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }

        private NbVector3 getAvgColor(byte[] pixels)
        {
            //Assume that I have the 4x4 mipmap
            //I need to fetch the first 2 colors and calculate the Average

            MemoryStream ms = new MemoryStream(pixels);
            BinaryReader br = new BinaryReader(ms);

            int color0 = br.ReadUInt16();
            int color1 = br.ReadUInt16();

            br.Close();

            //int rmask = 0x1F << 11;
            //int gmask = 0x3F << 5;
            //int bmask = 0x1F;
            uint temp;

            temp = (uint)(color0 >> 11) * 255 + 16;
            char r0 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            char g0 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            char b0 = (char)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            char r1 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            char g1 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            char b1 = (char)((temp / 32 + temp) / 32);

            char red = (char)(((int)(r0 + r1)) / 2);
            char green = (char)(((int)(g0 + g1)) / 2);
            char blue = (char)(((int)(b0 + b1)) / 2);


            return new NbVector3(red / 256.0f, green / 256.0f, blue / 256.0f);

        }

        private ulong PackRGBA(char r, char g, char b, char a)
        {
            return (ulong)((r << 24) | (g << 16) | (b << 8) | a);
        }


        public void Serialize(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Path");
            writer.WriteValue(Path);
            writer.WriteEndObject();
        }

        public static NbTexture Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string path = token.Value<string>("Path");
            return RenderState.engineRef.CreateTexture(path, false);
        }

    }

}
