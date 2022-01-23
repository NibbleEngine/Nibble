using NbCore.Math;

namespace NbCore
{
    public class NbUniform
    {
        public string Name = "Uniform"; //Uniform custom name
        public NbVector4 Values = new(0.0f);
        public NbUniformFormat Format;
        //Actual shader variable name where the uniform will be bound
        public string ShaderBinding; 
        public int ID = -1;
        
        public NbUniform(string name, int id, NbVector4 values)
        {
            Name = name;
            ID = id;
            Values = values;
        }

        public NbUniform() { }

    }

}