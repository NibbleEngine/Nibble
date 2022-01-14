﻿using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Globalization;
using OpenTK;
using NbCore.Math;
using libMBIN;
using libMBIN.NMS.Toolkit;
using System.Linq;
using NbCore;
using Console = System.Console;
using NbCore.Utils;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS;
using NbCore.Common;
using Quaternion = OpenTK.Mathematics.Quaternion;


namespace NibbleNMSPlugin
{
    public static class Importer
    {
        private static readonly Dictionary<Type, int> SupportedComponents = new()
        {
            {typeof(TkAnimPoseComponentData), 0},
            {typeof(TkAnimationComponentData), 1},
            {typeof(TkLODComponentData), 2},
            {typeof(TkPhysicsComponentData), 3},
            {typeof(GcTriggerActionComponentData), 4},
            {typeof(EmptyNode), 5}
        };

        private static TextureManager localTexMgr = new();
        private static Dictionary<long, NbMesh> localMeshDictionary = new();
        private static Dictionary<string, MeshMaterial> localMaterialDictionary = new();
        private static Engine EngineRef;

        public static void SetEngineReference(Engine engine)
        {
            EngineRef = engine;
        }

        private static void ProcessAnimPoseComponent(SceneGraphNode node, TkAnimPoseComponentData component)
        {
            //Load PoseFile
            AnimPoseComponent apc = CreateAnimPoseComponentFromStruct(component);
            apc.ref_object = node; //Set referenced animScene
            node.AddComponent<AnimPoseComponent>(apc);
        }

        private static AnimPoseComponent CreateAnimPoseComponentFromStruct(TkAnimPoseComponentData apcd)
        {
            AnimPoseComponent apc = new AnimPoseComponent();

            apc._poseData = new List<AnimPoseData>();
    
            
            //I really need to recheck the pose data files in order to remember what the fuck
            //I should do with pose animation data
            throw new NotImplementedException();

            
        }

        private static void ProcessAnimationComponent(SceneGraphNode node, TkAnimationComponentData component)
        {
            AnimComponent ac = CreateAnimComponentFromStruct(component);
            node.AddComponent<AnimComponent>(ac);
        }

        private static AnimationData CreateAnimationDataFromStruct(TkAnimationData data)
        {
            AnimationData ad = new AnimationData()
            {
                ActionFrame = data.ActionFrame,
                ActionStartFrame = data.ActionStartFrame,
                Active = data.Active,
                Additive = data.Additive,
                FileName = data.Filename,
                FrameEnd = data.FrameEnd,
                FrameStart = data.FrameStart,
                Mirrored = data.Mirrored,
                Name = data.Anim,
                StartNode = data.StartNode,
                Speed = data.Speed,
            };

            //Load Type
            switch (data.AnimType)
            {
                case TkAnimationData.AnimTypeEnum.Loop:
                    ad.AnimType = AnimationType.Loop;
                    break;
                case TkAnimationData.AnimTypeEnum.OneShot:
                    ad.AnimType = AnimationType.OneShot;
                    break;
            }
            
            //Load Animation data
            TkAnimMetadata metaData = (TkAnimMetadata)FileUtils.LoadNMSTemplate(data.Filename);

            ad.FrameCount = metaData.FrameCount;

            for (int j = 0; j < metaData.NodeCount; j++)
            {
                TkAnimNodeData node = metaData.NodeData[j];
                //Init dictionary entries

                ad.Rotations[node.Node] = new List<NbQuaternion>(metaData.FrameCount);
                ad.Translations[node.Node] = new List<NbVector3>(metaData.FrameCount);
                ad.Scales[node.Node] = new List<NbVector3>(metaData.FrameCount);

                for (int i = 0; i < metaData.FrameCount; i++)
                {
                    ad.Rotations[node.Node].Add(Util.fetchRotQuaternion(node, metaData, i));
                    ad.Translations[node.Node].Add(Util.fetchTransVector(node, metaData, i));
                    ad.Scales[node.Node].Add(Util.fetchScaleVector(node, metaData, i));
                }
            }
            
            
            return ad;
        }

        private static AnimComponent CreateAnimComponentFromStruct(TkAnimationComponentData data)
        {
            AnimComponent ac = new AnimComponent();
            
            //Load Animations
            if (data.Idle.Anim != "")
            {
                AnimationData animData = CreateAnimationDataFromStruct(data.Idle);

                Animation IdleAnim = new Animation()
                {
                    animData = animData
                };
                
                ac._animations.Add(IdleAnim); //Add Idle Animation
                ac._animDict[animData.Name] = ac._animations[0];
            }

            for (int i = 0; i < data.Anims.Count; i++)
            {
                AnimationData animData = CreateAnimationDataFromStruct(data.Anims[i]);
                //Create Animation Instances
                Animation anim = new Animation()
                {
                    animData = animData
                };
                
                ac._animations.Add(anim);
                ac._animDict[anim.animData.Name] = anim;
            }

            return ac;
        }

        private static PhysicsData CreatePhysicsDataFromStruct(TkPhysicsData data)
        {
            PhysicsData pd = new()
            {
                Friction = data.Friction,
                Gravity = data.Gravity,
                Mass = data.Mass,
                RollingFriction = data.RollingFriction
            };

            return pd;
        }

