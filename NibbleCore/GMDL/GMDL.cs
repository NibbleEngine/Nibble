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
using NbOpenGLAPI;
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
    }

    public class geomMeshData
    {
        public ulong hash;
        public byte[] vs_buffer;
        public byte[] is_buffer;
    }

    public class GeomObject : Entity
    {
        public string Name;
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public int vx_size;
        public int small_vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public DrawElementsType indicesLengthType;
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
        public Dictionary<ulong, geomMeshData> meshDataDict = new();

        //Joint info
        public int jointCount;
        public List<JointBindingData> jointData = new();
        public float[] invBMats = new float[256 * 16];

        //Dictionary with the compiled VAOs belonging on this gobject
        private readonly Dictionary<ulong, GLVao> GLVaos = new();
        //Dictionary to index 
        private readonly Dictionary<ulong, Dictionary<string, GLInstancedMesh>> GLMeshVaos = new();

        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public GeomObject() : base(EntityType.GeometryObject)
        {

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


        //Fetch Meshvao from dictionary
        public GLInstancedMesh findGLMeshVao(string material_name, ulong hash)
        {
            if (GLMeshVaos.ContainsKey(hash))
                if (GLMeshVaos[hash].ContainsKey(material_name))
                    return GLMeshVaos[hash][material_name];
                
            return null;
        }

        //Fetch Meshvao from dictionary
        public GLVao findVao(ulong hash)
        {
            if (GLVaos.ContainsKey(hash))
                return GLVaos[hash];
            return null;
        }

        //Save GLMeshVAO to gobject
        public bool saveGLMeshVAO(ulong hash, string matname, GLInstancedMesh meshVao)
        {
            if (GLMeshVaos.ContainsKey(hash))
            {
                if (GLMeshVaos[hash].ContainsKey(matname))
                {
                    Callbacks.Log("MeshVao already in the dictionary, nothing to do...", LogVerbosityLevel.INFO);
                    return false;
                }
            }
            else
                GLMeshVaos[hash] = new Dictionary<string, GLInstancedMesh>();
                
            GLMeshVaos[hash][matname] = meshVao;

            return true;

        }

        //Save VAO to gobject
        public bool saveVAO(ulong hash, GLVao vao)
        {
            //Double check tha the VAO is not already in the dictinary
            if (GLVaos.ContainsKey(hash))
            {
                Callbacks.Log("Vao already in the dictinary, nothing to do...", LogVerbosityLevel.INFO);
                return false;
            }
                
            //Save to dictionary
            GLVaos[hash] = vao;
            return true;
        }

        //Fetch main VAO
        public GLVao generateVAO(MeshMetaData md)
        {
            //Generate VAO
            GLVao vao = new();
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];
            
            //Bind vertex buffer
            int size;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[md.Hash].vs_size,
                meshDataDict[md.Hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (md.VertrEndGraphics + 1))
            {
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
                Callbacks.showError("Mesh metadata does not match the vertex buffer size from the geometry file",
                    "Error");
            }
                
            RenderStats.vertNum += md.VertrEndGraphics + 1; //Accumulate settings

            //Assign VertexAttribPointers
            for (int i = 0; i < bufInfo.Count; i++)
            {
                bufInfo buf = bufInfo[i];
                VertexAttribPointerType buftype = VertexAttribPointerType.Float; //default
                switch (buf.type)
                {
                    case NbPrimitiveDataType.Double:
                        buftype = VertexAttribPointerType.Double;
                        break;
                    case NbPrimitiveDataType.Float:
                        buftype = VertexAttribPointerType.Float;
                        break;
                    case NbPrimitiveDataType.HalfFloat:
                        buftype = VertexAttribPointerType.HalfFloat;
                        break;
                    case NbPrimitiveDataType.Int2101010Rev:
                        buftype = VertexAttribPointerType.Int2101010Rev;
                        break;
                    default:
                        throw new NotImplementedException();
                }
                
                GL.VertexAttribPointer(buf.semantic, buf.count, buftype, buf.normalize, vx_size, buf.offset);
                GL.EnableVertexAttribArray(i);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[md.Hash].is_size, 
                meshDataDict[md.Hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != meshMetaDataDict[md.Hash].is_size)
            {
                Callbacks.showError("Mesh metadata does not match the index buffer size from the geometry file", "Error");
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
            }

            RenderStats.trisNum += (int) (md.BatchCount / 3); //Accumulate settings

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }

        public GLVao getCollisionMeshVao(MeshMetaData metaData)
        {
            //Collision Mesh isn't used anywhere else.
            //No need to check for hashes and shit

            float[] vx_buffer_float = new float[(metaData.BoundHullEnd - metaData.BoundHullStart) * 3];

            for (int i = 0; i < metaData.BoundHullEnd - metaData.BoundHullStart; i++)
            {
                NbVector3 v = bhullverts[i + metaData.BoundHullStart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GeomObject temp_geom = new();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = metaData.BatchCount;
            temp_geom.indicesLength = indicesLength; 

            //Set Strides
            temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            temp_geom.mesh_descr = "vn";
            bufInfo buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            temp_geom.bufInfo.Add(buf);
            
            buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "nPosition",
                semantic = 2,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            temp_geom.bufInfo.Add(buf);
            
            //Set Buffers
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.BatchCount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(ibuffer, metaData.BatchStartPhysics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            return temp_geom.generateVAO();
        }

        public GLVao generateVAO()
        {

            GLVao vao = new();

            //Generate VAO
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            ErrorCode err = GL.GetError();
            if (err != ErrorCode.NoError)
                Console.WriteLine(GL.GetError());
            
            //Bind vertex buffer
            int size;
            //Upload Vertex Buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, vbuffer.Length,
                vbuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vbuffer.Length)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, ibuffer.Length,
                ibuffer, BufferUsageHint.StaticDraw);

            //Assign VertexAttribPointers
            for (int i = 0; i < bufInfo.Count; i++)
            {
                bufInfo buf = bufInfo[i];
                VertexAttribPointerType buftype = VertexAttribPointerType.Float; //default
                switch (buf.type)
                {
                    case NbPrimitiveDataType.Double:
                        buftype = VertexAttribPointerType.Double;
                        break;
                    case NbPrimitiveDataType.Float:
                        buftype = VertexAttribPointerType.Float;
                        break;
                    case NbPrimitiveDataType.HalfFloat:
                        buftype = VertexAttribPointerType.HalfFloat;
                        break;
                    case NbPrimitiveDataType.Int2101010Rev:
                        buftype = VertexAttribPointerType.Int2101010Rev;
                        break;
                    default:
                        throw new NotImplementedException();
                }
                GL.VertexAttribPointer(buf.semantic, buf.count, buftype, buf.normalize, buf.stride, buf.offset);
                GL.EnableVertexAttribArray(i);
            }

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
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

                //Clear buffers
                foreach (KeyValuePair<ulong, geomMeshMetaData> pair in meshMetaDataDict)
                    meshDataDict[pair.Key] = null;

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
        public int semantic;
        public NbPrimitiveDataType type;
        public int count;
        public int stride;
        public int offset;
        public string sem_text;
        public bool normalize;

        public bufInfo(int sem, NbPrimitiveDataType typ, int c, int s, int off, string t, bool n)
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

    //TODO: REDO
    //private void loadData()
    //{
    //    for (int j = 0; j < NodeCount; j++)
    //    {
    //        TkAnimNodeData node = NodeData[j];
    //        //Init dictionary entries

    //        anim_rotations[node.Node] = new Quaternion[FrameCount];
    //        anim_positions[node.Node] = new Vector3[FrameCount];
    //        anim_scales[node.Node] = new Vector3[FrameCount];

    //        for (int i = 0; i < FrameCount; i++)
    //        {
    //            Import.NMS.Util.fetchRotQuaternion(node, this, i, ref anim_rotations[node.Node][i]); //use Ref
    //            Import.NMS.Util.fetchTransVector(node, this, i, ref anim_positions[node.Node][i]); //use Ref
    //            Import.NMS.Util.fetchScaleVector(node, this, i, ref anim_scales[node.Node][i]); //use Ref
    //        }
    //    }
    //}
    

    
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
            BindMatrix = NbMatrix4.CreateScale(BindScale) * NbMatrix4.CreateFromQuaternion(BindRotation) * NbMatrix4.CreateTranslation(BindTranslate);

            //Check Results [Except from Joint 0, the determinant of the multiplication is always 1,
            // transforms should be good]
            //Console.WriteLine((BindMatrix * invBindMatrix).Determinant);
        }

        
        

    }
}