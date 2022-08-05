using System;
using System.Collections.Generic;
using System.Linq;

namespace NbCore
{
    public delegate void ShaderUpdatedEventHandler(NbShader shader);

    public class NbShader : Entity
    {
        //Program ID
        public int ProgramID = -1;
        public ulong Hash = 0;
        public int RefCounter = 0;
        public bool IsGeneric = false; //Used to flag internal shaders

        //References
        private GLSLShaderConfig RefShaderConfig = null;
        public List<string> directives = new(); //Extra Compilation directives (on top of the config directives)

        //Keep active uniforms
        public Dictionary<string, NbUniformFormat> uniformLocations = new();
        public NbShaderState CurrentState = NbShaderState.Create(); //Empty state
        public Dictionary<NbShaderSourceType, int> SourceObjects = new();
        public new NbShaderType Type;

        //Shader Compilation log
        public string CompilationLog = "";

        public ShaderUpdatedEventHandler IsUpdated;

        public NbShader() : base(EntityType.Shader)
        {
            
        }

        public NbShader(GLSLShaderConfig conf) : base(EntityType.Shader)
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

        public GLSLShaderConfig GetShaderConfig()
        {
            return RefShaderConfig;
        }

        public void SetShaderConfig(GLSLShaderConfig conf)
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

    }
}
