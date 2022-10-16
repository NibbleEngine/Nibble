using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;
using NbCore.Common;
using NbCore.Math;
using OpenTK.Graphics.ES11;

namespace NbCore.Platform.Graphics.OpenGL
{
    public enum GLMeshInstaneStatus
    {
        ADD,
        REMOVE,
        UPDATE
    }

    public static class GLMeshInstanceManager
    {
        public static MeshInstance[] atlas_cpmu = new MeshInstance[1024];
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
            
            mesh.InstanceIndexBuffer[instanceID] = insertionIndex;
            if (insertionIndex == instance_counter)
            {
                instance_counter++;
                ExtendAtlasArray();
            }
        }

        public static void RemoveMeshInstance(ref NbMesh mesh, int instanceID) 
        {
            //Bring last index to this instance's place
            MeshInstance last = mesh.InstanceDataBuffer[mesh.InstanceCount - 1];
            atlas_cpmu[mesh.InstanceIndexBuffer[instanceID]] = last;
            free_slots.Add(mesh.InstanceIndexBuffer[mesh.InstanceCount - 1]);
        }

        public static void UpdateMeshInstance(ref NbMesh mesh, int instanceID)
        {
            atlas_cpmu[mesh.InstanceIndexBuffer[instanceID]] = mesh.InstanceDataBuffer[instanceID];
        }

        private static void ExtendAtlasArray()
        {
            if (instance_counter > 0.9 * atlas_cpmu.Length)
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
