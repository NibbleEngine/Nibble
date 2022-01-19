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
        public float[] GroupTBO1Data; //used to store position, rotation and scale vectors
        public int[] boneRemapIndices;
        public List<JointBindingData> JointBindingDataList;
    }
}
