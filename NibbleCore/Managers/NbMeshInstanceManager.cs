using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NbCore
{
    public static class NbMeshInstanceManager
    {
        public static MeshInstance[] atlas_cpmu = new MeshInstance[1024];
        private static Dictionary<int, NbMesh> atlas_position_mesh_map = new();
        private static int instance_counter;
        private static List<int> free_slots = new();

        public static void AddMeshInstance(ref NbMesh mesh, int instanceID)
        {
            //Find Insertion Index
            int insertionIndex = instance_counter;
            if (free_slots.Count > 0)
            {
                insertionIndex = free_slots[0];
                free_slots.RemoveAt(0);
            }

            //Make sure that new instances of instanced meshes are positioned together
            if (mesh.InstanceCount > 1 && insertionIndex != mesh.InstanceIndexBuffer[mesh.InstanceCount - 2] + 1)
            {
                insertionIndex = mesh.InstanceIndexBuffer[mesh.InstanceCount - 2] + 1;

                //Check if a mesh occupies the insertion position
                if (atlas_position_mesh_map.ContainsKey(insertionIndex)) 
                {
                    //Old mesh has to be moved along with all its instances to the end of the buffer
                    ExtendAtlasArray(instance_counter + 2); //Extend if required
                    
                    NbMesh oldMesh = atlas_position_mesh_map[insertionIndex];
                    
                    for (int i = 0; i < oldMesh.InstanceCount; i++)
                    {
                        oldMesh.InstanceIndexBuffer[i] = instance_counter + i;
                        UpdateMeshInstance(ref oldMesh, i);
                    }
                }
            } else if (mesh.InstanceCount == 1)
            {
                atlas_position_mesh_map[insertionIndex] = mesh; //Keep reference of the mesh and its insertion index
            }

            //Normal Insertion
            mesh.InstanceIndexBuffer[instanceID] = insertionIndex;
            if (insertionIndex == instance_counter)
            {
                instance_counter++;
                ExtendAtlasArray(instance_counter);
            }
        
        }

        public static void RemoveMeshInstance(ref NbMesh mesh, int instanceID) 
        {
            //Bring last index to this instance's place
            //TODO: Remove the next 2 lines I think they are not needed
            //MeshInstance last = mesh.InstanceDataBuffer[mesh.InstanceCount - 1];
            //atlas_cpmu[mesh.InstanceIndexBuffer[instanceID]] = last;
            free_slots.Add(mesh.InstanceIndexBuffer[instanceID]);

            //Remove mesh from the index mesh map
            if (mesh.InstanceCount == 1)
            {
                atlas_position_mesh_map.Remove(mesh.InstanceIndexBuffer[instanceID]);
            }

        }

        public static void UpdateMeshInstance(ref NbMesh mesh, int instanceID)
        {
            atlas_cpmu[mesh.InstanceIndexBuffer[instanceID]] = mesh.InstanceDataBuffer[instanceID];
        }

        private static void ExtendAtlasArray(int threshold)
        {
            if (threshold > 0.9 * atlas_cpmu.Length)
            {
                //Make a new atlas array
                MeshInstance[] new_atlas_cpmu = new MeshInstance[atlas_cpmu.Length + 1024];
                //Copy old data
                Array.Copy(atlas_cpmu, new_atlas_cpmu, atlas_cpmu.Length);

                //Swap arrays
                atlas_cpmu = new_atlas_cpmu;
                //The orphan array should be collected by the GC
            }
        }

        public static int GetAtlasSize()
        {
            return Marshal.SizeOf(typeof(MeshInstance)) * atlas_cpmu.Length;
        }

    }
}
