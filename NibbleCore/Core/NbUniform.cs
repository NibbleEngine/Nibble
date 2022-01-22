using NbCore.Math;

namespace NbCore
{
    public class NbUniform
    {
        public string Name = "Uniform";
        public NbVector4 Values = new(0.0f);
        public int ShaderLocation = -1;
        public int ID = -1;
        
        public NbUniform(string name, int loc, int id, NbVector4 values)
        {
            Name = name;
            ShaderLocation = loc;
            ID = id;
            Values = values;
        }

        public NbUniform() { }

    }

}