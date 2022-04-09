using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using OpenTK.Graphics.OpenGL;

namespace NbCore.Managers
{
    public class ShaderManager: EntityManager<NbShader>
    {
        public readonly Queue<NbShader> ShaderCompilationQueue = new();
        
        private readonly Dictionary<long, NbShader> ShaderHashMap = new();
        private readonly Dictionary<long, NbShader> GenericShaderHashMap = new();
        
        public bool AddShader(NbShader shader)
        {
            if (Add(shader))
            {
                GUIDComponent gc = shader.GetComponent<GUIDComponent>() as GUIDComponent;
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

        public NbShader GetShaderByHash(long hash)
        {
            return ShaderHashMap[hash];
        }

        public NbShader GetGenericShaderByHash(long hash)
        {
            return GenericShaderHashMap[hash];
        }

        public NbShader GetShaderByType(NbShaderType type)
        {
            return Entities.Find(x=>x.Type == type);
        }

        public NbShader GetShaderByID(long id)
        {
            return Get(id);
        }

        public bool ShaderHashExists(long hash)
        {
            return ShaderHashMap.ContainsKey(hash);
        }

        public bool GenericShaderHashExists(long hash)
        {
            return GenericShaderHashMap.ContainsKey(hash);
        }

        public bool ShaderIDExists(long ID) //GUID
        {
            return EntityMap.ContainsKey(ID);
        }

        public void MakeShaderGeneric(long hash)
        {
            if (ShaderHashExists(hash))
            {
                NbShader shader = GetShaderByHash(hash);
                GenericShaderHashMap.Add(hash, shader);
                ShaderHashMap.Remove(hash);
            }
        }

        public void MakeShaderNonGeneric(long hash)
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
