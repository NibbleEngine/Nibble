using System.Collections.Generic;

namespace NbCore.Managers
{
    public class ShaderManager: NbEntityManager<NbShader>
    {
        public readonly Queue<NbShader> ShaderCompilationQueue = new();
        
        private readonly Dictionary<ulong, NbShader> ShaderHashMap = new();
        
        public bool AddShader(NbShader shader)
        {
            if (Add(shader))
            {
                ShaderHashMap[shader.Hash] = shader;
                return true;
            }
            return false;
        }

        public void AddShaderForCompilation(NbShader shader)
        {
            if (!ShaderCompilationQueue.Contains(shader))
                ShaderCompilationQueue.Enqueue(shader);
        }

        public NbShader GetShaderByHash(ulong hash)
        {
            return ShaderHashMap[hash];
        }
        
        public NbShader GetShaderByType(NbShaderType type)
        {
            return Entities.Find(x=>x.Type == type);
        }

        public NbShader GetShaderByID(ulong id)
        {
            return Get(id);
        }

        public bool ShaderHashExists(ulong hash)
        {
            return ShaderHashMap.ContainsKey(hash);
        }

        public new void CleanUp()
        {
            //Shader Cleanup
            ShaderHashMap.Clear();
            base.CleanUp();
        }
        
    }
}
