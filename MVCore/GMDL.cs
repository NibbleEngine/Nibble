﻿//#define DUMP_TEXTURES

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
//using MathNet.Numerics.LinearAlgebra;
//using MIConvexHull;
using KUtility;
using Model_Viewer;
using System.Linq;
using System.Net.Mime;
using System.Xml;
using libMBIN.NMS.Toolkit;
using System.Reflection;
using System.ComponentModel;
using MVCore;
using ExtTextureFilterAnisotropic = OpenTK.Graphics.ES30.ExtTextureFilterAnisotropic;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
//using Matrix4 = MathNet.Numerics.LinearAlgebra.Matrix<float>;


namespace MVCore.GMDL
{
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

    public class SimpleSampler
    {
        public string PName { get; set; }
        SimpleSampler()
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public abstract class model : IDisposable, INotifyPropertyChanged
    {
        public bool renderable;
        public bool occluded;
        public bool debuggable;
        public int selected;
        //public GLSLHelper.GLSLShaderConfig[] shader_programs;
        public int ID;
        public TYPES type;
        public string name;
        public ulong nameHash;
        public List<model> children = new List<model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag; //This is used to define procgen usage
        public TkSceneNodeData nms_template;
        public GLMeshVao meshVao;
        public int instanceId = -1;
        
        //Transformation Parameters
        public Vector3 worldPosition;
        public Matrix4 worldMat;
        public Matrix4 normMat;
        public Matrix4 localMat;

        public Vector3 _localPosition;
        public Vector3 _localScale;
        public Vector3 _localRotationAngles;
        public Matrix4 _localRotation;
        public Matrix4 _localPoseMatrix;

        public model parent;
        public int cIndex = 0;
        public bool updated = true; //Making it public just for the joints

        //Components
        public scene parentScene;
        public List<Component> _components = new List<Component>();
        public int animComponentID;
        public int animPoseComponentID;

        //LOD
        public float[] _LODDistances = new float[5];
        public int _LODNum = 1; //Default value of 1 LOD per model

        //Bounding Volume
        public Vector3 AABBMIN = new Vector3();
        public Vector3 AABBMAX = new Vector3();
        

        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }


        //Properties
        public Vector3 localPosition
        {
            get { return _localPosition; }
            set { _localPosition = value; updated = true; }
        }

        public Matrix4 localRotation
        {
            get { return _localRotation; }
            set { _localRotation = value; updated = true; }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set { _localScale = value; updated = true; }
        }

        public int LODNumber
        {
            get { return _LODNum; }
        }
        public List<float> LODDistances
        {
            get {
                List<float> l = new List<float>();
                for (int i = 0; i < _LODDistances.Length; i++)
                {
                    if (_LODDistances[i] > 0)
                        l.Add(_LODDistances[i]);
                }
                return l;
            }
        }

        public void updateRotationFromAngles(float x, float y, float z)
        {
            
        }

        public string Name
        {
            get { return name; }
        }
        public string Type
        {
            get { return type.ToString(); }
        }

        public virtual bool IsRenderable
        {
            get
            {
                return renderable;
            }
            set
            {
                renderable = value;
                updated = true;
                foreach (var child in Children)
                    child.IsRenderable = value;
                //meshVao?.setInstanceOccludedStatus(instanceId, !renderable);
                NotifyPropertyChanged("IsRenderable"); //Make sure to update the UI because of the subsequent changes
            }
        }

        public List<Component> Components
        {
            get {
                return _components;
            }
        }


        //Methods


        public abstract model Clone();

        public virtual void updateLODDistances()
        {
            foreach (model s in children)
                s.updateLODDistances();
        }

        public virtual void setupSkinMatrixArrays()
        {
            foreach (model s in children)
                s.setupSkinMatrixArrays();
        }


        public virtual void updateMeshInfo()
        {
            foreach (model child in children)
            {
                child.updateMeshInfo();
                

                AABBMIN.X = Math.Min(AABBMIN.X, child.AABBMIN.X);
                AABBMIN.Y = Math.Min(AABBMIN.Y, child.AABBMIN.Y);
                AABBMIN.Z = Math.Min(AABBMIN.Z, child.AABBMIN.Z);

                AABBMAX.X = Math.Max(AABBMAX.X, child.AABBMAX.X);
                AABBMAX.Y = Math.Max(AABBMAX.Y, child.AABBMAX.Y);
                AABBMAX.Z = Math.Max(AABBMAX.Z, child.AABBMAX.Z);
            }
        }

        public virtual void update()
        {

            //if (changed)
            {
                //Create scaling matrix
                Matrix4 scale = Matrix4.Identity;
                scale[0, 0] = _localScale.X;
                scale[1, 1] = _localScale.Y;
                scale[2, 2] = _localScale.Z;

                localMat = _localPoseMatrix * scale * _localRotation * Matrix4.CreateTranslation(_localPosition);
            }

            //Finally Update world Transformation Matrix
            if (parent != null)
            {
                worldMat = localMat * parent.worldMat;
            }

            else
                worldMat = localMat;

            //Update worldPosition
            if (parent != null)
            {
                //Add Translation as well
                worldPosition = (Vector4.Transform(new Vector4(0.0f, 0.0f, 0.0f, 1.0f), this.worldMat)).Xyz;
            }
            else
                worldPosition = localPosition;

            //Trigger the position update of all children nodes
            foreach (GMDL.model child in children)
            {
                child.update();
            }

            updated = true; //Transform changed, trigger mesh updates
        }

        //Properties for Data Binding
        public ObservableCollection<model> Children{
            get
            {
                return new ObservableCollection<model>(children.OrderBy(i=>i.Name));
            }
        }

        
        //TODO: Consider converting all such attributes using properties
        public void updatePosition(Vector3 newPosition)
        {
            localPosition = newPosition;
        }

        public void init(float[] trans)
        {
            //Get Local Position
            Vector3 rotation;
            _localPosition = new Vector3(trans[0], trans[1], trans[2]);
            
            //Save raw rotations
            rotation.X = MathUtils.radians(trans[3]);
            rotation.Y = MathUtils.radians(trans[4]);
            rotation.Z = MathUtils.radians(trans[5]);

            _localRotationAngles = new Vector3(trans[3], trans[4], trans[5]);
            //IF PARSED SEPARATELY USING THE AXIS ANGLES
            //OpenTK.Quaternion qx = OpenTK.Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), rotation.X);
            //OpenTK.Quaternion qy = OpenTK.Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), rotation.Y);
            //OpenTK.Quaternion qz = OpenTK.Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f), rotation.Z);

            //OpenTK.Quaternion q = qy * qz * qx; //ALWAYS YZX
            //OpenTK.Quaternion q = qx * qz * qy; //ALWAYS YZX
            //OpenTK.Quaternion q_euler = OpenTK.Quaternion.FromEulerAngles(MathUtils.radians(trans[3]),
            //                                            MathUtils.radians(trans[4]), MathUtils.radians(trans[5]));

            Matrix4 rotx = Matrix4.CreateRotationX(rotation.X);
            Matrix4 roty = Matrix4.CreateRotationY(rotation.Y);
            Matrix4 rotz = Matrix4.CreateRotationZ(rotation.Z);
            _localRotation = rotz * rotx * roty;
            
            //Get Local Scale
            _localScale = new Vector3(trans[6], trans[7], trans[8]);

            //Set paths
            if (parent!=null)
                this.cIndex = this.parent.children.Count;
        }

        //Default Constructor
        protected model()
        {
            renderable = true;
            debuggable = false;
            occluded = false;
            updated = true;
            selected = 0;
            ID = -1;
            name = "";
            procFlag = false;    //This is used to define procgen usage
        
            //Transformation Parameters
            worldPosition = new Vector3(0.0f, 0.0f, 0.0f);
            worldMat = Matrix4.Identity;
            normMat = Matrix4.Identity;
            localMat = Matrix4.Identity;

            _localPosition = new Vector3(0.0f, 0.0f, 0.0f);
            _localScale = new Vector3(1.0f, 1.0f, 1.0f);
            _localRotationAngles = new Vector3(0.0f, 0.0f, 0.0f);
            _localRotation = Matrix4.Identity;
            _localPoseMatrix = Matrix4.Identity;
            
            cIndex = 0;

            //Component Init
            _components = new List<Component>();
            animComponentID = -1;
            animPoseComponentID = -1;
    }


        public virtual void copyFrom(model input)
        {
            this.renderable = input.renderable; //Override Renderability
            this.debuggable = input.debuggable;
            this.selected = 0;
            this.type = input.type;
            this.name = input.name;
            this.ID = input.ID;
            this.updated = input.updated;
            this.cIndex = input.cIndex;
            //MESHVAO AND INSTANCE IDS SHOULD BE HANDLED EXPLICITLY
            
            //Clone transformation
            _localPosition = input._localPosition;
            _localRotationAngles = input._localRotationAngles;
            _localRotation = input._localRotation;
            _localScale = input._localScale;
            _localPoseMatrix = input._localPoseMatrix;

            this.localMat = input.localMat;
            this.worldMat = input.worldMat;
            this.normMat = input.normMat;

            //Clone LOD Info
            this._LODNum = input._LODNum;
            for (int i = 0; i < 5; i++)
                this._LODDistances[i] = input._LODDistances[i];

            //Component Stuff
            this.animComponentID = input.animComponentID;
            this.animPoseComponentID = input.animPoseComponentID;

            //Clone components
            for (int i = 0; i < input.Components.Count; i++)
            {
                this.Components.Add(input.Components[i].Clone());
            }
        }

        //Copy Constructor
        public model(model input)
        {
            this.copyFrom(input);
            foreach (GMDL.model child in input.children)
            {
                GMDL.model nChild = child.Clone();
                nChild.parent = this;
                this.children.Add(nChild);
            }
        }


        #region ComponentQueries
        public int hasComponent(Type ComponentType)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                Component temp = _components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }

        #endregion


        #region AnimationComponent

        public virtual void setParentScene(scene scene)
        {
            parentScene = scene;
            foreach (model child in children)
            {
                child.setParentScene(scene);
            }
        }

        #endregion

        #region AnimPoseComponent
        //TODO: It would be nice if I didn't have to do make the method public, but it needs a lot of work on the 
        //AnimPoseComponent class to temporarily store the selected pose frames, while also in the model.update method

        //Locator Animation Stuff
        
        public Dictionary<string, Matrix4> loadPose()
        {

            if (animPoseComponentID < 0)
                return new Dictionary<string, Matrix4>();

            AnimPoseComponent apc = _components[animPoseComponentID] as AnimPoseComponent;
            Dictionary<string, Matrix4> posematrices = new Dictionary<string, Matrix4>();

            foreach (TkAnimNodeData node in apc._poseFrameData.NodeData)
            {
                List<Quaternion> quats = new List<Quaternion>();
                List<Vector3> translations = new List<Vector3>();
                List<Vector3> scales = new List<Vector3>();

                //We should interpolate frame shit over all the selected Pose Data

                //Gather all the transformation data for all the pose factors
                for (int i = 0; i < apc._poseData.Count; i++)
                //for (int i = 0; i < 1; i++)
                {
                    //Get Pose Frame
                    int poseFrameIndex = apc._poseData[i].PActivePoseFrame;

                    Vector3 v_t, v_s;
                    Quaternion lq;
                    //Fetch Rotation Quaternion
                    lq = NMSUtils.fetchRotQuaternion(node, apc._poseFrameData, poseFrameIndex);
                    v_t = NMSUtils.fetchTransVector(node, apc._poseFrameData, poseFrameIndex);
                    v_s = NMSUtils.fetchScaleVector(node, apc._poseFrameData, poseFrameIndex);

                    quats.Add(lq);
                    translations.Add(v_t);
                    scales.Add(v_s);
                }

                float fact = 1.0f / quats.Count;
                Quaternion fq = new Quaternion();
                Vector3 f_vt = new Vector3();
                Vector3 f_vs = new Vector3();


                fq = quats[0];
                f_vt = translations[0];
                f_vs = scales[0];

                //Interpolate all data
                for (int i = 1; i < quats.Count; i++)
                {
                    Quaternion.Slerp(fq, quats[i], 0.5f);
                    Vector3.Lerp(f_vt, translations[i], 0.5f);
                    Vector3.Lerp(f_vs, scales[i], 0.5f);
                }

                //Generate Transformation Matrix
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq) * Matrix4.CreateTranslation(f_vt);
                Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq);
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs);
                posematrices[node.Node] = poseMat;
            
            }

            return posematrices;

        }
        
        public virtual void applyPoses(Dictionary<string, Matrix4> poseMatrices)
        {

        }
        

        #endregion


        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                
                //Free other resources here
                if (children!=null)
                    foreach (model c in children) c.Dispose();
                children.Clear();

                //Free textureManager
            }

            //Free unmanaged resources

            disposed = true;
        }

#if DEBUG
        ~model()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + type);
        }
