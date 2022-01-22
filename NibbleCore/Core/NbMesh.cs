using System.Collections.Generic;
using System;
using NbCore.Math;
using System.Runtime.InteropServices;

namespace NbCore
{
    public enum NbMeshType
    {
        Mesh,
        Locator,
        Light,
        LightVolume,
        Joint,
        Collision
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MeshInstance
    {
        //4 x Vec4 Uniforms
        [FieldOffset(0)]
        public NbMatrix4 uniforms;
        //Matrices
        [FieldOffset(64)]
        public NbMatrix4 worldMat;
        [FieldOffset(128)]
        public NbMatrix4 normalMat;
        [FieldOffset(192)]
        public NbMatrix4 worldMatInv;
        [FieldOffset(256)]
        public NbVector3 color;
        [FieldOffset(268)]
        public float isSelected;
    };

    public class NbMesh : Entity
    {
        public ulong Hash;
        public new NbMeshType Type;
        public NbMeshMetaData MetaData; //Each mesh has its own object instance
        public NbMeshData Data; //Reference that might be shared with other NbMeshes
        public MeshInstance[] InstanceDataBuffer = new MeshInstance[2];
        public int InstanceCount = 0;
        public NbMeshGroup Group = null;
        public MeshMaterial Material;
        
        //This is needed only for removing render instances, so that InstanceIDs for relocated meshes in the buffer are updated
        //I think I should find a way to get rid of this at some point. Till then I'll keep it
        public MeshComponent[] instanceRefs = new MeshComponent[10]; 
        
        public const int MAX_INSTANCES = 512;

        private bool _disposed = false;
        
        public NbMesh() : base(EntityType.Mesh)
        {
            
        }

        public override NbMesh Clone()
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Data.Dispose();
                }
                
                _disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}