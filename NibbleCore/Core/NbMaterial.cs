using System;
using System.Collections.Generic;
using NbCore.Common;
using Newtonsoft.Json;

namespace NbCore
{
    //Stolen from NMS sorry HG ^.^
    public enum NbMaterialFlagEnum
    {
        _NB_DIFFUSE_MAP,
        _NB_NORMAL_MAP,
        _NB_TWO_CHANNEL_NORMAL_MAP,
        _NB_METALLIC_ROUGHNESS_MAP,
        _NB_AO_MAP,
        _NB_AO_METALLIC_ROUGHNESS_MAP,
        _NB_EMISSIVE_MAP,
        _NB_UNLIT,
        _NB_VERTEX_COLOUR,

        //OLD FLAGS TO BE TRANSFORMED
        //_F01_DIFFUSEMAP, //CHECK
        //_F02_SKINNED, //CHECK
        //_F03_NORMALMAP, //CHECK
        _F04_,
        _F05_INVERT_ALPHA,
        _F06_BRIGHT_EDGE,
        //_F07_UNLIT, //CHECK
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
        //_F21_VERTEXCOLOUR, //CHECK
        _F22_TRANSPARENT_SCALAR,
        _F23_TRANSLUCENT,
        //_F24_AOMAP, //CHECK
        //_F25_ROUGHNESS_MASK, //CHECK
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
        //_F39_METALLIC_MASK, //CHECK
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

    [NbSerializable]
    public class NbMaterial : Entity
    {
        public string Name = "";
        public string Class = "";
        public NbUniform DiffuseColor = new(NbUniformType.Vector4, "DiffuseColor", 1.0f, 1.0f, 1.0f, 1.0f);
        public NbUniform AmbientColor = new(NbUniformType.Vector4, "AmbientColor", 1.0f, 1.0f, 1.0f, 1.0f);
        public NbUniform SpecularColor = new(NbUniformType.Vector4, "SpecularColor", 1.0f, 1.0f, 1.0f, 1.0f);
        public bool IsPBR = false;
        public NbUniform MetallicFactor = new(NbUniformType.Float, "MetallicFactor", 0.0f);
        public NbUniform RoughnessFactor = new(NbUniformType.Float, "RoughnessFactor", 0.0f);
        public NbUniform EmissiveFactor = new(NbUniformType.Float, "EmissiveFactor", 0.0f);

        public bool IsGeneric = false;
        public TextureManager texMgr;
        public NbShader Shader;
        private List<NbMaterialFlagEnum> Flags = new();
        public List<NbUniform> Uniforms = new();
        public List<NbSampler> Samplers = new();

        public float[] material_flags = new float[64];

        public static List<NbMaterialFlagEnum> supported_flags = new() {
            NbMaterialFlagEnum._NB_DIFFUSE_MAP,
            NbMaterialFlagEnum._NB_NORMAL_MAP,
            NbMaterialFlagEnum._NB_TWO_CHANNEL_NORMAL_MAP,
            NbMaterialFlagEnum._NB_METALLIC_ROUGHNESS_MAP,
            NbMaterialFlagEnum._NB_AO_METALLIC_ROUGHNESS_MAP,
            NbMaterialFlagEnum._NB_AO_MAP,
            NbMaterialFlagEnum._NB_EMISSIVE_MAP,
            NbMaterialFlagEnum._NB_UNLIT,
            NbMaterialFlagEnum._NB_VERTEX_COLOUR,


            NbMaterialFlagEnum._F09_TRANSPARENT,
            NbMaterialFlagEnum._F22_TRANSPARENT_SCALAR,
            NbMaterialFlagEnum._F11_ALPHACUTOUT,
            NbMaterialFlagEnum._F14_UVSCROLL,
            NbMaterialFlagEnum._F16_DIFFUSE2MAP,
            NbMaterialFlagEnum._F17_MULTIPLYDIFFUSE2MAP,
            NbMaterialFlagEnum._F34_GLOW,
            NbMaterialFlagEnum._F35_GLOW_MASK,
            NbMaterialFlagEnum._F43_NORMAL_TILING,
            NbMaterialFlagEnum._F51_DECAL_DIFFUSE,
            NbMaterialFlagEnum._F52_DECAL_NORMAL,
            NbMaterialFlagEnum._F55_MULTITEXTURE
        };

        public List<NbUniform> ActiveUniforms = new();
        public List<NbSampler> ActiveSamplers = new();

        public Dictionary<string, NbUniform> UniformBindings = new();

        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public NbMaterial() : base(EntityType.Material)
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
            if (Shader.uniformLocations.ContainsKey(sampler.ShaderBinding))
            {
                sampler.ShaderLocation = Shader.uniformLocations[sampler.ShaderBinding].loc;
                if (!ActiveSamplers.Contains(sampler))
                    ActiveSamplers.Add(sampler);
            }
            else
            {
                sampler.ShaderBinding = "";
                sampler.ShaderLocation = -1;
                if (ActiveSamplers.Contains(sampler))
                    ActiveSamplers.Remove(sampler);
            }
        }

        public void RemoveSampler(NbSampler sampler)
        {
            Samplers.Remove(sampler);
            ActiveSamplers.Remove(sampler);
            //No need to dispose the sampler
        }