        private static void ProcessPhysicsComponent(SceneGraphNode node, TkPhysicsComponentData component)
        {
            PhysicsComponent pc = new();
            pc.Data = CreatePhysicsDataFromStruct(component.Data);
            node.AddComponent<PhysicsComponent>(pc);
        }

        private static void ProcessTriggerActionComponent(SceneGraphNode node, GcTriggerActionComponentData component)
        {
            TriggerActionComponent tac = new();
            //TODO: Fix that 
            node.AddComponent<TriggerActionComponent>(tac);
        }

        private static void ProcessLODComponent(SceneGraphNode node, TkLODComponentData component)
        {
            //Load all LOD models as children to the node
            LODModelComponent lodmdlcomp = new();
            
            for (int i = 0; i < component.LODModel.Count; i++)
            {
                string filepath = component.LODModel[i].LODModel.Filename;
                PluginState.PluginRef.Log("Loading LOD " + filepath, LogVerbosityLevel.INFO);
                SceneGraphNode so = ImportScene(filepath);
                //Create LOD Resource
                LODModelResource lodres = new()
                {
                    FileName = component.LODModel[i].LODModel.Filename,
                    SceneRef = so,
                };
                lodmdlcomp.Resources.Add(lodres);
            }
            
            node.AddComponent<LODModelComponent>(lodmdlcomp);
        }

        private static void ProcessComponents(SceneGraphNode node, TkAttachmentData attachment)
        {
            if (attachment == null)
                return;

            for (int i = 0; i < attachment.Components.Count; i++)
            {
                NMSTemplate comp = attachment.Components[i];
                Type comp_type = comp.GetType();
                
                if (!SupportedComponents.ContainsKey(comp_type))
                {
                    PluginState.PluginRef.Log("Unsupported Component Type " + comp_type, LogVerbosityLevel.INFO);
                    continue;
                }
                    
                switch (SupportedComponents[comp_type])
                {
                    case 0:
                        ProcessAnimPoseComponent(node, comp as TkAnimPoseComponentData);
                        break;
                    case 1:
                        ProcessAnimationComponent(node, comp as TkAnimationComponentData);
                        break;
                    case 2:
                        ProcessLODComponent(node, comp as TkLODComponentData);
                        break;
                    case 3:
                        ProcessPhysicsComponent(node, comp as TkPhysicsComponentData);
                        break;
                    case 4:
                        ProcessTriggerActionComponent(node, comp as GcTriggerActionComponentData);
                        break;
                    case 5: //Empty Node do nothing
                        break;
                }   
            
            }
            
            //Add default LOD distances
            for (int i = 0; i < attachment.LodDistances.Length; i++)
                node.LODDistances.Add(attachment.LodDistances[i]);
        }

        public static MeshMaterial ImportMaterial(string path, TextureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            TkMaterialData template = FileUtils.LoadNMSTemplate(path) as TkMaterialData;
#if DEBUG
            //Save NMSTemplate to exml
            template.WriteToExml("Temp\\" + template.Name + ".exml");
#endif

            //Make new material based on the template
            MeshMaterial mat = CreateMaterialFromStruct(template, input_texMgr);
            
            mat.texMgr = input_texMgr;
            //TODO: Maybe I can check if the shader is compiled during registration
            NbCore.Platform.Graphics.OpenGL.GLSLShaderConfig shader = EngineRef.CompileMaterialShader(mat, 
                NbCore.Platform.Graphics.OpenGL.SHADER_MODE.DEFFERED);
            EngineRef.renderSys.Renderer.AttachShaderToMaterial(mat, shader);
            return mat;
        }

        public static Sampler CreateSamplerFromStruct(TkMaterialSampler ms, TextureManager texMgr)
        {
            Sampler sam = new Sampler();
            
            switch (ms.Name.Value)
            {
                case "gDiffuseMap":
                case "gNormalMap":
                case "gDiffuse2Map":
                case "gMasksMap":
                    sam.Name = "mpCustomPerMaterial." + ms.Name.Value;
                    sam.Map = ms.Map.Value;
                    sam.texUnit = Util.MapTextureUnit[sam.Name];
                    sam.SamplerID = Util.MapTexUnitToSampler[sam.Name];
                    break;
                default:
                    Callbacks.Log("Not sure how to handle Sampler " + ms.Name.Value, LogVerbosityLevel.WARNING);
                    return null;
            }
            
            //Save texture to material
            string[] split = ms.Map.Value.Split('.');
            
            string temp = "";
            if (sam.Name == "mpCustomPerMaterial.gDiffuseMap")
            {
                //Check if the sampler describes a proc gen texture
                temp = split[0];
                //Construct main filename
                
                string texMbin = temp + ".TEXTURE.MBIN";
                
                //Detect Procedural Texture
                if (FileUtils.NMSFileToArchiveMap.Keys.Contains(texMbin))
                { 
                    TextureMixer.combineTextures(sam.Map, Palettes.paletteSel, ref texMgr);
                    //Override Map
                    sam.isProcGen = true;
                }
            }

            //Load the texture to the sampler
            Util.loadSamplerTexture(sam, texMgr);
            
            return sam;
        }

