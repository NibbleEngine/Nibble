using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public class NbMeshGroup
    {
        public int ID;
        public List<NbMesh> Meshes;
        public int ActiveLOD;
        public List<float> LODDistances;
        public int GroupTBO1;
    }
}
