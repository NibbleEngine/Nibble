using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using OpenTK.Graphics.OpenGL;

namespace NbCore.Managers
{
    public class ShaderManager: EntityManager<NbShader>
    {
        //public readonly List<GLSLShaderConfig> GLDeferredShaders = new();
        //public readonly List<GLSLShaderConfig> GLForwardTransparentShaders = new();
        //public readonly List<GLSLShaderConfig> GLDeferredDecalShaders = new();
        public readonly Queue<GLSLShaderConfig> CompilationQueue = new();

        private readonly Dictionary<long, NbShader> ShaderHashMap = new();
        private readonly Dictionary<long, List<MeshMaterial>> ShaderMaterialMap = new();

        public bool AddShader(NbShader shader)
        {
            if (Add(shader))
            {
                GUIDComponent gc = shader.GetComponent<GUIDComponent>() as GUIDComponent;
                ShaderHashMap[shader.Hash] = shader;
                ShaderMaterialMap[gc.ID] = new();
                
                return true;
            }
            return false;
        }

        public void AddShaderForCompilation(GLSLShaderConfig shader)
        {
            CompilationQueue.Enqueue(shader);
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

        public void AddMaterialToShader(MeshMaterial mat)
        {
            ShaderMaterialMap[mat.Shader.GetID()].Add(mat);
        }

        public bool ShaderContainsMaterial(NbShader shader, MeshMaterial mat)
        {
            return ShaderMaterialMap[shader.GetID()].Contains(mat);
        }

        public List<MeshMaterial> GetShaderMaterials(NbShader shader)
        {
            return ShaderMaterialMap[shader.GetID()];
        }

        public new void CleanUp()
        {
            //Shader Cleanup
            ShaderMaterialMap.Clear();
            ShaderHashMap.Clear();
            
            base.CleanUp();
        }
        


    }
}
