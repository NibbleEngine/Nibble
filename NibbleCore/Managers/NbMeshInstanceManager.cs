using Microsoft.CodeAnalysis;
using Microsoft.VisualBasic.FileIO;
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

        public static void Report()
        {
            foreach (KeyValuePair<int, NbMesh> keyValuePair in atlas_position_mesh_map)
            {
                Console.WriteLine($"Mesh {keyValuePair.Value.ID} Position {keyValuePair.Key} Count {keyValuePair.Value.InstanceCount}");
            }
        }

        public static void AddMeshInstance(ref NbMesh mesh)
        {
            //At first check if the mesh is already in the atlas
            if (mesh.AtlasBufferOffset != -1)
            {
                //Check if a new instance fits
                if (atlas_position_mesh_map.ContainsKey(mesh.AtlasBufferOffset + mesh.InstanceCount - 1))
                {
                    //At first check if we need to extend the array
                    //consider worst case scenario
                    if (free_slots[free_slots.Count - 1] + mesh.InstanceCount > atlas_cpmu.Length)
                        ExtendAtlasArray();


                    //Now try to find a free index that can host all the mesh instances
                    int new_mesh_slot = -1;
                    for (int i=free_slots.Count - 1; i >= 0; i--)
                    {
                        bool is_good = true;
                        for (int j = 0; j < mesh.InstanceCount; j++)
                        {
                            if (atlas_position_mesh_map.ContainsKey(free_slots[i] + j))
                                is_good = false;
                        }

                        if (is_good)
                        {
                            new_mesh_slot = free_slots[i];
                            break;
                        }
                    }

                    if (new_mesh_slot == -1)
                        throw new NotImplementedException();

                    //Relocate previous + instance data to the new position
                    for (int i = 0; i < mesh.InstanceCount; i++)
                        atlas_cpmu[new_mesh_slot + i] = atlas_cpmu[mesh.AtlasBufferOffset + i];
                    
                    atlas_position_mesh_map.Remove(mesh.AtlasBufferOffset);
                    if (new_mesh_slot == free_slots[free_slots.Count - 1])
                        free_slots.Add(new_mesh_slot + mesh.InstanceCount);
                    
                    free_slots.Remove(new_mesh_slot);
                    free_slots.Add(mesh.AtlasBufferOffset);
                    free_slots.Sort();
                    mesh.AtlasBufferOffset = new_mesh_slot; //Set new slot
                    atlas_position_mesh_map[new_mesh_slot] = mesh; //Save mesh
                } else
                {
                    //Fits
                    //Remove index from the free slots
                    int index = free_slots.IndexOf(mesh.AtlasBufferOffset + mesh.InstanceCount - 1);
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
                if (!free_slots.Contains(insertionIndex + 1) && !atlas_position_mesh_map.ContainsKey(insertionIndex + 1))
                    free_slots.Insert(0, insertionIndex + 1);
            }
            //Report();
        }

        public static void RemoveMeshInstance(ref NbMesh mesh) 
        {
            //Always remove the last instance of the mesh from the atlas
            if (!free_slots.Contains(mesh.AtlasBufferOffset + mesh.InstanceCount - 1))
            {
                free_slots.Add(mesh.AtlasBufferOffset + mesh.InstanceCount - 1);
                free_slots.Sort(); //TODO: THIS HAS TO BE CHANGED AT SOME POINT WITH A SMARTER DATA STRUCTURE
            }
            
            //Remove mesh from the index mesh map
            if (mesh.InstanceCount == 1)
            {
                atlas_position_mesh_map.Remove(mesh.AtlasBufferOffset);
                mesh.AtlasBufferOffset = -1;
            }
            //Report();
        }

        public static void UpdateMeshInstance(ref NbMesh mesh, int instanceID)
        {
            atlas_cpmu[mesh.AtlasBufferOffset + instanceID] = mesh.InstanceDataBuffer[instanceID];
        }

        private static void ExtendAtlasArray()
        {
            //Make a new atlas array
            MeshInstance[] new_atlas_cpmu = new MeshInstance[atlas_cpmu.Length + 1024];
            //Copy old data
            Array.Copy(atlas_cpmu, new_atlas_cpmu, atlas_cpmu.Length);

            //Swap arrays
            atlas_cpmu = new_atlas_cpmu;
            //The orphan array should be collected by the GC
        }

        public static int GetAtlasSize()
        {
            return Marshal.SizeOf(typeof(MeshInstance)) * atlas_cpmu.Length;
        }

    }
}
