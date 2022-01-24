using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using OpenTK.Graphics.OpenGL;

namespace NbCore.Managers
{
    public class ShaderManager: EntityManager<NbShader>
    {
        public readonly Queue<NbShader> CompilationQueue = new();

        private readonly Dictionary<long, NbShader> ShaderHashMap = new();
        
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

        public void AddShaderForCompilation(NbShader req)
        {
            CompilationQueue.Enqueue(req);
        }

        public NbShader GetShaderByHash(long hash)
        {
            return ShaderHashMap[hash];
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

        public bool ShaderIDExists(long ID) //GUID
        {
            return EntityMap.ContainsKey(ID);
        }

        public new void CleanUp()
        {
            //Shader Cleanup
            ShaderHashMap.Clear();
            
            base.CleanUp();
        }
        


    }
}
