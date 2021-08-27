﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using MVCore.Systems;

namespace MVCore
{
    public class Scene : Locator
    {
        public GeomObject gobject; //Keep GeomObject reference
        public TextureManager texMgr;

        //Keep reference of all the animation Joints of the scene and the skinmatrices
        public float[] skinMats; //Final Matrices
        public Dictionary<string, Joint> jointDict;
        public int activeLOD = 0;
        
        public Scene()
        {
            Type = TYPES.MODEL;
            texMgr = new TextureManager();
            //Init Animation Stuff
            skinMats = new float[256 * 16];
            jointDict = new Dictionary<string, Joint>();
        }


        /*public void resetPoses()
        {
            foreach (Joint j in jointDict.Values)
                j.localPoseMatrix = Matrix4.Identity;
            update();
        }

        public override void applyPoses(Dictionary<string, Matrix4> poseMatrices)
        {
            foreach (KeyValuePair<string, Matrix4> kp in poseMatrices)
            {
                string node_name = kp.Key;

                if (jointDict.ContainsKey(node_name))
                {
                    Joint j = jointDict[node_name];
                    //j.localPoseMatrix = kp.Value;

                    //Vector3 tr = kp.Value.ExtractTranslation();
                    Vector3 sc = kp.Value.ExtractScale();
                    Quaternion q = kp.Value.ExtractRotation();

                    //j.localRotation = Matrix4.CreateFromQuaternion(q);
                    //j.localPosition = tr;

                    //TODO this is probably bullshit
                    TransformationSystem.SetEntityRotation(j, q);
                    TransformationSystem.SetEntityScale(j, sc);
                    //j.localPoseMatrix = kp.Value;
                }
            }

            update();
        }
        */

        public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {

            return base.assimpExport(ref scn, ref meshImportStatus);
        }

        public Scene(Scene input) : base(input)
        {
            gobject = input.gobject;
        }

        public void copyFrom(Scene input)
        {
            base.copyFrom(input); //Copy base stuff
            gobject = input.gobject;
            texMgr = input.texMgr;
            
        }

        public override Model Clone()
        {
            Scene new_s = new Scene();
            new_s.copyFrom(this);

            new_s.meshVao = this.meshVao;
            new_s.instanceId = GLMeshBufferManager.AddInstance(ref new_s.meshVao, this);

            //Clone children
            foreach (Model child in children)
            {
                Model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.children.Add(new_child);
            }

            //Recursively update parentScene to all the new objects
            new_s.setParentScene(new_s);

            //Initialize jointDictionary
            new_s.jointDict.Clear();
            new_s.setupJointDict(new_s);

            return new_s;
        }

        public void setupJointDict(Model m)
        {
            if (m.Type == TYPES.JOINT)
                jointDict[m.Name] = (Joint)m;

            foreach (Model c in m.children)
                setupJointDict(c);
        }

        public override void updateLODDistances()
        {
            //TODO: Cache the distance elsewhere

            //Set Current LOD Level
            double distance = (TransformationSystem.GetEntityWorldPosition(this).Xyz - Common.RenderState.activeCam.Position).Length;

            //Find active LOD
            activeLOD = _LODNum - 1;
            for (int j = 0; j < _LODNum - 1; j++)
            {
                if (distance < _LODDistances[j])
                {
                    activeLOD = j;
                    break;
                }
            }

            base.updateLODDistances();
        }


        public override void updateMeshInfo(bool lod_filter = false)
        {
            //Update Skin Matrices
            foreach (Joint j in jointDict.Values)
            {
                Matrix4 jointSkinMat = j.invBMat * TransformationSystem.GetEntityWorldMat(j);
                MathUtils.insertMatToArray16(skinMats, j.jointIndex * 16, jointSkinMat);
            }

            base.updateMeshInfo(lod_filter);
        }


        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                skinMats = null;
                jointDict.Clear();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }

    }

}
