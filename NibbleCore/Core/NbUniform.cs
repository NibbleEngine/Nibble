using NbCore.Math;

namespace NbCore
{
    public class NbUniform
    {
        public string Name = "Uniform"; //Uniform custom name
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