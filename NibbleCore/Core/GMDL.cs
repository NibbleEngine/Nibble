﻿//#define DUMP_TEXTURES

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;
using System.Linq;
using System.ComponentModel;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Runtime.InteropServices;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using NbCore.Common;
using NbCore.Utils;

namespace NbCore
{
    public enum NbPrimitiveDataType
    {
        UnsignedByte,
        UnsignedShort,
        UnsignedInt,
        HalfFloat,
        Float,
        Double,
        Int2101010Rev,
        Int,
    }
    
    public enum COLLISIONTYPES
    {
        MESH = 0x0,
        SPHERE,
        CYLINDER,
        BOX,
        CAPSULE    
    }

    public enum RENDERPASS
    {
        DEFERRED = 0x0,
        FORWARD,
        DECAL,
        BHULL,
        BBOX,
        DEBUG,
        PICK,
        COUNT
    }

    public class geomMeshMetaData
    {
        public string name;
        public ulong hash;
        public uint vs_size;
        public uint vs_abs_offset;
        public uint is_size;
        public uint is_abs_offset;
        public bool double_buffering;
    }

    public class GeomObject : Entity
    {
        public string Name;
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public uint vx_size;
        public uint small_vx_size;

        //Counters
        public int indicesCount=0;
        public NbPrimitiveDataType indicesType;        
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
        public int[] ibuffer_int;
        public byte[] vbuffer;
        public byte[] small_vbuffer;
        public byte[] cbuffer;
        public byte[] nbuffer;
        public byte[] ubuffer;
        public byte[] tbuffer;
        public List<int[]> bIndices = new();
        public List<float[]> bWeights = new();
        public List<bufInfo> bufInfo = new();
        public List<bufInfo> smallBufInfo = new();
        public short[] boneRemap;
        public List<NbVector3[]> bboxes = new();
        public List<NbVector3> bhullverts = new();
        public List<int> bhullstarts = new();
        public List<int> bhullends = new();
        public List<int[]> bhullindices = new();
        public List<int> vstarts = new();
        public Dictionary<ulong, geomMeshMetaData> meshMetaDataDict = new();
        public Dictionary<ulong, NbMeshData> meshDataDict = new();

        //Joint info
        public int jointCount;
        public List<JointBindingData> jointData = new();
        public float[] invBMats = new float[256 * 16];

        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public GeomObject() : base(EntityType.GeometryObject)
        {

        }

        public NbMeshData GetCollisionMeshData(NbMeshMetaData mmd)
        {
            //Collision Mesh isn't used anywhere else.
            //No need to check for hashes and shit

            float[] vx_buffer_float = new float[(mmd.BoundHullEnd - mmd.BoundHullStart) * 3];

            for (int i = 0; i < mmd.BoundHullEnd - mmd.BoundHullStart; i++)
            {
                NbVector3 v = bhullverts[i + mmd.BoundHullStart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GeomObject temp_geom = new();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = mmd.BatchCount;
            temp_geom.indicesType = indicesType;

            //Set Strides
            temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            temp_geom.mesh_descr = "v";
            bufInfo buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = temp_geom.vx_size,
                type = NbPrimitiveDataType.Float
            };
            temp_geom.bufInfo.Add(buf);

            int indicesLength = 0x4;
            if (indicesType == NbPrimitiveDataType.UnsignedShort)
                indicesLength = 0x2;
            //Set Buffers
            temp_geom.ibuffer = new byte[indicesLength * mmd.BatchCount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];
            
            System.Buffer.BlockCopy(ibuffer, mmd.BatchStartGraphics * indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            NbMeshData temp_geom_data = temp_geom.GetMeshData();
            temp_geom.Dispose();
            return temp_geom_data;
        }

        public NbMeshMetaData GetMetaData()
        {
            
            int indicesLength = 0x4;
            if (indicesType == NbPrimitiveDataType.UnsignedShort)
                indicesLength = 0x2;

            //Warning: For now this method assumes 
            return new NbMeshMetaData()
            {
                BoneRemapIndices = new int[1],
                BatchCount = ibuffer.Length / indicesLength,
                FirstSkinMat = 0,
                LastSkinMat = 0,
                VertrEndGraphics = vbuffer.Length / ((int) vx_size) - 1,
                VertrEndPhysics = vbuffer.Length / ((int) vx_size)
            };
        }

        public NbMeshData GetMeshData(ulong hash)
        {
            if (meshDataDict.ContainsKey(hash))
                return meshDataDict[hash];
            return NbMeshData.Create();
        }

        public NbMeshData GetMeshData()
        {
            NbMeshData data = new();
            data.Hash = (ulong)(ibuffer.GetHashCode() ^ vbuffer.GetHashCode());
            data.IndexBuffer = new byte[ibuffer.Length];
            data.VertexBuffer = new byte[vbuffer.Length];
            data.VertexBufferStride = vx_size;
            data.buffers = bufInfo.ToArray();
            data.IndicesLength = indicesType;

            //Calculate vertex count on stream
            uint vx_count = (uint) vbuffer.Length / vx_size;

            data.IndicesLength = NbPrimitiveDataType.UnsignedShort;
            if (vx_count > 0xFFFF)
                data.IndicesLength = NbPrimitiveDataType.UnsignedInt;

            //Copy buffer data
            System.Buffer.BlockCopy(vbuffer, 0, data.VertexBuffer, 0, vbuffer.Length);
            System.Buffer.BlockCopy(ibuffer, 0, data.IndexBuffer, 0, ibuffer.Length);

            return data;
        }

        public static NbVector3 get_vec3_half(BinaryReader br)
        {
            NbVector3 temp = new();
            //Get Values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            uint val3 = br.ReadUInt16();
            //Convert Values
            temp.X = Math.Half.decompress(val1);
            temp.Y = Math.Half.decompress(val2);
            temp.Z = Math.Half.decompress(val3);
            //Console.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
            return temp;
        }

        public static NbVector2 get_vec2_half(BinaryReader br)
        {
            NbVector2 temp = new();
            //Get values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            //Convert Values
            temp.X = Math.Half.decompress(val1);
            temp.Y = Math.Half.decompress(val2);
            return temp;
        }

        public override GeomObject Clone()
        {
            throw new NotImplementedException();
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                ibuffer = null;
                vbuffer = null;
                small_vbuffer = null;
                boneRemap = null;
                invBMats = null;
                    
                bIndices.Clear();
                bWeights.Clear();
                bufInfo.Clear();
                bboxes.Clear();
                bhullverts.Clear();
                vstarts.Clear();
                jointData.Clear();

                meshDataDict.Clear();
                meshMetaDataDict.Clear();

                handle.Dispose();
            }

            disposed = true;

            base.Dispose(disposing);
        }

        #endregion


    }

