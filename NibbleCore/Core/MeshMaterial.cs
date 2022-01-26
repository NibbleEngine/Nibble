﻿using System;
using System.Collections.Generic;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using NbCore.Common;

namespace NbCore
{
    //Stolen from NMS sorry HG ^.^
    public enum MaterialFlagEnum
    {
        _F01_DIFFUSEMAP,
        _F02_SKINNED,
        _F03_NORMALMAP,
        _F04_,
        _F05_INVERT_ALPHA,
        _F06_BRIGHT_EDGE,
        _F07_UNLIT,
        _F08_REFLECTIVE,
        _F09_TRANSPARENT,
        _F10_NORECEIVESHADOW,
        _F11_ALPHACUTOUT,
        _F12_BATCHED_BILLBOARD,
        _F13_UVANIMATION,
        _F14_UVSCROLL,
        _F15_WIND,
        _F16_DIFFUSE2MAP,
        _F17_MULTIPLYDIFFUSE2MAP,
        _F18_UVTILES,
        _F19_BILLBOARD,
        _F20_PARALLAXMAP,
        _F21_VERTEXCOLOUR,
        _F22_TRANSPARENT_SCALAR,
        _F23_TRANSLUCENT,
        _F24_AOMAP,
        _F25_ROUGHNESS_MASK,
        _F26_STRETCHY_PARTICLE,
        _F27_VBTANGENT,
        _F28_VBSKINNED,
        _F29_VBCOLOUR,
        _F30_REFRACTION,
        _F31_DISPLACEMENT,
        _F32_REFRACTION_MASK,
        _F33_SHELLS,
        _F34_GLOW,
        _F35_GLOW_MASK,
        _F36_DOUBLESIDED,
        _F37_,
        _F38_NO_DEFORM,
        _F39_METALLIC_MASK,
        _F40_SUBSURFACE_MASK,
        _F41_DETAIL_DIFFUSE,
        _F42_DETAIL_NORMAL,
        _F43_NORMAL_TILING,
        _F44_IMPOSTER,
        _F45_VERTEX_BLEND,
        _F46_BILLBOARD_AT,
        _F47_REFLECTION_PROBE,
        _F48_WARPED_DIFFUSE_LIGHTING,
        _F49_DISABLE_AMBIENT,
        _F50_DISABLE_POSTPROCESS,
        _F51_DECAL_DIFFUSE,
        _F52_DECAL_NORMAL,
        _F53_COLOURISABLE,
        _F54_COLOURMASK,
        _F55_MULTITEXTURE,
        _F56_MATCH_GROUND,
        _F57_DETAIL_OVERLAY,
        _F58_USE_CENTRAL_NORMAL,
        _F59_SCREENSPACE_FADE,
        _F60_ACUTE_ANGLE_FADE,
        _F61_CLAMP_AMBIENT,
        _F62_DETAIL_ALPHACUTOUT,
        _F63_DISSOLVE,
        _F64_,
    }
    
    public class MeshMaterial : Entity
    {
        public string Name = "";
        public string Class = "";
        public bool proc = false;
        public string name_key = "";
        public TextureManager texMgr;
        public NbShader Shader;
        public GLSLShaderConfig ShaderConfig;
        public List<NbUniform> Uniforms = new();
        public List<NbSampler> Samplers = new();
        public Dictionary<string, NbSampler> SamplerMap = new();
        public List<MaterialFlagEnum> Flags = new();
        
        public float[] material_flags = new float[64];

        public static List<MaterialFlagEnum> supported_flags = new() {
            MaterialFlagEnum._F01_DIFFUSEMAP,
            MaterialFlagEnum._F02_SKINNED,
            MaterialFlagEnum._F03_NORMALMAP,
            MaterialFlagEnum._F07_UNLIT,
            MaterialFlagEnum._F09_TRANSPARENT,
            MaterialFlagEnum._F22_TRANSPARENT_SCALAR,
            MaterialFlagEnum._F11_ALPHACUTOUT,
            MaterialFlagEnum._F14_UVSCROLL,
            MaterialFlagEnum._F16_DIFFUSE2MAP,
            MaterialFlagEnum._F17_MULTIPLYDIFFUSE2MAP,
            MaterialFlagEnum._F21_VERTEXCOLOUR,
            MaterialFlagEnum._F25_ROUGHNESS_MASK,
            MaterialFlagEnum._F24_AOMAP,
            MaterialFlagEnum._F34_GLOW,
            MaterialFlagEnum._F35_GLOW_MASK,
            MaterialFlagEnum._F39_METALLIC_MASK,
            MaterialFlagEnum._F43_NORMAL_TILING,
            MaterialFlagEnum._F51_DECAL_DIFFUSE,
            MaterialFlagEnum._F52_DECAL_NORMAL,
            MaterialFlagEnum._F55_MULTITEXTURE
        };

        public List<NbUniform> ActiveUniforms = new();
        public List<NbSampler> ActiveSamplers = new();

        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public MeshMaterial() : base(EntityType.Material)
        {
            Name = "NULL";
            Class = "NULL";
            Type = EntityType.Material;
            
            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public void UpdateSampler(NbSampler sampler)
        {
            if (Shader.uniformLocations.ContainsKey(sampler.State.ShaderBinding))
            {
                sampler.State.ShaderLocation = Shader.uniformLocations[sampler.State.ShaderBinding].loc;
                if (!ActiveSamplers.Contains(sampler))
                    ActiveSamplers.Add(sampler);
            } else
            {
                sampler.State.ShaderBinding = "";
                sampler.State.ShaderLocation = -1;
                if (ActiveSamplers.Contains(sampler))
                    ActiveSamplers.Remove(sampler);
            }
        }

        public void UpdateUniform(NbUniform uf) 
        { 
            if (Shader.uniformLocations.ContainsKey(uf.State.ShaderBinding))
            {
                NbUniformFormat fmt = Shader.uniformLocations[uf.State.ShaderBinding];
                uf.State.ShaderBinding = fmt.name;
                uf.State.ShaderLocation = fmt.loc;
                if (!ActiveUniforms.Contains(uf))
                    ActiveUniforms.Add(uf);
            }
            else
            {
                //TODO: Clear uniform state; (maybe not required?)
                if (ActiveUniforms.Contains(uf))
                    ActiveUniforms.Remove(uf);
            }
        }


        //Wrapper to support uberflags
        public bool has_flag(MaterialFlagEnum flag)
        {
            return material_flags[(int) flag] > 0.0f;
        }

        public bool add_flag(MaterialFlagEnum flag)
        {
            if (has_flag((flag)))
                return false;

            material_flags[(int) flag] = 1.0f;
            Flags.Add(flag);
            
            return true;
        }

        public override Entity Clone()
        {
            MeshMaterial newmat = new();
            
            newmat.CopyFrom(this); //Copy components
            
            //Remix textures
            return newmat;
        }

        public void SetShader(NbShader shader)
        {
            Shader = shader;
            shader.IsUpdated += OnShaderUpdate;
        }

        public void OnShaderUpdate()
        {
            //Re-check samplers
            foreach (NbSampler s in Samplers)
                UpdateSampler(s);

            //Re-check uniforms
            foreach (NbUniform uf in Uniforms)
                UpdateUniform(uf);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //DISPOSE SAMPLERS HERE
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~MeshMaterial()
        {
            Dispose(false);
        }

        public static int calculateShaderHash(List<MaterialFlagEnum> flags)
        {
            string hash = "";

            for (int i = 0; i < flags.Count; i++)
            {
                if (supported_flags.Contains(flags[i]))
                    hash += "_" + flags[i];
            }

            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }

        

    }

}
