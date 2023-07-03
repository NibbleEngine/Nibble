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
        private static List<int> free_slots = new() { 0 };

        public static void AddMeshInstance(ref NbMesh mesh, int instanceID)
        {
            //At first check if the mesh is already in the atlas
            if (mesh.AtlasBufferOffset != -1)
            {
                //Check if a new instance fits
                if (atlas_position_mesh_map.ContainsKey(mesh.AtlasBufferOffset + instanceID))
                {
                    //Does not fit do relocates
                    throw new NotImplementedException();
                } else
                {
                    //Fits
                    //Remove index from the free slots
                    int index = free_slots.IndexOf(mesh.AtlasBufferOffset + instanceID);
                    free_slots.RemoveAt(index);
                    if (!atlas_position_mesh_map.ContainsKey(mesh.AtlasBufferOffset + mesh.InstanceCount) && 
                        !free_slots.Contains(mesh.AtlasBufferOffset + mesh.InstanceCount))
                        free_slots.Insert(index, mesh.AtlasBufferOffset + mesh.InstanceCount);
                }
                
            } else
            {
                //Insert mesh for the first time
                //pop first free slot
                int insertionIndex = free_slots[0];
                free_slots.RemoveAt(0);
                
                mesh.AtlasBufferOffset = insertionIndex;
                atlas_position_mesh_map[insertionIndex] = mesh;
                if (!free_slots.Contains(insertionIndex + 1))
                    free_slots.Insert(0, insertionIndex + 1);
            }
        }

        public static void RemoveMeshInstance(ref NbMesh mesh, int instanceID) 
        {
            //Bring last index to this instance's place
            //TODO: Remove the next 2 lines I think they are not needed
            //MeshInstance last = mesh.InstanceDataBuffer[mesh.InstanceCount - 1];
            //atlas_cpmu[mesh.InstanceIndexBuffer[instanceID]] = last;
           
            if (!free_slots.Contains(mesh.AtlasBufferOffset + instanceID))
            {
                free_slots.Add(mesh.AtlasBufferOffset + instanceID);
                free_slots.Sort(); //TODO: THIS HAS TO BE CHANGED AT SOME POINT WITH A SMARTER DATA STRUCTURE
            }
            
            //Remove mesh from the index mesh map
            if (mesh.InstanceCount == 1)
            {
                atlas_position_mesh_map.Remove(mesh.AtlasBufferOffset);
                mesh.AtlasBufferOffset = -1;
            }

        }

        public static void UpdateMeshInstance(ref NbMesh mesh, int instanceID)
        {
            atlas_cpmu[mesh.AtlasBufferOffset + instanceID] = mesh.InstanceDataBuffer[instanceID];
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
