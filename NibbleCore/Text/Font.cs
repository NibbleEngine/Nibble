using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using Newtonsoft.Json.Linq;
using NbCore.Common;
using OpenTK.Core.Native;

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
        public Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();
        private bool disposedValue;

        public Font(string fnt_path, string img_path, int format)
        {
            //TODO:
            //Use this generator for SDF fonts
            // https://evanw.github.io/font-texture-generator/
            // Hiero and Json that I use for now may work but they are not clean solutions
            // Imgui uses TTFs directly, so this class should be merged with NbFont
            // and manage Font details and character atlalses for text rendering during rendering

            if (format == 1)
                loadHieroFont(fnt_path, img_path);
            else
                loadJsonFont(fnt_path, img_path);

        }


        private void loadHieroFont(string fnt_path, string img_path)
        {
            StreamReader fnt_sr = new StreamReader(fnt_path);
            
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

            byte[] img_data = File.ReadAllBytes(img_path);

            //Generate texture
            NbTexture tex = RenderState.engineRef.CreateTexture(img_data, img_path, NbTextureWrapMode.Repeat, 
                                                                NbTextureFilter.LinearMipmapLinear, NbTextureFilter.Linear);
            
            //tex.Data.target = NbTextureTarget.Texture2DArray;
            //tex.texID = genGLTexture(img_data);

            //Generate Sampler
            NbSampler sampl = new NbSampler();
            //TODO: Remove NMS stuff from here
            sampl.Name = "gDiffuseMap";
            sampl.SamplerID = 0;
            sampl.ShaderBinding = "mpCustomPerMaterial.gDiffuseMap";
            sampl.Texture = tex;

        }

        private void loadJsonFont(string fnt_path, string img_path)
        {
            StreamReader fnt_sr = new StreamReader(fnt_path);
            
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
            //TODO: This should be done by the engine. MOVE IT
            byte[] img_data = File.ReadAllBytes(img_path);
            NbTexture tex = RenderState.engineRef.CreateTexture(img_data, img_path,
                NbTextureWrapMode.Repeat, NbTextureFilter.LinearMipmapLinear, NbTextureFilter.Linear);
            
            //Generate Sampler
            NbSampler sampl = new NbSampler();
            sampl.Name = "gDiffuseMap";
            sampl.SamplerID = 0;
            sampl.ShaderBinding = "mpCustomPerMaterial.gDiffuseMap";
            sampl.Texture = tex;

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
