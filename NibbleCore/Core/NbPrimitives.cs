using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using NbCore;
using OpenTK.Graphics.OpenGL4;
using NbCore.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NbCore
{
    public class NbPrimitive : IDisposable
    {
        internal float[] verts;
        internal float[] normals;
        internal float[] uvs;
        internal float[] colors;
        internal int[] indices;
        
        public GeomObject geom;
        private bool disposedValue;

        public void applyTransform(NbMatrix4 transform)
        {
            for (int i = 0; i < verts.Length / 3; i++)
            {
                //Load 
                float x = verts[3 * i + 0];
                float y = verts[3 * i + 1];
                float z = verts[3 * i + 2];

                NbVector4 vec = new NbVector4(x, y, z, 1.0f) * transform;

                //Save
                verts[3 * i + 0] = vec.X;
                verts[3 * i + 1] = vec.Y;
                verts[3 * i + 2] = vec.Z;
            }

        }

        public virtual GeomObject getGeom(string name="")
        {
            GeomObject geom = new();
            geom.Name = name;

            //Find AABBs
            NbVector3 AABBMIN = new NbVector3(100000000);
            NbVector3 AABBMAX = new NbVector3(-100000000);
            for (int i = 0; i < verts.Length / 3; i++)
            {
                AABBMIN.X = System.Math.Min(verts[i + 0], AABBMIN.X);
                AABBMIN.Y = System.Math.Min(verts[i + 1], AABBMIN.Y);
                AABBMIN.Z = System.Math.Min(verts[i + 2], AABBMIN.Z);

                AABBMAX.X = System.Math.Max(verts[i + 0], AABBMAX.X);
                AABBMAX.Y = System.Math.Max(verts[i + 1], AABBMAX.Y);
                AABBMAX.Z = System.Math.Max(verts[i + 2], AABBMAX.Z);
            }
            geom.bboxes.Add(new NbVector3[] { AABBMIN, AABBMAX });

            //Set main Geometry Info
            geom.vertCount = verts.Length / 3;
            geom.indicesCount = indices.Length;

            geom.indicesType = NbPrimitiveDataType.UnsignedShort;
            int indicesLength = 0x2;
            if (geom.vertCount > 0xFFFF)
            {
                geom.indicesType = NbPrimitiveDataType.Int;
                indicesLength = 0x4;
            }

            //Calculate vx size
            geom.vx_size = 0;
            if (verts.Length > 0)
                geom.vx_size += 3 * 4; //3 Floats per vertex
            if (uvs is not null)
                geom.vx_size += 2 * 4; //2 Floats per vertex
            if (normals is not null)
                geom.vx_size += 3 * 4; //3 Floats per vertex
            if (colors is not null)
                geom.vx_size += 3 * 4; //3 Floats per vertex

            //Init Buffer
            float[] float_vbuffer = new float[geom.vx_size * geom.vertCount / 4];
            
            int off = 0;

            //Set Buffer Offsets
            if (verts.Length > 0) {
                geom.bufInfo.Add(new()
                {
                    count = 3,
                    normalize = false,
                    offset = off,
                    sem_text = "vPosition",
                    semantic = NbBufferSemantic.VERTEX,
                    stride = (int) geom.vx_size,
                    type = NbPrimitiveDataType.Float
                });

                //Copy position data to temp float array
                for (int i=0; i < geom.vertCount; i++)
                {
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 0] = verts[3 * i + 0];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 1] = verts[3 * i + 1];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 2] = verts[3 * i + 2];
                }
                
                off += 12;
            }

            if (uvs is not null)
            {
                geom.bufInfo.Add(new()
                {
                    count = 2,
                    normalize = false,
                    offset = off,
                    sem_text = "uvPosition",
                    semantic = NbBufferSemantic.NORMAL,
                    stride = (int) geom.vx_size,
                    type = NbPrimitiveDataType.Float
                });

                //Copy normal data to temp float array
                for (int i = 0; i < geom.vertCount; i++)
                {
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 0] = uvs[2 * i + 0];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 1] = uvs[2 * i + 1];
                }

                off += 8;
            }


            if (normals is not null)
            {
                geom.bufInfo.Add(new()
                {
                    count = 3,
                    normalize = false,
                    offset = off,
                    sem_text = "nPosition",
                    semantic = NbBufferSemantic.NORMAL,
                    stride = (int) geom.vx_size,
                    type = NbPrimitiveDataType.Float
                });

                //Copy normal data to temp float array
                for (int i = 0; i < geom.vertCount; i++)
                {
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 0] = normals[3 * i + 0];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 1] = normals[3 * i + 1];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 2] = normals[3 * i + 2];
                }

                off += 12;
            }

            if (colors is not null)
            {
                geom.bufInfo.Add(new()
                {
                    count = 3,
                    normalize = false,
                    offset = off,
                    sem_text = "bPosition",
                    semantic = NbBufferSemantic.BITANGENT,
                    stride = (int) geom.vx_size,
                    type = NbPrimitiveDataType.Float
                });

                //Copy colors data to temp float array
                for (int i = 0; i < geom.vertCount; i++)
                {
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 0] = colors[3 * i + 0];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 1] = colors[3 * i + 1];
                    float_vbuffer[off / 4 + i * (geom.vx_size / 4) + 2] = colors[3 * i + 2];
                }
                
                off += 12;
            }

            //Set Buffers
            geom.ibuffer = new byte[indicesLength * indices.Length];
            if (geom.indicesType == NbPrimitiveDataType.UnsignedShort)
            {
                short[] sindices = new short[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                    sindices[i] = (short) indices[i];

                System.Buffer.BlockCopy(sindices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            }
            else
                System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            
            geom.vbuffer = new byte[geom.vx_size * geom.vertCount];
            System.Buffer.BlockCopy(float_vbuffer, 0, geom.vbuffer, 0, geom.vbuffer.Length);
            
            return geom;
        }

        public static NbPrimitive mergePrimitives(NbPrimitive p1, NbPrimitive p2)
        {
            NbPrimitive p = new();

            //Merge vertices
            p.verts = new float[p1.verts.Length + p2.verts.Length];
            p.indices = new int[p1.indices.Length + p2.indices.Length];
            p.normals = new float[p1.normals.Length + p2.normals.Length];
            p.colors = new float[p1.colors.Length + p2.colors.Length];

            //Copy verts
            Array.Copy(p1.verts, 0, p.verts, 0, p1.verts.Length);
            Array.Copy(p2.verts, 0, p.verts, p1.verts.Length, p2.verts.Length);

            //Copy colors
            Array.Copy(p1.colors, 0, p.colors, 0, p1.colors.Length);
            Array.Copy(p2.colors, 0, p.colors, p1.colors.Length, p2.colors.Length);

            //Copy normals
            Array.Copy(p1.normals, 0, p.normals, 0, p1.normals.Length);
            Array.Copy(p2.normals, 0, p.normals, p1.normals.Length, p2.normals.Length);

            //Copy indices
            Array.Copy(p1.indices, 0, p.indices, 0, p1.indices.Length);
            Array.Copy(p2.indices, 0, p.indices, p1.indices.Length, p2.indices.Length);

            //Fix indices
            for (int i=p1.indices.Length; i<p.indices.Length; i++)
                p.indices[i] += (p1.verts.Length / 3);
            
            return p;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    geom.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class Sphere : NbPrimitive
    {
        
        //Constructor
        public Sphere(NbVector3 center, float radius, int bands=10)
        {
            int latBands = bands;
            int longBands = bands;

            //Init Arrays
            int arraysize = (latBands + 1) * (longBands + 1) * 3;
            int indarraysize = latBands * longBands * 3;
            verts = new float[arraysize];
            normals = new float[arraysize];
            indices = new int[2 * indarraysize];
            
            
            for (int lat = 0; lat <= latBands; lat++)
            {
                float theta = lat * (float)System.Math.PI / latBands;
                float sintheta = (float)System.Math.Sin(theta);
                float costheta = (float)System.Math.Cos(theta);

                for (int lng = 0; lng <= longBands; lng++)
                {
                    float phi = lng * 2 * (float) System.Math.PI / longBands;
                    float sinphi = (float) System.Math.Sin(phi);
                    float cosphi = (float) System.Math.Cos(phi);

                    float x = cosphi * sintheta;
                    float y = costheta;
                    float z = sinphi * sintheta;

                    verts[lat * (longBands + 1) * 3 + 3 * lng + 0] = center.X + radius * x;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y + radius * y;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 2] = center.Z + radius * z;
                    
                    normals[lat * (longBands + 1) * 3 + 3 * lng + 0] = x;
                    normals[lat * (longBands + 1) * 3 + 3 * lng + 1] = y;
                    normals[lat * (longBands + 1) * 3 + 3 * lng + 2] = z;
                }
            }


            //Indices
            for (int lat = 0; lat < latBands; lat++)
            {
                for (int lng = 0; lng < longBands; lng++)
                {
                    int first = lat * (longBands + 1) + lng;
                    int second = first + longBands + 1;

                    indices[lat * longBands * 6 + 6 * lng + 0] = second;
                    indices[lat * longBands * 6 + 6 * lng + 1] = first;
                    indices[lat * longBands * 6 + 6 * lng + 2] = first + 1;
                    

                    indices[lat * longBands * 6 + 6 * lng + 3] = second + 1;
                    indices[lat * longBands * 6 + 6 * lng + 4] = second;
                    indices[lat * longBands * 6 + 6 * lng + 5] = first + 1;
                }
            }
            

            
            geom = getGeom();
        }

    }

    public class Capsule : NbPrimitive
    {
        //Constructor
        public Capsule(NbVector3 center, float height, float radius)
        {
            int latBands = 11;
            int longBands = 11;

            //Init Arrays
            int arraysize = (latBands + 1) * (longBands + 1) * 3;
            int indarraysize = latBands * longBands * 3;
            verts = new float[arraysize];
            normals = new float[arraysize];
            indices = new int[2 * indarraysize];

            List<float> vlist = new();
            List<int> ilist = new();


            for (int lat = 0; lat <= latBands; lat++)
            {
                float theta = lat * (float)System.Math.PI / latBands;
                float sintheta = (float)System.Math.Sin(theta);
                float costheta = (float)System.Math.Cos(theta);

                for (int lng = 0; lng <= longBands; lng++)
                {
                    float phi = lng * 2 * (float)System.Math.PI / longBands;
                    float sinphi = (float)System.Math.Sin(phi);
                    float cosphi = (float)System.Math.Cos(phi);

                    float x = cosphi * sintheta;
                    float y = costheta;
                    float z = sinphi * sintheta;

                    verts[lat * (longBands + 1) * 3 + 3 * lng + 0] = center.X + radius * x;
                    if (lat <= latBands / 2)
                        verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y + (0.5f * height - radius) + radius * y;
                    else
                        verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y - (0.5f * height - radius) + radius * y;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 2] = center.Z + radius * z;

                    normals[lat * latBands * 3 + 3 * lng + 0] = x;
                    normals[lat * latBands * 3 + 3 * lng + 1] = y;
                    normals[lat * latBands * 3 + 3 * lng + 2] = z;
                }
            }


            //Indices
            for (int lat = 0; lat < latBands; lat++)
            {
                for (int lng = 0; lng < longBands; lng++)
                {
                    int first = lat * (longBands + 1) + lng;
                    int second = first + longBands + 1;

                    indices[lat * longBands * 6 + 6 * lng + 0] = second;
                    indices[lat * longBands * 6 + 6 * lng + 1] = first;
                    indices[lat * longBands * 6 + 6 * lng + 2] = first + 1;


                    indices[lat * longBands * 6 + 6 * lng + 3] = second + 1;
                    indices[lat * longBands * 6 + 6 * lng + 4] = second;
                    indices[lat * longBands * 6 + 6 * lng + 5] = first + 1;
                }
            }
            geom = getGeom();
        }

    }

    public class Cylinder : NbPrimitive
    {
        //Constructor
        public Cylinder(float radius, float height, NbVector3 color, bool generateGeom = false, int latBands = 10)
        {
            //Init Arrays
            int arraysize = latBands;
            verts = new float[2* (1 + arraysize) * 3];
            normals = new float[2* (1 + arraysize) * 3];
            indices = new int[3 * latBands + 3*latBands + latBands * 2 * 3];
            colors = new float[2 * (1 + arraysize) * 3];

            //Add Top Cap Verts
            float y = height / 2.0f;
            //Add center vertex
            verts[0] = 0.0f;
            verts[1] = y;
            verts[2] = 0.0f;
            
            for (int lat = 0; lat < latBands; lat++)
            {
                float theta = lat * (2 * (float) System.Math.PI / latBands);
                verts[3 + 3 * lat + 0] = radius * (float) System.Math.Cos(theta);
                verts[3 + 3 * lat + 1] = y;
                verts[3 + 3 * lat + 2] = radius * (float) System.Math.Sin(theta);
            }

            //Top Cap Indices
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[3 * (lat - 1) + 0] = 0;
                indices[3 * (lat - 1) + 1] = lat;
                indices[3 * (lat - 1) + 2] = lat+1;
            }
            //Close the circle
            indices[3 * (latBands - 1) + 0] = 0;
            indices[3 * (latBands - 1) + 1] = latBands;
            indices[3 * (latBands - 1) + 2] = 1;


            //Add Bottom Cap Verts
            int voff = (latBands + 1) * 3;
            //Add center vertex
            verts[voff + 0] = 0.0f;
            verts[voff + 1] = -y;
            verts[voff + 2] = 0.0f;

            
            for (int lat = 0; lat < latBands; lat++)
            {
                float theta = lat * (2 * (float)System.Math.PI / latBands);
                verts[voff + 3 + 3 * lat + 0] = radius * (float)System.Math.Cos(theta);
                verts[voff + 3 + 3 * lat + 1] = -y;
                verts[voff + 3 + 3 * lat + 2] = radius * (float)System.Math.Sin(theta);
            }

            //Bottom Cap Indices
            int ioff = latBands + 1;
            int array_ioff = 3 * latBands;
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[array_ioff + 3 * (lat - 1) + 0] = ioff + 0;
                indices[array_ioff + 3 * (lat - 1) + 1] = ioff + lat;
                indices[array_ioff + 3 * (lat - 1) + 2] = ioff + lat + 1;
            }
            //Close the circle
            indices[array_ioff + 3 * (latBands - 1) + 0] = ioff + 0;
            indices[array_ioff + 3 * (latBands - 1) + 1] = ioff + latBands;
            indices[array_ioff + 3 * (latBands - 1) + 2] = ioff + 1;


            //Fix Side Indices
            //No need to add other vertices all are there
            array_ioff = 2 * 3 * latBands;
            for (int lat = 1; lat < latBands; lat++)
            {
                //First Tri
                indices[array_ioff + 6 * (lat - 1) + 0] = lat;
                indices[array_ioff + 6 * (lat - 1) + 1] = lat + latBands + 1;
                indices[array_ioff + 6 * (lat - 1) + 2] = lat + latBands + 2;
                //Second Tri
                indices[array_ioff + 6 * (lat - 1) + 3] = lat;
                indices[array_ioff + 6 * (lat - 1) + 4] = lat + latBands + 2;
                indices[array_ioff + 6 * (lat - 1) + 5] = lat + 1;
            }
            //Last quad
            indices[array_ioff + 6 * (latBands - 1) + 0] = latBands;
            indices[array_ioff + 6 * (latBands - 1) + 1] = 2*latBands + 1;
            indices[array_ioff + 6 * (latBands - 1) + 2] = latBands + 2;
            //Second Tri
            indices[array_ioff + 6 * (latBands - 1) + 3] = 1;
            indices[array_ioff + 6 * (latBands - 1) + 4] = latBands;
            indices[array_ioff + 6 * (latBands - 1) + 5] = latBands +2;

            //Set Normals
            Array.Copy(verts, normals, verts.Length);
            
            //Set Colors
            for (int i=0; i<verts.Length/3; i++)
            {
                colors[3 * i + 0] = color.X;
                colors[3 * i + 1] = color.Y;
                colors[3 * i + 2] = color.Z;
            }

            if (generateGeom)
                geom = getGeom();
        }
    }

    public class Arrow : NbPrimitive
    {
        public Arrow(float radius, float length, NbVector3 color, bool generateGeom=false, int latBands = 10)
        {
            ArrowHead head = new(radius, 2 * radius, color, false, latBands);
            Cylinder cyl = new(radius/2.0f, length, color, false, latBands);

            //Transform Primitives before merging
            //Move arrowhead up in place
            NbMatrix4 t = NbMatrix4.CreateTranslation(0.0f, length, 0.0f);
            head.applyTransform(t);
            //Move cylinder up to fit into place
            t = NbMatrix4.CreateTranslation(0.0f, length/2.0f, 0.0f);
            cyl.applyTransform(t);

            //Merge Primitives
            NbPrimitive p = mergePrimitives(head, cyl);
            verts = p.verts;
            indices = p.indices;
            normals = p.normals;
            colors = p.colors;

            if (generateGeom)
                geom = getGeom();
        }

        public override GeomObject getGeom(string name = "")
        {
            GeomObject geom = new();

            //Set main Geometry Info
            geom.vertCount = verts.Length / 0x3;
            geom.indicesCount = indices.Length;
            geom.indicesType = NbPrimitiveDataType.Int;
            

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            geom.bufInfo = new List<NbMeshBufferInfo>();

            //geom.bufInfo[0] = new bufInfo(0, NbVertexAttribPointerType.Float, 3, 0, 0, "vPosition", false);
            //geom.bufInfo[2] = new bufInfo(2, NbVertexAttribPointerType.Float, 3, 0, 0, "nPosition", false);
            //geom.bufInfo[4] = new bufInfo(4, NbVertexAttribPointerType.Float, 3, 0, geom.vertCount * 12, "bPosition", false);

            NbMeshBufferInfo buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = NbBufferSemantic.VERTEX,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "nPosition",
                semantic = NbBufferSemantic.NORMAL,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = geom.vertCount * 12,
                sem_text = "bPosition",
                semantic = NbBufferSemantic.BITANGENT,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            
            geom.vbuffer = new byte[4 * verts.Length + 4 * colors.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, 4 * verts.Length); //Copy Vertices
            System.Buffer.BlockCopy(colors, 0, geom.vbuffer, 4 * verts.Length, 4 * colors.Length); //Copy Colors

            return geom;
        }
    }

    public class SquareCross2D : NbPrimitive
    {
        public SquareCross2D(float size, NbVector3 col, bool generateGeom = false)
        {
            Box b1 = new(size, size, size, col, false);
            Box b2 = new(size, size, size, col, false);
            Box b3 = new(size, size, size, col, false);
            Box b4 = new(size, size, size, col, false);
            Box b5 = new(size, size, size, col, false);

            NbMatrix4 t;
            t = NbMatrix4.CreateTranslation(new NbVector3(-size, 0.0f, 0.0f));
            b2.applyTransform(t); //Left
            t = NbMatrix4.CreateTranslation(new NbVector3(size, 0.0f, 0.0f));
            b3.applyTransform(t); //Right
            t = NbMatrix4.CreateTranslation(new NbVector3(0.0f, size, 0.0f));
            b4.applyTransform(t); //Up
            t = NbMatrix4.CreateTranslation(new NbVector3(0.0f, -size, 0.0f));
            b5.applyTransform(t); //Down

            //Merge

            NbPrimitive p = mergePrimitives(b2, b3);
            NbPrimitive p2 = mergePrimitives(b4, b5);
            p = mergePrimitives(p, p2);
            p = mergePrimitives(p, b1);

            verts = p.verts;
            indices = p.indices;
            colors = p.colors;

            if (generateGeom)
                geom = getGeom();

        }
    }

    public class Cross : NbPrimitive
    {
        public Cross(float scale, bool generateGeom = false)
        {
            NbPrimitive p = generatePrimitive(new NbVector3(scale));

            verts = p.verts;
            indices = p.indices;
            colors = p.colors;
            normals = p.normals;

            if (generateGeom)
                geom = getGeom();
        }
        

        public Cross(NbVector3 scale, bool generateGeom = false)
        {
            NbPrimitive p = generatePrimitive(scale);
            
            verts = p.verts;
            indices = p.indices;
            colors = p.colors;
            normals = p.normals;

            if (generateGeom)
                geom = getGeom();
        }

        
        private static NbPrimitive generatePrimitive(NbVector3 scale)
        {
            Arrow XPosAxis = new(0.02f, scale.X, new NbVector3(10.5f, 0.0f, 0.0f), false, 5);
            Arrow XNegAxis = new(0.01f, scale.X, new NbVector3(10.5f, 0.1f, 0.1f), false, 5);
            Arrow YPosAxis = new(0.02f, scale.Y, new NbVector3(0.0f, 10.5f, 0.0f), false, 5);
            Arrow YNegAxis = new(0.01f, scale.Y, new NbVector3(0.1f, 10.5f, 0.1f), false, 5);
            Arrow ZPosAxis = new(0.02f, scale.Z, new NbVector3(0.0f, 0.0f, 10.5f), false, 5);
            Arrow ZNegAxis = new(0.01f, scale.Z, new NbVector3(0.1f, 0.1f, 10.5f), false, 5);
            
            //SquareCross2D c = new SquareCross2D(0.01f, new Vector3(0.0f, 10.5f, 0.0f), false);

            //Transform Primitives before merging
            //Global Scale matrix
            //Matrix4 s = Matrix4.CreateScale(scale);
            NbMatrix4 s = NbMatrix4.Identity();
            NbMatrix4 t;

            //Move arrowhead up in place
            t = s * NbMatrix4.CreateRotationZ(Math.Radians(90));
            XNegAxis.applyTransform(t);
            t = s * NbMatrix4.CreateRotationZ(Math.Radians(-90));
            XPosAxis.applyTransform(t);

            t = s * NbMatrix4.CreateRotationX(Math.Radians(90));
            ZPosAxis.applyTransform(t);
            t = s * NbMatrix4.CreateRotationX(Math.Radians(-90));
            ZNegAxis.applyTransform(t);

            t = s * NbMatrix4.CreateRotationX(Math.Radians(180));
            YNegAxis.applyTransform(t);
            YPosAxis.applyTransform(s);

            //Merge Primitives
            NbPrimitive py = mergePrimitives(YPosAxis, YNegAxis);
            NbPrimitive px = mergePrimitives(XNegAxis, XPosAxis);
            NbPrimitive pz = mergePrimitives(ZNegAxis, ZPosAxis);

            NbPrimitive p = mergePrimitives(py, px);
            p = mergePrimitives(p, pz);
            

            return p;
        }

        public GeomObject getGeom()
        {
            GeomObject geom = new();

            //Find AABBs
            NbVector3 AABBMIN = new NbVector3(100000000);
            NbVector3 AABBMAX = new NbVector3(-100000000);
            for (int i = 0; i < verts.Length / 3; i++)
            {
                AABBMIN.X = System.Math.Min(verts[i + 0], AABBMIN.X);
                AABBMIN.Y = System.Math.Min(verts[i + 1], AABBMIN.Y);
                AABBMIN.Z = System.Math.Min(verts[i + 2], AABBMIN.Z);

                AABBMAX.X = System.Math.Max(verts[i + 0], AABBMAX.X);
                AABBMAX.Y = System.Math.Max(verts[i + 1], AABBMAX.Y);
                AABBMAX.Z = System.Math.Max(verts[i + 2], AABBMAX.Z);
            }
            geom.bboxes.Add(new NbVector3[] { AABBMIN, AABBMAX });

            //Set main Geometry Info
            geom.vertCount = verts.Length / 0x3;
            geom.indicesCount = indices.Length;
            
            geom.indicesType = NbPrimitiveDataType.UnsignedShort;
            int indicesLength = 0x2;
            if (geom.vertCount > 0xFFFF)
            {
                geom.indicesType = NbPrimitiveDataType.Int;
                indicesLength = 0x4;
            }

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            
            NbMeshBufferInfo buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = NbBufferSemantic.VERTEX,
                stride = 12,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = geom.vertCount * 12,
                sem_text = "nPosition",
                semantic = NbBufferSemantic.NORMAL,
                stride = 12,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = 2 * geom.vertCount * 12,
                sem_text = "bPosition",
                semantic = NbBufferSemantic.BITANGENT,
                stride = 12,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);

            //geom.bufInfo[0] = new bufInfo(0, VertexAttribPointerType.Float, 3, 12, 0, "vPosition", false);
            //geom.bufInfo[2] = new bufInfo(2, VertexAttribPointerType.Float, 3, 12, geom.vertCount * 12, "nPosition", false);
            //geom.bufInfo[4] = new bufInfo(4, VertexAttribPointerType.Float, 3, 12, 2 * geom.vertCount * 12, "bPosition", false);

            //Set Buffers
            geom.ibuffer = new byte[indicesLength * indices.Length];
            if (geom.indicesType == NbPrimitiveDataType.UnsignedShort)
            {
                short[] sindices = new short[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                    sindices[i] = (short)indices[i];

                System.Buffer.BlockCopy(sindices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            }
            else
                System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);

            geom.vbuffer = new byte[4 * (verts.Length + colors.Length + normals.Length)];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, 4 * verts.Length); //Copy Vertices
            System.Buffer.BlockCopy(normals, 0, geom.vbuffer, 4 * verts.Length, 4 * normals.Length); //Copy Normals
            System.Buffer.BlockCopy(colors, 0, geom.vbuffer, 4 * (verts.Length + normals.Length), 4 * colors.Length); //Copy Colors
            

            return geom;
        }
    }

    public class ArrowHead : NbPrimitive
    {
        //Constructor
        public ArrowHead(float radius, float height, NbVector3 col, bool generateGeom=false, int latBands=10)
        {
            //Init Arrays
            verts = new float[3 * (2 + latBands)]; 
            colors = new float[3 * (2 + latBands)]; 
            normals = new float[3 * (2 + latBands)];
            indices = new int[2 * 3 * latBands];

            //First create the arrow edge
            
            //Create circle
            
            //Add center vertex
            verts[0] = 0.0f;
            verts[1] = 0.0f;
            verts[2] = 0.0f;
            
            for (int lat = 0; lat < latBands; lat++)
            {
                float theta = lat * (2 * (float) System.Math.PI / latBands);
                verts[3 * (lat + 1) + 0] = radius * (float) System.Math.Cos(theta);
                verts[3 * (lat + 1) + 1] = 0.0f;
                verts[3 * (lat + 1) + 2] = radius * (float) System.Math.Sin(theta);
            }

            //Top Cap Indices
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[3 * (lat - 1) + 0] = 0;
                indices[3 * (lat - 1) + 1] = lat;
                indices[3 * (lat - 1) + 2] = lat + 1;
            }
            
            //Close the circle
            indices[3 * (latBands - 1) + 0] = 0;
            indices[3 * (latBands - 1) + 1] = latBands;
            indices[3 * (latBands - 1) + 2] = 1;

            //Add Top vertex 
            verts[3 * (latBands + 1) + 0] = 0.0f;
            verts[3 * (latBands + 1) + 1] = height;
            verts[3 * (latBands + 1) + 2] = 0.0f;

            //Connect all vertices to the top vertex
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[3 * latBands + 3 * (lat - 1) + 0] = latBands + 1;
                indices[3 * latBands + 3 * (lat - 1) + 1] = lat;
                indices[3 * latBands + 3 * (lat - 1) + 2] = lat + 1;
            }

            //Close the circle
            indices[3 * latBands + 3 * (latBands - 1) + 0] = latBands + 1;
            indices[3 * latBands + 3 * (latBands - 1) + 1] = latBands;
            indices[3 * latBands + 3 * (latBands - 1) + 2] = 1;

            //Add colors
            for (int i=0; i< (2 + latBands); i++)
            {
                colors[3 * i + 0] = col.X;
                colors[3 * i + 1] = col.Y;
                colors[3 * i + 2] = col.Z;
            }

            //Set normals
            Array.Copy(verts, normals, verts.Length);

            if (generateGeom)
                geom = getGeom();
        }

        public override GeomObject getGeom(string name = "")
        {
            GeomObject geom = new();

            //Set main Geometry Info
            geom.vertCount = verts.Length / 0x3;
            geom.indicesCount = indices.Length;
            geom.indicesType = NbPrimitiveDataType.Int;

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            
            NbMeshBufferInfo buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "nPosition",
                semantic = NbBufferSemantic.NORMAL,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            buf = new()
            {
                count = 3,
                normalize = false,
                offset = geom.vertCount * 12,
                sem_text = "bPosition",
                semantic = NbBufferSemantic.BITANGENT,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            //geom.bufInfo[0] = new bufInfo(0, VertexAttribPointerType.Float, 3, 0, 0, "vPosition", false);
            //geom.bufInfo[2] = new bufInfo(2, VertexAttribPointerType.Float, 3, 0, 0, "nPosition", false);
            //geom.bufInfo[4] = new bufInfo(4, VertexAttribPointerType.Float, 3, 0, geom.vertCount * 12, "bPosition", false);

            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            return geom;
        }

    }

    public class Box : NbPrimitive
    {
        //Constructor
        public Box(float width, float height, float depth, NbVector3 col, bool generateGeom = false)
        {
            //Init Arrays
            verts = new float[8 * 3];
            uvs = null;
            colors = new float[8 * 3];
            normals = new float[8 * 3];
            indices = new int[12 * 3];

            NbVector3 vec = new NbVector3();
            //Verts
            //0
            verts[0] = width / 2.0f;
            verts[1] = height / 2.0f;
            verts[2] = depth / 2.0f;
            vec = new NbVector3(1.0f, 1.0f, 1.0f);
            vec.Normalize();
            normals[0] = vec.X;
            normals[1] = vec.Y;
            normals[2] = vec.Z;
            //1
            verts[3] = -width / 2.0f;
            verts[4] = height / 2.0f;
            verts[5] = depth / 2.0f;
            vec = new NbVector3(-1.0f, 1.0f, 1.0f);
            vec.Normalize();
            normals[3] = vec.X;
            normals[4] = vec.Y;
            normals[5] = vec.Z;
            //2
            verts[6] = -width / 2.0f;
            verts[7] = height / 2.0f;
            verts[8] = -depth / 2.0f;
            vec = new NbVector3(-1.0f, 1.0f, -1.0f);
            vec.Normalize();
            normals[6] = vec.X;
            normals[7] = vec.Y;
            normals[8] = vec.Z;
            //3
            verts[9] = width / 2.0f;
            verts[10] = height / 2.0f;
            verts[11] = -depth / 2.0f;
            vec = new NbVector3(1.0f, 1.0f, -1.0f);
            vec.Normalize();
            normals[9] = vec.X;
            normals[10] = vec.Y;
            normals[11] = vec.Z;
            //4
            verts[12] = width / 2.0f;
            verts[13] = -height / 2.0f;
            verts[14] = depth / 2.0f;
            vec = new NbVector3(1.0f, -1.0f, 1.0f);
            vec.Normalize();
            normals[12] = vec.X;
            normals[13] = vec.Y;
            normals[14] = vec.Z;
            //5
            verts[15] = -width / 2.0f;
            verts[16] = -height / 2.0f;
            verts[17] = depth / 2.0f;
            vec = new NbVector3(-1.0f, -1.0f, 1.0f);
            vec.Normalize();
            normals[15] = vec.X;
            normals[16] = vec.Y;
            normals[17] = vec.Z;
            //6
            verts[18] = -width / 2.0f;
            verts[19] = -height / 2.0f;
            verts[20] = -depth / 2.0f;
            vec = new NbVector3(-1.0f, -1.0f, -1.0f);
            vec.Normalize();
            normals[18] = vec.X;
            normals[19] = vec.Y;
            normals[20] = vec.Z;
            //7
            verts[21] = width / 2.0f;
            verts[22] = -height / 2.0f;
            verts[23] = -depth / 2.0f;
            vec = new NbVector3(1.0f, -1.0f, -1.0f);
            vec.Normalize();
            normals[21] = vec.X;
            normals[22] = vec.Y;
            normals[23] = vec.Z;

            indices = new int[]{
                0, 3, 1,
                1, 3, 2,
                4, 5, 6,
                4, 6, 7,
                1, 2, 5,
                2, 6, 5,
                0, 4, 3,
                4, 7, 3,
                2, 3, 7,
                2, 7, 6,
                0, 1, 4,
                1, 5, 4 };

            //Set colors
            for (int i = 0; i < 8; i++)
            {
                colors[3 * i + 0] = col.X;
                colors[3 * i + 1] = col.Y;
                colors[3 * i + 2] = col.Z;
            }

            
            if (generateGeom)
                geom = getGeom();
        }

    }

    public class Quad : NbPrimitive
    {
        
        //Constructor
        public Quad(float width, float height)
        {
            //Init Arrays

            //Define Quad
            verts = new float[6 * 3] {
               -1.0f*width/2, 0.0f, -1.0f*height/2,
                1.0f*width/2, 0.0f, -1.0f*height/2,
               -1.0f*width/2, 0.0f,  1.0f*height/2,
               -1.0f*width/2, 0.0f,  1.0f*height/2,
                1.0f*width/2, 0.0f, -1.0f*height/2,
                1.0f*width/2, 0.0f,  1.0f*height/2};

            normals = new float[6 * 3] {
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f };

            uvs = new float[6 * 2] {
                0.0f, 0.0f,
                0.0f, 1.0f,
                1.0f, 0.0f,
                1.0f, 0.0f,
                0.0f, 1.0f,
                1.0f, 1.0f};

            //Indices
            indices = new Int32[2 * 3] { 0, 2, 1, 3, 5, 4 };

            geom = getGeom("Quad");
        }

        //RenderQuad Constructor
        public Quad()
        {
            //Init Arrays
            //Define Quad
            verts = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

            normals = new float[6 * 3] {
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f };

            uvs = new float[6 * 2] {
                0.0f, 0.0f,
                0.0f, 1.0f,
                1.0f, 0.0f,
                1.0f, 0.0f,
                0.0f, 1.0f,
                1.0f, 1.0f};

            //Indices
            indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            geom = getGeom("Quad");
        }

        public override GeomObject getGeom(string name)
        {
            GeomObject geom = new();
            geom.Name = name;

            //Find AABBs
            NbVector3 AABBMIN = new NbVector3(100000000);
            NbVector3 AABBMAX = new NbVector3(-100000000);
            for (int i = 0; i < verts.Length / 3; i++)
            {
                AABBMIN.X = System.Math.Min(verts[i + 0], AABBMIN.X);
                AABBMIN.Y = System.Math.Min(verts[i + 1], AABBMIN.Y);
                AABBMIN.Z = System.Math.Min(verts[i + 2], AABBMIN.Z);

                AABBMAX.X = System.Math.Max(verts[i + 0], AABBMAX.X);
                AABBMAX.Y = System.Math.Max(verts[i + 1], AABBMAX.Y);
                AABBMAX.Z = System.Math.Max(verts[i + 2], AABBMAX.Z);
            }
            geom.bboxes.Add(new NbVector3[] { AABBMIN, AABBMAX });

            //Set main Geometry Info
            geom.vertCount = verts.Length / 0x3;
            geom.indicesCount = indices.Length;

            geom.indicesType = NbPrimitiveDataType.UnsignedShort;
            int indicesLength = 0x2;
            if (geom.vertCount > 0xFFFF)
            {
                geom.indicesType = NbPrimitiveDataType.Int;
                indicesLength = 0x4;
            }

            //Set Strides
            geom.vx_size = (3 + 2 + 3) * 4; //Positions, Uvs, Normals

            //Set Buffer Offsets

            NbMeshBufferInfo buf = new()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = (int) geom.vx_size,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);

            buf = new NbMeshBufferInfo()
            {
                count = 2,
                normalize = false,
                offset = 12, //Skipping position data
                sem_text = "uvPosition0",
                semantic = NbBufferSemantic.UV,
                stride = (int)geom.vx_size,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);

            buf = new()
            {
                count = 3,
                normalize = false,
                offset = 20, //Skipping position + uv data
                sem_text = "nPosition",
                semantic = NbBufferSemantic.NORMAL,
                stride = (int)geom.vx_size, 
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);

            //Set Buffers
            geom.ibuffer = new byte[indicesLength * indices.Length];
            if (geom.indicesType == NbPrimitiveDataType.UnsignedShort)
            {
                short[] sindices = new short[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                    sindices[i] = (short)indices[i];

                System.Buffer.BlockCopy(sindices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            }
            else
                System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);

            //Create Interleaved Vertex buffer
            float[] data = new float[verts.Length + normals.Length + uvs.Length];
            for (int i=0;i<geom.vertCount; i++)
            {
                data[8 * i + 0] = verts[3 * i + 0];
                data[8 * i + 1] = verts[3 * i + 1];
                data[8 * i + 2] = verts[3 * i + 2];
                data[8 * i + 3] = uvs[2 * i + 0];
                data[8 * i + 4] = uvs[2 * i + 1];
                data[8 * i + 5] = normals[3 * i + 0];
                data[8 * i + 6] = normals[3 * i + 1];
                data[8 * i + 7] = normals[3 * i + 2];

            }

            geom.vbuffer = new byte[4 * data.Length];
            System.Buffer.BlockCopy(data, 0, geom.vbuffer, 0, 4 * data.Length); 

            return geom;
        }

    }

    public class LineCross : NbPrimitive
    {
        //Constructor
        public LineCross(float line_width, float scale)
        {
            verts = new float[12 * 3] { 
                                       //Y,X axis 
                                       -line_width,    scale,   0.0f,
                                        line_width,    scale,   0.0f,
                                        line_width,   -scale,   0.0f,
                                        -line_width,  -scale,   0.0f,
                                        //X,Z axis 
                                        0.0f,  -line_width,   scale,
                                        0.0f,  line_width,    scale,
                                        0.0f,  line_width,   -scale,
                                        0.0f,  -line_width,  -scale,
                                        //X,Z axis 
                                        scale,  -line_width,   0.0f,
                                        scale,   line_width,   0.0f,
                                       -scale,   line_width,   0.0f,
                                       -scale,  -line_width,   0.0f
            };

            //Normals
            normals = new float[12 * 3] { 
                                       //Y,X axis 
                                        0.0f,    0.0f,   1.0f,
                                        0.0f,    0.0f,   1.0f,
                                        0.0f,    0.0f,   1.0f,
                                        0.0f,    0.0f,   1.0f,
                                        //X,Z axis 
                                        0.0f,  1.0f,    0.0f,
                                        0.0f,  1.0f,    0.0f,
                                        0.0f,  1.0f,    0.0f,
                                        0.0f,  1.0f,    0.0f,
                                        //X,Z axis 
                                       1.0f,   0.0f,    0.0f,
                                       1.0f,   0.0f,    0.0f,
                                       1.0f,   0.0f,    0.0f,
                                       1.0f,   0.0f,    0.0f
            };


            //Colors
            colors = new float[12 * 3] { 1.0f, 0.0f, 0.0f,
                                         1.0f, 0.0f, 0.0f,
                                         1.0f, 0.0f, 0.0f,
                                         1.0f, 0.0f, 0.0f,
                                         0.0f, 1.0f, 0.0f,
                                         0.0f, 1.0f, 0.0f,
                                         0.0f, 1.0f, 0.0f,
                                         0.0f, 1.0f, 0.0f,
                                         0.0f, 0.0f, 1.0f,
                                         0.0f, 0.0f, 1.0f,
                                         0.0f, 0.0f, 1.0f,
                                         0.0f, 0.0f, 1.0f };

            //Indices
            indices = new Int32[12 * 3] { 0, 1, 2,
                                          0, 2, 1,
                                          2, 3, 0,
                                          3, 2, 0,
                                          4, 5, 6,
                                          5, 4, 6,
                                          6, 7, 4,
                                          7, 6, 4,
                                          8, 9, 10,
                                          9, 8, 10,
                                          10, 11, 8,
                                          11, 10, 8
            };

            geom = getGeom();
        }
    }

    public class LineSegment : NbPrimitive
    {
        //Constructor
        public LineSegment(int instance_num, NbVector3 color)
        {
            instance_num = System.Math.Max(instance_num, 1); //Should be always >=1
            verts = new float[instance_num * 2 * 3];
            Array.Clear(verts, 0, instance_num * 2 * 3);
            
            //Colors
            colors = new float[instance_num * 2 * 3];

            for (int i=0; i < instance_num; i++)
            {
                colors[6 * i + 0] = color.X;
                colors[6 * i + 1] = color.Y;
                colors[6 * i + 2] = color.Z;
                colors[6 * i + 3] = color.X;
                colors[6 * i + 4] = color.Y;
                colors[6 * i + 5] = color.Z;
            }

            //Indices
            indices = new Int32[instance_num * 2];
            for (int i = 0; i <instance_num * 2; i++)
                indices[i] = i;
            
            geom = getGeom();
        }

        public LineSegment(NbVector3 start, NbVector3 end, NbVector3 color)
        {
            verts = new float[2 * 3];
            verts[0] = start.X; verts[1] = start.Y; verts[2] = start.Z;
            verts[3] = end.X; verts[4] = end.Y; verts[5] = end.Z;

            float[] colors = new float[2 * 3];
            colors[0] = color.X; colors[1] = color.Y; colors[2] = color.Z;
            colors[3] = color.X; colors[4] = color.Y; colors[5] = color.Z;

            indices = new int[0];

            geom = getGeom();
        }

    }

}
