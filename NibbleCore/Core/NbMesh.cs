using System.Collections.Generic;
using System;
using NbCore;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace NbCore
{
    public enum NbMeshType
    {
        Mesh,
        Locator,
        Light,
        LightVolume,
        Joint,
        Collision,
        Generic
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MeshInstance
    {
        //4 x Vec4 Uniforms
        [FieldOffset(0)]
        public NbMatrix4 uniforms;
        [FieldOffset(64)]
        public fixed int boneIndices[64];
        //Matrices
        [FieldOffset(320)]
        public NbMatrix4 worldMat;
        [FieldOffset(384)]
        public NbMatrix4 normalMat;
        [FieldOffset(448)]
        public NbMatrix4 worldMatInv;
        [FieldOffset(512)]
        public NbVector3 color;
        [FieldOffset(524)]
        public float isSelected;
    };

    [NbSerializable]
    public class NbMesh : Entity
    {
        public ulong Hash;
        public new NbMeshType Type;
        public NbMeshMetaData MetaData; //Each mesh has its own object instance
        public NbMeshData Data; //Reference that might be shared with other NbMeshes
        public MeshInstance[] InstanceDataBuffer = new MeshInstance[2];
        public Dictionary<int, MeshComponent> ComponentDict = new();
        public int AtlasBufferOffset = -1;
        public int InstanceCount = 0;
        public NbMeshGroup Group = null;
        public NbMaterial Material;
        public bool IsGeneric = false;
        
        //This is needed only for removing render instances,
        //so that InstanceIDs for relocated meshes in the buffer are updated
        //I think I should find a way to get rid of this at some point. Till then I'll keep it
        //public MeshComponent[] instanceRefs = new MeshComponent[10]; 
        
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


        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);

            writer.WritePropertyName("Hash");
            writer.WriteValue(Hash.ToString());
            writer.WritePropertyName("MeshType");
            writer.WriteValue(Type);
            writer.WritePropertyName("MetaData");
            IO.NbSerializer.WriteField(typeof(NbMesh).GetField("MetaData"), this, writer);
            writer.WritePropertyName("MeshDataHash");
            writer.WriteValue(Data.Hash.ToString());
            writer.WritePropertyName("Material");
            writer.WriteValue(Material is null ? "" : Material.Name);
            writer.WriteEndObject();
        }

        public static NbMesh Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbMesh mesh = new();
            mesh.Hash = ulong.Parse(token.Value<string>("Hash"));
            mesh.Type = (NbMeshType) token.Value<int>("MeshType");
            mesh.MetaData = (NbMeshMetaData)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("MetaData"));

            //Material and MeshData references should be loaded from the engine
            mesh.Material = Common.RenderState.engineRef.GetMaterialByName(token.Value<string>("Material"));
            string mesh_data_hash = token.Value<string>("MeshDataHash");
            mesh.Data = Common.RenderState.engineRef.GetSystem<Systems.RenderingSystem>().MeshDataMgr.Get(ulong.Parse(mesh_data_hash));
            
            return mesh;
        }
    }
}