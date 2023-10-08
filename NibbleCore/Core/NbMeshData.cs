using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using Newtonsoft.Json;
using NbCore;
using NbCore.Common;

namespace NbCore
{
    [NbSerializable]
    public struct NbMeshData
    {
        public ulong Hash;
        public uint VertexBufferStride;
        public byte[] VertexBuffer;
        public byte[] IndexBuffer;
        public NbMeshBufferInfo[] buffers;
        public NbPrimitiveDataType IndexFormat;
        public NbRenderPrimitive IndicesType;
            
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
                IndexFormat = NbPrimitiveDataType.UnsignedInt,
                IndicesType = NbRenderPrimitive.Triangles
            };
            return md;
        }

        public void UpdateVertex(int vertexId, NbVector3 vec)
        {
            //Calculate Offset
            int offset = 0;
            foreach (NbMeshBufferInfo info in buffers)
            {
                if (info.semantic == NbBufferSemantic.VERTEX)
                {
                    offset = info.offset + (int)VertexBufferStride * vertexId;
                    break;
                }
                    
            }
            
            //Add the new vertex in the offset
            float[] tempBuffer = new float[3] {vec.X, vec.Y, vec.Z};
            Buffer.BlockCopy(tempBuffer, 0, VertexBuffer, offset, sizeof(float) * tempBuffer.Length);
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Hash");
            writer.WriteValue(Hash.ToString());
            writer.WritePropertyName("VertexBufferStride");
            writer.WriteValue(VertexBufferStride);
            writer.WritePropertyName("IndexFormat");
            writer.WriteValue(IndexFormat);
            writer.WritePropertyName("IndicesType");
            writer.WriteValue(IndicesType);

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
        
        public static NbMeshData Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbMeshData data = new();
            data.Hash = ulong.Parse(token.Value<string>("Hash"));
            data.VertexBufferStride = token.Value<uint>("VertexBufferStride");
            data.IndexFormat = (NbPrimitiveDataType) token.Value<int>("IndexFormat");
            data.IndicesType = (NbRenderPrimitive) token.Value<int>("IndicesType");

            Newtonsoft.Json.Linq.JToken bufs = token.Value<Newtonsoft.Json.Linq.JToken>("buffers");

            //Import buffers
            List<NbMeshBufferInfo> tbufs = new();
            foreach (Newtonsoft.Json.Linq.JToken tkn in bufs.Children())
            {
                NbMeshBufferInfo info = (NbMeshBufferInfo) IO.NbDeserializer.Deserialize(tkn);
                tbufs.Add(info);
            }
            data.buffers = tbufs.ToArray();

            //Deserialize Vertex Buffer
            byte[] vx_buffer_bytes = Convert.FromBase64String(token.Value<string>("VertexBuffer"));
            MemoryStream vx_in = new MemoryStream(vx_buffer_bytes);
            vx_in.Seek(0, SeekOrigin.Begin);
            MemoryStream vx_out = new MemoryStream();
            
            using (var decompressor = new DeflateStream(vx_in, CompressionMode.Decompress))
            {
                decompressor.CopyTo(vx_out);
            }
            
            data.VertexBuffer = vx_out.ToArray();

            //Deserialize Index Buffer
            byte[] ix_buffer_bytes = Convert.FromBase64String(token.Value<string>("IndexBuffer"));
            MemoryStream ix_in = new MemoryStream(ix_buffer_bytes);
            ix_in.Seek(0, SeekOrigin.Begin);
            MemoryStream ix_out = new MemoryStream();

            using (var decompressor = new DeflateStream(ix_in, CompressionMode.Decompress))
            {
                decompressor.CopyTo(ix_out);
            }

            data.IndexBuffer = ix_out.ToArray();

            return data;
        }

    }
}