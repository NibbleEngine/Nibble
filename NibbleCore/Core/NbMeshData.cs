using System;
using System.IO.Compression;
using System.IO;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public struct NbMeshData: IDisposable
    {
        public ulong Hash;
        public uint VertexBufferStride;
        public byte[] VertexBuffer;
        public byte[] IndexBuffer;
        public bufInfo[] buffers;
        public NbPrimitiveDataType IndicesLength;

        public void Dispose()
        {
            VertexBuffer = null;
            IndexBuffer = null;
        }

        public static NbMeshData Create()
        {
            NbMeshData md = new()
            {
                Hash = 0,
                VertexBuffer = null,
                IndexBuffer = null,
                IndicesLength = NbPrimitiveDataType.UnsignedInt
            };
            return md;
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Hash");
            writer.WriteValue(Hash);
            writer.WritePropertyName("VertexBufferStride");
            writer.WriteValue(VertexBufferStride);
            writer.WritePropertyName("IndicesLength");
            writer.WriteValue(IndicesLength);

            writer.WritePropertyName("buffers");
            writer.WriteStartArray();
            for (int i = 0; i < buffers.Length; i++)
                IO.NbSerializer.Serialize(buffers[i], writer);
            writer.WriteEndArray();

            //Serialize Vertex Buffer
            MemoryStream ms = new MemoryStream(VertexBuffer);
            ms.Seek(0, SeekOrigin.Begin);
            MemoryStream vx_out = new MemoryStream();

            using (var compressor = new DeflateStream(vx_out, CompressionLevel.Optimal))
            {
                ms.CopyTo(compressor);
            }

            byte[] compressed_vx = vx_out.ToArray();

            writer.WritePropertyName("VertexBuffer");
            writer.WriteValue(Convert.ToBase64String(compressed_vx));


            //Serialize Index Buffer
            ms = new MemoryStream(IndexBuffer);
            ms.Seek(0, SeekOrigin.Begin);
            vx_out = new MemoryStream();

            using (var compressor = new DeflateStream(vx_out, CompressionLevel.Optimal))
            {
                ms.CopyTo(compressor);
            }

            compressed_vx = vx_out.ToArray();

            writer.WritePropertyName("IndexBuffer");
            writer.WriteValue(Convert.ToBase64String(compressed_vx));

            writer.WriteEndObject();

        }
        
    }
}