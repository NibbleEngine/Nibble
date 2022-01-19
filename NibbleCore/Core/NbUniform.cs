using OpenTK.Mathematics;

namespace NbCore
{
    public class NbUniform
    {
        public string Name;
        public Vector4 Values;
        public int ShaderLocation = -1;
        public int ID = -1;
        
        public NbUniform()
        {
            Values = new Vector4(0.0f);
        }

        public NbUniform(string name)
        {
            Name = name;
            Values = new Vector4(0.0f);
        }
    }

}