#endif

       
    }

    public class reference : locator
    {
        public model ref_scene; //holds the referenced scene

        public reference()
        {
            type = TYPES.REFERENCE;
        }

        public reference(reference input)
        {
            //Copy info
            base.copyFrom(input);
            
            ref_scene = input.ref_scene.Clone();
            ref_scene.parent = this;
            children.Add(ref_scene);
        }

        public void copyFrom(reference input)
        {
            base.copyFrom(input); //Copy base stuff
            this.ref_scene = input.ref_scene;
        }

        public override model Clone()
        {
            return new reference(this);
        }


        public override void setParentScene(scene animscene)
        {
            //DO NOTHING
        }
        
        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }




    public class scene : locator
    {
        public GeomObject gobject; //Keep GeomObject reference
        public textureManager texMgr;

        //Keep reference of all the animation joints of the scene and the skinmatrices
        public float[] skinMats; //Final Matrices
        public Dictionary<string, Joint> jointDict;
        public int activeLOD = 0;
        
        public scene() {
            type = TYPES.MODEL;
            texMgr = new textureManager();
            //Init Animation Stuff
            skinMats = new float[256 * 16];
            jointDict = new Dictionary<string, Joint>();
        }

        
        public void resetPoses()
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

                    j.localRotation = Matrix4.CreateFromQuaternion(q) * j.localRotation;
                    j.localScale *= sc;

                    //j.localPoseMatrix = kp.Value;
                }
            }

            update();
        }

        //TODO Add button in the UI to toggle that shit
        private void resetAnimation()
        {
            foreach (Joint j in jointDict.Values)
            {
                j._localScale = j.BindMat.ExtractScale();
                j._localRotation = Matrix4.CreateFromQuaternion(j.BindMat.ExtractRotation());
                j._localPosition = j.BindMat.ExtractTranslation();
                j._localPoseMatrix = Matrix4.Identity;
            }
        }


        public scene(scene input) :base(input)
        {
            gobject = input.gobject;
        }      

        public void copyFrom(scene input)
        {
            base.copyFrom(input); //Copy base stuff
            gobject = input.gobject;
            texMgr = input.texMgr;
        }

        public override model Clone()
        {
            scene new_s = new scene();
            new_s.copyFrom(this);

            new_s.meshVao = this.meshVao;
            new_s.instanceId = new_s.meshVao.addInstance(this);
            
            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
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

        public void setupJointDict(model m)
        {
            if (m.type == TYPES.JOINT)
                jointDict[m.Name] = (Joint) m;

            foreach (model c in m.children)
                setupJointDict(c);
        }

        public override void updateLODDistances()
        {
            //TODO: Cache the distance elsewhere

            //Set Current LOD Level
            double distance = (worldPosition - Common.RenderState.activeCam.Position).Length;

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

        
        public override void updateMeshInfo()
        {
            //Update Skin Matrices
            foreach (Joint j in jointDict.Values)
            {
                Matrix4 jointSkinMat = j.invBMat * j.worldMat;
                MathUtils.insertMatToArray16(skinMats, j.jointIndex * 16, jointSkinMat);
            }

            base.updateMeshInfo();
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

    public class locator: model
    {
        public locator()
        {
            //Set type
            type = TYPES.LOCATOR;
            //Set BBOX
            AABBMIN = new Vector3(-1.0f, -1.0f, -1.0f);
            AABBMAX = new Vector3(1.0f, 1.0f, 1.0f);
            
            //Assemble geometry in the constructor
            meshVao = MVCore.Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_cross"];
            instanceId = meshVao.addInstance(this);
        }

        public void copyFrom(locator input)
        {
            base.copyFrom(input); //Copy stuff from base class
        }

        protected locator(locator input) : base(input)
        {
            this.copyFrom(input);
        }

        public override GMDL.model Clone()
        {
            locator new_s = new locator();
            new_s.copyFrom(this);

            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.children.Add(new_child);
            }

            return new_s;
        }

        public override void update()
        {
            base.update();
        }

        public override void updateMeshInfo()
        {
            if (Common.RenderOptions.RenderLocators && renderable)
            {
                //Uplod worldMat to the meshVao
                meshVao.setInstanceWorldMat(instanceId, worldMat);
                meshVao.setInstanceOccludedStatus(instanceId, false);
                //Console.WriteLine("Updating Light");
            }
            else
                meshVao.setInstanceOccludedStatus(instanceId, true);

            base.updateMeshInfo();
            updated = false; //All done
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    meshVao = null; //VAO will be deleted from the resource manager since it is a common mesh
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        #endregion

    }

    public class GLVao : IDisposable
    {
        //VAO ID
        public int vao_id;
        //VBO IDs
        public int vertex_buffer_object;
        public int element_buffer_object;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public GLVao()
        {
            vao_id = -1;
            vertex_buffer_object = -1;
            element_buffer_object = -1;
        }

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (vao_id > 0)
                    {
                        GL.DeleteVertexArray(vao_id);
                        GL.DeleteBuffer(vertex_buffer_object);
                        GL.DeleteBuffer(element_buffer_object);
                    }
                    
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }


    public class MeshMetaData
    {
        //Mesh Properties
        public int vertrstart_physics;
        public int vertrend_physics;
        public int vertrstart_graphics;
        public int vertrend_graphics;
        public int batchstart_physics;
        public int batchstart_graphics;
        public int batchcount;
        public int firstskinmat;
        public int lastskinmat;
        public int lodLevel;
        //New stuff Properties
        public int boundhullstart;
        public int boundhullend;
        public Vector3 AABBMIN;
        public Vector3 AABBMAX;
        public ulong Hash;

        public MeshMetaData()
        {
            //Init values to null
            vertrend_graphics = 0;
            vertrstart_graphics = 0;
            vertrend_physics = 0;
            vertrstart_physics = 0;
            batchstart_graphics = 0;
            batchstart_physics = 0;
            batchcount = 0;
            firstskinmat = 0;
            lastskinmat = 0;
            boundhullstart = 0;
            boundhullend = 0;
            Hash = 0xFFFFFFFF;
            AABBMIN = new Vector3();
            AABBMAX= new Vector3();
        }

        public MeshMetaData(MeshMetaData input)
        {
            //Init values to null
            vertrend_graphics = input.vertrend_graphics;
            vertrstart_graphics = input.vertrstart_graphics;
            vertrend_physics = input.vertrend_physics;
            vertrstart_physics = input.vertrstart_physics;
            batchstart_graphics = input.batchstart_graphics;
            batchstart_physics = input.batchstart_physics;
            batchcount = input.batchcount;
            firstskinmat = input.firstskinmat;
            lastskinmat = input.lastskinmat;
            boundhullstart = input.boundhullstart;
            boundhullend = input.boundhullend;
            Hash = input.Hash;
            lodLevel = input.lodLevel;
            AABBMIN = new Vector3(input.AABBMIN);
            AABBMAX = new Vector3(input.AABBMAX);
        }
    }


    public class GLMeshVao : IDisposable
    {
        //Class static properties
        public static int MAX_INSTANCES = 300;

        public GLVao vao;
        public GLVao bHullVao;
        public MeshMetaData metaData;

        //Mesh type
        public COLLISIONTYPES collisionType;
        public TYPES type;

        //Instance Data
        public CommonPerMeshUniformsInstanced UBO;
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Ofset 

        public int instance_count = 0;
        public int visible_instances = 0;
        public List<model> instanceRefs = new List<model>();
        public float[] instanceBoneMatrices;
        private int instanceBoneMatricesTex;
        private int instanceBoneMatricesTexTBO;

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        //public float[] BoneRemapMatrices = new float[16 * 128];
        public bool skinned = false;
        
        
        //public static int instance_worldMat_Offset = 0;
        public static int instance_worldMat_Float_Offset = 0;
        //public static int instance_normalMat_Offset = 64;
        public static int instance_normalMat_Float_Offset = 16;
        //public static int instance_worldMatInv_Offset = 128;
        public static int instance_worldMatInv_Float_Offset = 32;
        //public static int instance_isOccluded_Offset = 192;
        public static int instance_isOccluded_Float_Offset = 48;
        //public static int instance_isSelected_Offset = 196;
        public static int instance_isSelected_Float_Offset = 49;
        //public static int instance_color_Offset = 200; //TODO make that a vec4
        public static int instance_color_Float_Offset = 50;
        //public static int instance_struct_size_bytes = 204;
        public static int instance_struct_size_floats = 52; //Aligned 208 Bytes

        //Instance Data Format:
        //0-16 : instance WorldMatrix
        //16-17: isOccluded
        //17-18: isSelected
        //18-20: padding


        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;

        //Material Properties
        public Material material;
        public Vector3 color;

        

        //Constructor
        public GLMeshVao()
        {
            vao = new GLVao();
        }

        public GLMeshVao(MeshMetaData data) 
        {
            vao = new GLVao();
            metaData = new MeshMetaData(data);
        }


        //Geometry Setup
        //BSphere calculator
        public GLVao setupBSphere(int instance_id)
        {
            float radius = 0.5f * (metaData.AABBMIN - metaData.AABBMAX).Length;
            Vector4 bsh_center = new Vector4(metaData.AABBMIN + 0.5f * (metaData.AABBMAX - metaData.AABBMIN), 1.0f);

            Matrix4 t_mat;
            unsafe
            {
                fixed(float* ar = UBO.instanceData)
                {
                    t_mat = MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats);
                }
            }
            
            bsh_center = bsh_center * t_mat;

            //Create Sphere vbo
            return new Primitives.Sphere(bsh_center.Xyz, radius).getVAO();
        }


        //Rendering Methods

        public void renderBBoxes(int pass)
        {
            for (int i = 0; i > instance_count; i++)
                renderBbox(pass, i);
        }


        public void renderBbox(int pass, int instance_id)
        {
            GL.UseProgram(pass);

            if (getInstanceOccludedStatus(instance_id))
                return;

            Matrix4 worldMat = getInstanceWorldMat(instance_id);
            //worldMat = worldMat.ClearRotation();
            
            Vector4[] tr_AABB = new Vector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new Vector4(instanceRefs[instance_id].AABBMIN, 1.0f);
            tr_AABB[1] = new Vector4(instanceRefs[instance_id].AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            int vb_bbox, eb_bbox;
            GL.GenBuffers(1, out vb_bbox);
            GL.GenBuffers(1, out eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }

        public void renderBSphere(GLSLHelper.GLSLShaderConfig shader)
        {
            for (int i = 0; i < instance_count; i++)
            {
                GLVao bsh_Vao = setupBSphere(i);

                //Rendering

                GL.UseProgram(shader.program_id);

                //Step 2 Bind & Render Vao
                //Render Bounding Sphere
                GL.BindVertexArray(bsh_Vao.vao_id);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, 600, DrawElementsType.UnsignedInt, (IntPtr)0);

                GL.BindVertexArray(0);
                bsh_Vao.Dispose();
            }


        }

        private void renderMesh()
        {
            //Step 2 Bind & Render Vao
            //Render Elements
            GL.BindVertexArray(vao.vao_id);
            
            //GL.DrawElements(PrimitiveType.Triangles, batchcount, indicesLength, IntPtr.Zero);
            //Use Instancing
            GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount, indicesLength,
                IntPtr.Zero, instance_count);

            GL.BindVertexArray(0);
        }

        private void renderLight()
        {
            //Step 2 Bind & Render Vao
            //Render Elements
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, instance_count);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, instance_count); //Draw both points
            GL.BindVertexArray(0);
        }

        private void renderCollision()
        {
            //Step 2: Render Elements
            GL.PointSize(10.0f);
            GL.BindVertexArray(vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            switch (collisionType)
            {
                //Rendering based on the original mesh buffers
                case COLLISIONTYPES.MESH:
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    break;

                //Rendering custom geometry
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, instance_count);
                    break;

            }

            GL.PolygonMode(MaterialFace.FrontAndBack, Common.RenderOptions.RENDERMODE);

            GL.BindVertexArray(0);
        }

        private void renderLocator()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElements(PrimitiveType.Lines, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);
        }

        private void renderJoint()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, metaData.batchcount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public virtual void renderMain(GLSLHelper.GLSLShaderConfig shader)
        {
            //Upload Material Information

            //Step 1 Upload uniform variables
            GL.Uniform1(shader.uniformLocations["mpCustomPerMaterial.matflags[0]"], 64, material.material_flags); //Upload Material Flags

            //Upload Custom Per Material Uniforms
            foreach (Uniform un in material.CustomPerMaterialUniforms.Values)
            {
                if (shader.uniformLocations.Keys.Contains(un.Name))
                    GL.Uniform4(shader.uniformLocations[un.Name], un.vec.Vec);
            }

            //BIND TEXTURES
            //Diffuse Texture

            foreach (Sampler s in material.PSamplers.Values)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], MyTextureUnit.MapTexUnitToSampler[s.Name]);
                    GL.ActiveTexture(s.texUnit.texUnit);
                    GL.BindTexture(s.tex.target, s.tex.bufferID);
                }
            }

            //BIND TEXTURE Buffer
            if (skinned)
            {
                GL.Uniform1(shader.uniformLocations["mpCustomPerMaterial.skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            }

            //if (instance_count > 100)
            //    Console.WriteLine("Increase the buffers");


            switch (type)
            {
                case TYPES.MESH:
                    renderMesh();
                    break;
                case TYPES.LOCATOR:
                case TYPES.MODEL:
                    renderLocator();
                    break;
                case TYPES.JOINT:
                    renderJoint();
                    break;
                case TYPES.COLLISION:
                    renderCollision();
                    break;
                case TYPES.LIGHT:
                    renderLight();
                    break;
            }
        }

        public virtual void renderDecals(GLSLHelper.GLSLShaderConfig shader, GBuffer gbuf)
        {
            //Bind Depth Buffer
            GL.Uniform1(shader.uniformLocations["depthTex"], 6);
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DMultisample, gbuf.dump_depth);

            renderMain(shader);
        }

        private void renderBHull(GLSLHelper.GLSLShaderConfig shader)
        {
            GL.UseProgram(shader.program_id);

            GL.Uniform1(shader.uniformLocations["scale"], 1.0f);

            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(10.0f);
            GL.BindVertexArray(bHullVao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.BindVertexArray(0);
        }

        public virtual void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < material.Flags.Count; i++)
                GL.Uniform1(loc + (int) material.Flags[i].MaterialFlag, 1.0f);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            /*
            Util.mulMatArrays(ref skinMats, gobject.invBMats, scene.JMArray, 256);
            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 256, false, skinMats);
            */

            //Step 2: Render VAO
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, metaData.batchcount, DrawElementsType.UnsignedShort, (IntPtr)0);
            GL.BindVertexArray(0);
        }



        //Default render method
        public bool render(GLSLHelper.GLSLShaderConfig shader, RENDERPASS pass, GBuffer gbuf = null)
        {
            if (instance_count == 0)
                return false;

            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERPASS.DEFERRED:
                case RENDERPASS.FORWARD:
                    renderMain(shader);
                    break;
                case RENDERPASS.DECAL:
                    renderDecals(shader, gbuf);
                    break;
                case RENDERPASS.BBOX:
                case RENDERPASS.BHULL:
                    renderBbox(shader.program_id, 0);
                    //renderBSphere(shader);
                    //renderBHull(shader);
                    break;
                //Render Debug
                case RENDERPASS.DEBUG:
                    //renderDebug(shader.program_id);
                    break;
                //Render for Picking
                case RENDERPASS.PICK:
                    //renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }

            return true;
        }


        public int addInstance(model m)
        {
            //Set instance id
            int instance_id = instance_count;
            
            if (instance_id < MAX_INSTANCES)
            {
                //Uplod worldMat to the meshVao
                instance_id = instance_count;

                setInstanceWorldMat(instance_id, m.worldMat);
                setInstanceNormalMat(instance_id, Matrix4.Transpose(m.worldMat.Inverted()));
                instanceRefs.Add(m); //Keep reference
                instance_count++;
            }

            return instance_id;
        }

        public void removeInstance(model m)
        {
            int id = instanceRefs.IndexOf(m);
            
            //TODO: Make all the memory shit to push the instances backwards
        }


        public void setInstanceOccludedStatus(int instance_id, bool status)
        {
            visible_instances += (status ? -1 : 1);
            unsafe
            {
                UBO.instanceData[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] = status ? 1.0f : 0.0f;
            }
            
        }

        public bool getInstanceOccludedStatus(int instance_id)
        {
            unsafe
            {
                return UBO.instanceData[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] > 0.0f ? true : false;
            }
        }

        public void setInstanceSelectedStatus(int instance_id, bool status)
        {
            unsafe
            {
                UBO.instanceData[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public Matrix4 getInstanceWorldMat(int instance_id)
        {
            unsafe
            {
                fixed(float* ar = UBO.instanceData)
                {
                    return MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset);
                }
            }
            
        }

        public Matrix4 getInstanceNormalMat(int instance_id)
        {
            unsafe
            {
                fixed (float* ar = UBO.instanceData)
                {
                    return MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset);
                }
            }
        }

        public Vector3 getInstanceColor(int instance_id)
        {
            float col;
            unsafe
            {
                col = UBO.instanceData[instance_id * instance_struct_size_floats + instance_color_Float_Offset];
            }
            
            return new Vector3(col, col, col);
        }

        public void setInstanceWorldMat(int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = UBO.instanceData)
                {
                    MathUtils.insertMatToArray16(ar, instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset, mat);
                }
            }
        }

        public void setInstanceWorldMatInv(int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = UBO.instanceData)
                {
                    MathUtils.insertMatToArray16(ar, instance_id * instance_struct_size_floats + instance_worldMatInv_Float_Offset, mat);
                }
            }
        }

        public void setInstanceNormalMat(int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = UBO.instanceData)
                {
                    MathUtils.insertMatToArray16(ar, instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset, mat);
                }
            }
        }


        public void setSkinMatrices(scene animScene, int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;


            if (instance_id < 0)
                Console.WriteLine("test");
            if (instance_id >= instance_count)
                Console.WriteLine("test");

            if (BoneRemapIndicesCount > 128)
                Console.WriteLine("test");

            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                Array.Copy(animScene.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
            }
        }

        public void setDefaultSkinMatrices(int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, Matrix4.Identity);
            }
                
        }

        public void initializeSkinMatrices()
        {
            if (instance_count == 0)
                return;
            //Re-initialize the array based on the number of instances
            instanceBoneMatrices = new float[instance_count * 128 * 16];
            int bufferSize = instance_count * 128 * 16 * 4;

            //Setup the TBO
            instanceBoneMatricesTex = GL.GenTexture();
            instanceBoneMatricesTexTBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, bufferSize, instanceBoneMatrices, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

        }

        public void uploadSkinningData()
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            int bufferSize = instance_count * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls







        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    BoneRemapIndices = null;
                    instanceBoneMatrices = null;
                    
                    vao?.Dispose();

                    if (instanceBoneMatricesTex > 0)
                    {
                        GL.DeleteTexture(instanceBoneMatricesTex);
                        GL.DeleteBuffer(instanceBoneMatricesTexTBO);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~mainGLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }


    public class meshModel : model
    {
        public int LodLevel
        {
            get
            {
                return metaData.lodLevel;
            }
            
        }

        public ulong Hash
        {
            get
            {
                return metaData.Hash;
            }
        }
        
        public MeshMetaData metaData = new MeshMetaData();
        public Vector3 color = new Vector3(); //Per instance
        public bool hasLOD = false;
        public bool Skinned { 
            get
            {
                if (meshVao.material != null)
                {
                    return meshVao.material.has_flag(TkMaterialFlags.UberFlagEnum._F02_SKINNED);
                }
                return false;
            }
        }
        
        public GLVao bHull_Vao;
        public GeomObject gobject; //Ref to the geometry shit
        
        //Exposable Uniforms Properties
        private Uniform gUserDataVec4;
        public Uniform PgUserDataVec4
        {
            get { return gUserDataVec4; }

            set { gUserDataVec4 = value; }
        }

        public Material material
        {
            get
            {
                return meshVao.material;
            }
        }
        
        //Constructor
        public meshModel() : base()
        {
            type = TYPES.MESH;
            metaData = new MeshMetaData();
            
            //Init Properties
            gUserDataVec4 = new Uniform();
            gUserDataVec4.PName = "mpCommonPerMesh.gUserDataVec4";
        }

        public meshModel(meshModel input) : base(input)
        {
            //Copy attributes
            this.metaData = new MeshMetaData(input.metaData);
            
            //Copy Vao Refs
            this.meshVao = input.meshVao;
            
            //Material Stuff
            this.color = input.color;
            
            this.palette = input.palette;
            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
        }

        public void copyFrom(meshModel input)
        {
            //Copy attributes
            metaData = new MeshMetaData(input.metaData);
            hasLOD = input.hasLOD;

            //Copy Vao Refs
            meshVao = input.meshVao;

            //Material Stuff
            color = input.color;

            palette = input.palette;
            gobject = input.gobject;

            base.copyFrom(input);
        }

        public override model Clone()
        {
            meshModel new_m = new meshModel();
            new_m.copyFrom(this);

            new_m.meshVao = this.meshVao;
            new_m.instanceId = new_m.meshVao.addInstance(new_m);
            
            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
            }
            
            return new_m;
        }

        public override void update()
        {
            base.update();
            //Calculate transfomration matrix for the normals
            normMat = Matrix4.Transpose(worldMat.Inverted());
        }

        public override void setupSkinMatrixArrays()
        {
            meshVao?.initializeSkinMatrices();

            base.setupSkinMatrixArrays();
            
        }

        public override void updateMeshInfo()
        {
            if (!updated)
            {
                base.updateMeshInfo();
                return;
            }

            if (instanceId < 0)
                Console.WriteLine("test");
            if (instanceId >= meshVao.instance_count)
                Console.WriteLine("test");
            if (meshVao.BoneRemapIndicesCount > 128)
                Console.WriteLine("test");


            if (!renderable)
            {
                meshVao.setInstanceOccludedStatus(instanceId, true);
            } else
            {
                //Apply frustum culling
                /*
                if (!Common.RenderState.activeCam.frustum_occlude(meshVao.metaData.AABBMIN,
                    meshVao.metaData.AABBMAX, worldMat))
                {
                    Common.RenderStats.occludedNum++;
                    meshVao.setInstanceOccludedStatus(instanceId, true);
                    base.updateMeshInfo();
                    return;
                }

                //Apply LOD filtering
                if (hasLOD && Common.RenderOptions.LODFiltering)
                //if (false)
                {
                    //Console.WriteLine("Active LoD {0}", parentScene.activeLOD);
                    if (parentScene.activeLOD != LodLevel)
                    {
                        meshVao.setInstanceOccludedStatus(instanceId, true);
                        base.updateMeshInfo();
                        return;
                    }
                }
                */

                meshVao.setInstanceOccludedStatus(instanceId, false);
                meshVao.setInstanceWorldMat(instanceId, worldMat);
                meshVao.setInstanceWorldMatInv(instanceId, worldMat.Inverted());
                meshVao.setInstanceNormalMat(instanceId, normMat);

                if (Skinned)
                {
                    
                    //Update the mesh remap matrices and continue with the transform updates
                    meshVao.setSkinMatrices(parentScene, instanceId);

                    //Fallback
                    //main_Vao.setDefaultSkinMatrices();
                }

                recalculateAABB(); //Update AABB
            }

            base.updateMeshInfo();
            updated = false; //All done
        }


        public void recalculateAABB()
        {

            //Revert back to the original values
            AABBMIN = meshVao.metaData.AABBMIN;
            AABBMAX = meshVao.metaData.AABBMAX;

            //Generate all 8 points from the AABB
            List<Vector4> vecs = new List<Vector4>();
            vecs.Add(new Vector4(AABBMIN.X, AABBMIN.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMIN.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMIN.X, AABBMAX.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMAX.Y, AABBMIN.Z, 1.0f));
            
            vecs.Add(new Vector4(AABBMIN.X, AABBMIN.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMIN.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMIN.X, AABBMAX.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMAX.Y, AABBMAX.Z, 1.0f));


            //Transform all Vectors using the worldMat
            for (int i = 0; i < 8; i++)
                vecs[i] = vecs[i] * worldMat;

            //Init vectors to max
            AABBMIN = new Vector3(float.MaxValue);
            AABBMAX = new Vector3(float.MinValue);
            
            //Align values
            
            for (int i = 0; i < 8; i++)
            {
                AABBMIN.X = Math.Min(AABBMIN.X, vecs[i].X);
                AABBMIN.Y = Math.Min(AABBMIN.Y, vecs[i].Y);
                AABBMIN.Z = Math.Min(AABBMIN.Z, vecs[i].Z);

                AABBMAX.X = Math.Max(AABBMAX.X, vecs[i].X);
                AABBMAX.Y = Math.Max(AABBMAX.Y, vecs[i].Y);
                AABBMAX.Z = Math.Max(AABBMAX.Z, vecs[i].Z);
            }


        }


        public void writeGeomToStream(StreamWriter s, ref uint index)
        {
            int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
            MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
            MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
            BinaryReader vbr = new BinaryReader(vms);
            BinaryReader ibr = new BinaryReader(ims);
            //Start Writing
            //Object name
            s.WriteLine("o " + name);
            //Get Verts

            //Preset Matrices for faster export
            Matrix4 wMat = this.worldMat;
            Matrix4 nMat = Matrix4.Invert(Matrix4.Transpose(wMat));

            vbr.BaseStream.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 v;
                VertexAttribPointerType ntype = gobject.bufInfo[0].type;
                int v_section_bytes = 0;

                switch (ntype)
                {
                    case VertexAttribPointerType.HalfFloat:
                        uint v1 = vbr.ReadUInt16();
                        uint v2 = vbr.ReadUInt16();
                        uint v3 = vbr.ReadUInt16();
                        //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                        //Transform vector with worldMatrix
                        v = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3), 1.0f);
                        v_section_bytes = 6;
                        break;
                    case VertexAttribPointerType.Float: //This is used in my custom vbos
                        float f1 = vbr.ReadSingle();
                        float f2 = vbr.ReadSingle();
                        float f3 = vbr.ReadSingle();
                        //Transform vector with worldMatrix
                        v = new Vector4(f1, f2, f3, 1.0f);
                        v_section_bytes = 12;
                        break;
                    default:
                        throw new Exception("Unimplemented Vertex Type");
                }


                v = Vector4.Transform(v, this.worldMat);

                //s.WriteLine("v " + Half.decompress(v1).ToString() + " "+ Half.decompress(v2).ToString() + " " + Half.decompress(v3).ToString());
                s.WriteLine("v " + v.X.ToString() + " " + v.Y.ToString() + " " + v.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - v_section_bytes, SeekOrigin.Current);
            }
            //Get Normals

            vbr.BaseStream.Seek(gobject.offsets[2] + 0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 vN;
                VertexAttribPointerType ntype = gobject.bufInfo[2].type;
                int n_section_bytes = 0;

                switch (ntype)
                {
                    case (VertexAttribPointerType.Float):
                        float f1, f2, f3;
                        f1 = vbr.ReadSingle();
                        f2 = vbr.ReadSingle();
                        f3 = vbr.ReadSingle();
                        vN = new Vector4(f1, f2, f3, 1.0f);
                        n_section_bytes = 12;
                        break;
                    case (VertexAttribPointerType.HalfFloat):
                        uint v1, v2, v3;
                        v1 = vbr.ReadUInt16();
                        v2 = vbr.ReadUInt16();
                        v3 = vbr.ReadUInt16();
                        vN = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3), 1.0f);
                        n_section_bytes = 6;
                        break;
                    case (VertexAttribPointerType.Int2101010Rev):
                        int i1, i2, i3;
                        uint value;
                        byte[] a32 = new byte[4];
                        a32 = vbr.ReadBytes(4);

                        value = BitConverter.ToUInt32(a32, 0);
                        //Convert Values
                        i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                        i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                        i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                        //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                        float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                        vN = new Vector4(Convert.ToSingle(i1) / norm,
                                         Convert.ToSingle(i2) / norm,
                                         Convert.ToSingle(i3) / norm,
                                         1.0f);

                        n_section_bytes = 4;
                        //Debug.WriteLine(vN);
                        break;
                    default:
                        throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                }

                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                //Transform normal with normalMatrix


                vN = Vector4.Transform(vN, nMat);

                s.WriteLine("vn " + vN.X.ToString() + " " + vN.Y.ToString() + " " + vN.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - n_section_bytes, SeekOrigin.Current);
            }
            //Get UVs, only for mesh objects

            vbr.BaseStream.Seek(Math.Max(gobject.offsets[1], 0) + gobject.vx_size * metaData.vertrstart_graphics, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector2 uv;
                int uv_section_bytes = 0;
                if (gobject.offsets[1] != -1) //Check if uvs exist
                {
                    uint v1 = vbr.ReadUInt16();
                    uint v2 = vbr.ReadUInt16();
                    uint v3 = vbr.ReadUInt16();
                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                    uv = new Vector2(Half.decompress(v1), Half.decompress(v2));
                    uv_section_bytes = 0x6;
                }
                else
                {
                    uv = new Vector2(0.0f, 0.0f);
                    uv_section_bytes = gobject.vx_size;
                }

                s.WriteLine("vt " + uv.X.ToString() + " " + (1.0 - uv.Y).ToString());
                vbr.BaseStream.Seek(gobject.vx_size - uv_section_bytes, SeekOrigin.Current);
            }


            //Some Options
            s.WriteLine("usemtl(null)");
            s.WriteLine("s off");

            //Get indices
            ibr.BaseStream.Seek(0, SeekOrigin.Begin);
            bool start = false;
            uint fstart = 0;
            for (int i = 0; i < metaData.batchcount / 3; i++)
            {
                uint f1, f2, f3;
                //NEXT models assume that all gstream meshes have uint16 indices
                f1 = ibr.ReadUInt16();
                f2 = ibr.ReadUInt16();
                f3 = ibr.ReadUInt16();

                if (!start && this.type != TYPES.COLLISION)
                { fstart = f1; start = true; }
                else if (!start && this.type == TYPES.COLLISION)
                {
                    fstart = 0; start = true;
                }

                uint f11, f22, f33;
                f11 = f1 - fstart + index;
                f22 = f2 - fstart + index;
                f33 = f3 - fstart + index;


                s.WriteLine("f " + f11.ToString() + "/" + f11.ToString() + "/" + f11.ToString() + " "
                                + f22.ToString() + "/" + f22.ToString() + "/" + f22.ToString() + " "
                                + f33.ToString() + "/" + f33.ToString() + "/" + f33.ToString() + " ");


            }
            index += (uint)vertcount;
        }



        #region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {

                    // TODO: dispose managed state (managed objects).
                    //if (material != null) material.Dispose();
                    //NOTE: No need to dispose material, because the materials reside in the resource manager
                    base.Dispose(disposing);
                }
            }
        }

        #endregion

    }

    [StructLayout(LayoutKind.Explicit)]
    struct CustomPerMaterialUniforms
    {
        [FieldOffset(0)] //256 Bytes
        public unsafe fixed int matflags[64];
        [FieldOffset(256)] //64 Bytes
        public int diffuseTex;
        [FieldOffset(260)] //4 bytes
        public int maskTex;
        [FieldOffset(264)] //4 bytes
        public int normalTex;
        [FieldOffset(276)] //16 bytes
        public Vector4 gMaterialColourVec4;
        [FieldOffset(292)] //16 bytes
        public Vector4 gMaterialParamsVec4;
        [FieldOffset(308)] //16 bytes
        public Vector4 gMaterialSFXVec4;
        [FieldOffset(324)] //16 bytes
        public Vector4 gMaterialSFXColVec4;
        [FieldOffset(340)] //16 bytes
        public Vector4 gDissolveDataVec4;
        [FieldOffset(356)] //16 bytes
        public Vector4 gCustomParams01Vec4;
        
        public static readonly int SizeInBytes = 360;
    };

    public class Collision : model
    {
        public COLLISIONTYPES collisionType;
        public GeomObject gobject;
        public MeshMetaData metaData = new MeshMetaData();
        
        //Custom constructor
        public Collision()
        {
            
        }

        public override model Clone()
        {
            Collision new_m = new Collision();
            new_m.collisionType = collisionType;
            new_m.copyFrom(this);

            new_m.meshVao = this.meshVao;
            new_m.instanceId = new_m.meshVao.addInstance(new_m);

            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
            }

            return new_m;
        }
        
        
        protected Collision(Collision input) : base(input)
        {
            collisionType = input.collisionType;
        }

        public override void update()
        {
            base.update();

        }

        public override void updateMeshInfo()
        {
            if (!renderable || !Common.RenderOptions.RenderCollisions)
            {
                meshVao.setInstanceOccludedStatus(instanceId, true);
                base.updateMeshInfo();
                return;
            }

            //Apply frustum culling
            if (!Common.RenderState.activeCam.frustum_occlude(meshVao.metaData.AABBMIN,
                meshVao.metaData.AABBMAX, worldMat))
            {
                Common.RenderStats.occludedNum++;
                meshVao.setInstanceOccludedStatus(instanceId, true);
                base.updateMeshInfo();
                return;
            }
            else
            {
                meshVao.setInstanceOccludedStatus(instanceId, false);
                meshVao.setInstanceWorldMat(instanceId, worldMat);
                meshVao.setInstanceNormalMat(instanceId, Matrix4.Transpose(worldMat.Inverted()));
            }

            base.updateMeshInfo();
        }

    }


    public class GeomObject : IDisposable
    {
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
        public List<int[]> bIndices = new List<int[]>();
        public List<float[]> bWeights = new List<float[]>();
        public List<bufInfo> bufInfo = new List<GMDL.bufInfo>();
        public int[] offsets; //List to save strides according to meshdescr
        public int[] small_offsets; //Same thing for the small description
        public short[] boneRemap;
        public List<Vector3[]> bboxes = new List<Vector3[]>();
        public List<Vector3> bhullverts = new List<Vector3>();
        public List<int> bhullstarts = new List<int>();
        public List<int> bhullends = new List<int>();
        public List<int[]> bhullindices = new List<int[]>();
        public List<int> vstarts = new List<int>();
        public Dictionary<ulong, geomMeshMetaData> meshMetaDataDict = new Dictionary<ulong, geomMeshMetaData>();
        public Dictionary<ulong, geomMeshData> meshDataDict = new Dictionary<ulong, geomMeshData>();
        
        //Joint info
        public List<JointBindingData> jointData = new List<JointBindingData>();
        public float[] invBMats = new float[256 * 16];

        //Dictionary with the compiled VAOs belonging on this gobject
        private Dictionary<ulong, GMDL.GLVao> GLVaos = new Dictionary<ulong, GLVao>();
        //Dictionary to index 
        private Dictionary<ulong, Dictionary<string, GLMeshVao>> GLMeshVaos = new Dictionary<ulong, Dictionary<string, GLMeshVao>>();



        public Vector3 get_vec3_half(BinaryReader br)
        {
            Vector3 temp;
            //Get Values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            uint val3 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            temp.Z = Half.decompress(val3);
            //Console.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
            return temp;
        }

        public Vector2 get_vec2_half(BinaryReader br)
        {
            Vector2 temp;
            //Get values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            return temp;
        }





        //Fetch Meshvao from dictionary
        public GLMeshVao findGLMeshVao(string material_name, ulong hash)
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
        public bool saveGLMeshVAO(ulong hash, string matname, GLMeshVao meshVao)
        {
            if (GLMeshVaos.ContainsKey(hash))
            {
                if (GLMeshVaos[hash].ContainsKey(matname))
                {
                    Console.WriteLine("MeshVao already in the dictinary, nothing to do...");
                    return false;
                }
            }
            else
                GLMeshVaos[hash] = new Dictionary<string, GLMeshVao>();
                
            GLMeshVaos[hash][matname] = meshVao;

            return true;

        }

        //Save VAO to gobject
        public bool saveVAO(ulong hash, GLVao vao)
        {
            //Double check tha the VAO is not already in the dictinary
            if (GLVaos.ContainsKey(hash))
            {
                Console.WriteLine("Vao already in the dictinary, nothing to do...");
                return false;
            }
                
            //Save to dictionary
            GLVaos[hash] = vao;
            return true;
        }

        //Fetch main VAO
        public GLVao generateVAO(meshModel so)
        {
            //Generate VAO
            GLVao vao = new GLVao();
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
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].vs_size,
                meshDataDict[so.metaData.Hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (so.metaData.vertrend_graphics + 1))
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            MVCore.Common.RenderStats.vertNum += so.metaData.vertrend_graphics + 1; //Accumulate settings

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (this.bufInfo[i] == null) continue;
                bufInfo buf = this.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, buf.normalize, this.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].is_size, 
                meshDataDict[so.metaData.Hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            Console.WriteLine(GL.GetError());
            if (size != meshMetaDataDict[so.metaData.Hash].is_size)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));


            MVCore.Common.RenderStats.trisNum += (int) (so.metaData.batchcount / 3); //Accumulate settings

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

            float[] vx_buffer_float = new float[(metaData.boundhullend - metaData.boundhullstart) * 3];

            for (int i = 0; i < metaData.boundhullend - metaData.boundhullstart; i++)
            {
                Vector3 v = bhullverts[i + metaData.boundhullstart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GMDL.GeomObject temp_geom = new GMDL.GeomObject();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = metaData.batchcount;
            temp_geom.indicesLength = indicesLength; 

            //Set Strides
            temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            temp_geom.offsets = new int[7];
            temp_geom.bufInfo = new List<GMDL.bufInfo>();

            for (int i = 0; i < 7; i++)
            {
                temp_geom.bufInfo.Add(null);
                temp_geom.offsets[i] = -1;
            }

            temp_geom.mesh_descr = "vn";
            temp_geom.offsets[0] = 0;
            temp_geom.offsets[2] = 0;
            temp_geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition", false);
            temp_geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 0, "nPosition", false);

            //Set Buffers
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.batchcount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(ibuffer, metaData.batchstart_physics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            return temp_geom.generateVAO();
        }

        public GLVao generateVAO()
        {

            GLVao vao = new GLVao();

            //Generate VAO
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            if (GL.GetError() != ErrorCode.NoError)
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
            for (int i = 0; i < 7; i++)
            {
                if (this.bufInfo[i] == null) continue;
                bufInfo buf = this.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vx_size, buf.stride);
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
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    ibuffer = null;
                    vbuffer = null;
                    small_vbuffer = null;
                    offsets = null;
                    small_offsets = null;
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

                    //Clear Vaos
                    foreach (GLVao p in GLVaos.Values)
                        p.Dispose();
                    GLVaos.Clear();

                    //Dispose GLmeshes
                    foreach (Dictionary<string, GLMeshVao> p in GLMeshVaos.Values)
                    {
                        foreach (GLMeshVao m in p.Values)
                            m.Dispose(); 
                        p.Clear();
                        //Materials are stored globally
                    }
                
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
#endregion

        
    }

    public class bufInfo
    {
        public int semantic;
        public VertexAttribPointerType type;
        public int count;
        public int stride;
        public string sem_text;
        public bool normalize;

        public bufInfo(int sem,VertexAttribPointerType typ, int c, int s, string t, bool n)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
            normalize = n;
        }
    }


    public class Sampler : TkMaterialSampler, IDisposable
    {
        public MyTextureUnit texUnit;
        public Texture tex;
        public textureManager texMgr; //For now it should be inherited from the scene. In the future I can use a delegate
        public bool isProcGen = false;

        //Override Properties
        public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PMap
        {
            get
            {
                return Map;
            }
            set
            {
                Map = value;
            }
        }

        public Sampler()
        {

        }

        public Sampler(TkMaterialSampler ms)
        {
            //Pass everything here because there is no base copy constructor in the NMS template
            this.Name = "mpCustomPerMaterial." + ms.Name;
            this.Map = ms.Map;
            this.IsCube = ms.IsCube;
            this.IsSRGB = ms.IsSRGB;
            this.UseCompression = ms.UseCompression;
            this.UseMipMaps = ms.UseMipMaps;
        }

        public Sampler Clone()
        {
            Sampler newsampler = new Sampler();

            newsampler.PName = PName;
            newsampler.PMap = PMap;
            newsampler.texMgr = texMgr;
            newsampler.tex = tex;
            newsampler.texUnit = texUnit;
            newsampler.TextureAddressMode = TextureAddressMode;
            newsampler.TextureFilterMode = TextureFilterMode;

            return newsampler;
        }


        public void init(textureManager input_texMgr)
        {
            texMgr = input_texMgr;
            texUnit = new MyTextureUnit(Name);

            //Save texture to material
            switch (Name)
            {
                case "mpCustomPerMaterial.gDiffuseMap":
                case "mpCustomPerMaterial.gDiffuse2Map":
                case "mpCustomPerMaterial.gMasksMap":
                case "mpCustomPerMaterial.gNormalMap":
                    prepTextures();
                    break;
                default:
                    MVCore.Common.CallBacks.Log("Not sure how to handle Sampler " + Name);
                    break;
            }
        }


        public void prepTextures()
        {
            string[] split = Map.Split('.');

            string temp = "";
            if (Name == "mpCustomPerMaterial.gDiffuseMap")
            {
                //Check if the sampler describes a proc gen texture
                temp = split[0] + ".";
                //Construct main filename

                string texMbin = temp + "TEXTURE.MBIN";
                texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, texMbin));

                //Detect Procedural Texture
                if (File.Exists(texMbin))
                {
                    TextureMixer.combineTextures(Map, Palettes.paletteSel, ref texMgr);
                    //Override Map
                    Map = temp + "DDS";
                    isProcGen = true;
                }
            }

            //Load the texture to the sampler
            loadTexture();
        }


        private void loadTexture()
        {
            Console.WriteLine("Trying to load Texture");

            if (Map == "")
                return;

            //Try to load the texture
            if (texMgr.hasTexture(Map))
            {
                tex = texMgr.getTexture(Map);
            }
            else
            {
                tex = new Texture(Map);
                tex.palOpt = new PaletteOpt(false);
                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                //At this point this should be a common texture. Store it to the master texture manager
                Common.RenderState.activeResMgr.texMgr.addTexture(tex);
            }

        }


        public static void dump_texture(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.GetTexImage(TextureTarget.Texture2DArray, 0, PixelFormat.Rgba, PixelType.Byte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }

        public static void dump_texture_fb(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }


        public static int generate2DTexture(PixelInternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex_id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static int generateTexture2DArray(PixelInternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, tex_id);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static void generateTexture2DMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public static void generateTexture2DArrayMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        }

        public static void setupTextureParameters(TextureTarget texTarget, int texture, int wrapMode, int magFilter, int minFilter, float af_amount)
        {

            GL.BindTexture(texTarget, texture);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(texTarget, TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(texTarget, (TextureParameterName)0x84FE, af_amount);
        }



        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls



        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //Texture lists should have been disposed from the dictionary
                    //Free other resources here
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion


    }

    public class Material : TkMaterialData, IDisposable
    {
        private bool disposed = false;
        public bool proc = false;
        public float[] material_flags = new float[64];
        public string name_key = "";
        public textureManager texMgr;

        public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PClass
        {
            get
            {
                return Class;
            }
        }

        public List<string> MaterialFlags
        {
            get
            {
                List<string> l = new List<string>();

                foreach (TkMaterialFlags f in Flags)
                {
                    l.Add(((TkMaterialFlags.UberFlagEnum) f.MaterialFlag).ToString());
                }

                return l;
            }
        }

        public string type;
        //public MatOpts opts;
        public Dictionary<string, Sampler> _PSamplers = new Dictionary<string, Sampler>();

        public Dictionary<string, Sampler> PSamplers {
            get
            {
                return _PSamplers;
            }
        }

        private Dictionary<string, Uniform> _CustomPerMaterialUniforms = new Dictionary<string, Uniform>();
        public Dictionary<string, Uniform> CustomPerMaterialUniforms {
            get
            {
                return _CustomPerMaterialUniforms;
            }
        }

        public Material()
        {
            Name = "NULL";
            Shader = "NULL";
            Link = "NULL";
            Class = "NULL";
            TransparencyLayerID = -1;
            CastShadow = false;
            DisableZTest = false;
            Flags = new List<TkMaterialFlags>();
            Samplers = new List<TkMaterialSampler>();
            Uniforms = new List<TkMaterialUniform>();

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public Material(TkMaterialData md)
        {
            Name = md.Name;
            Shader = md.Shader;
            Link = md.Link;
            Class = md.Class;
            TransparencyLayerID = md.TransparencyLayerID;
            CastShadow = md.CastShadow;
            DisableZTest = md.DisableZTest;
            Flags = new List<TkMaterialFlags>();
            Samplers = new List<TkMaterialSampler>();
            Uniforms = new List<TkMaterialUniform>();

            for (int i = 0; i < md.Flags.Count; i++)
                Flags.Add(md.Flags[i]);
            for (int i = 0; i < md.Samplers.Count; i++)
                Samplers.Add(md.Samplers[i]);
            for (int i = 0; i < md.Uniforms.Count; i++)
                Uniforms.Add(md.Uniforms[i]);

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public static Material Parse(string path, textureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            TkMaterialData template = (TkMaterialData)mbinf.GetData();
            mbinf.Dispose();

#if DEBUG
            //Save NMSTemplate to exml
            template.WriteToExml("Temp\\" + template.Name + ".exml");
#endif
            
            //Make new material based on the template
            Material mat = new Material(template);

            mat.texMgr = input_texMgr;
            mat.init();
            return mat;
        }

        public void init()
        {
            
            //Get MaterialFlags
            MVCore.Common.CallBacks.Log("Material Flags: ");
            
            foreach (TkMaterialFlags f in Flags)
            {
                material_flags[(int) f.MaterialFlag] = 1.0f;
                MVCore.Common.CallBacks.Log(((TkMaterialFlags.MaterialFlagEnum)f.MaterialFlag).ToString() + " ");
            }

            //Get Uniforms
            foreach (TkMaterialUniform un in Uniforms)
            {
                Uniform my_un = new Uniform("mpCustomPerMaterial.", un);
                CustomPerMaterialUniforms[my_un.Name] = my_un;
            }

            //Get Samplers
            foreach (TkMaterialSampler sm in Samplers)
            {
                Sampler s = new Sampler(sm);
                s.init(texMgr);
                PSamplers[s.PName] = s;
            }


            //Workaround for Procedurally Generated Samplers
            //I need to check if the diffuse sampler is procgen and then force the maps
            //on the other samplers with the appropriate names

            foreach (Sampler s in PSamplers.Values)
            {
                //Check if the first sampler is procgen
                if (s.isProcGen)
                {
                    string name = s.Map;

                    //Properly assemble the mask and the normal map names

                    string[] split = name.Split('.');
                    string pre_ext_name = "";
                    for (int i = 0; i < split.Length-1; i++)
                        pre_ext_name += split[i] + '.';

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gMasksMap"))
                    {
                        string new_name = pre_ext_name + "MASKS.DDS";
                        PSamplers["mpCustomPerMaterial.gMasksMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gMasksMap"].tex = PSamplers["mpCustomPerMaterial.gMasksMap"].texMgr.getTexture(new_name);
                    }

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gNormalMap"))
                    {
                        string new_name = pre_ext_name + "NORMAL.DDS";
                        PSamplers["mpCustomPerMaterial.gNormalMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gNormalMap"].tex = PSamplers["mpCustomPerMaterial.gNormalMap"].texMgr.getTexture(new_name);
                    }
                    break;
                }
            }

            //EXPLICIT FIXES TO MATERIAL PARAMETERS
            /*
            if (has_flag(TkMaterialFlags.MaterialFlagEnum._F22_))
            {
                CustomPerMaterialUniforms["mpCustomPerMaterial.gMaterialColourVec4"].Vec.W = 
                    Math.Min(CustomPerMaterialUniforms["mpCustomPerMaterial.gMaterialColourVec4"].Vec.W, 0.1f);
            }
            */
            

                
            MVCore.Common.CallBacks.Log("\n");
        }

        //Wrapper to support uberflags
        public bool has_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            return has_flag((TkMaterialFlags.MaterialFlagEnum) flag);
        }

        public bool has_flag(TkMaterialFlags.MaterialFlagEnum flag)
        {
            for (int i = 0; i < Flags.Count; i++)
            {
                if (Flags[i].MaterialFlag == flag)
                    return true;
            }
            return false;
        }

        public bool add_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            //Check if material has flag
            foreach (TkMaterialFlags f in Flags)
            {
                if (f.MaterialFlag == (TkMaterialFlags.MaterialFlagEnum)flag)
                    return false;
            }
            
            TkMaterialFlags ff = new TkMaterialFlags();
            ff.MaterialFlag = (TkMaterialFlags.MaterialFlagEnum) flag;
            Flags.Add(ff);

            return true;
        }

        public GMDL.Material Clone()
        {
            GMDL.Material newmat = new GMDL.Material();
            //Remix textures
            return newmat;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //DISPOSE SAMPLERS HERE
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~Material()
        {
            Dispose(false);
        }

    }
    
    public class Uniform: TkMaterialUniform
    {
        public MVector4 vec;
        private string prefix;

        public Uniform()
        {
            prefix = "";
            vec = new MVector4(0.0f);
        }

        public Uniform(TkMaterialUniform un)
        {
            //Copy Attributes
            Name = un.Name;
            vec = new MVector4(un.Values.x, un.Values.y, un.Values.z, un.Values.t);
        }

        public Uniform(string pref, TkMaterialUniform un) : this(un)
        {
            prefix = pref;
            Name = prefix + un.Name;
        }

        public void setPrefix(string pref)
        {
            prefix = pref;
        }

        public string PName
        {
            get { return Name; }
            set { Name = value; }
        }

        public MVector4 Vec
        {
            get {
                return vec;
            }

            set
            {
                vec = value;
            }
        }

    }

    public class MVector4: INotifyPropertyChanged
    {
        private Vector4 vec4;

        public MVector4(Vector4 v)
        {
            vec4 = v;
        }

        public MVector4(float x , float y, float z, float w)
        {
            vec4 = new Vector4(x, y, z, w);
        }

        public MVector4(float x)
        {
            vec4 = new Vector4(x);
        }

        //Properties
        public Vector4 Vec
        {
            get { return vec4; }
            set { vec4 = value; RaisePropertyChanged("Vec"); }
        }
        public float X
        {
            get { return vec4.X; }
            set { vec4.X = value; RaisePropertyChanged("X"); }
        }
        public float Y
        {
            get { return vec4.Y; }
            set { vec4.Y = value; RaisePropertyChanged("Y"); }
        }

        public float Z
        {
            get { return vec4.Z; }
            set { vec4.Z = value; RaisePropertyChanged("Z"); }
        }

        public float W
        {
            get { return vec4.W; }
            set { vec4.W = value; RaisePropertyChanged("W"); }
        }

        //Property Change callbacks
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        
    }

    public class MatOpts
    {
        public int transparency;
        public bool castshadow;
        public bool disableTestz;
        public string link;
        public string shadername;
    }


    public class MyTextureUnit
    {
        public OpenTK.Graphics.OpenGL4.TextureUnit texUnit;

        public static Dictionary<string, TextureUnit> MapTextureUnit = new Dictionary<string, TextureUnit> {
            { "mpCustomPerMaterial.gDiffuseMap" , TextureUnit.Texture0 },
            { "mpCustomPerMaterial.gMasksMap" ,   TextureUnit.Texture1 },
            { "mpCustomPerMaterial.gNormalMap" ,  TextureUnit.Texture2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , TextureUnit.Texture3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", TextureUnit.Texture4},
            { "mpCustomPerMaterial.gDetailNormalMap", TextureUnit.Texture5}
        };

        public static Dictionary<string, int> MapTexUnitToSampler = new Dictionary<string, int> {
            { "mpCustomPerMaterial.gDiffuseMap" , 0 },
            { "mpCustomPerMaterial.gMasksMap" ,   1 },
            { "mpCustomPerMaterial.gNormalMap" ,  2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , 3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", 4},
            { "mpCustomPerMaterial.gDetailNormalMap", 5}
        };

        public MyTextureUnit(string sampler_name)
        {
            texUnit = MapTextureUnit[sampler_name];
        }
    }


    public static class TextureMixer
    {
        //Local storage
        public static Dictionary<string, Dictionary<string, Vector4>> palette = new Dictionary<string, Dictionary<string, Vector4>>();
        public static List<PaletteOpt> palOpts = new List<PaletteOpt>();
        public static List<Texture> difftextures = new List<Texture>(8);
        public static List<Texture> masktextures = new List<Texture>(8);
        public static List<Texture> normaltextures = new List<Texture>(8);
        public static float[] baseLayersUsed = new float[8];
        public static float[] alphaLayersUsed = new float[8];
        public static List<float[]> reColourings = new List<float[]>(8);
        public static List<float[]> avgColourings = new List<float[]>(8);
        private static int[] old_vp_size = new int[4];


        public static void clear()
        {
            //Cleanup temp buffers
            difftextures.Clear();
            masktextures.Clear();
            normaltextures.Clear();
            reColourings.Clear();
            avgColourings.Clear();
            for (int i = 0; i < 8; i++)
            {
                difftextures.Add(null);
                masktextures.Add(null);
                normaltextures.Add(null);
                reColourings.Add(new float[] { 0.0f, 0.0f, 0.0f, 0.0f });
                avgColourings.Add(new float[] { 0.5f, 0.5f, 0.5f, 0.5f });
                palOpts.Add(null);
            }
        }

        public static void combineTextures(string path, Dictionary<string, Dictionary<string, Vector4>> pal_input, ref textureManager texMgr)
        {
            clear();
            palette = pal_input;

            //Contruct .mbin file from dds
            string[] split = path.Split('.');
            //Construct main filename
            string temp = split[0] + ".";
            
            string mbinPath = temp + "TEXTURE.MBIN";
            mbinPath = Path.GetFullPath(Path.Combine(FileUtils.dirpath, mbinPath));

            prepareTextures(texMgr, mbinPath);

            //Init framebuffer
            int tex_width = 0;
            int tex_height = 0;
            int fbo_tex = -1;
            int fbo = -1;
            
            bool fbo_status = setupFrameBuffer(ref fbo, ref fbo_tex, ref tex_width, ref tex_height);

            if (!fbo_status)
            {
                MVCore.Common.CallBacks.Log("Unable to mix textures, probably 0x0 textures...\n");
                return;
            }
                
            Texture diffTex = mixDiffuseTextures(tex_width, tex_height);
            diffTex.name = temp + "DDS";

            Texture maskTex = mixMaskTextures(tex_width, tex_height);
            maskTex.name = temp + "MASKS.DDS";

            Texture normalTex = mixNormalTextures(tex_width, tex_height);
            normalTex.name = temp + "NORMAL.DDS";

            revertFrameBuffer(fbo, fbo_tex);

            //Add the new procedural textures to the textureManager
            texMgr.addTexture(diffTex);
            texMgr.addTexture(maskTex);
            texMgr.addTexture(normalTex);
        }

        //Generate procedural textures
        private static void prepareTextures(textureManager texMgr, string path)
        {
            //At this point, at least one sampler exists, so for now I assume that the first sampler
            //is always the diffuse sampler and I can initiate the mixing process
            string texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, path));

            Console.WriteLine("Procedural Texture Detected: " + texMbin);
            MVCore.Common.CallBacks.Log(string.Format("Parsing Procedural Texture"));

            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(texMbin);
            mbinf.Load();
            TkProceduralTextureList template = (TkProceduralTextureList)mbinf.GetData();
            mbinf.Dispose();


            List<TkProceduralTexture> texList = new List<TkProceduralTexture>(8);
            for (int i = 0; i < 8; i++) texList.Add(null);
            ModelProcGen.parse_procTexture(ref texList, template, ref Common.RenderState.activeResMgr);


            Common.CallBacks.Log("Proc Texture Selection");
            for (int i = 0; i < 8; i++)
            {
                if (texList[i] != null)
                {
                    string partNameDiff = texList[i].Diffuse;
                    Common.CallBacks.Log(partNameDiff);
                }
            }

            Common.CallBacks.Log("Procedural Material. Trying to generate procTextures...");

            for (int i = 0; i < 8; i++)
            {

                TkProceduralTexture ptex = texList[i];
                //Add defaults
                if (ptex == null)
                {
                    baseLayersUsed[i] = 0.0f;
                    alphaLayersUsed[i] = 0.0f;
                    continue;
                }

                string partNameDiff = ptex.Diffuse;
                string partNameMask = ptex.Mask;
                string partNameNormal = ptex.Normal;

                TkPaletteTexture paletteNode = ptex.Palette;
                string paletteName = paletteNode.Palette.ToString();
                string colorName = paletteNode.ColourAlt.ToString();
                Vector4 palColor = palette[paletteName][colorName];
                //Randomize palette Color every single time
                //Vector3 palColor = Model_Viewer.Palettes.get_color(paletteName, colorName);

                //Store pallete color to Recolouring List
                reColourings[i] = new float[] { palColor[0], palColor[1], palColor[2], palColor[3] };
                if (ptex.OverrideAverageColour)
                    avgColourings[i] = new float[] { ptex.AverageColour.R, ptex.AverageColour.G, ptex.AverageColour.B, ptex.AverageColour.A };
                    
                //Create Palette Option
                PaletteOpt palOpt = new PaletteOpt();
                palOpt.PaletteName = paletteName;
                palOpt.ColorName = colorName;
                palOpts[i] = palOpt;
                Console.WriteLine("Index {0} Palette Selection {1} {2} ", i, palOpt.PaletteName, palOpt.ColorName);
                Console.WriteLine("Index {0} Color {1} {2} {3} {4}", i, palColor[0], palColor[1], palColor[2], palColor[3]);

                //DIFFUSE
                if (partNameDiff == "")
                {
                    //Add White
                    baseLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameDiff))
                {
                    //Configure the Diffuse Texture
                    try
                    {
                        Texture tex = new Texture(partNameDiff);
                        tex.palOpt = palOpt;
                        tex.procColor = palColor;
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(tex);
                        
                        //Save Texture to material
                        difftextures[i] = tex;
                        baseLayersUsed[i] = 1.0f;
                        alphaLayersUsed[i] = 1.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Texture Not Found Continue
                        Console.WriteLine("Diffuse Texture " + partNameDiff + " Not Found, Appending White Tex");
                        MVCore.Common.CallBacks.Log(string.Format("Diffuse Texture {0} Not Found", partNameDiff));
                        baseLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameDiff);
                    //Save Texture to material
                    difftextures[i] = tex;
                    baseLayersUsed[i] = 1.0f;
                }

                //MASK
                if (partNameMask == "")
                {
                    //Skip
                    alphaLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameMask))
                {
                    string pathMask = Path.Combine(FileUtils.dirpath, partNameMask);
                    //Configure Mask
                    try
                    {
                        Texture texmask = new Texture(partNameMask);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texmask);
                        //Store Texture to material
                        masktextures[i] = texmask;
                        alphaLayersUsed[i] = 0.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Mask Texture not found
                        Console.WriteLine("Mask Texture " + pathMask + " Not Found");
                        MVCore.Common.CallBacks.Log(string.Format("Mask Texture {0} Not Found", pathMask));
                        alphaLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameMask);
                    //Store Texture to material
                    masktextures[i] = tex;
                    alphaLayersUsed[i] = 1.0f;
                }


                //NORMALS
                if (partNameNormal == "")
                {
                    //Skip

                }
                else if (!texMgr.hasTexture(partNameNormal))
                {
                    string pathNormal = Path.Combine(FileUtils.dirpath, partNameNormal);

                    try
                    {
                        Texture texnormal = new Texture(partNameNormal);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texnormal);
                        //Store Texture to material
                        normaltextures[i] = texnormal;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Normal Texture not found
                        Console.WriteLine("Normal Texture " + pathNormal + " Not Found");
                        MVCore.Common.CallBacks.Log(string.Format("Normal Texture {0} Not Found", pathNormal));
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameNormal);
                    //Store Texture to material
                    normaltextures[i] = tex;
                }
            }
        }

        private static bool setupFrameBuffer(ref int fbo, ref int fbo_tex, ref int texWidth, ref int texHeight)
        {
            for (int i = 0; i < 8; i++)
            {
                if (difftextures[i] != null)
                {
                    texHeight = difftextures[i].height;
                    texWidth = difftextures[i].width;
                    break;
                }
            }

            if (texWidth == 0 || texHeight == 0)
            {
                //FUCKING HG HAS FUCKING EMPTY TEXTURES WTF AM I SUPPOSED TO MIX HERE
                return false;
            }


            //Diffuse Output
            fbo_tex = Sampler.generate2DTexture(PixelInternalFormat.Rgba, texWidth, texHeight, PixelFormat.Rgba, PixelType.UnsignedByte, 1);
            Console.WriteLine(GL.GetError());
            Sampler.setupTextureParameters(TextureTarget.Texture2D, fbo_tex, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);
            Console.WriteLine(GL.GetError());

            //Create New RenderBuffer for the diffuse
            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Attach Textures to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fbo_tex, 0);
            Console.WriteLine(GL.GetError());

            //Check
            Debug.Assert(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete);
            
            //Bind the FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Set Viewport
            GL.GetInteger(GetPName.Viewport, old_vp_size);
            GL.Viewport(0, 0, texWidth, texHeight);

            return true;
        }

        private static void revertFrameBuffer(int fbo, int fbo_tex)
        {
            //Bring Back screen
            GL.Viewport(0, 0, old_vp_size[2], old_vp_size[3]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            //Delete Fraomebuffer Textures
            GL.DeleteTexture(fbo_tex);
        }

        public static Texture mixDiffuseTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    int active_id = i;
                    GL.Uniform1(loc + i, baseLayersUsed[active_id]);
                    if (baseLayersUsed[i] > 0.0f)
                        baseLayerIndex = i;
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);

            //Upload DiffuseTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 0.0f);

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 1.0f);

            //Upload Recolouring Information
            loc = GL.GetUniformLocation(pass_program, "lRecolours");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, (float)reColourings[i][0],
                                     (float)reColourings[i][1],
                                     (float)reColourings[i][2],
                                     (float)reColourings[i][3]);
                }
            }


            //Upload Average Colors Information
            loc = GL.GetUniformLocation(pass_program, "lAverageColors");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, 0.5f, 0.5f, 0.5f, 0.5f);
                }
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_diffuse = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_diffuse, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_diffuse);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_diffuse);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.width = texWidth;
            new_tex.height = texHeight;
            new_tex.bufferID = out_tex_2darray_diffuse;
            new_tex.target = TextureTarget.Texture2DArray;
            
