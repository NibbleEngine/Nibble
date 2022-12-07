using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NbCore
{

    public delegate void ShaderConfigUpdatedEventHandler();

    [NbSerializable]
    public class NbShaderConfig : Entity
    {
        public string Name = "";
        public ulong Hash;
        public bool IsGeneric = false;
        public Dictionary<NbShaderSourceType, NbShaderSource> Sources = new();
        public ShaderConfigUpdatedEventHandler IsUpdated;

        //Store the raw shader text temporarily
        public NbShaderMode ShaderMode = NbShaderMode.DEFAULT;
        
        //Default shader versions
        public const string version = "#version 450\n #extension GL_ARB_explicit_uniform_location : enable\n" +
                                       "#extension GL_ARB_separate_shader_objects : enable\n" +
                                       "#extension GL_ARB_texture_query_lod : enable\n" +
                                       "#extension GL_ARB_gpu_shader5 : enable\n";

        public NbShaderConfig() : base(EntityType.ShaderConfig)
        {

        }

        public NbShaderConfig(NbShaderSource vvs,
            NbShaderSource ffs, NbShaderSource ggs,
            NbShaderSource ttcs, NbShaderSource ttes,
            NbShaderMode mode) : base(EntityType.ShaderConfig)
        {
            ShaderMode = mode;

            //Store objects
            AddSource(NbShaderSourceType.VertexShader, vvs);
            AddSource(NbShaderSourceType.FragmentShader, ffs);
            AddSource(NbShaderSourceType.GeometryShader, ggs);
            AddSource(NbShaderSourceType.TessControlShader, ttcs);
            AddSource(NbShaderSourceType.TessEvaluationShader, ttes);

            //Calculate Config Hash
            Hash = GetHash(vvs,ffs,ggs,ttcs, ttes, mode);
        }

        public static ulong GetHash(NbShaderSource vvs,
            NbShaderSource ffs, NbShaderSource ggs,
            NbShaderSource ttcs, NbShaderSource ttes,
            NbShaderMode mode)
        {
            ulong hs = (ulong)mode;
            if (vvs != null)
                hs = NbHasher.CombineHash(hs, vvs.Hash);
            if (ffs != null)
                hs = NbHasher.CombineHash(hs, ffs.Hash);
            if (ggs != null)
                hs = NbHasher.CombineHash(hs, ggs.Hash);
            if (ttcs != null)
                hs = NbHasher.CombineHash(hs, ttcs.Hash);
            if (ttes != null)
                hs = NbHasher.CombineHash(hs, ttes.Hash);

            return hs;
        }

        public void AddSource(NbShaderSourceType t, NbShaderSource s)
        {
            if (s == null)
                return;
            Sources[t] = s;

            //Add shader reference to source object
            List<NbShaderSource> RefShaderSources = new();
            s.GetReferencedShaderSources(ref RefShaderSources);
            foreach (NbShaderSource ss in RefShaderSources)
                ss.IsUpdated += OnSourceUpdate;
        }   

        public override NbShaderConfig Clone()
        {
            throw new NotImplementedException();
        }

        public void OnSourceUpdate()
        {
            IsUpdated?.Invoke();
        }

        public void Serialize(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("Name");
            writer.WriteValue(Name);

            writer.WritePropertyName("ShaderMode");
            writer.WriteValue(ShaderMode);

            writer.WritePropertyName("Sources");
            writer.WriteStartArray();
            foreach (var kvp in Sources)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(kvp.Key.ToString());
                writer.WriteValue(kvp.Value.SourceFilePath);
                writer.WriteEndObject();
            }
                
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public static NbShaderConfig Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string name = token.Value<string>("Name");

            NbShaderSource vs = null;
            NbShaderSource fs = null;

            foreach (var ct in token["Sources"])
            {
                if (ct["VertexShader"] != null)
                {
                    string path = ct.Value<string>("VertexShader");
                    vs = Common.RenderState.engineRef.GetShaderSourceByFilePath(path);
                    if (vs == null)
                    {
                        vs = new NbShaderSource(path, true);
                    }
                } else if (ct["FragmentShader"] != null)
                {
                    string path = ct.Value<string>("FragmentShader");
                    fs = Common.RenderState.engineRef.GetShaderSourceByFilePath(path);
                    if (fs == null)
                    {
                        fs = new NbShaderSource(path, true);
                    }
                }
            }

            NbShaderMode mode = (NbShaderMode) token.Value<int>("ShaderMode");

            NbShaderConfig config = new NbShaderConfig(vs, fs, null, null, null, mode);
            config.Name = name;
            config.Hash = GetHash(vs, fs, null, null, null, mode);
            
            return config;
        }

    }

}
