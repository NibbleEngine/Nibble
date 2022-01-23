using System;
using NbCore.Math;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL4;
using System.IO;
using NbCore.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NbCore
{
    public enum NbTextureTarget
    {
        Texture1D,
        Texture2D,
        Texture2DArray
    }

    public enum NbTextureInternalFormat
    {
        DXT1,
        DXT3,
        DXT5,
        RGTC2,
        BC7,
        DX10,
    }

    public class Texture : Entity
    {
        public int texID = -1;
        private bool disposed = false;
        public NbTextureTarget target;
        public string Path = "";
        public int Width;
        public int Height;
        public int Depth;
        public int MipMapCount;
        public NbTextureInternalFormat pif;
        public PaletteOpt palOpt;
        public NbVector4 procColor;
        public NbVector3 avgColor;

        //Empty Initializer
        public Texture() :base(EntityType.Texture) { }
        //Path Initializer

        public Texture(byte[] data, bool isDDS, string path) : base(EntityType.Texture)
        {
            Path = path;
            if (isDDS)
            {
                textureInitDDS(data);
            } else
            {
                textureInit(data, "temp");
            }
        }

        public Texture(string path, bool isCustom = false) : base(EntityType.Texture)
        {
            Stream fs;
            byte[] image_data;
            int data_length;

            fs = new FileStream(path, FileMode.Open);

            if (fs == null)
            {
                //throw new System.IO.FileNotFoundException();
                Console.WriteLine("Texture {0} Missing. Using default.dds", path);

                //Load default.dds from resources
                image_data = File.ReadAllBytes("default.dds");
                data_length = image_data.Length;
            }
            else
            {
                data_length = (int) fs.Length;
                image_data = new byte[data_length];
            }

            fs.Read(image_data, 0, data_length);

            
            textureInit(image_data, System.IO.Path.GetExtension(path).ToUpper());
        }

        private void textureInitPNG(byte[] imageData)
        {
            MemoryStream ms = new MemoryStream(imageData);
            
            //Load the image from file
            Image<Bgra32> bmpTexture = Image.Load<Bgra32>(ms);
#if DEBUG
            bmpTexture.Save("test_image.bmp");
#endif
            Span<Bgra32> pixels;
            bmpTexture.TryGetSinglePixelSpan(out pixels);
            
            //Console.WriteLine(GL.GetError());
            //Generate PBO
            //pboID = GL.GenBuffer();
            //GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboID);
            //GL.BufferData(BufferTarget.PixelUnpackBuffer, 
            //        oTextureData.Stride * oTextureData.Height, oTextureData.Scan0, 
            //        BufferUsageHint.StaticDraw);
            
            //Upload to GPU
            texID = GL.GenTexture();
            target = NbTextureTarget.Texture2D;

            TextureTarget gl_target = TextureTarget.Texture2D;

            //Copy the image data into the texture
            GL.BindTexture(gl_target, texID);
            unsafe
            {
                fixed (void *ptr = pixels)
                {
                    GL.TexImage2D(gl_target, 0, PixelInternalFormat.Rgba, bmpTexture.Width, bmpTexture.Height, 0,
                    PixelFormat.Bgra, PixelType.UnsignedByte, (IntPtr) ptr);
                }
            }
            
            GL.TexParameter(gl_target, TextureParameterName.TextureMinFilter, (float) TextureMinFilter.Linear);
            GL.TexParameter(gl_target, TextureParameterName.TextureMagFilter, (float) TextureMagFilter.Linear);

            //Cleanup
            bmpTexture = null;
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO

        }

        public void SubTextureData(byte[] data, int mipmap_id, int depth_id)
        {


        }

        public override Texture Clone()
        {
            throw new NotImplementedException();
        }

        private void textureInitDDS(byte[] imageData)
        {
            DDSImage ddsImage;
            
            ddsImage = new DDSImage(imageData);
            RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + Path + " Width {0} Height {1}", 
                ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            Width = ddsImage.header.dwWidth;
            Height = ddsImage.header.dwHeight;
            int blocksize = 16;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = NbTextureInternalFormat.DXT1;
                    blocksize = 8;
                    break;
                //DXT5
                case (0x35545844):
                    pif = NbTextureInternalFormat.DXT5;
                    break;
                //ATI2A2XY
                case (0x32495441):
                    pif = NbTextureInternalFormat.RGTC2; //Normal maps are probably never srgb
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = NbTextureInternalFormat.BC7;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }

            //Temp Variables
            int w = Width;
            int h = Height;
            int mm_count = System.Math.Max(1, ddsImage.header.dwMipMapCount); //Fix the counter to 1 to handle textures with single mipmaps
            int depth_count = System.Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;

            MipMapCount = mm_count;
            Depth = depth_count;
            
            //Generate PBO
            //GL.BufferData(BufferTarget.PixelUnpackBuffer, ddsImage.Data.Length, ddsImage.Data, BufferUsageHint.StaticDraw);
            //GL.BufferSubData(BufferTarget.PixelUnpackBuffer, IntPtr.Zero, ddsImage.bdata.Length, ddsImage.bdata);

            //Upload to GPU
            texID = GL.GenTexture();
            
            if (depth_count > 1)
                target = NbTextureTarget.Texture2DArray;
            else
                target = NbTextureTarget.Texture2D;
            
            TextureTarget gl_target = GraphicsAPI.TextureTargetMap[target];
            InternalFormat gl_pif = GraphicsAPI.InternalFormatMap[pif];

            GL.BindTexture(gl_target, texID);
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(gl_target, TextureParameterName.TextureBaseLevel, 0);
            //GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);

            int offset = 0;
            for (int i = 0; i < mm_count; i++)
            {
                byte[] temp_data = new byte[temp_size * depth_count];
                System.Buffer.BlockCopy(ddsImage.Data, offset, temp_data, 0, temp_size * depth_count);
                if (depth_count > 1)
                    GL.CompressedTexImage3D(gl_target, i, gl_pif, w, h, depth_count, 0, temp_size * depth_count, temp_data);
                else
                    GL.CompressedTexImage2D(gl_target, i, gl_pif, w, h, 0, temp_size * depth_count, temp_data);
                offset += temp_size * depth_count;

                w = System.Math.Max(w >> 1, 1);
                h = System.Math.Max(h >> 1, 1);

                temp_size = System.Math.Max(1, (w + 3) / 4) * System.Math.Max(1, (h + 3) / 4) * blocksize;
                //This works only for square textures
                //temp_size = Math.Max(temp_size/4, blocksize);
            }

            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(gl_target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(gl_target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(gl_target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(gl_target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float) System.Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureMaxLevel, out int max_level);
            GL.GetTexParameter(gl_target, GetTextureParameter.TextureBaseLevel, out int base_level);

            int maxsize = System.Math.Max(Height, Width);
            int p = (int)System.Math.Floor(System.Math.Log(maxsize, 2)) + base_level;
            int q = System.Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
            ddsImage = null;
            //GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO
        }

        public void textureInit(byte[] imageData, string ext)
        {
            switch (ext)
            {
                case ".DDS":
                    {
                        textureInitDDS(imageData);
                        break;
                    }
                case ".PNG":
                    {
                        textureInitPNG(imageData);
                        break;
                    }
                default:
                    {
                        Console.WriteLine("Unsupported Texture Extension");
                        break;
                    }
            }

        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (texID != -1) GL.DeleteTexture(texID);
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

        public static void dump_texture(Texture tex, string name)
        {
            var pixels = new byte[4 * tex.Width * tex.Height];
            GL.BindTexture(GraphicsAPI.TextureTargetMap[tex.target], tex.texID);
            GL.GetTexImage(GraphicsAPI.TextureTargetMap[tex.target], 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            Image<Rgba32> test = Image.LoadPixelData<Rgba32>(pixels, tex.Width, tex.Height);
            test.SaveAsPng("Temp//framebuffer_raw_" + name + ".png");
        }

    }

}