        public void UpdateUniform(NbUniform uf)
        {
            if (Shader.uniformLocations.ContainsKey(uf.ShaderBinding))
            {
                NbUniformFormat fmt = Shader.uniformLocations[uf.ShaderBinding];
                uf.ShaderBinding = fmt.name;
                uf.ShaderLocation = fmt.loc;
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

        public void RemoveUniform(NbUniform uf)
        {
            Uniforms.Remove(uf);
            ActiveUniforms.Remove(uf);
            //No need to dispose the uf
        }

        //Wrapper to support uberflags
        public bool HasFlag(NbMaterialFlagEnum flag)
        {
            return material_flags[(int)flag] > 0.0f;
        }

        public List<NbMaterialFlagEnum> GetFlags()
        {
            return Flags;
        }

        public void RemoveFlag(NbMaterialFlagEnum flag)
        {
            if (!HasFlag((flag)))
                return;

            material_flags[(int)flag] = 0.0f;
            Flags.Remove(flag);
            //Raise Material Modified event

        }

        public bool AddFlag(NbMaterialFlagEnum flag)
        {
            if (HasFlag((flag)))
                return false;

            material_flags[(int)flag] = 1.0f;
            Flags.Add(flag);

            return true;
        }

        public override Entity Clone()
        {
            NbMaterial newmat = new();

            newmat.CopyFrom(this); //Copy components

            //Remix textures
            return newmat;
        }

        public void AttachShader(NbShader shader)
        {
            if (shader != Shader) //Ref Comparison should work
            {
                DettachShader(); //Just in case
                Shader = shader;
                Shader.AddReference();
                OnShaderUpdate(shader);
                shader.IsUpdated += OnShaderUpdate;
            }
        }

        public void DettachShader()
        {
            if (Shader != null)
            {
                Shader.RemoveReference();
                Shader.IsUpdated -= OnShaderUpdate;
                Shader = null;
            }
        }

        public void OnShaderUpdate(NbShader shader)
        {
            ActiveSamplers.Clear();
            ActiveUniforms.Clear();

            //Re-check samplers
            foreach (NbSampler s in Samplers)
                UpdateSampler(s);

            //Re-check uniforms
            foreach (NbUniform uf in Uniforms)
                UpdateUniform(uf);

            //Clear Uniforms and -recreate
            //AddUniforms();
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

        ~NbMaterial()
        {
            Dispose(false);
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("Name");
            writer.WriteValue(Name);

            writer.WritePropertyName("Class");
            writer.WriteValue(Class);

            writer.WritePropertyName("ShaderConfig");
            writer.WriteValue(Shader?.GetShaderConfig()?.Name);

            //Write Flags
            writer.WritePropertyName("Flags");
            writer.WriteStartArray();
            foreach (NbMaterialFlagEnum flag in Flags)
                writer.WriteValue(flag.ToString());
            writer.WriteEndArray();

            //Write Uniforms
            writer.WritePropertyName("Uniforms");
            writer.WriteStartArray();
            foreach (NbUniform kp in Uniforms)
                IO.NbSerializer.Serialize(kp, writer);
            writer.WriteEndArray();

            //Write Samplers
            writer.WritePropertyName("Samplers");
            writer.WriteStartArray();
            foreach (NbSampler kp in Samplers)
                IO.NbSerializer.Serialize(kp, writer);
            writer.WriteEndArray();

            writer.WriteEndObject();

        }

        public static NbMaterial Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbMaterial mat = new NbMaterial();
            mat.Name = token.Value<string>("Name");
            mat.Class = token.Value<string>("Class");

            NbShaderConfig conf = RenderState.engineRef.GetShaderConfigByName(token.Value<string>("ShaderConfig"));

            //Deserialize flags
            Newtonsoft.Json.Linq.JToken flag_tkns = token.Value<Newtonsoft.Json.Linq.JToken>("Flags");
            foreach (Newtonsoft.Json.Linq.JToken tkn in flag_tkns.Children())
                mat.AddFlag((NbMaterialFlagEnum)Enum.Parse(typeof(NbMaterialFlagEnum), tkn.ToString()));

            //Deserialize samplers
            Newtonsoft.Json.Linq.JToken sampler_tkns = token.Value<Newtonsoft.Json.Linq.JToken>("Samplers");
            foreach (Newtonsoft.Json.Linq.JToken tkn in sampler_tkns.Children())
                mat.Samplers.Add((NbSampler)IO.NbDeserializer.Deserialize(tkn));

            //Deserialize uniforms
            Newtonsoft.Json.Linq.JToken uniform_tkns = token.Value<Newtonsoft.Json.Linq.JToken>("Uniforms");
            foreach (Newtonsoft.Json.Linq.JToken tkn in uniform_tkns.Children())
                mat.Uniforms.Add((NbUniform)IO.NbDeserializer.Deserialize(tkn));

            //Calculate Shader hash
            ulong shader_hash = RenderState.engineRef.CalculateShaderHash(conf, RenderState.engineRef.GetMaterialShaderDirectives(mat));

            NbShader shader = RenderState.engineRef.GetShaderByHash(shader_hash);

            if (shader == null)
            {
                //Compile shader
                shader = new()
                {
                    directives = RenderState.engineRef.GetMaterialShaderDirectives(mat)
                };

                shader.SetShaderConfig(conf);
                RenderState.engineRef.CompileShader(shader);

            }

            mat.AttachShader(shader);
            return mat;
        }


    }


}