    public struct bufInfo
    {
        [NbSerializable] public int semantic;
        [NbSerializable] public NbPrimitiveDataType type;
        [NbSerializable] public int count;
        [NbSerializable] public uint stride;
        [NbSerializable] public int offset;
        [NbSerializable] public string sem_text;
        [NbSerializable] public bool normalize;

        public bufInfo(int sem, NbPrimitiveDataType typ, int c, uint s, int off, string t, bool n)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
            normalize = n;
            offset = off;
        }
    }

    public class PaletteOpt
    {
        public string PaletteName;
        public string ColorName;

        //Default Empty Constructor
        public PaletteOpt() { }
        //Empty Palette Constructor
        public PaletteOpt(bool flag)
        {
            if (!flag)
            {
                PaletteName = "Fur";
                ColorName = "None";
            }
        }
    }

    
    //Animation Classes
    public class AnimNodeFrameData
    {
        public List<NbQuaternion> rotations = new();
        public List<NbVector3> translations = new();
        public List<NbVector3> scales = new();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbQuaternion q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                    W = br.ReadSingle()
                };
                
                rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbVector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbVector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }
    

    public class AnimPoseData
    {
        public AnimationData animData;
        public int FrameStart;
        public int FrameEnd;

        public AnimPoseData()
        {
            
        }

        public AnimPoseData(AnimPoseData apd)
        {
            animData = apd.animData;
            FrameStart = apd.FrameStart;
            FrameEnd = apd.FrameEnd;
        }

    }

    public class JointBindingData
    {
        public NbMatrix4 invBindMatrix = NbMatrix4.Identity();
        public NbMatrix4 BindMatrix = NbMatrix4.Identity();

        public void Load(Stream fs)
        {
            //Binary Reader
            BinaryReader br = new(fs);
            //Lamest way to read a matrix
            invBindMatrix.M11 = br.ReadSingle();
            invBindMatrix.M12 = br.ReadSingle();
            invBindMatrix.M13 = br.ReadSingle();
            invBindMatrix.M14 = br.ReadSingle();
            invBindMatrix.M21 = br.ReadSingle();
            invBindMatrix.M22 = br.ReadSingle();
            invBindMatrix.M23 = br.ReadSingle();
            invBindMatrix.M24 = br.ReadSingle();
            invBindMatrix.M31 = br.ReadSingle();
            invBindMatrix.M32 = br.ReadSingle();
            invBindMatrix.M33 = br.ReadSingle();
            invBindMatrix.M34 = br.ReadSingle();
            invBindMatrix.M41 = br.ReadSingle();
            invBindMatrix.M42 = br.ReadSingle();
            invBindMatrix.M43 = br.ReadSingle();
            invBindMatrix.M44 = br.ReadSingle();

            //Calculate Binding Matrix
            NbVector3 BindTranslate = new();
            NbVector3 BindScale = new();
            NbQuaternion BindRotation = new();

            //Get Translate
            BindTranslate.X = br.ReadSingle();
            BindTranslate.Y = br.ReadSingle();
            BindTranslate.Z = br.ReadSingle();
            //Get Quaternion
            BindRotation.X = br.ReadSingle();
            BindRotation.Y = br.ReadSingle();
            BindRotation.Z = br.ReadSingle();
            BindRotation.W = br.ReadSingle();
            //Get Scale
            BindScale.X = br.ReadSingle();
            BindScale.Y = br.ReadSingle();
            BindScale.Z = br.ReadSingle();

            //Generate Matrix
            BindMatrix = NbMatrix4.CreateScale(BindScale) * 
                         NbMatrix4.CreateFromQuaternion(BindRotation) * 
                         NbMatrix4.CreateTranslation(BindTranslate);

            //Check Results [Except from Joint 0, the determinant of the multiplication is always 1,
            // transforms should be good]
            //Console.WriteLine((BindMatrix * invBindMatrix).Determinant);
        }

        
        

    }
}