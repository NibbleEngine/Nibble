using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NbCore.Math;
#if OPENGL
    using OpenTK.Graphics;
#endif

namespace NbCore
{
    public class NbMeshGroup
    {
        public int ID;
        public List<NbMesh> Meshes = new();
        public int ActiveLOD;
        public List<float> LODDistances;
#if OPENGL
        public BufferHandle GroupTBO1;
#else
        public int GroupTBO1;
#endif
        //Following arrays store matrices
        public NbMatrix4[] NextFrameJointData; 
        public NbMatrix4[] PrevFrameJointData;
        public float FrameInterolationCoeff = 0.0f;
        public NbMatrix4[] GroupTBO1Data; //use this for rendering
        public List<JointBindingData> JointBindingDataList;
        public int JointCount = 0;
        
        public NbMeshGroup()
        {
            JointBindingDataList = new List<JointBindingData>();
            for (int i = 0; i < 512; i++)
                JointBindingDataList.Add(new JointBindingData());
        }

        public void AddMesh(NbMesh mesh)
        {
            if (Meshes.Contains(mesh))
            {
                Common.Callbacks.Log(this, "Mesh Already in group", LogVerbosityLevel.WARNING);
                return;
            }

            Meshes.Add(mesh);
            mesh.Group = this;
        }
    }

    
}
