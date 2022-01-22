using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Drawing;
using NbCore.Common;
using System.Linq;

namespace NbCore
{
    public class NbSampler
    {
        public string Name = "";
        public string Map = "";
        public Texture Tex = null;
        public bool IsCube = false;
        public bool IsSRGB = true;
        public bool UseCompression = false;
        public bool UseMipMaps = false;
        public TextureUnit texUnit;
        public int SamplerID; // Shader sampler ID
        public int ShaderLocation = -1;
        public bool isProcGen = false; //TODO : to be removed once we are done with the stupid proc gen texture parsing

        //Override Properties
        public NbSampler()
        {
            
        }

        public NbSampler Clone()
        {
            NbSampler newsampler = new()
            {
                Name = Name,
                Map = Map,
                IsSRGB = IsSRGB,
                IsCube = IsCube,
                UseCompression = UseCompression,
                UseMipMaps = UseMipMaps,
                Tex = Tex,
                texUnit = texUnit
            };

            return newsampler;
        }
        
    }
}