        public static Dictionary<string, int> MaterialUniformDict = new()
        {
            { "gMaterialColourVec4", 0 },
            { "gMaterialParamsVec4", 1 },
            { "gMaterialSFXVec4", 2 },
            { "gMaterialSFXColVec4", 3 },
            { "gUVScrollStepVec4", 4 },
            { "gDissolveDataVec4", 5 },
            { "gCustomParams01Vec4", 6 }
        };
        
        public static MeshMaterial CreateMaterialFromStruct(TkMaterialData md, TextureManager texMgr)
        {
            MeshMaterial mat = new()
            {
                Name = md.Name,
                Class = md.Class
            };
            
            //Copy flags and uniforms

            for (int i = 0; i < md.Flags.Count; i++)
                mat.add_flag((MaterialFlagEnum) md.Flags[i].MaterialFlag);

            
            //Get Samplers
            for (int i = 0; i < md.Samplers.Count; i++)
            {
                TkMaterialSampler ms = md.Samplers[i];
                Sampler s = CreateSamplerFromStruct(md.Samplers[i], texMgr);
                if (s != null)
                {
                    mat.Samplers.Add(s);
                }
            }
            
            //Get Uniforms
            for (int i = 0; i < md.Uniforms.Count; i++)
            {
                TkMaterialUniform mu = md.Uniforms[i];

                if (!MaterialUniformDict.ContainsKey(mu.Name))
                {
                    PluginState.PluginRef.Log($"Uniform {mu.Name.Value} not yet supported",
                        LogVerbosityLevel.WARNING);
                    continue;
                }
                
                Uniform uf = new()
                {
                    Name = mu.Name,
                    ID = MaterialUniformDict[mu.Name],
                    Values = new(mu.Values.x,
                                mu.Values.y,
                                mu.Values.z,
                                mu.Values.t)
                };
                mat.Uniforms.Add(uf);
            }
            return mat;
        }
    
        
        
        private static bufInfo get_bufInfo_item(int buf_id, int offset, uint stride, int count, int buf_type)
        {
            int sem = buf_id;
            int off = offset;
            NbPrimitiveDataType typ = get_type(buf_type);
            string text = get_shader_sem(buf_id);
            bool normalize = false;
            if (text == "bPosition")
                normalize = true;
            return new bufInfo(sem, typ, count, stride, off, text, normalize);
        }


        private static string get_shader_sem(int buf_id)
        {
            switch (buf_id)
            {
                case 0:
                    return "vPosition"; //Verts
                case 1:
                    return "uvPosition0"; //Verts
                case 2:
                    return "nPosition"; //Verts
                case 3:
                    return "tPosition"; //Verts
                case 4:
                    return "bPosition"; //Verts
                case 5:
                    return "blendIndices"; //Verts
                case 6:
                    return "blendWeights"; //Verts
                default:
                    return "shit"; //Default
            }
        }

        private static NbPrimitiveDataType get_type(int val){

            switch (val)
            {
                case (0x140B):
                    return NbPrimitiveDataType.HalfFloat;
                case (0x1401):
                    return NbPrimitiveDataType.UnsignedByte;
                case (0x8D9F):
                    return NbPrimitiveDataType.Int2101010Rev;
                default:
                    PluginState.PluginRef.Log("Unknown VERTEX SECTION TYPE-----------------------------------", LogVerbosityLevel.WARNING);
                    throw new ApplicationException("NEW VERTEX SECTION TYPE. FIX IT ASSHOLE...");
                    //return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
            }
        }

        private static int get_type_count(int val)
        {

            switch (val)
            {
                case (0x140B):
                    return 4;
                case (0x1401):
                    return 1;
                default:
                    PluginState.PluginRef.Log("Unknown VERTEX SECTION TYPE-----------------------------------",
                        LogVerbosityLevel.INFO);
                    return 1;
            }
        }

        private static string getDescr(List<bufInfo> lBufInfo)
        {
            string mesh_desc = "";


            for (int i = 0; i < lBufInfo.Count; i++)
            {
                switch (lBufInfo[i].semantic)
                {
                    case 0:
                        mesh_desc += "v"; //Verts
                        break;
                    case 1:
                        mesh_desc += "u"; //UVs
                        break;
                    case 2:
                        mesh_desc += "n"; //Normals
                        break;
                    case 3:
                        mesh_desc += "t"; //Tangents
                        break;
                    case 4:
                        mesh_desc += "p"; //Vertex Color
                        break;
                    case 5:
                        mesh_desc += "b"; //BlendIndices
                        break;
                    case 6:
                        mesh_desc += "w"; //BlendWeights
                        break;
                    default:
                        mesh_desc += "x"; //Default
                        break;
                }
                
            }

            return mesh_desc;
        }

        
        private static GeomObject ImportGeometry(ref Stream fs, ref Stream gfs)
        {
            //FileStream testfs = new FileStream("test.geom", FileMode.CreateNew);
            //byte[] fs_data = new byte[fs.Length];
            //fs.Read(fs_data, 0, (int) fs.Length);
            //testfs.Write(fs_data, 0, (int) fs.Length);
            //testfs.Close();

            BinaryReader br = new(fs);
            PluginState.PluginRef.Log("Parsing Geometry MBIN", LogVerbosityLevel.INFO);

            fs.Seek(0x60, SeekOrigin.Begin);

            var vert_num = br.ReadInt32();
            var indices_num = br.ReadInt32();
            var indices_flag = br.ReadInt32();
            var collision_index_count = br.ReadInt32();

            PluginState.PluginRef.Log($"Model Vertices: {vert_num}", LogVerbosityLevel.INFO);
            PluginState.PluginRef.Log($"Model Indices: {indices_num}", LogVerbosityLevel.INFO);
            PluginState.PluginRef.Log($"Indices Flag: {indices_flag}", LogVerbosityLevel.INFO);
            PluginState.PluginRef.Log($"Collision Index Count: {collision_index_count}", LogVerbosityLevel.INFO);

            //Joint Bindings
            var jointbindingOffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var jointCount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            //Skip Unknown yet offset sections
            //Joint Bindings
            //Joint Extensions
            //Joint Mirror Pairs
            //Joint Mirror Axes
            fs.Seek(3 * 0x10, SeekOrigin.Current);

            //Usefull Bone Remapping information

            var skinmatoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bc = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //Vertstarts
            var vsoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var partcount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            
            //VertEnds
            fs.Seek(0x10, SeekOrigin.Current);

            //Bound Hull Vert start
            var boundhull_vertstart_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Vert end
            var boundhull_vertend_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //MatrixLayouts
            fs.Seek(0x10, SeekOrigin.Current);

            //BoundBoxes
            var bboxminoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            var bboxmaxoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Verts
            var bhulloffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bhull_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);


