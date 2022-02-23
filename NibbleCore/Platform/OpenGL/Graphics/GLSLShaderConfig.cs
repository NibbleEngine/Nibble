using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NbCore
{
    [JsonConverter(typeof(GLSLShaderConfigConverter))]
    public class GLSLShaderConfig : Entity
    {
        public string Name = "";

        public Dictionary<NbShaderSourceType, GLSLShaderSource> Sources = new();
        public List<string> directives = new();

        public HashSet<NbShader> ReferencedByShaders = new();
        
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
            
        }
        


        public override GLSLShaderConfig Clone()
        {
            throw new NotImplementedException();
        }

    }

    public class GLSLShaderConfigConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            GLSLShaderConfig conf = (GLSLShaderConfig)value;

            writer.WriteStartObject();

            writer.WritePropertyName("Name");
            writer.WriteValue(conf.Name);

            //Write Sources
            foreach (var kp in conf.Sources)
            {
                writer.WritePropertyName(kp.Key.ToString());
                writer.WriteValue(kp.Value.SourceFilePath);
            }

            writer.WritePropertyName("Directives");
            writer.WriteStartArray();
            foreach (string directive in conf.directives)
            {
                writer.WriteValue(directive);
            }

            writer.WriteEndArray();
            
            writer.WriteEndObject();
        }
    }


}
