﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NbCore.Text
{
    public struct Symbol
    {
        public string symbol;
        public int x_pos;
        public int y_pos;
        public int width;
        public int height;
        public int x_origin;
        public int y_origin;
        public int advance;

    }
    public class Font : IDisposable
    {
        public string Name;
        public int Size;
        public int baseHeight; //Baseline font height in pixels
        public int lineHeight; //LineHeight in pixels
        public int texWidth; //Texture width in pixels
        public int texHeight; //Texture height in pixels
        public int texID;
        public MeshMaterial material;
        public Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();
        private bool disposedValue;

        public Font(string path, int format)
        {
            FileStream fnt_fs = new FileStream(path, FileMode.Open);
            StreamReader fnt_sr = new StreamReader(fnt_fs);

            string img_path = Path.ChangeExtension(path, "png");

            Image<Rgba32> bmp = Image.Load<Rgba32>(img_path);
            
            if (format == 1)
                loadHieroFont(fnt_sr, bmp);
            else
                loadJsonFont(fnt_sr, bmp);
        }

        public Font(byte[] fnt_data, Image<Rgba32> img_data, int format)
        {
            MemoryStream ms = new MemoryStream(fnt_data);
            StreamReader fnt_sr = new StreamReader(ms);

            if (format == 1)
                loadHieroFont(fnt_sr, img_data);
        }

        private void loadHieroFont(StreamReader fnt_sr, Image<Rgba32> img_data)
        {
            while (!fnt_sr.EndOfStream)
            {
                string line = fnt_sr.ReadLine();
                string[] sp;



                if (line.StartsWith("info"))
                {
                    sp = line.Split(new string[] {"info=", "face=", "size=", "bold=",
                                             "italic=", "charset=", "unicode=",
                                             "stretchH=", "smooth=", "aa=",
                                             "padding=",  "spacing=" }, StringSplitOptions.None);

                    Name = sp[1].Trim(' ').Trim('\"');
                    int.TryParse(sp[2], out Size);

                }
                else if (line.StartsWith("common"))
                {
                    sp = line.Split(new string[] {"common=", "lineHeight=", "base=", "scaleW=",
                                             "scaleH=", "pages=", "packed="}, StringSplitOptions.None);

                    int.TryParse(sp[1], out lineHeight);
                    int.TryParse(sp[2], out baseHeight);
                    int.TryParse(sp[3], out texWidth);
                    int.TryParse(sp[4], out texHeight);
                }
                else if (line.StartsWith("page"))
                {

                    sp = line.Split(new string[] { "page id=", "file=" }, StringSplitOptions.None);
                }
                else if (line.StartsWith("chars"))
                {
                    continue;
                }
                else if (line.StartsWith("char"))
                {
                    sp = line.Split(new string[] { "char id=", "x=", "y=", "width=", "height=",
                                                   "xoffset=", "yoffset=", "xadvance=", "page=", "chnl="}, StringSplitOptions.None);

                    Symbol s = new Symbol();
                    int.TryParse(sp[1].Trim(' '), out int char_id);
                    s.symbol = char.ConvertFromUtf32(char_id);
                    int.TryParse(sp[2].Trim(' '), out s.x_pos);
                    int.TryParse(sp[3].Trim(' '), out s.y_pos);
                    int.TryParse(sp[4].Trim(' '), out s.width);
                    int.TryParse(sp[5].Trim(' '), out s.height);
                    int.TryParse(sp[6].Trim(' '), out s.x_origin);
                    int.TryParse(sp[7].Trim(' '), out s.y_origin);
                    int.TryParse(sp[8].Trim(' '), out s.advance);


                    symbols[s.symbol] = s;
                }
            }

            //Generate texture
            NbTexture tex = new NbTexture();
            tex.Data.target = NbTextureTarget.Texture2DArray;
            tex.texID = genGLTexture(img_data);

            //Generate Sampler
            NbSampler sampl = new NbSampler();
            //TODO: Remove NMS stuff from here
            sampl.Name = "mpCustomPerMaterial.gDiffuseMap";
            sampl.State.SamplerID = 0;
            sampl.SetTexture(tex);
            
            //Generate Font Material
            material = new MeshMaterial();
        }

        private void loadJsonFont(StreamReader fnt_sr, Image<Rgba32> img_data)
        {
            fnt_sr.BaseStream.Seek(0, SeekOrigin.Begin);
            string data = fnt_sr.ReadToEnd();

            JObject main = JObject.Parse(data);

            Name = (string)main["name"];
            Size = (int)main["size"];
            texWidth = (int)main["width"];
            texHeight = (int)main["height"];


            //Iterate through all symbols
            foreach (JProperty k in main["characters"])
            {
                Symbol s = new Symbol();
                s.symbol = k.Name;
                s.x_pos = (int)k.Value["x"];
                s.y_pos = (int)k.Value["y"];
                s.width = (int)k.Value["width"];
                s.height = (int)k.Value["height"];
                s.x_origin = (int)k.Value["originX"];
                s.y_origin = (int)k.Value["originY"];
                s.advance = (int)k.Value["advance"];
                symbols[s.symbol] = s;
            }

            //Generate texture
            NbTexture tex = new NbTexture();
            tex.Data.target = NbTextureTarget.Texture2DArray;
            tex.texID = genGLTexture(img_data);

            //Generate Sampler
            NbSampler sampl = new NbSampler();
            sampl.Name = "mpCustomPerMaterial.gDiffuseMap";
            sampl.State.SamplerID = 0;
            sampl.SetTexture(tex);
            

            //Generate Font Material
            material = new MeshMaterial();
        }

        private unsafe int genGLTexture(Image<Rgba32> bmp)
        {   
            Span<Rgba32> pixels;
            bmp.TryGetSinglePixelSpan(out pixels);

            int texID = GL.GenTexture();
            Console.WriteLine(GL.GetError());
            GL.BindTexture(TextureTarget.Texture2DArray, texID);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);

            fixed (void* ptr = pixels)
            {
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, bmp.Width, bmp.Height,
                0, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr) ptr);
            }   
            
            Console.WriteLine(GL.GetError());
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);


            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            
            return texID;
        }


        public void clearTextures()
        {
            GL.DeleteTexture(texID);
        }

        ~Font()
        {
            symbols.Clear();

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    GL.DeleteTexture(texID);
                }
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Font()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


}
