using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NbCore
{

    public delegate void ShaderConfigUpdatedEventHandler();

    [NbSerializable, NbDeserializable]
    public class GLSLShaderConfig : Entity
    {
        public string Name = "";
        public bool IsGeneric = false;
        public Dictionary<NbShaderSourceType, GLSLShaderSource> Sources = new();
        public List<string> directives = new();
        
        public HashSet<MeshMaterial> ReferencedByMaterials = new();

        public ShaderConfigUpdatedEventHandler IsUpdated;

        //Store the raw shader text temporarily
        public NbShaderMode ShaderMode = NbShaderMode.DEFAULT;
        
        //Default shader versions
        public const string version = "#version 450\n #extension GL_ARB_explicit_uniform_location : enable\n" +
                                       "#extension GL_ARB_separate_shader_objects : enable\n" +
                                       "#extension GL_ARB_texture_query_lod : enable\n" +
                                       "#extension GL_ARB_gpu_shader5 : enable\n";

        public GLSLShaderConfig() : base(EntityType.ShaderConfig)
        {

        }

        public GLSLShaderConfig(GLSLShaderSource vvs,
            GLSLShaderSource ffs, GLSLShaderSource ggs,
            GLSLShaderSource ttcs, GLSLShaderSource ttes,
            List<string> directives, NbShaderMode mode) : base(EntityType.ShaderConfig)
        {
            ShaderMode = mode;

            //Store objects
            AddSource(NbShaderSourceType.VertexShader, vvs);
            AddSource(NbShaderSourceType.FragmentShader, ffs);
            AddSource(NbShaderSourceType.GeometryShader, ggs);
            AddSource(NbShaderSourceType.TessControlShader, ttcs);
            AddSource(NbShaderSourceType.TessEvaluationShader, ttes);
                
            foreach (string d in directives)
                this.directives.Add(d);
        }

        public void AddSource(NbShaderSourceType t, GLSLShaderSource s)
        {
            if (s == null)
                return;
            Sources[t] = s;

            //Add shader reference to source object
            s.SetConfigReference(this);
            s.IsUpdated += OnSourceUpdate;

        }

        public override GLSLShaderConfig Clone()
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

            writer.WritePropertyName("Directives");
            writer.WriteStartArray();
            foreach (string d in directives)
                writer.WriteValue(d);
            writer.WriteEndArray();


            writer.WriteEndObject();
        }

        public static GLSLShaderConfig Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string name = token.Value<string>("Name");

            GLSLShaderSource vs = null;
            GLSLShaderSource fs = null;

            foreach (var ct in token["Sources"])
            {
                if (ct["VertexShader"] != null)
                {
                    string path = ct.Value<string>("VertexShader");
                    vs = Common.RenderState.engineRef.GetShaderSourceByFilePath(path);
                    if (vs == null)
                    {
                        vs = new GLSLShaderSource(path, true);
                    }
                } else if (ct["FragmentShader"] != null)
                {
                    string path = ct.Value<string>("FragmentShader");
                    fs = Common.RenderState.engineRef.GetShaderSourceByFilePath(path);
                    if (fs == null)
                    {
                        fs = new GLSLShaderSource(path, true);
                    }
                }
            }

            //parse Directives
            List<string> directives = new();
            foreach (var ct in token["Directives"])
                directives.Add(ct.ToString());

            NbShaderMode mode = (NbShaderMode) token.Value<int>("ShaderMode");

            GLSLShaderConfig config = new GLSLShaderConfig(vs, fs, null, null, null, directives, mode);
            config.Name = name;
            
            return config;
        }

    }

}