            var lod_count = br.ReadInt32();
            var vx_type = br.ReadUInt32();
            PluginState.PluginRef.Log($"Buffer Count: {lod_count} VxType {vx_type}", LogVerbosityLevel.INFO);
            fs.Seek(0x8, SeekOrigin.Current);
            var mesh_descr_offset = fs.Position + br.ReadInt64();
            var buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //Parse Small Vertex Layout Info
            var small_bufcount = br.ReadInt32();
            var small_vx_type = br.ReadUInt32();
            PluginState.PluginRef.Log($"Small Buffer Count: {small_bufcount} VxType {small_vx_type}", LogVerbosityLevel.INFO);
            fs.Seek(0x8, SeekOrigin.Current);
            var small_mesh_descr_offset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            br.ReadInt32(); //Skip second buf count
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //fs.Seek(0x20, SeekOrigin.Current); //Second lod offsets

            //Get primary geom offsets
            var indices_offset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current); //Skip Section Sizes and a 1

            var meshMetaData_offset = fs.Position + br.ReadInt64();
            var meshMetaData_counter = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skip Section Sizes and a 1

            //fs.Seek(0x10, SeekOrigin.Current);

            //Initialize geometry object
            var geom = new GeomObject();
            
            //Store Counts
            geom.indicesCount = indices_num;
            int indexByteLength = 0x4;
            if (indices_flag == 0x1)
            {
                geom.indicesType = NbPrimitiveDataType.UnsignedShort;
                indexByteLength = 0x2;
            }
            else
            {
                geom.indicesType = NbPrimitiveDataType.UnsignedInt;
            }
                
            geom.vertCount = vert_num;
            geom.vx_size = vx_type;
            geom.small_vx_size = small_vx_type;

            //Get Bone Remapping Information
            //I'm 99% sure that boneRemap is not a case in NEXT models
            //it is still there though...
            fs.Seek(skinmatoffset, SeekOrigin.Begin);
            geom.boneRemap = new short[bc];
            for (int i = 0; i < bc; i++)
                geom.boneRemap[i] = (short) br.ReadInt32();

            //Store Joint Data
            fs.Seek(jointbindingOffset, SeekOrigin.Begin);
            geom.jointCount = jointCount;
            for (int i = 0; i < jointCount; i++)
            {
                JointBindingData jdata = new();
                jdata.Load(fs);
                //Copy Matrix
                Array.Copy(MathUtils.convertMat(jdata.invBindMatrix), 0, geom.invBMats, 16 * i, 16);
                //Store the struct
                geom.jointData.Add(jdata);
            }

