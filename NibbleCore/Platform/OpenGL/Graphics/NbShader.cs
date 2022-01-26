using System;
using System.Collections.Generic;
using System.Linq;

namespace NbCore
{
    public class NbShader : Entity
    {
        //Program ID
        public int ProgramID = -1;
        public long Hash = -1;
        //Keep active uniforms
        public Dictionary<string, NbUniformFormat> uniformLocations = new();
        public NbShaderState CurrentState = NbShaderState.Create(); //Empty state
        public Dictionary<NbShaderSourceType, int> SourceObjects = new();
        public new NbShaderType Type;

        public MeshMaterial RefMaterial;
        public GLSLShaderConfig RefConfig;
        
        //Shader Compilation log
        public string CompilationLog = "";

        public delegate void OnShaderUpdateEventHandler();
        public OnShaderUpdateEventHandler IsUpdated;

        public NbShader() : base(EntityType.Shader)
        {
            
        }

        public void ClearCurrentState()
        {
            CurrentState.Clear();
        }

        public void FilterState(ref NbShaderState state)
        {
            //Floats;
            var arr = state.Floats.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr[i].Key))
                    state.Floats.Remove(arr[i].Key);
            }

            //Vec2s
            var arr1 = state.Vec2s.ToArray();
            for (int i = 0; i < arr1.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr1[i].Key))
                    state.Vec2s.Remove(arr1[i].Key);
            }

            //Vec3s
            var arr3 = state.Vec3s.ToArray();
            for (int i = 0; i < arr3.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr3[i].Key))
                    state.Vec3s.Remove(arr3[i].Key);
            }

            //Vec4s
            var arr4 = state.Vec4s.ToArray();
            for (int i = 0; i < arr4.Length; i++)
            {
                if (!uniformLocations.ContainsKey(arr4[i].Key))
                    state.Vec4s.Remove(arr4[i].Key);
            }

        }



        public override Entity Clone()
        {
            throw new NotImplementedException();
        }

    }
}
