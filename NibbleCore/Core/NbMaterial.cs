﻿using System;
using System.Collections.Generic;
using NbCore.Common;
using NbCore.IO;
using Newtonsoft.Json;

namespace NbCore
{
    public enum NbMaterialClass
    {
        Default,
        Transluscent,
        Opaque
    }

    [NbSerializable]
    public class NbMaterial : Entity
    {
        public string Name = "";
        public NbMaterialClass Class = NbMaterialClass.Default;
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
            NbMaterialFlagEnum._NB_EMISSIVE,
            NbMaterialFlagEnum._NB_EMISSIVE_MAP,
            NbMaterialFlagEnum._NB_UNLIT,
            NbMaterialFlagEnum._NB_VERTEX_COLOUR,
            NbMaterialFlagEnum._NB_SMOOTH_LINES,

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
            Class = NbMaterialClass.Default;
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
            //Prevent crashes with empty uniforms during shader recompilation
            if (uf.ShaderBinding == null)
                return;

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
            mat.Class = (NbMaterialClass) Enum.Parse(typeof(NbMaterialClass), token.Value<string>("Class"));

            string conf_name = token.Value<string>("ShaderConfig");
            NbShaderConfig conf = NbRenderState.engineRef.GetShaderConfigByName(conf_name);

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


            if (conf == null)
            {
                Callbacks.Log(typeof(NbDeserializer), $"Unknown shader configuration {conf_name}. Unable to attach shader to material {mat.Name}", LogVerbosityLevel.WARNING);
            } else
            {
                //Calculate Shader hash
                ulong shader_hash = NbRenderState.engineRef.CalculateShaderHash(conf, NbRenderState.engineRef.GetMaterialShaderDirectives(mat));
                NbShader shader = NbRenderState.engineRef.GetShaderByHash(shader_hash);

                if (shader == null)
                {
                    //Compile shader
                    shader = new()
                    {
                        directives = NbRenderState.engineRef.GetMaterialShaderDirectives(mat)
                    };

                    shader.SetShaderConfig(conf);
                    NbRenderState.engineRef.CompileShader(shader);

                }
                mat.AttachShader(shader);
            }

            return mat;
        }


    }


}