            //Get Vertex Starts
            //I'm fetching that just for getting the object id within the geometry file
            fs.Seek(vsoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
                geom.vstarts.Add(br.ReadInt32());
        
            //Get BBoxes
            //Init first
            for (int i = 0; i < partcount; i++)
            {
                NbVector3[] bb = new NbVector3[2];
                geom.bboxes.Add(bb);
            }

            fs.Seek(bboxminoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++) {
                geom.bboxes[i][0] = new NbVector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            fs.Seek(bboxmaxoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bboxes[i][1] = new NbVector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            //Get BoundHullStarts
            fs.Seek(boundhull_vertstart_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullstarts.Add(br.ReadInt32());
            }

            //Get BoundHullEnds
            fs.Seek(boundhull_vertend_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullends.Add(br.ReadInt32());
            }

            //TODO : Recheck and fix that shit
            fs.Seek(bhulloffset, SeekOrigin.Begin);
            for (int i = 0; i < bhull_count; i++)
            {
                geom.bhullverts.Add(new NbVector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                br.ReadBytes(4);
            }

            //Get indices buffer
            fs.Seek(indices_offset, SeekOrigin.Begin);
            geom.ibuffer = new byte[indices_num * indexByteLength];
            fs.Read(geom.ibuffer, 0, indices_num * indexByteLength);

            //Get MeshMetaData
            fs.Seek(meshMetaData_offset, SeekOrigin.Begin);
            for (int i = 0; i < meshMetaData_counter; i++)
            {
                geomMeshMetaData mmd = new()
                {
                    name = StringUtils.read_string(br, 0x80),
                    hash = br.ReadUInt64(),
                    vs_size = br.ReadUInt32(),
                    vs_abs_offset = br.ReadUInt32(),
                    is_size = br.ReadUInt32(),
                    is_abs_offset = br.ReadUInt32()
                };
                geom.meshMetaDataDict[mmd.hash] = mmd;
                PluginState.PluginRef.Log(mmd.name, LogVerbosityLevel.INFO);
            }
        
            //Get main mesh description
            fs.Seek(mesh_descr_offset, SeekOrigin.Begin);
            var mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            //Set size excplicitly to 7
            
            for (int i = 0; i < buf_count; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();
                var buf_stride = geom.vx_size;
                //var buf_test1 = br.ReadInt32();
                //var buf_test2 = br.ReadInt32();
                //var buf_test3 = br.ReadInt32();
                //var buf_test4 = br.ReadInt32();
                geom.bufInfo.Add(get_bufInfo_item(buf_id, buf_localoffset, buf_stride, buf_elem_count, buf_type));
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Descr
            mesh_desc = getDescr(geom.bufInfo);
            PluginState.PluginRef.Log("Mesh Description: " + mesh_desc, LogVerbosityLevel.INFO);

            //Store description
            geom.mesh_descr = mesh_desc;
            //Get small description
            fs.Seek(small_mesh_descr_offset, SeekOrigin.Begin);
            var small_mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            
            for (int i = 0; i < small_bufcount; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();

                bufInfo buf = get_bufInfo_item(buf_id,
                    buf_localoffset, geom.small_vx_size, buf_elem_count, buf_type);
                geom.smallBufInfo.Add(buf);
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Small Descr
            small_mesh_desc = getDescr(geom.smallBufInfo);
            PluginState.PluginRef.Log("Small Mesh Description: " + small_mesh_desc, LogVerbosityLevel.INFO);

            //Store description
            geom.small_mesh_descr = small_mesh_desc;
            //Set geom interleaved
            geom.interleaved = true;

            //Load streams from the geometry stream file
            
            foreach (KeyValuePair<ulong, geomMeshMetaData> pair in geom.meshMetaDataDict)
            {
                geomMeshMetaData mmd = pair.Value;
                NbMeshData md = new()
                {
                    Hash = mmd.hash,
                    VertexBuffer = new byte[mmd.vs_size],
                    IndexBuffer = new byte[mmd.is_size],
                    buffers = geom.bufInfo.ToArray(),
                    VertexBufferStride = geom.vx_size
                };

                //Fetch Buffers
                gfs.Seek((int) mmd.vs_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.VertexBuffer, 0, (int) mmd.vs_size);

                gfs.Seek((int) mmd.is_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.IndexBuffer, 0, (int) mmd.is_size);


                //Calculate vertex count on stream
                uint vx_count = mmd.vs_size / geom.vx_size;

                md.IndicesLength = NbPrimitiveDataType.UnsignedShort;
                if (vx_count > 0xFFFF)
                    md.IndicesLength = NbPrimitiveDataType.UnsignedInt;

                geom.meshDataDict[mmd.hash] = md;
            }

            return geom;

        }
        
        public static SceneGraphNode ImportScene(string path)
        {
            TkSceneNodeData template = (TkSceneNodeData)FileUtils.LoadNMSTemplate(path);

            PluginState.PluginRef.Log("Loading Objects from MBINFile", LogVerbosityLevel.INFO);

            string sceneName = template.Name;
            PluginState.PluginRef.Log(string.Format("Trying to load Scene {0}", sceneName), LogVerbosityLevel.INFO);
            string[] split = sceneName.Split('\\');
            string scnName = split[^1];
            Callbacks.updateStatus("Importing Scene: " + scnName);
            PluginState.PluginRef.Log(string.Format("Importing Scene: {0}", scnName), LogVerbosityLevel.INFO);
            
            //Get Geometry File
            //Parse geometry once
            string geomfile = FileUtils.parseNMSTemplateAttrib(template.Attributes, "GEOMETRY");
            int num_lods = int.Parse(FileUtils.parseNMSTemplateAttrib(template.Attributes, "NUMLODS"));

            GeomObject gobject;
            if (RenderState.engineRef.renderSys.GeometryMgr.HasGeom(geomfile))
            {
                //Load from dict
                gobject = RenderState.engineRef.renderSys.GeometryMgr.GetGeom(geomfile);

            } else
            {

#if DEBUG
                //Use libMBIN to decompile the file
                TkGeometryData geomdata = (TkGeometryData)FileUtils.LoadNMSTemplate(geomfile + ".PC");
                //Save NMSTemplate to exml
                string xmlstring = EXmlFile.WriteTemplate(geomdata);
                File.WriteAllText("Temp\\temp_geom.exml", xmlstring);
#endif
                //Load Gstream and Create gobject

                Stream fs, gfs;
                
                fs = FileUtils.LoadNMSFileStream(geomfile + ".PC");

                //Try to fetch the geometry.data.mbin file in order to fetch the geometry streams
                string gstreamfile = "";
                split = geomfile.Split('.');
                for (int i = 0; i < split.Length - 1; i++)
                    gstreamfile += split[i] + ".";
                gstreamfile += "DATA.MBIN.PC";

                gfs = FileUtils.LoadNMSFileStream(gstreamfile);

                FileStream gffs = new FileStream("testfilegeom.mbin", FileMode.Create);
                gfs.CopyTo(gffs);
                gffs.Close();

                if (fs is null)
                {
                    Callbacks.showError("Could not find geometry file " + geomfile + ".PC", "Error");
                    PluginState.PluginRef.Log(string.Format("Could not find geometry file {0} ", geomfile + ".PC"), LogVerbosityLevel.ERROR);

                    //Create Dummy Scene
                    SceneGraphNode dummy = new(SceneNodeType.MODEL)
                    {
                        Name = "DUMMY_SCENE"
                    };
                    return null;
                }

                gobject = ImportGeometry(ref fs, ref gfs);
                gobject.Name = geomfile;
                PluginState.PluginRef.Log(string.Format("Geometry file {0} successfully parsed",
                    geomfile + ".PC"), LogVerbosityLevel.INFO);
                
                fs.Close();
                gfs.Close();
            }

            //Random Generetor for colors
            Random randgen = new();

            //Parse root scene
            SceneGraphNode root = CreateNodeFromTemplate(template, gobject, null);
            gobject.Dispose();
            
            return root;
        }

        private static SceneGraphNode CreateNodeFromTemplate(TkSceneNodeData node, 
            GeomObject gobject, SceneGraphNode parent)
        {
            PluginState.PluginRef.Log(string.Format("Importing Node {0}", node.Name.Value), 
                LogVerbosityLevel.INFO);
            Callbacks.updateStatus($"Importing Part: {node.Name.Value}");

            if (!Enum.TryParse(node.Type, out SceneNodeType typeEnum))
                throw new Exception("Node Type " + node.Type.Value + "Not supported");

            SceneGraphNode so = new(typeEnum)
            {
                Name = node.Name.Value,
                NameHash = node.NameHash
            };


            ////Angle Test
            //NbMatrix4 rotx = NbMatrix4.CreateRotationX(MathUtils.radians(node.Transform.RotX));
            //NbMatrix4 roty = NbMatrix4.CreateRotationY(MathUtils.radians(node.Transform.RotY));
            //NbMatrix4 rotz = NbMatrix4.CreateRotationZ(MathUtils.radians(node.Transform.RotZ));
            //NbMatrix4 rot = rotz * rotx * roty;

            //NbQuaternion test_q = NbQuaternion.FromMatrix(rot);
            //NbVector3 test_q_angles = NbQuaternion.ToEulerAngles(test_q);
            //test_q_angles.X = MathUtils.degrees(test_q_angles.X);
            //test_q_angles.Y = MathUtils.degrees(test_q_angles.Y);
            //test_q_angles.Z = MathUtils.degrees(test_q_angles.Z);

            //Engine way
            NbQuaternion engine_q = NbQuaternion.FromEulerAngles(MathUtils.radians(node.Transform.RotX),
                                                                 MathUtils.radians(node.Transform.RotY),
                                                                 MathUtils.radians(node.Transform.RotZ), "YXZ");

            //Transform rotations to XYZ mode
            NbVector3 rotations = NbQuaternion.ToEulerAngles(engine_q);


            //Add Transform Component
            TransformData td = new(node.Transform.TransX,
                                   node.Transform.TransY,
                                   node.Transform.TransZ,
                                   MathUtils.degrees(rotations.X),
                                   MathUtils.degrees(rotations.Y),
                                   MathUtils.degrees(rotations.Z),
                                   node.Transform.ScaleX,
                                   node.Transform.ScaleY,
                                   node.Transform.ScaleY);
            TransformComponent tc = new(td);
            so.AddComponent<TransformComponent>(tc);

            //Set Parent after the transform component has been initialized
            if (parent != null)
                so.SetParent(parent);
            
            //For now fetch only one attachment
            string attachment = FileUtils.parseNMSTemplateAttrib(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = FileUtils.LoadNMSTemplate(attachment) as TkAttachmentData;
            }

            //Process Attachments
            //TODO: Skip components for now
            //ProcessComponents(so, attachment_data);

            if (typeEnum == SceneNodeType.MESH)
            {
                PluginState.PluginRef.Log(string.Format("Parsing Mesh {0}", node.Name.Value),
                    LogVerbosityLevel.INFO);

                //Get Material Name
                string matname = FileUtils.parseNMSTemplateAttrib(node.Attributes, "MATERIAL");

                //Search for the material

                //TODO: Restore material import
                MeshMaterial mat;
                if (localMaterialDictionary.ContainsKey(matname))
                    mat = localMaterialDictionary[matname];
                else
                {
                    mat = ImportMaterial(matname, localTexMgr);
                    localMaterialDictionary.Add(matname, mat);
                }

                //Fill Mesh Meta Data
                NbMeshMetaData mmd = new()
                {
                    BatchStartPhysics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTARTPHYSI")),
                    VertrStartPhysics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTARTPHYSI")),
                    VertrEndPhysics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRENDPHYSICS")),
                    BatchStartGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTARTGRAPH")),
                    BatchCount = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHCOUNT")),
                    VertrStartGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTARTGRAPH")),
                    VertrEndGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRENDGRAPHIC")),
                    FirstSkinMat = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "FIRSTSKINMAT")),
                    LastSkinMat = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "LASTSKINMAT")),
                    LODLevel = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "LODLEVEL")),
                    BoundHullStart = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLST")),
                    BoundHullEnd = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLED")),
                    AABBMIN = new NbVector3(MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINX")),
                                          MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINY")),
                                          MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINZ"))),
                    AABBMAX = new NbVector3(MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXX")),
                                          MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXY")),
                                          MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXZ"))),
                    Hash = ulong.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "HASH"))
                };

                //Common.Callbacks.Log(string.Format("Randomized Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]), Common.LogVerbosityLevel.INFO);
                PluginState.PluginRef.Log(string.Format("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3} Vertex Graphics {4} - {5} SkinMats {6}-{7}",
                    mmd.BatchStartPhysics, mmd.BatchCount, mmd.VertrStartPhysics, mmd.VertrEndPhysics, mmd.VertrStartGraphics, mmd.VertrEndGraphics,
                    mmd.FirstSkinMat, mmd.LastSkinMat), LogVerbosityLevel.INFO);

                PluginState.PluginRef.Log($"Object {so.Name}, Number of skinmatrices required: {mmd.LastSkinMat - mmd.FirstSkinMat}",
                    LogVerbosityLevel.INFO);

                //Configure boneRemap properly in the mesh metadata
                mmd.BoneRemapIndicesCount = mmd.LastSkinMat - mmd.FirstSkinMat;
                mmd.BoneRemapIndices = new int[mmd.BoneRemapIndicesCount];
                for (int i = 0; i < mmd.BoneRemapIndicesCount; i++)
                    mmd.BoneRemapIndices[i] = gobject.boneRemap[mmd.FirstSkinMat + i];

                //Set skinned flag
                if (mmd.BoneRemapIndicesCount > 0)
                    mmd.skinned = true;

                //Load Mesh Data
                NbMeshData md = gobject.GetMeshData(mmd.Hash); //TODO: Check that function

                //Generate NbMesh
                NbMesh nm = new();
                //TODO differentiate mesh from mesh stream hashes, technically 
                //another mesh should be able to use the same data with a different hash
                nm.Hash = (ulong)mmd.GetHashCode() ^ md.Hash;
                nm.Data = md;
                nm.MetaData = mmd;

                //Set skinned flag if its set as a material flag
                //if (mat.has_flag(MaterialFlagEnum._F02_SKINNED))
                //    mmd.skinned = true;

                //Finally Add MeshComponent
                MeshComponent mc = new()
                {
                    Mesh = nm,
                    Material = mat
                };

                //TODO Process the corresponding mesh if needed
                so.AddComponent<MeshComponent>(mc);



            }
            else if (typeEnum == SceneNodeType.MODEL)
            {
                //Create MeshComponent
                MeshComponent mc = new()
                {
                    Mesh = RenderState.engineRef.GetPrimitiveMesh((ulong)"default_cross".GetHashCode()),
                    Material = RenderState.engineRef.GetMaterialByName("crossMat")
                };

                so.AddComponent<MeshComponent>(mc);

                //Create SceneComponent
                SceneComponent sc = new()
                {
                    NumLods = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "NUMLODS"))
                };

                so.AddComponent<SceneComponent>(sc);

                //Fetch extra LOD attributes
                for (int i = 1; i < sc.NumLods; i++)
                {
                    float attr_val = MathUtils.FloatParse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "LODDIST" + i));
                    sc.LODDistances.Add(attr_val);
                }

            }
            else if (typeEnum == SceneNodeType.LOCATOR)
            {
                //Create MeshComponent
                MeshComponent mc = new()
                {
                    Mesh = EngineRef.GetPrimitiveMesh((ulong)"default_cross".GetHashCode()),
                    Material = EngineRef.GetMaterialByName("crossMat")
                };

                so.AddComponent<MeshComponent>(mc);
            }
            else if (typeEnum == SceneNodeType.JOINT)
            {
                PluginState.PluginRef.Log("Joints not supported atm", LogVerbosityLevel.WARNING);
            }
            else if (typeEnum == SceneNodeType.REFERENCE)
            {
                //Create Reference Component
                ReferenceComponent rc = new()
                {
                    Reference = FileUtils.parseNMSTemplateAttrib(node.Attributes, "SCENEGRAPH").ToUpper()
                };

                so.AddComponent<ReferenceComponent>(rc);
                
                SceneGraphNode ref_node = ImportScene(rc.Reference);
                
                ref_node.SetParent(so);

            }
            else if (typeEnum == SceneNodeType.COLLISION)
            {
                string collisionType = FileUtils.parseNMSTemplateAttrib(node.Attributes, "TYPE").ToUpper();
                PluginState.PluginRef.Log($"Collision Detected {node.Name.Value} {collisionType}", LogVerbosityLevel.INFO);

                MeshMaterial collisionMat = EngineRef.GetMaterialByName("collisionMat");

                //Create Collision Component
                CollisionComponent cc = new();

                //Create MeshComponent
                MeshComponent mc = new()
                {
                    Material = collisionMat
                };

                //Get Collision Mesh
                if (collisionType == "MESH")
                {
                    cc.CollisionType = COLLISIONTYPES.MESH;

                    //Generate Collision Mesh
                    //Fill Mesh Meta Data
                    NbMeshMetaData mmd = new()
                    {
                        BatchStartGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTART")),
                        BatchCount = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHCOUNT")),
                        VertrStartGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTART")),
                        VertrEndGraphics = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VERTREND")),
                        FirstSkinMat = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "FIRSTSKINMAT")),
                        LastSkinMat = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "LASTSKINMAT")),
                        BoundHullStart = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLST")),
                        BoundHullEnd = int.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLED")),
                    };


                    if (mmd.LastSkinMat - mmd.FirstSkinMat > 0)
                    {
                        throw new Exception("SKINNED COLLISION. CHECK YOUR SHIT!");
                    }

                    NbMeshData md = gobject.GetCollisionMeshData(mmd);

                    //Generate Mesh
                    mc.Mesh = new()
                    {
                        Hash = (ulong)mmd.GetHashCode() ^ (ulong)"CollisionMesh".GetHashCode(),
                        Type = NbMeshType.Collision,
                        Data = md,
                        MetaData = mmd
                    };
                }
                else if (collisionType == "CAPSULE")
                {
                    float radius = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "RADIUS"));
                    float height = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "HEIGHT"));

                    //Default quad
                    NbCore.Primitives.Capsule q = new(new(0.0f), height, radius);


                    NbMeshData md = q.geom.GetData();
                    NbMeshMetaData mmd = q.geom.GetMetaData();

                    q.Dispose();
                    
                    //Generate Mesh
                    mc.Mesh = new()
                    {
                        Hash = (ulong) mmd.GetHashCode() ^ (ulong) "CollisionCapsule".GetHashCode(),
                        Type = NbMeshType.Collision,
                        Data = md,
                        MetaData = mmd
                    };
                }
                else if (collisionType == "SPHERE")
                {
                    float radius = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "RADIUS"));
                    
                    //Default quad
                    NbCore.Primitives.Sphere q = new(new(0.0f), radius);
                        
                    NbMeshData md = q.geom.GetData();
                    NbMeshMetaData mmd = q.geom.GetMetaData();

                    q.Dispose();

                    //Generate Mesh
                    mc.Mesh = new()
                    {
                        Hash = (ulong)mmd.GetHashCode() ^ (ulong)"CollisionSphere".GetHashCode(),
                        Type = NbMeshType.Collision,
                        Data = md,
                        MetaData = mmd
                    };
                }
                else
                {
                    PluginState.PluginRef.Log($"Unsupported collision type {collisionType}", LogVerbosityLevel.WARNING);
                }

                //Add Mesh component to node
                so.AddComponent<MeshComponent>(mc);

                //Add Collision component to node
                so.AddComponent<CollisionComponent>(cc);

            }
            else if (typeEnum == SceneNodeType.LIGHT)
            {
                //Parse extra light attributes
                float fov = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "FOV"));
                string falloff = FileUtils.parseNMSTemplateAttrib(node.Attributes, "FALLOFF");
                float falloff_rate = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "FALLOFF_RATE"));
                float intensity = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "INTENSITY"));
                float color_r = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "COL_R"));
                float color_g = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "COL_G"));
                float color_b = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "COL_B"));
                float volumetric = float.Parse(FileUtils.parseNMSTemplateAttrib(node.Attributes, "VOLUMETRIC"));

                //Add Mesh Component
                NbCore.Primitives.LineSegment ls = new NbCore.Primitives.LineSegment(2, new NbVector3(1.0f, 0.0f, 0.0f));
                MeshComponent mc = new()
                {
                    Mesh = new()
                    {
                        Type = NbMeshType.Light,
                        MetaData = ls.geom.GetMetaData(),
                        Data = ls.geom.GetData(),
                        Hash = (ulong) (so.Name.GetHashCode() ^ DateTime.Now.GetHashCode())
                    },
                    Material = EngineRef.GetMaterialByName("lightMat")
                };

                so.AddComponent<MeshComponent>(mc);
                ls.Dispose();

                //Add Light Component
                LightComponent lc = new()
                {
                    Mesh = EngineRef.renderSys.GeometryMgr.GetPrimitiveMesh((ulong)"default_light_sphere".GetHashCode()),
                    Material = EngineRef.GetMaterialByName("lightMat"),
                    Data = new()
                    {
                        Intensity = intensity,
                        FOV = fov,
                        IsRenderable = true,
                        Falloff = (ATTENUATION_TYPE)Enum.Parse(typeof(ATTENUATION_TYPE), falloff.ToUpper()),
                        Color = new NbVector3(color_r, color_g, color_b),
                        IsUpdated = true
                    }
                };
                so.AddComponent<LightComponent>(lc);
            
            }
            else if (typeEnum == SceneNodeType.EMITTER)
            {
                Callbacks.Log("Emmiters not supported atm", LogVerbosityLevel.WARNING);
            } else
            {
                Callbacks.Log("Unknown scenenode type. Please contant the developer", LogVerbosityLevel.WARNING);
            }

            //PluginState.PluginRef.Log("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in node.Children)
            {
                CreateNodeFromTemplate(child, gobject, so);
            }

            //Finally Order children by name
            so.Children.Sort((a, b) => string.Compare(a.Name, b.Name));

            return so;
        }
 
    }
    
    
}
