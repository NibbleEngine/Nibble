using System;
using System.Collections.Generic;
using System.Linq;

namespace NbCore
{

    public delegate void ShaderUpdatedEventHandler();

    public class NbShader : Entity
    {
        //Program ID
        public int ProgramID = -1;
        public long Hash = -1;
        public int RefCounter = 0;
        public bool IsGeneric = false; //Used to flag internal shaders

        //References
        private MeshMaterial RefMaterial = null;
        private GLSLShaderConfig RefShaderConfig = null;

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

        public void ClearCurrentState()
        {
            CurrentState.Clear();
        }

        public void RemoveReference()
        {
            RefCounter--;
        }

        public void AddReference()
        {
            RefCounter++;
        }

        public MeshMaterial GetMaterial()
        {
            return RefMaterial;
        }

        public void SetMaterial(MeshMaterial mat)
        {
            RefMaterial = mat;
            RefShaderConfig = mat.ShaderConfig;
            IsUpdated -= IsUpdated;
        }

        public GLSLShaderConfig GetShaderConfig()
        {
            return RefShaderConfig;
        }

        public void SetShaderConfig(GLSLShaderConfig conf)
        {
            RefShaderConfig = conf;
            RefMaterial = null;
            IsUpdated -= IsUpdated;
            conf.IsUpdated += OnShaderUpdate;
        }

        public void OnShaderUpdate()
        {
            //Issue shader for re-compilation
            Common.RenderState.engineRef.renderSys.ShaderMgr.AddShaderForCompilation(this);
        }

        public void FilterState(ref NbShaderState state)
        {
            var arr = state.Data.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                string uf_name = arr[i].Key.Split(':')[1];
                if (!uniformLocations.ContainsKey(uf_name))
                    state.Data.Remove(uf_name);
            }
        }

        public override Entity Clone()
        {
            throw new NotImplementedException();
        }

    }
}
