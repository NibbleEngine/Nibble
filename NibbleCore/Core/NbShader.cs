using NbCore.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NbCore
{
    public delegate void ShaderUpdatedEventHandler(NbShader shader);

    [NbSerializable]
    public class NbShader : Entity
    {
        //Program ID
        public int ProgramID = -1;
        public ulong Hash = 0;
        public int RefCounter = 0;
        public bool IsGeneric = false; //Used to flag internal shaders

        //References
        private NbShaderConfig RefShaderConfig = null;
        public List<string> directives = new(); //Extra Compilation directives (on top of the config directives)

        //Keep active uniforms
        public Dictionary<string, NbUniformFormat> uniformLocations = new();
        public NbShaderState CurrentState = NbShaderState.Create(); //Empty state
        public Dictionary<NbShaderSourceType, int> SourceObjects = new();
        public new NbShaderType Type = NbShaderType.NULL_SHADER;

        //Shader Compilation log
        public string CompilationLog = "";

        public ShaderUpdatedEventHandler IsUpdated;

        public NbShader() : base(EntityType.Shader)
        {
            
        }

        public NbShader(NbShaderConfig conf) : base(EntityType.Shader)
        {
            SetShaderConfig(conf);
        }

        public NbShader(NbShader shader) : base(EntityType.Shader)
        {
            RefShaderConfig = shader.RefShaderConfig;
            IsGeneric = shader.IsGeneric;
            directives = new List<string>(shader.directives);
            Type = shader.Type;
        }

        public void ClearCurrentState()
        {
            CurrentState.Clear();
        }

        public NbShaderConfig GetShaderConfig()
        {
            return RefShaderConfig;
        }

        public void SetShaderConfig(NbShaderConfig conf)
        {
            if (RefShaderConfig != null)
                RefShaderConfig.IsUpdated -= OnShaderUpdate;
            
            RefShaderConfig = conf;
            conf.IsUpdated += OnShaderUpdate;
        }

        public void AddReference()
        {
            RefCounter++;
        }

        public void RemoveReference()
        {
            RefCounter--;
        }

        public void OnShaderUpdate()
        {
            //Issue shader for re-compilation
            Common.RenderState.engineRef.GetSystem<Systems.RenderingSystem>().ShaderMgr.AddShaderForCompilation(this);
        }

        public void FilterState(ref NbShaderState state)
        {
            var arr = state.Data.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                string uf_name = arr[i].Key.Split(':')[1];
                if (!uniformLocations.ContainsKey(uf_name))
                    state.Data.Remove(arr[i].Key);
            }
        }

        public override Entity Clone()
        {
            throw new NotImplementedException();
        }


        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("Path");
            writer.WriteValue(Path);

            writer.WritePropertyName("Config");
            writer.WriteValue(RefShaderConfig.Name);

            writer.WritePropertyName("Type");
            writer.WriteValue(Type);

            writer.WritePropertyName("IsGeneric");
            writer.WriteValue(IsGeneric);

            writer.WritePropertyName("Directives");
            writer.WriteStartArray();
            foreach (string val in directives)
                writer.WriteValue(val);
            
            writer.WriteEndArray();
            writer.WriteEndObject();

        }
        
        public static NbShader Deserialize(JToken token)
        {
            string path = token.Value<string>("Path");
            string confname = token.Value<string>("Config");
            
            //LIT
            NbShader shader = new()
            {
                Type = (NbShaderType)token.Value<int>("Type"),
                IsGeneric = token.Value<bool>("IsGeneric") //TODO: Fix
            };

            //Add Directives
            foreach (string directive in token["directives"])
                shader.directives.Add(directive);
            
            shader.SetShaderConfig(RenderState.engineRef.GetShaderConfigByName(confname));
            Callbacks.Assert(RenderState.engineRef.CompileShader(shader), "Error on shader compilation");
            return shader;
        }


    }
}
