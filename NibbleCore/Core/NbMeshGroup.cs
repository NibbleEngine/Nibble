using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NbCore.Math;

namespace NbCore
{
    public class NbMeshGroup
    {
        public int ID;
        public List<NbMesh> Meshes;
        public int ActiveLOD;
        public List<float> LODDistances;
        public int GroupTBO1;
        //Following arrays store matrices
        public NbMatrix4[] NextFrameJointData; 
        public NbMatrix4[] PrevFrameJointData;
        public float FrameInterolationCoeff = 0.0f;
        public NbMatrix4[] GroupTBO1Data; //use this for rendering
        public int[] boneRemapIndices;
        public List<JointBindingData> JointBindingDataList;
        
        public NbMeshGroup()
        {
            JointBindingDataList = new List<JointBindingData>();
            for (int i = 0; i < 256; i++)
                JointBindingDataList.Add(new JointBindingData());
        }
    }

    
}