#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("diffuse", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixMaskTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    } else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                        tex = masktextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);

            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.bufferID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURESNONO)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("mask", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixNormalTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    }
                    else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                        tex = normaltextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.bufferID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("normal", texWidth, texHeight);
#endif
            return new_tex;
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

    public class Texture : IDisposable
    {
        private bool disposed = false;
        public int bufferID = -1;
        public TextureTarget target;
        public string name;
        public int width;
        public int height;
        public InternalFormat pif;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public Vector3 avgColor;
        
        //Empty Initializer
        public Texture() {}
        //Path Initializer
        public Texture(string path)
        {
            DDSImage ddsImage;
            name = path;

            path = Path.Combine(FileUtils.dirpath, path);
            if (!File.Exists(path))
            {
                //throw new System.IO.FileNotFoundException();
                Console.WriteLine("Texture {0} Missing. Using default.dds", path);
                path = "default.dds";
            }
            
            ddsImage = new DDSImage(File.ReadAllBytes(path));


            MVCore.Common.RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + path + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            width = ddsImage.header.dwWidth;
            height = ddsImage.header.dwHeight;
            int blocksize = 16;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext;
                    blocksize = 8;
                    break;
                case (0x35545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                    break;
                case (0x32495441): //ATI2A2XY
                    pif = InternalFormat.CompressedRgRgtc2; //Normal maps are probably never srgb
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = InternalFormat.CompressedSrgbAlphaBptcUnorm;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }
            
            //Temp Variables
            int w = width;
            int h = height;
            int mm_count = ddsImage.header.dwMipMapCount; 
            int depth_count = Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;

            //Upload to GPU
            bufferID = GL.GenTexture();
            target = TextureTarget.Texture2DArray;
            
            GL.BindTexture(target, bufferID);
            
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);
            
            int offset = 0;
            for (int i=0; i < mm_count; i++)
            {
                byte[] tex_data = new byte[temp_size * depth_count];
                Array.Copy(ddsImage.bdata, offset, tex_data, 0, temp_size * depth_count);
                GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, tex_data);
                
                //GL.TexImage3D(target, i, PixelInternalFormat.Rgba8, w, h, depth_count, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                //Console.WriteLine(GL.GetError());

                offset += temp_size * depth_count;

                w = Math.Max(w >> 1, 1);
                h = Math.Max(h >> 1, 1);

                temp_size = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * blocksize;
                //This works only for square textures
                //temp_size = Math.Max(temp_size/4, blocksize);
            }


            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float) Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            int max_level = 0;
            GL.GetTexParameter(target, GetTextureParameter.TextureMaxLevel, out max_level);
            int base_level = 0;
            GL.GetTexParameter(target, GetTextureParameter.TextureBaseLevel, out base_level);

            int maxsize = Math.Max(height, width);
            int p = (int) Math.Floor(Math.Log(maxsize, 2)) + base_level;
            int q = Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
            ddsImage = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (bufferID != -1) GL.DeleteTexture(bufferID);
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        private Vector3 getAvgColor(byte[] pixels)
        {
            //Assume that I have the 4x4 mipmap
            //I need to fetch the first 2 colors and calculate the Average

            MemoryStream ms = new MemoryStream(pixels);
            BinaryReader br = new BinaryReader(ms);

            int color0 = br.ReadUInt16();
            int color1 = br.ReadUInt16();

            br.Close();

            //int rmask = 0x1F << 11;
            //int gmask = 0x3F << 5;
            //int bmask = 0x1F;
            uint temp;

            temp = (uint) (color0 >> 11) * 255 + 16;
            char r0 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            char g0 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            char b0 = (char)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            char r1 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            char g1 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            char b1 = (char)((temp / 32 + temp) / 32);

            char red = (char) (((int) ( r0 + r1)) / 2);
            char green = (char)(((int)(g0 + g1)) / 2);
            char blue = (char)(((int)(b0 + b1)) / 2);
            

            return new Vector3(red / 256.0f, green / 256.0f, blue / 256.0f);
            
        }

        private ulong PackRGBA( char r, char g, char b, char a)
        {
            return (ulong) ((r << 24) | (g << 16) | (b << 8) | a);
        }

        // void DecompressBlockDXT1(): Decompresses one block of a DXT1 texture and stores the resulting pixels at the appropriate offset in 'image'.
        //
        // unsigned long x:						x-coordinate of the first pixel in the block.
        // unsigned long y:						y-coordinate of the first pixel in the block.
        // unsigned long width: 				width of the texture being decompressed.
        // unsigned long height:				height of the texture being decompressed.
        // const unsigned char *blockStorage:	pointer to the block to decompress.
        // unsigned long *image:				pointer to image where the decompressed pixel data should be stored.

        private void DecompressBlockDXT1(ulong x, ulong y, ulong width, byte[] blockStorage, byte[] image)
        {

        }

    }

    public class Joint : model
    {
        public int jointIndex;
        public Vector3 color;

        //Add a bunch of shit for posing
        //public Vector3 _localPosePosition = new Vector3(0.0f);
        //public Matrix4 _localPoseRotation = Matrix4.Identity;
        //public Vector3 _localPoseScale = new Vector3(1.0f);
        public Matrix4 BindMat = Matrix4.Identity; //This is the local Bind Matrix related to the parent joint
        public Matrix4 invBMat = Matrix4.Identity; //This is the inverse of the local Bind Matrix related to the parent
        //DO NOT MIX WITH THE gobject.invBMat which is reverts the transformation to the global space
        
        //Props
        public Matrix4 localPoseMatrix
        {
            get { return _localPoseMatrix; }
            set { _localPoseMatrix = value; updated = true; }
        }

        public Joint()
        {
            type = TYPES.JOINT;   
        }

        protected Joint(Joint input) : base(input)
        {
            this.jointIndex = input.jointIndex;
            this.BindMat = input.BindMat;
            this.invBMat = input.invBMat;
            this.color = input.color;

            meshVao = new GLMeshVao();
            instanceId = meshVao.addInstance(this);
            meshVao.setInstanceWorldMat(instanceId, Matrix4.Identity); //This does not change 
            meshVao.type = TYPES.JOINT;
            meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            meshVao.vao = new MVCore.Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];
        }

        public override void updateMeshInfo()
        {
            
            //We do not apply frustum occlusion on joint objects
            if (Common.RenderOptions.RenderJoints && renderable && (children.Count > 0))
            {
                //Update Vertex Buffer based on the new positions
                float[] verts = new float[2 * children.Count * 3];
                int arraysize = 2 * children.Count * 3 * sizeof(float);

                for (int i = 0; i < children.Count; i++)
                {
                    verts[i * 6 + 0] = worldPosition.X;
                    verts[i * 6 + 1] = worldPosition.Y;
                    verts[i * 6 + 2] = worldPosition.Z;
                    verts[i * 6 + 3] = children[i].worldPosition.X;
                    verts[i * 6 + 4] = children[i].worldPosition.Y;
                    verts[i * 6 + 5] = children[i].worldPosition.Z;
                }

                meshVao.metaData.batchcount = 2 * children.Count;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                meshVao.setInstanceOccludedStatus(instanceId, false);
            } else
                meshVao.setInstanceOccludedStatus(instanceId, true);

            base.updateMeshInfo();
        }

        public override model Clone()
        {
            Joint j = new Joint();
            j.copyFrom(this);

            j.jointIndex = this.jointIndex;
            j.BindMat = this.BindMat;
            j.invBMat = this.invBMat;
            j.color = this.color;

            j.meshVao = new GLMeshVao();
            j.instanceId = j.meshVao.addInstance(j);
            j.meshVao.setInstanceWorldMat(j.instanceId, Matrix4.Identity); //This does not change 
            j.meshVao.type = TYPES.JOINT;
            j.meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            j.meshVao.vao = new MVCore.Primitives.LineSegment(this.children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            j.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];

            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = j;
                j.children.Add(new_child);
            }

            return j;
        }

        //DIsposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            meshVao?.Dispose();
            base.Dispose(disposing);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct GLLight
    {
        [FieldOffset(0)]
        public Vector4 position; //w is renderable
        [FieldOffset(16)]
        public Vector4 color; //w is intensity
        [FieldOffset(32)]
        public Vector4 direction; //w is fov
        [FieldOffset(48)]
        public int falloff;
        [FieldOffset(52)]
        public float type;
        
        public static readonly int SizeInBytes = 64;
    }

    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }

    public class Light : model
    {
        //I should expand the light properties here
        public MVector4 color = new MVector4(1.0f);
        //public GLMeshVao main_Vao;
        public float fov = 360.0f;
        public ATTENUATION_TYPE falloff;
        public LIGHT_TYPE light_type;
        
        public float intensity = 1.0f;
        public Vector3 direction = new Vector3();
        
        public bool update_changes = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;
        public GLLight strct;

        //Properties
        public MVector4 Color
        {
            get {
                return color;
            }

            set
            {
                catchPropertyChanged(color, new PropertyChangedEventArgs("Vec"));
            }
        }

        public float FOV
        {
            get
            {
                return fov;
            }

            set
            {
                fov = value;
                strct.direction.W = MathUtils.radians(fov);
                update_changes = true;
            }
        }

        public float Intensity
        {
            get
            {
                return intensity;
            }

            set
            {
                intensity = value;
                strct.color.W = value;
                update_changes = true;
            }
        }

        public string Attenuation
        {
            get
            {
                return falloff.ToString();
            }

            set
            {
                Enum.TryParse<ATTENUATION_TYPE>(value, out falloff);
                strct.falloff = (int) falloff;
                update_changes = true;
            }
        }

        public override bool IsRenderable
        {
            get
            {
                return renderable;
            }

            set
            {
                strct.position.W = value ? 1.0f : 0.0f;
                base.IsRenderable = value;
                update_changes = true;
            }
        }

        //Add event handler to catch changes to the Vector property

        private void catchPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            MVector4 t = sender as MVector4;
            
            //Update struct
            strct.color.X = t.X;
            strct.color.Y = t.Y;
            strct.color.Z = t.Z;
            update_changes = true;
        }


        public Light()
        {
            type = TYPES.LIGHT;
            fov = 360;
            intensity = 1.0f;
            falloff = ATTENUATION_TYPE.CONSTANT;


            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new MVCore.Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = meshVao.addInstance(this); //Add instance

            //Init projection Matrix
            lightProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathUtils.radians(90), 1.0f, 1.0f, 300f);
            
            //Init lightSpace Matrices
            lightSpaceMatrices = new Matrix4[6];
            for (int i=0; i < 6; i++)
            {
                lightSpaceMatrices[i] = Matrix4.Identity * lightProjectionMatrix;
            }

            //Catch changes to MVector from the UI
            color = new MVector4(1.0f);
            color.PropertyChanged += catchPropertyChanged;
        }

        protected Light(Light input) : base(input)
        {
            Color = input.Color;
            intensity = input.intensity;
            falloff = input.falloff;
            fov = input.fov;
            strct = input.strct;
            
            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new MVCore.Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = meshVao.addInstance(this); //Add instance

            //Copy Matrices
            lightProjectionMatrix = input.lightProjectionMatrix;
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
                lightSpaceMatrices[i] = input.lightSpaceMatrices[i];

            update_struct();
            Common.RenderState.activeResMgr.GLlights.Add(this);
        }

        public override void updateMeshInfo()
        {
            if (Common.RenderOptions.RenderLights && renderable)
            {
                //End Point
                Vector4 ep;
                //Lights with 360 FOV are points
                if (FOV - 360.0f <= 1e-4)
                {
                    ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                    light_type = LIGHT_TYPE.POINT;
                }
                else
                {
                    ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                    light_type = LIGHT_TYPE.SPOT;
                }

                ep = ep * _localRotation;

                //Update Vertex Buffer based on the new data
                float[] verts = new float[6];
                int arraysize = 6 * sizeof(float);

                //Origin Point
                verts[0] = worldPosition.X;
                verts[1] = worldPosition.Y;
                verts[2] = worldPosition.Z;

                ep.X += worldPosition.X;
                ep.Y += worldPosition.Y;
                ep.Z += worldPosition.Z;

                verts[3] = ep.X;
                verts[4] = ep.Y;
                verts[5] = ep.Z;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                
                //Uplod worldMat to the meshVao
                meshVao.setInstanceWorldMat(instanceId, Matrix4.Identity);
                meshVao.setInstanceOccludedStatus(instanceId, false);
                //Console.WriteLine("Updating Light");
            } else
                meshVao.setInstanceOccludedStatus(instanceId, true);

            base.updateMeshInfo();
            updated = false; //All done
        }

        public override void update()
        {
            base.update();

            //End Point
            Vector4 ep;
            //Lights with 360 FOV are points
            if (FOV - 360.0f <= 1e-4)
            {
                ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                light_type = LIGHT_TYPE.POINT;
            }
            else
            {
                ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                light_type = LIGHT_TYPE.SPOT;
            }

            ep = ep * _localRotation;
            ep.Normalize();

            direction = ep.Xyz; //Set spotlight direction
            update_struct();

            //Assume that this is a point light for now
            //Right
            lightSpaceMatrices[0] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Left
            lightSpaceMatrices[1] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(-1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Up
            lightSpaceMatrices[2] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, -1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Down
            lightSpaceMatrices[3] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Near
            lightSpaceMatrices[4] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, 1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Far
            lightSpaceMatrices[5] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, -1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
        }

        public void update_struct()
        {
            Vector4 old_pos = strct.position;
            strct.position = new Vector4(worldPosition, renderable ? 1.0f : 0.0f);
            strct.color = new Vector4(Color.Vec.Xyz, (float) intensity);
            strct.direction = new Vector4(direction, (float) MathUtils.radians(fov));
            strct.falloff = (int) falloff;
            strct.type = (light_type == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f;
            
            if (old_pos != strct.position)
                update_changes = true;
        }

        public override model Clone()
        {
            return new Light(this);
        }

        //Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                base.Dispose(true);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }


    //Animation Classes

    //model Components
    //TODO Move them somewhere else
    public abstract class Component : IDisposable
    {
        public abstract Component Clone();
        
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    };

    
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public List<AnimData> _animations = new List<AnimData>();
        public List<AnimData> Animations
        {
            get
            {
                return _animations;
            }
        }
        
        //Default Constructor
        public AnimComponent()
        {
            
        }

        public AnimComponent(TkAnimationComponentData data)
        {
            //Load Animations
            if (data.Idle.Anim != "")
                _animations.Add(new AnimData(data.Idle)); //Add Idle Animation
            
            for (int i = 0; i < data.Anims.Count; i++)
            {
                //Check if the animation is already loaded
                AnimData my_ad = new AnimData(data.Anims[i]);
                _animations.Add(my_ad);
            }

        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //TODO: Copy Animations
            
        }

        public override Component Clone()
        {
            AnimComponent ac = new AnimComponent();

            //Copy Animations
            foreach (AnimData ad in _animations)
                ac.Animations.Add(ad.Clone());
            
            return ac;
        }

        public void update()
        {
            
        }

        protected AnimComponent(AnimComponent input)
        {
            this.copyFrom(input);
        }

        #region IDisposable Support
        private bool disposed = false; // To detect redundant calls
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        
        #endregion

    }

    public class LODModelComponent: Component
    {
        private List<LODModelResource> _resources;

        //Properties
        public List<LODModelResource> Resources => _resources;

        public LODModelComponent()
        {
            _resources = new List<LODModelResource>();
        }

        public override Component Clone()
        {
            LODModelComponent lmc = new LODModelComponent();
            return lmc;
        }


    }

    public class LODModelResource
    {
        private string _filename;
        private float _crossFadeTime;
        private float _crossFadeoverlap;

        //Properties
        public string Filename
        {
            get
            {
                return _filename;
            }
        }

        public LODModelResource(TkLODModelResource res)
        {
            _filename = res.LODModel.Filename;
            _crossFadeTime = res.CrossFadeTime;
            _crossFadeoverlap = res.CrossFadeOverlap;
        }
    }

    public class AnimPoseComponent: Component
    {
        public model ref_object = null;
        public TkAnimMetadata animMeta = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new List<AnimPoseData>();
        public TkAnimMetadata _poseFrameData = null; //Stores the actual poseFrameData
        public List<AnimPoseData> poseData
        {
            get
            {
                return _poseData;
            }
        }

        public ICommand ApplyPose
        {
            get { return new ApplyPoseCommand(); }
        }

        public ICommand ResetPose
        {
            get { return new ResetPoseCommand(); }
        }

        //Default Constructor
        public AnimPoseComponent()
        {

        }

        public AnimPoseComponent(TkAnimPoseComponentData apcd)
        {
            _poseFrameData = (TkAnimMetadata) NMSUtils.LoadNMSFile(Path.GetFullPath(Path.Combine(FileUtils.dirpath, apcd.Filename)));

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(apcd.PoseAnims[i]);
                poseData.Add(my_apd);
            }
        }

        public override Component Clone()
        {
            return new AnimPoseComponent();
        }

        //ICommands

        private class ApplyPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                ((scene) apc.ref_object.parentScene).applyPoses(apc.ref_object.loadPose());
            }
        }

        private class ResetPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                apc.ref_object.parentScene.resetPoses();
            }
        }

    }


    



    public class AnimNodeFrameData
    {
        public List<OpenTK.Quaternion> rotations = new List<OpenTK.Quaternion>();
        public List<Vector3> translations = new List<Vector3>();
        public List<Vector3> scales = new List<Vector3>();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                OpenTK.Quaternion q = new OpenTK.Quaternion();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                q.W = br.ReadSingle();

                this.rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }
    

    public class AnimPoseData: TkAnimPoseData
    {

        public AnimPoseData(TkAnimPoseData apd)
        {
            Anim = apd.Anim;
            FrameStart = apd.FrameStart;
            FrameEnd = apd.FrameEnd;
            PActivePoseFrame = (int) ((apd.FrameEnd - apd.FrameStart) / 2 + apd.FrameStart);
        }

        public string PAnim
        {
            get
            {
                return Anim;
            }
        }

        public int PFrameStart
        {
            get
            {
                return FrameStart;
            }
            set
            {
                FrameStart = value;
            }
        }

        public int PFrameEnd
        {
            get
            {
                return FrameEnd;
            }
            set
            {
                FrameEnd = value;
            }
        }

        public int PActivePoseFrame
        {
            get; set;
        }



    }

    
    public class AnimMetadata: TkAnimMetadata
    {
        public float duration;
        public float interval;
        public Dictionary<string, Quaternion[]> anim_rotations;
        public Dictionary<string, Vector3[]> anim_positions;
        public Dictionary<string, Vector3[]> anim_scales;

        public AnimMetadata(TkAnimMetadata amd)
        {
            //Copy struct info
            FrameCount = amd.FrameCount;
            NodeCount = amd.NodeCount;
            NodeData = amd.NodeData;
            AnimFrameData = amd.AnimFrameData;
            StillFrameData = amd.StillFrameData;

            anim_rotations = new Dictionary<string, Quaternion[]>();
            anim_positions = new Dictionary<string, Vector3[]>();
            anim_scales = new Dictionary<string, Vector3[]>();

            duration = FrameCount * 1000.0f / MVCore.Common.RenderOptions.animFPS;
            interval = duration / FrameCount;

            //Fetch animation data
            loadData();
        }

        public AnimMetadata()
        {
            duration = 0.0f;
            anim_rotations = new Dictionary<string, Quaternion[]>();
            anim_positions = new Dictionary<string, Vector3[]>();
            anim_scales = new Dictionary<string, Vector3[]>();
        }


        private void loadData()
        {
            for (int j = 0; j < NodeCount; j++)
            {
                TkAnimNodeData node = NodeData[j];
                //Init dictionary entries

                if (anim_rotations.ContainsKey(node.Node))
                    Console.WriteLine("This shoult not happen");

                anim_rotations[node.Node] = new Quaternion[FrameCount];
                anim_positions[node.Node] = new Vector3[FrameCount];
                anim_scales[node.Node] = new Vector3[FrameCount];

                for (int i = 0; i < FrameCount; i++)
                {
                    Quaternion q = NMSUtils.fetchRotQuaternion(node, this, i);
                    Vector3 s = NMSUtils.fetchScaleVector(node, this, i);
                    Vector3 p = NMSUtils.fetchTransVector(node, this, i);

                    //Save Info
                    anim_rotations[node.Node][i] = q;
                    anim_positions[node.Node][i] = p;
                    anim_scales[node.Node][i] = s;
                }
            }
        }
    }

    public class AnimData : TkAnimationData, INotifyPropertyChanged
    {
        public AnimMetadata animMeta;
        public float animationTime = 0.0f;
        public bool _animationToggle = false;
        private int prevFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float LERP_coeff = 0.0f;

        public event PropertyChangedEventHandler PropertyChanged;

        //Constructors
        public AnimData(TkAnimationData ad){
            Anim = ad.Anim; 
            Filename = ad.Filename;
            FrameStart = ad.FrameStart;
            FrameEnd = ad.FrameEnd;
            StartNode = ad.StartNode;
            AnimType = ad.AnimType;
            Speed = ad.Speed;
            Additive = ad.Additive;
            
            //Load Animation File
            if (Filename != "")
                fetchAnimMetaData();
        }

        public AnimData()
        {
            
        }

        public AnimData Clone()
        {
            AnimData ad = new AnimData();
            
            ad.Anim = Anim;
            ad.Filename = Filename;
            ad.FrameStart = FrameStart;
            ad.FrameEnd = FrameEnd;
            ad.StartNode = StartNode;
            ad.AnimType = AnimType;
            ad.Speed = Speed;
            ad.Additive = Additive;
            ad.animMeta = animMeta;

            return ad;
        }
        
        //Properties

        public string PName
        {
            get { return Anim; }
            set { Anim = value; }
        }

        public bool PActive
        {
            get { return Active; }
            set { Active = value; }
        }
        
        public bool AnimationToggle
        {
            get { return _animationToggle; }
            set { _animationToggle = value;
                NotifyPropertyChanged("AnimationToggle"); 
            }
        }

        public bool isValid
        {
            get { return animMeta != null;}
        }

        public string PAnimType
        {
            get
            {
                return AnimType.ToString();
            }
        }

        public bool PAdditive
        {
            get { return Additive; }
            set { Additive = value; }
        }

        public float PSpeed
        {
            get { return Speed; }
            set { Speed = value; }
        }

        //UI update
        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }


        private void fetchAnimMetaData()
        {
            if (MVCore.Common.RenderState.activeResMgr.Animations.ContainsKey(Filename))
            {
                animMeta = MVCore.Common.RenderState.activeResMgr.Animations[Filename];
            }
            else
            {
                TkAnimMetadata amd = NMSUtils.LoadNMSFile(Path.GetFullPath(Path.Combine(FileUtils.dirpath, Filename))) as TkAnimMetadata;
                animMeta = new AnimMetadata(amd);
                MVCore.Common.RenderState.activeResMgr.Animations[Filename] = animMeta;
            }
        }


        public void animate(float dt)
        {
            if (animMeta != null)
            {
                float activeAnimDuration = animMeta.duration / Speed;
                float activeAnimInterval = animMeta.interval / Speed;

                animationTime += dt; //Progress time

                if ((AnimType == AnimTypeEnum.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    _animationToggle = false;
                    AnimationToggle = false;
                    return;
                } 
                else
                    animationTime = animationTime % activeAnimDuration; //Clamp to correct time span

                
                //Find frames
                prevFrameIndex = (int) Math.Floor((double) (animationTime / activeAnimInterval));
                nextFrameIndex = (prevFrameIndex + 1) % animMeta.FrameCount;

                float prevFrameTime = prevFrameIndex * activeAnimInterval;
                LERP_coeff = (animationTime - prevFrameTime) / activeAnimInterval;
            }
        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void applyNodeTransform(model m, string node)
        {
            //Fetch prevFrame stuff
            Quaternion prev_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 prev_p = animMeta.anim_positions[node][prevFrameIndex];

            //Fetch nextFrame stuff
            Quaternion next_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 next_p = animMeta.anim_positions[node][prevFrameIndex];

            //Interpolate

            Quaternion q = Quaternion.Slerp(prev_q, next_q, LERP_coeff);
            Vector3 p = prev_p * LERP_coeff + next_p * (1.0f - LERP_coeff);

            //Convert transforms
            m.localRotation = Matrix4.CreateFromQuaternion(q);
            m.localPosition = p;
        }

    }




        
    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Matrix4 BindMatrix = Matrix4.Identity;

        public void Load(FileStream fs)
        {
            //Binary Reader
            BinaryReader br = new BinaryReader(fs);
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
            Vector3 BindTranslate, BindScale;
            Quaternion BindRotation = new Quaternion();

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
            BindMatrix = Matrix4.CreateScale(BindScale) * Matrix4.CreateFromQuaternion(BindRotation) * Matrix4.CreateTranslation(BindTranslate);

            //Check Results [Except from joint 0, the determinant of the multiplication is always 1,
            // transforms should be good]
            //Console.WriteLine((BindMatrix * invBindMatrix).Determinant);
        }

        
        public float[] convertVec(Vector3 vec)
        {
            float[] fmat = new float[3];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            
            return fmat;
        }

        public float[] convertVec(Vector4 vec)
        {
            float[] fmat = new float[4];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            fmat[3] = vec.W;

            return fmat;
        }

        public float[] convertMat()
        {
            float[] fmat = new float[16];
            fmat[0] = this.invBindMatrix.M11;
            fmat[1] = this.invBindMatrix.M12;
            fmat[2] = this.invBindMatrix.M13;
            fmat[3] = this.invBindMatrix.M14;
            fmat[4] = this.invBindMatrix.M21;
            fmat[5] = this.invBindMatrix.M22;
            fmat[6] = this.invBindMatrix.M23;
            fmat[7] = this.invBindMatrix.M24;
            fmat[8] = this.invBindMatrix.M31;
            fmat[9] = this.invBindMatrix.M32;
            fmat[10] = this.invBindMatrix.M33;
            fmat[11] = this.invBindMatrix.M34;
            fmat[12] = this.invBindMatrix.M41;
            fmat[13] = this.invBindMatrix.M42;
            fmat[14] = this.invBindMatrix.M43;
            fmat[15] = this.invBindMatrix.M44;

            return fmat;
        }

    }
}