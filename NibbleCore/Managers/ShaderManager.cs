using System.Collections.Generic;

namespace NbCore.Managers
{
    public class ShaderManager: NbEntityManager<NbShader>
    {
        public readonly Queue<NbShader> ShaderCompilationQueue = new();
        
        private readonly Dictionary<ulong, NbShader> ShaderHashMap = new();
        private readonly Dictionary<ulong, NbShader> GenericShaderHashMap = new();
        
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
        
        public NbShader GetGenericShaderByHash(ulong hash)
        {
            return GenericShaderHashMap[hash];
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

        public bool GenericShaderHashExists(ulong hash)
        {
            return GenericShaderHashMap.ContainsKey(hash);
        }

        public bool ShaderIDExists(ulong ID) //GUID
        {
            return EntityMap.ContainsKey(ID);
        }

        public void MakeShaderGeneric(ulong hash)
        {
            if (ShaderHashExists(hash))
            {
                NbShader shader = GetShaderByHash(hash);
                GenericShaderHashMap.Add(hash, shader);
                ShaderHashMap.Remove(hash);
            }
        }

        public void MakeShaderNonGeneric(ulong hash)
        {
            if (GenericShaderHashExists(hash))
            {
                NbShader shader = GetGenericShaderByHash(hash);
                ShaderHashMap.Add(hash, shader);
                GenericShaderHashMap.Remove(hash);
            }
        }

        public new void CleanUp()
        {
            //Shader Cleanup
            ShaderHashMap.Clear();
            
            base.CleanUp();
        }
        


    }
}
