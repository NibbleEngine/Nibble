

namespace NbCore
{
    public class NbMeshMetaData
    {
        //Mesh Properties
        [NbSerializable] public NbVector3 AABBMIN;
        [NbSerializable] public NbVector3 AABBMAX;
        [NbSerializable] public int VertrStartPhysics;
        [NbSerializable] public int VertrEndPhysics;
        [NbSerializable] public int VertrStartGraphics;
        [NbSerializable] public int VertrEndGraphics;
        [NbSerializable] public int BatchStartPhysics;
        [NbSerializable] public int BatchStartGraphics;
        [NbSerializable] public int BatchCount;
        [NbSerializable] public int FirstSkinMat;
        [NbSerializable] public int LastSkinMat;
        [NbSerializable] public int LODLevel;
        [NbSerializable] public int BoundHullStart;
        [NbSerializable] public int BoundHullEnd;

        //LOD Properties
        public int LODDistance1;
        public int LODDistance2;

        //Skinning Data
        //public bool skinned = false;
        public int[] BoneRemapIndices;

        public NbMeshMetaData()
        {
            //Init values to null
            VertrEndGraphics = 0;
            VertrStartGraphics = 0;
            VertrEndPhysics = 0;
            VertrStartPhysics = 0;
            BatchStartGraphics = 0;
            BatchStartPhysics = 0;
            BatchCount = 0;
            FirstSkinMat = 0;
            LastSkinMat = 0;
            BoundHullStart = 0;
            BoundHullEnd = 0;
            AABBMIN = new();
            AABBMAX = new();
        }

        public NbMeshMetaData(NbMeshMetaData input)
        {
            //Init values to null
            VertrEndGraphics = input.VertrEndGraphics;
            VertrStartGraphics = input.VertrStartGraphics;
            VertrEndPhysics = input.VertrEndPhysics;
            VertrStartPhysics = input.VertrStartPhysics;
            BatchStartGraphics = input.BatchStartGraphics;
            BatchStartPhysics = input.BatchStartPhysics;
            BatchCount = input.BatchCount;
            FirstSkinMat = input.FirstSkinMat;
            LastSkinMat = input.LastSkinMat;
            BoundHullStart = input.BoundHullStart;
            BoundHullEnd = input.BoundHullEnd;
            LODLevel = input.LODLevel;
            AABBMIN = new(input.AABBMIN);
            AABBMAX = new(input.AABBMAX);
        }


        public ulong GetHash()
        {
            ulong hash = (uint)VertrStartPhysics;
            hash = NbHasher.CombineHash(hash, (uint) VertrEndPhysics);
            hash = NbHasher.CombineHash(hash, (uint) VertrStartGraphics);
            hash = NbHasher.CombineHash(hash, (uint) VertrEndGraphics);
            hash = NbHasher.CombineHash(hash, (uint) BatchStartPhysics);
            hash = NbHasher.CombineHash(hash, (uint) BatchCount);
            hash = NbHasher.CombineHash(hash, (uint) FirstSkinMat);
            hash = NbHasher.CombineHash(hash, (uint) LastSkinMat);
            hash = NbHasher.CombineHash(hash, (uint) LODLevel);
            hash = NbHasher.CombineHash(hash, (uint) BoundHullStart);
            hash = NbHasher.CombineHash(hash, (uint) BoundHullEnd);
            return hash;
        }
    }
}
