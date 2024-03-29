﻿using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL4;
using NbCore.Utils;


namespace NbCore
{
    public class GLInstancedMesh
    {
        //Class static properties
        public string Name;
        public NbMesh Mesh;
        public GLVao vao;
        public GLVao bHullVao;
        
        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int instanceBoneMatricesTex;
        public int instanceBoneMatricesTexTBO;

        public static Dictionary<NbPrimitiveDataType, DrawElementsType> IndicesLengthMap = new()
        {
            { NbPrimitiveDataType.UnsignedByte, DrawElementsType.UnsignedByte },
            { NbPrimitiveDataType.UnsignedInt, DrawElementsType.UnsignedInt },
            { NbPrimitiveDataType.UnsignedShort, DrawElementsType.UnsignedShort }
        };

        //GLSpecific Properties
        public DrawElementsType IndicesLength { 
            get
            {
                return IndicesLengthMap[Mesh.Data.IndicesLength];
            }
        }

        public GLInstancedMesh()
        {
            vao = new GLVao();
        }

    }
    
    //    public class Mesh : Model
    //    {
    //        public GLInstancedMesh meshVao;

    //        public int LodLevel
    //        {
    //            get
    //            {
    //                return metaData.LODLevel;
    //            }

    //        }

    //        public ulong Hash
    //        {
    //            get
    //            {
    //                return metaData.Hash;
    //            }
    //        }

    //        public MeshMetaData metaData = new MeshMetaData();
    //        public Vector3 color = new Vector3(); //Per instance
    //        public bool hasLOD = false;

    //        public GLVao bHull_Vao;
    //        public GeomObject gobject; //Ref to the geometry shit

    //        private static List<string> supportedCommonPerMeshUniforms = new List<string>() { "gUserDataVec4" };

    //        private Dictionary<string, Uniform> _CommonPerMeshUniforms = new Dictionary<string, Uniform>();

    //        public Dictionary<string, Uniform> CommonPerMeshUniforms
    //        {
    //            get
    //            {
    //                return _CommonPerMeshUniforms;
    //            }
    //        }

    //        //Constructor
    //        public Mesh() : base()
    //        {
    //            Type = TYPES.MESH;
    //            metaData = new MeshMetaData();



    //            //Init MeshModel Uniforms
    //            foreach (string un in supportedCommonPerMeshUniforms)
    //            {
    //                Uniform my_un = new Uniform(un);
    //                _CommonPerMeshUniforms[my_un.Name] = my_un;
    //            }
    //        }

    //        public Mesh(Mesh input) : base(input)
    //        {
    //            //Copy attributes
    //            this.metaData = new MeshMetaData(input.metaData);

    //            //Copy Vao Refs
    //            this.meshVao = input.meshVao;

    //            //Material Stuff
    //            this.color = input.color;

    //            this.palette = input.palette;
    //            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
    //        }

    //        public void copyFrom(Mesh input)
    //        {
    //            //Copy attributes
    //            metaData = new MeshMetaData(input.metaData);
    //            hasLOD = input.hasLOD;

    //            //Copy Vao Refs
    //            meshVao = input.meshVao;

    //            //Material Stuff
    //            color = input.color;

    //            palette = input.palette;
    //            gobject = input.gobject;

    //            base.copyFrom(input);
    //        }


    //        public override void update()
    //        {
    //            base.update();
    //            recalculateAABB(); //Update AABB
    //        }

    //        // TODO MOVE THAT TO THE CORRESPONDING SYSTEM
    ////        public override void updateMeshInfo(bool lod_filter = false)
    ////        {

    ////#if(DEBUG)
    ////            if (instanceId < 0)
    ////                Console.WriteLine("test");
    ////            if (meshVao.BoneRemapIndicesCount > 128)
    ////                Console.WriteLine("test");
    ////#endif

    ////            if (!active || !renderable || (parentScene.activeLOD != LodLevel) && RenderState.settings.renderSettings.LODFiltering)
    ////            {
    ////                base.updateMeshInfo(true);
    ////                RenderStats.occludedNum += 1;
    ////                return;
    ////            }

    ////            Matrix4 worldMat = TransformationSystem.GetEntityWorldMat(this);
    ////            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
    ////            bool occluded_status = !fr_status && Common.RenderState.settings.renderSettings.UseFrustumCulling;

    ////            //Recalculations && Data uploads
    ////            if (!occluded_status)
    ////            {

    ////                ////Apply LOD filtering
    ////                //if (hasLOD && Common.RenderOptions.LODFiltering)
    ////                ////if (false)
    ////                //{
    ////                //    //Console.WriteLine("Active LoD {0}", parentScene.activeLOD);
    ////                //    if (parentScene.activeLOD != LodLevel)
    ////                //    {
    ////                //        meshVao.setInstanceOccludedStatus(instanceId, true);
    ////                //        base.updateMeshInfo();
    ////                //        return;
    ////                //    }
    ////                //}


    ////                instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);

    ////                //Upload commonperMeshUniforms
    ////                GLMeshBufferManager.SetInstanceUniform4(meshVao, instanceId,
    ////                    "gUserDataVec4", CommonPerMeshUniforms["gUserDataVec4"].Vec.Vec);

    ////                if (Skinned)
    ////                {
    ////                    //Update the mesh remap matrices and continue with the transform updates
    ////                    meshVao.setSkinMatrices(parentScene, instanceId);
    ////                    //Fallback
    ////                    //main_Vao.setDefaultSkinMatrices();
    ////                }
    ////            }
    ////            else
    ////            {
    ////                Common.RenderStats.occludedNum += 1;
    ////            }

    ////            //meshVao.setInstanceOccludedStatus(instanceId, occluded_status);
    ////            base.updateMeshInfo();
    ////        }






    //        #region IDisposable Support

    //        protected override void Dispose(bool disposing)
    //        {
    //            if (!disposed)
    //            {
    //                if (disposing)
    //                {

    //                    // TODO: dispose managed state (managed objects).
    //                    //if (material != null) material.Dispose();
    //                    //NOTE: No need to dispose material, because the materials reside in the resource manager
    //                    base.Dispose(disposing);
    //                }
    //            }
    //        }

    //        #endregion

    //    }

}
