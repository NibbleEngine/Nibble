using System;
using NbCore.Common;
using NbCore;

namespace NbCore
{
    /**
     * Instance Buffer Documentation
     * Instancing using the GLMeshBufferManager works as follows.
     *      
     *      Every time an instance of a mesh has to be created, the AddMeshInstance method is called.
     *      This method is responsible for mainly incremented the mesh's global instance counter
     *      but also to allocate enough space in the underlying instance buffer to store the instance's
     *      properties.
     *      
     *      Note: I think that this process can be skipped. Managing RenderInstances is more than enough
     *      to differentiate instances, keep track of the active instances and also manage the instance buffer
     * 
     *      Every time the rendering status of an instance is modified, the AddRenderInstance/RemoveRenderInstance
     *      methods are called.
     *      
     *      The AddRenderInstance method, stores the instance data of the requested instance at the end of the instance buffer.
     *      It also sets the new render instance id to the requested meshcomponent 
     *      
     *      Schematic Representation of the instance insertion
     *      | 0 | 1 | 2 | 3 | 4 | * | <----
     *      
     *      
     *      The RemoveRenderInstance method, is responsible for removing the requested instance from the buffer, 
     *      using its stored renderInstanceID, which reveals its position in the buffer. In order to prevent the
     *      update of all the instance refs of all intermediate instances, the method swaps the instance data
     *      with just the last instance of the buffer and decreases the renderInstanceCounter.
     *      
     *      Schematic Representation of the instance removal (removing Instance 2)
     
     *      Start:     
     *      | 0 | 1 | 2 | 3 | 4 | x | x |
     *      Swap 2 with 4 that is the last member:     
     *      | 0 | 1 | 4 | 3 | 2 | x | x |
     *      Data for 2 is still in the buffer, but the counter has been decreased so it won't be used.     
     *      | 0 | 1 | 4 | 3 | 2 | x | x |
     *
     * 
     * 
     */


    public class NbMeshBufferManager
    {
        public static int GetNextMeshInstanceID(ref NbMesh mesh)
        {
            int render_instance_id = mesh.InstanceCount;

            //Expand mesh data buffer if required
            if (render_instance_id + 1 > mesh.InstanceDataBuffer.Length)
            {
                MeshInstance[] newBuffer = new MeshInstance[mesh.InstanceDataBuffer.Length + 5];//Extend by 5 instances
                Array.Copy(mesh.InstanceDataBuffer, newBuffer, mesh.InstanceDataBuffer.Length);
                mesh.InstanceDataBuffer = newBuffer;
            }

            return render_instance_id;
        }

        public static void AddRenderInstance(ref MeshComponent mc, NbTransformData td)
        {
            NbMesh mesh = mc.Mesh;

            if (mc.InstanceID >= 0)
            {
                Callbacks.Assert(false, "Non negative instanceID on a non visible mesh. This should not happen");
                return;
            }

            mc.InstanceID = GetNextMeshInstanceID(ref mesh);

            //Store Component
            mesh.ComponentDict[mc.InstanceID] = mc;
            
            //Uplod worldMat to the meshVao
            NbMatrix4 actualWorldMat = td.WorldTransformMat;
            NbMatrix4 actualWorldMatInv = (actualWorldMat).Inverted();
            SetInstanceWorldMat(mesh, mc.InstanceID, actualWorldMat);
            SetInstanceWorldMatInv(mesh, mc.InstanceID, actualWorldMatInv);
            SetInstanceNormalMat(mesh, mc.InstanceID, NbMatrix4.Transpose(actualWorldMatInv));
            SetInstanceUniform4(mesh, mc.InstanceID, 0, new NbVector4(mesh.MetaData.FirstSkinMat, 0.0f, 0.0f, 0.0f));
            SetInstanceEntityID(mesh, mc.InstanceID, (uint) mc.RefEntity.ID);

            if (mesh.MetaData.BoneRemapIndices != null)
                SetInstanceBoneRemap(mesh, mc.InstanceID, mesh.MetaData.BoneRemapIndices);
            mesh.InstanceCount++;
        }
        
        public static void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            Callbacks.Assert(mc.InstanceID >= 0, "Negative instance ID. ILLEGAL instance removal");

            if (mc.InstanceID != mesh.InstanceCount - 1)
            {
                mesh.ComponentDict[mc.InstanceID] = mesh.ComponentDict[mesh.InstanceCount - 1];
                mesh.ComponentDict.Remove(mesh.ComponentDict[mesh.InstanceCount - 1].InstanceID);
                mesh.ComponentDict[mc.InstanceID].InstanceID = mc.InstanceID;
                mesh.ComponentDict[mc.InstanceID].IsUpdated = true;
                
            } else
            {
                mesh.ComponentDict.Remove(mc.InstanceID);
            }
            
            mesh.InstanceCount--;
        }
        
        //Overload with transform overrides
        public static void ClearMeshInstances(NbMesh mesh)
        {
            mesh.InstanceCount = 0;
        }

        public static void SetInstanceEntityID(NbMesh mesh, int instanceID, uint entityID)
        {
            mesh.InstanceDataBuffer[instanceID].entityID = entityID;
        }
        
        public static uint GetInstanceEntityID(NbMesh mesh, int instance_id)
        {
            MeshInstance mi = mesh.InstanceDataBuffer[instance_id];
            return mi.entityID;
        }

        public static NbMatrix4 GetInstanceWorldMat(NbMesh mesh, int instance_id)
        {
            MeshInstance mi = mesh.InstanceDataBuffer[instance_id];
            return mi.worldMat;
        }

        public static NbMatrix4 GetInstanceNormalMat(NbMesh mesh, int instance_id)
        {
            MeshInstance mi = mesh.InstanceDataBuffer[instance_id];
            return mi.normalMat;
        }

        public static NbVector3 GetInstanceColor(NbMesh mesh, int instance_id)
        {
            MeshInstance mi = mesh.InstanceDataBuffer[instance_id];
            return mi.color;
        }

        public static void SetInstanceColor(NbMesh mesh, int instance_id, NbVector3 color)
        {
            mesh.InstanceDataBuffer[instance_id].color = color;
        }

        public static void SetInstanceUniform4(NbMesh mesh, int instance_id, int uniform_id, NbVector4 un)
        {
            mesh.InstanceDataBuffer[instance_id].uniforms[uniform_id] = un;
        }

        public static NbVector4 GetInstanceUniform4(NbMesh mesh, int instance_id, int uniform_id)
        {
            MeshInstance mi = mesh.InstanceDataBuffer[instance_id];
            return mi.uniforms[uniform_id];
        }

        public static void SetInstanceWorldMat(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            mesh.InstanceDataBuffer[instance_id].worldMat = mat;
        }

        public static void SetInstanceWorldMatInv(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            mesh.InstanceDataBuffer[instance_id].worldMatInv = mat;
        }

        public static void SetInstanceNormalMat(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            mesh.InstanceDataBuffer[instance_id].normalMat = mat;
        }

        public static void SetInstanceBoneRemap(NbMesh mesh, int instance_id, int[] boneRemap)
        {
            unsafe
            {
                for (int i= 0; i < boneRemap.Length; i++)
                    mesh.InstanceDataBuffer[instance_id].boneIndices[i] = boneRemap[i];
            }
        }


    }
}
