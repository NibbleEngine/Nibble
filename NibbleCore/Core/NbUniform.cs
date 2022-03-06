using NbCore.Math;
using Newtonsoft.Json;
using System;

namespace NbCore
{
    public class NbUniform
    {
        [NbSerializable]
        public string Name = "Uniform"; //Uniform custom name
        [NbSerializable]
        public NbVector4 Values = new(0.0f);
        public NbUniformState State;

        public NbUniform(string name, NbVector4 values)
        {
            Name = name;
            Values = values;
            State = new()
            {
                ShaderBinding = "",
                ShaderLocation = -1,
                Type = NbUniformType.Float
            };
        }

        public NbUniform() { }

    }

}