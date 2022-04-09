using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using NbCore;
using NbCore.Systems;
using NbCore.Common;
using NbCore.Input;
using NbCore.Math;
using NbCore.Primitives;
using NbCore.Utils;
using NbCore.Plugins;
using System.Timers;
using NbCore.Platform.Graphics.OpenGL; //Add an implementation independent shader definition
using OpenTK.Windowing.GraphicsLibraryFramework; //TODO: figure out how to remove that shit
using OpenTK.Windowing.Desktop; //TODO: figure out how to remove that shit
using System.IO;
using System.Reflection;
using Font = NbCore.Text.Font;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using System.Diagnostics;

namespace NbCore
{
    public enum EngineRenderingState
    {
        EXIT = 0x0,
        UNINITIALIZED,
        PAUSED,
        ACTIVE
    }

    public class Engine : EngineSystem
    {
        //Init Systems
        private EntityRegistrySystem registrySys;
        public TransformationSystem transformSys;
        public ActionSystem actionSys;
        public AnimationSystem animationSys;
        public SceneManagementSystem sceneMgmtSys;
        public RenderingSystem renderSys; //TODO: Try to make it private. Noone should have a reason to access it
        private readonly RequestHandler reqHandler;

        private Dictionary<EngineSystemEnum, EngineSystem> _engineSystemMap = new(); //TODO fill up

        //Events
        public delegate void NewSceneEventHandler(SceneGraph s);
        public NewSceneEventHandler NewSceneEvent;

        //Plugin List
        public Dictionary<string, PluginBase> Plugins = new();

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        public Engine(NativeWindow win) : base(EngineSystemEnum.CORE_SYSTEM)
        {
            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller
            reqHandler = new RequestHandler();

            InitSystems();

            LoadDefaultResources();
        }

        ~Engine()
        {
            Log("Goodbye!", LogVerbosityLevel.INFO);
        }

        private void InitSystems()
        {
            //Systems Init
            renderSys = new RenderingSystem(); //Init renderManager of the engine
            registrySys = new EntityRegistrySystem();
            actionSys = new ActionSystem();
            animationSys = new AnimationSystem();
            transformSys = new TransformationSystem();
            sceneMgmtSys = new SceneManagementSystem();

            SetEngine(this);
            renderSys.SetEngine(this);
            registrySys.SetEngine(this);
            actionSys.SetEngine(this);
            animationSys.SetEngine(this);
            transformSys.SetEngine(this);
            sceneMgmtSys.SetEngine(this);
        }

        #region plugin_loader

        private AssemblyName GetAssemblyName(string name)
        {
            //Fetch AssemblyName
            AssemblyName aName = null;
            try
            {
                aName = AssemblyName.GetAssemblyName(name);
            }
            catch (FileNotFoundException)
            {
                var plugindirectory = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins");
                var path = Path.Join(plugindirectory, name);

                if (File.Exists(path))
                {
                    aName = AssemblyName.GetAssemblyName(path);
                } else
                {
                    Log($"Unable to find assembly {name}", LogVerbosityLevel.WARNING);
                }
            }

            return aName;
        }

        private Assembly GetAssembly(AssemblyName aName)
        {
            Assembly a = null;
            try
            {
                //First try to load using the assembly name just in case its a system dll    
                a = Assembly.Load(aName);
            }
            catch (FileNotFoundException ex)
            {
                Log($"Unable to load assembly {aName.Name}, Looking in plugin directory...", LogVerbosityLevel.WARNING);
                //Look in plugin directory
                var plugindirectory = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Plugins");
                var path = Path.Join(plugindirectory, aName.Name + ".dll");

                if (File.Exists(path))
                {
                    a = Assembly.LoadFrom(path);
                } else
                {
                    Log($"Unable to load assembly {aName.Name}, Error: {ex.Message}", LogVerbosityLevel.WARNING);
                }
            }

            return a;
        }

        private System.Version GetAssemblyRequiredNibbleVersion(Assembly test)
        {
            //Load Referenced Assemblies
            AssemblyName[] l = test.GetReferencedAssemblies();

            foreach (AssemblyName a2 in l)
            {
                //Check version compatibility with the Nibble library
                if (a2.Name == "Nibble")
                {
                    return a2.Version;
                }
            }
            return null;
        }

        private void LoadAssembly(string name)
        {
            AssemblyName aName = GetAssemblyName(name);

            if (aName == null)
                return;

            Assembly test = GetAssembly(aName);
            if (test == null)
                return;

            //FetchAssembly
            AppDomain.CurrentDomain.Load(test.GetName());
            Log($"Loaded Assembly {test.GetName()}", LogVerbosityLevel.WARNING);

            //Load Referenced Assemblies
            AssemblyName[] l = test.GetReferencedAssemblies();
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (AssemblyName a2 in l)
            {
                var asm = loadedAssemblies.FirstOrDefault(a => a.FullName == a2.FullName);

                if (asm == null)
                {
                    LoadAssembly(a2.Name + ".dll");
                }
            }

        }

        public void LoadPlugin(string filepath)
        {

            //Load Assembly
            try
            {
                //Get Nibble Info
                AssemblyName NibbleName = GetAssemblyName("Nibble.dll");

                Assembly a = Assembly.LoadFile(Path.GetFullPath(filepath));

                //Try to find the type the derived plugin class
                foreach (Type t in a.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(PluginBase)))
                    {
                        Log($"Nibble Plugin detected! {a.GetName().Name}", LogVerbosityLevel.INFO);

                        System.Version testNibbleVersion = GetAssemblyRequiredNibbleVersion(a);

                        if (testNibbleVersion == null)
                        {
                            string msg = "Unable to fetch required Nibble.dll version. Plugin not loaded";
                            Callbacks.Logger.Log(this, msg, LogVerbosityLevel.WARNING);
                            continue;
                        }

                        if (testNibbleVersion.Major != NibbleName.Version.Major || testNibbleVersion.Minor != NibbleName.Version.Minor)
                        {
                            string msg = $"Plugin incompatible with Nibble.dll Version {NibbleName.Version}. Plugin was build against : {testNibbleVersion}.";
                            Callbacks.Logger.Log(this, msg, LogVerbosityLevel.WARNING);
                            continue;
                        }

                        //Load Assembly to AppDomain and Initialize
                        LoadAssembly(filepath);

                        object c = Activator.CreateInstance(t, new object[] { this });
                        Plugins[Path.GetFileName(filepath)] = c as PluginBase;
                        //Call Dll initializers
                        t.GetMethod("OnLoad").Invoke(c, new object[] { });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error during loading of plugin {filepath}", LogVerbosityLevel.INFO);
                Log($"Exception type {ex.Data}", LogVerbosityLevel.INFO);
                Log($"Exception message {ex.Message} ", LogVerbosityLevel.INFO);
            }
        }


        private void LoadDefaultResources()
        {
            //Iterate in local folder and load existing resources
            string fontPath = "Fonts";

            if (Directory.Exists(fontPath))
            {
                foreach (string fontFileName in Directory.GetFiles(fontPath))
                {
                    string ext = Path.GetExtension(fontFileName).ToUpper();
                    if (ext == "FNT")
                    {
                        string fontAtlasName = fontFileName;
                        Path.ChangeExtension(fontAtlasName, "png");

                        if (File.Exists(Path.Combine(fontPath, fontAtlasName)))
                        {
                            AddFont(Path.Combine(fontPath, fontFileName),
                                Path.Combine(fontPath, fontAtlasName));
                        }
                        else
                        {
                            Log(string.Format("Cannot load font {0}. Missing font atas", fontFileName),
                                LogVerbosityLevel.WARNING);
                        }
                    }
                }
            }

        }

        #endregion

        public Font AddFont(string fontPath, string fontAtlas)
        {
            byte[] fontData = File.ReadAllBytes(fontPath);
            byte[] fontAtlasData = File.ReadAllBytes(fontAtlas);
            Image<Rgba32> FontAtlas = SixLabors.ImageSharp.Image.Load<Rgba32>(fontAtlasData);

            Font f = new Font(fontData, FontAtlas, 1);
            renderSys.FontMgr.addFont(f);
            return f;
        }

        public void ImportScene(SceneGraphNode scene)
        {
            RegisterEntity(scene);
            RequestEntityTransformUpdate(scene);

            scene.SetParent(GetActiveSceneGraph().Root);
            scene.Root = scene.Parent;

            //Post Import procedures
            renderSys.SubmitOpenMeshGroups();

            //Invoke Event
            NewSceneEvent?.Invoke(GetActiveSceneGraph());
        }

        //Entity Registration Methods
        public void RegisterEntity(NbShader shader)
        {
            if (registrySys.RegisterEntity(shader))
            {
                renderSys.ShaderMgr.AddShader(shader);
            }
        }
        
        public void RegisterEntity(NbTexture tex)
        {
            if (registrySys.RegisterEntity(tex))
            {
                renderSys.TextureMgr.AddTexture(tex);
            }
        }

        public void RegisterEntity(MeshMaterial mat)
        {
            if (registrySys.RegisterEntity(mat))
            {
                //Add material to teh material manager
                renderSys.MaterialMgr.AddMaterial(mat);
                
                foreach (NbSampler sampl in mat.Samplers)
                {
                    NbTexture tex = sampl.GetTexture();
                    if (tex != null)
                        RegisterEntity(tex);
                }
            }
        }

        public void RegisterEntity(SceneGraphNode e, bool recurse = true)
        {
            //Add Entity to main registry
            if (RegisterEntity((Entity) e))
            {
                sceneMgmtSys.AddNode(e);
                
                if (recurse)
                {
                    foreach (SceneGraphNode child in e.Children)
                        RegisterEntity(child);
                }
            }
        }

        public bool RegisterEntity(Entity e)
        {
            //Add Entity to main registry
            if (registrySys.RegisterEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    transformSys.RegisterEntity(e);

                //Register to rendering System
                if (e.HasComponent<MeshComponent>())
                {
                    //Register mesh, material and the corresponding shader if necessary
                    MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;

                    RegisterEntity(mc.Mesh);
                    RegisterEntity(mc.Mesh.Material);
                    
                    renderSys.RegisterEntity(e); //Register Mesh
                }

                if (e.HasComponent<AnimComponent>())
                {
                    AnimComponent ac = e.GetComponent<AnimComponent>() as AnimComponent;
                    //Iterate to all Animations
                    foreach (Animation anim in ac.AnimGroup.Animations)
                    {
                        RegisterEntity(anim);
                    }

                    animationSys.RegisterEntity(ac);

                }

                return true;
            }
            return false;
        }

        public void DestroyEntity(MeshMaterial mat)
        {
            //Remove from main registry
            if (registrySys.DeleteEntity(mat))
            {
                renderSys.MaterialMgr.Remove(mat);
            }
        }

        public void DestroyEntity(Entity e)
        {
            //Remove from main registry
            if (registrySys.DeleteEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    transformSys.DeleteEntity(e);
            }

            e.Dispose();
        }


        public void RequestEntityTransformUpdate(SceneGraphNode node)
        {
            transformSys.RequestEntityUpdate(node);
            foreach (SceneGraphNode child in node.Children)
                RequestEntityTransformUpdate(child);
        }

        #region SceneManagement

        public void ClearActiveSceneGraph()
        {
            sceneMgmtSys.ClearSceneGraph(sceneMgmtSys.ActiveSceneGraph);

        }

        #endregion

        


        #region EngineQueries

        //Asset Setters
        public void AddTexture(NbTexture tex)
        {
            renderSys.TextureMgr.AddTexture(tex);
        }

        public Entity GetEntityByID(long id)
        {
            return registrySys.GetEntity(id);
        }

        //Asset Getter
        public NbTexture GetTexture(string name)
        {
            return renderSys.TextureMgr.Get(name);
        }

        public NbMesh GetPrimitiveMesh(ulong hash)
        {
            return renderSys.GeometryMgr.GetPrimitiveMesh(hash);
        }

        public MeshMaterial GetMaterialByName(string name)
        {
            return renderSys.MaterialMgr.GetByName(name);
        }

        public SceneGraphNode GetSceneNodeByName(string name)
        {
            return registrySys.GetEntityTypeList(EntityType.SceneNode).Find(x=>((SceneGraphNode) x).Name == name) as SceneGraphNode;
        }
        
        public SceneGraphNode GetSceneNodeByNameType(SceneNodeType type, string name)
        {
            EntityType etype = EntityType.SceneNode;
            switch (type)
            {
                case SceneNodeType.LOCATOR:
                    etype = EntityType.SceneNodeLocator;
                    break;
                case SceneNodeType.MODEL:
                    etype = EntityType.SceneNodeModel;
                    break;
                case SceneNodeType.MESH:
                    etype = EntityType.SceneNodeMesh;
                    break;
                case SceneNodeType.LIGHT:
                    etype = EntityType.SceneNodeLight;
                    break;
            }
            return registrySys.GetEntityTypeList(etype).Find(x=>((SceneGraphNode) x).Name == name && ((SceneGraphNode) x).Type == type) as SceneGraphNode;
        }

        public GLSLShaderSource GetShaderSourceByFilePath(string path)
        {
            return registrySys.GetEntityTypeList(EntityType.ShaderSource)
                .Find(x => ((GLSLShaderSource)x).SourceFilePath == FileUtils.FixPath(path)) as GLSLShaderSource;
        }

        public GLSLShaderConfig GetShaderConfigByName(string name)
        {
            return registrySys.GetEntityTypeList(EntityType.ShaderConfig)
                .Find(x => ((GLSLShaderConfig)x).Name == name) as GLSLShaderConfig;
        }

        public NbShader GetShaderByHash(int hash)
        {
            return registrySys.GetEntityTypeList(EntityType.Shader)
                .Find(x => ((NbShader)x).Hash == hash) as NbShader;
        }

        public int GetEntityListCount(EntityType type)
        {
            return registrySys.GetEntityTypeList(type).Count;
        }

        public int GetShaderSourceCount()
        {
            return GetEntityListCount(EntityType.ShaderSource);
        }

        public int GetLightCount()
        {
            return GetEntityListCount(EntityType.LightComponent);
        }

        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return registrySys.GetEntityTypeList(type);
        }

        #endregion

        public void init(int width, int height)
        {
            //Initialize the render manager
            renderSys.init(width, height);
            
            Log("Initialized", LogVerbosityLevel.INFO);
        }
        
        

        //Main Rendering Routines

        private void rt_ResizeViewport(int w, int h)
        {
            renderSys.Resize(w, h);
        }

#if DEBUG

        private void rt_SpecularTestScene()
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            //Import.NMS.Palettes.set_palleteColors();

            //Clear Systems
            actionSys.CleanUp();
            animationSys.CleanUp();

            //Clear Resources
            //ModelProcGen.procDecisions.Clear();

            //Stop animation if on
            bool animToggleStatus = RenderState.settings.renderSettings.ToggleAnimations;
            RenderState.settings.renderSettings.ToggleAnimations = false;

            //Setup new object
            SceneGraphNode scene = new(SceneNodeType.MODEL)
            {
                Name = "DEFAULT SCENE"
            };

            //Add Lights
            SceneGraphNode l = CreateLightNode("Light 1", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l, new NbVector3(0.2f, 0.2f, -2.0f));
            RegisterEntity(l);
            scene.Children.Add(l);

            SceneGraphNode l1 = CreateLightNode("Light 2", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l1, new NbVector3(0.2f, -0.2f, -2.0f));
            RegisterEntity(l1);
            scene.Children.Add(l1);

            SceneGraphNode l2 = CreateLightNode("Light 3", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l2, new NbVector3(-0.2f, 0.2f, -2.0f));
            RegisterEntity(l2);
            scene.Children.Add(l2);

            SceneGraphNode l3 = CreateLightNode("Light 4", 100.0f,
                ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);

            TransformationSystem.SetEntityLocation(l3, new NbVector3(-0.2f, -0.2f, -2.0f));
            RegisterEntity(l3);
            scene.Children.Add(l3);

            //Generate a Sphere and center it in the scene

            SceneGraphNode sphere = new(SceneNodeType.MESH)
            {
                Name = "Test Sphere"
            };
            
            sphere.SetParent(scene);


            //Add Mesh Component
            int bands = 80;
            MeshComponent mc = new()
            {
                Mesh = new()
                {
                    MetaData = new()
                    {
                        BatchCount = bands * bands * 6,
                        BatchStartGraphics = 0,
                        VertrStartGraphics = 0,
                        VertrEndGraphics = (bands + 1) * (bands + 1) - 1
                    },
                    Data = (new Sphere(new NbVector3(), 2.0f, 40)).geom.GetData()
                }
                
            };

            NbShader shader = null;

            //Sphere Material
            MeshMaterial mat = new();
            mat.Name = "default_scn";
            
            NbUniform uf = new();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new(1.0f,0.0f,0.0f,1.0f);
            mat.Uniforms.Add(uf);

            uf = new();
            uf.Name = "gMaterialParamsVec4";
            uf.Values = new(0.15f, 0.0f, 0.2f, 0.0f);
            //x: roughness
            //z: metallic
            mat.Uniforms.Add(uf);
            mat.ShaderConfig = GetShaderConfigByName("UberShader");
            shader = CompileMaterialShader(mat);
            AttachShaderToMaterial(mat, shader);

            RegisterEntity(mat);
            
            scene.Children.Add(sphere);

            //Explicitly add default light to the rootObject
            scene.Children.Add(l);

            //Populate RenderManager
            //renderSys.populate(null); //OBSOLETE

            scene.IsSelected = true;
            //RenderState.activeModel = root; //Set the new scene as the new activeModel

            //Restart anim worker if it was active
            RenderState.settings.renderSettings.ToggleAnimations = animToggleStatus;

        }

        private void rt_addTestScene(int sceneID)
        {
            
            switch (sceneID)
            {
                case 0:
                    rt_SpecularTestScene();
                    break;
                default:
                    Log("Non Implemented Test Scene", LogVerbosityLevel.WARNING);
                    break;
            }

        }

#endif

        public void SendRequest(ref ThreadRequest r)
        {
            reqHandler.AddRequest(ref r);
        }

        public override void CleanUp()
        {
            actionSys.CleanUp();
            animationSys.CleanUp();
            transformSys.CleanUp();
            
            renderSys.CleanUp();
            sceneMgmtSys.CleanUp();
            registrySys.CleanUp();

        }


        //API
        //The following static methods should be used to expose
        //functionality to the user abstracted from engine systems and other
        //iternals. The idea is to pass a reference to an instantiated
        //engine object (whenever needed) and let the method do the rest
        
        #region NodeGenerators

        public SceneGraphNode CreateLocatorNode(string name)
        {
            SceneGraphNode n = new(SceneNodeType.LOCATOR)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            tc.IsControllable = false;
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                Mesh = GetPrimitiveMesh((ulong) "default_cross".GetHashCode())
            };
            
            n.AddComponent<MeshComponent>(mc);

            return n;
        }
        
        public SceneGraphNode CreateMeshNode(string name, NbMesh mesh)
        {
            SceneGraphNode n = new(SceneNodeType.MESH)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                Mesh = mesh
            };
            
            n.AddComponent<MeshComponent>(mc);
            
            return n;
        }
        
        public SceneGraphNode CreateSceneNode(string name)
        {
            SceneGraphNode n = new(SceneNodeType.MODEL)
            {
                Name = name
            };

            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            tc.IsControllable = false;
            n.AddComponent<TransformComponent>(tc);

            //Create MeshComponent
            MeshComponent mc = new()
            {
                Mesh = GetPrimitiveMesh((ulong)"default_cross".GetHashCode())
            };
            
            n.AddComponent<MeshComponent>(mc);

            //Create SceneComponent
            SceneComponent sc = new();
            n.AddComponent<SceneComponent>(sc);

            return n;
        }

        public static SceneGraphNode CreateJointNode()
        {
            SceneGraphNode n = new(SceneNodeType.JOINT);
            
            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component 
            Primitive seg = new LineSegment(n.Children.Count, new NbVector3(1.0f, 0.0f, 0.0f));
            MeshComponent mc = new()
            {
                Mesh = new()
                {
                    Data = seg.geom.GetData(),
                    MetaData = seg.geom.GetMetaData(),
                    Material = RenderState.engineRef.GetMaterialByName("jointMat")
                }
            };
            n.AddComponent<MeshComponent>(mc);
            
            //Add Joint Component
            JointComponent jc = new();
            n.AddComponent<JointComponent>(jc);
            
            return n;
        }

        
        public SceneGraphNode CreateLightNode(string name="default light", float intensity=1.0f, 
                                                ATTENUATION_TYPE attenuation=ATTENUATION_TYPE.QUADRATIC,
                                                LIGHT_TYPE lighttype = LIGHT_TYPE.POINT)
        {
            SceneGraphNode n = new(SceneNodeType.LIGHT)
            {
                Name = name
            };
            
            //Add Transform Component
            TransformData td = new();
            TransformComponent tc = new(td);
            n.AddComponent<TransformComponent>(tc);

            //Add Mesh Component
            LineSegment ls = new LineSegment(2, new NbVector3(1.0f, 0.0f, 0.0f));
            MeshComponent mc = new()
            {
                Mesh = new()
                {
                    Hash = (ulong)(name.GetHashCode() ^ DateTime.Now.GetHashCode()),
                    Type = NbMeshType.Light,
                    MetaData = ls.geom.GetMetaData(),
                    Material = GetMaterialByName("lightMat"),
                    Data = ls.geom.GetData()
                }
            };
            
            n.AddComponent<MeshComponent>(mc);
            ls.Dispose();

            //Add Light Component
            LightComponent lc = new()
            {
                Mesh = EngineRef.renderSys.GeometryMgr.GetPrimitiveMesh((ulong)"default_light_sphere".GetHashCode()),
                Data = new()
                {
                    FOV = 360.0f,
                    Intensity = intensity,
                    Falloff = attenuation,
                    LightType = lighttype,
                    IsRenderable = true,
                    IsUpdated = true
                }
            };
            n.AddComponent<LightComponent>(lc);

            return n;
        }

        
        #endregion
        
        #region GLRelatedRequests

        public NbTexture CreateTexture(string filepath, bool keepData = false)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return CreateTexture(data, Path.GetFileName(filepath), keepData);
        }
        
        public NbTexture CreateTexture(byte[] data, string name, bool keepData= false)
        {
            //TODO: Possibly move that to a separate rendering thread
            NbTexture tex = new(name, data);
            Platform.Graphics.GraphicsAPI.GenerateTexture(tex);
            Platform.Graphics.GraphicsAPI.UploadTexture(tex);
            if (!keepData)
                tex.Data.Data = null;
            //renderSys.TextureMgr.AddTexture(tex);
            return tex;
        }

        public void AttachShaderToMaterial(MeshMaterial mat, NbShader shader)
        {
            mat.SetShader(shader);
            
            //Clear active uniforms and samplers from material
            mat.ActiveSamplers.Clear();
            mat.ActiveUniforms.Clear();

            //Load Active Uniforms to Material
            for (int i = 0; i < mat.Uniforms.Count; i++)
            {
                mat.UpdateUniform(mat.Uniforms[i]);
            }

            foreach (NbSampler s in mat.Samplers)
            {
                //I don't like htis method being here. I think I need to relocate it
                mat.UpdateSampler(s); 
            }
    
        }

        public List<string> GetMaterialShaderDirectives(MeshMaterial mat)
        {
            List<string> includes = new();

            for (int i = 0; i < mat.Flags.Count; i++)
            {
                if (MeshMaterial.supported_flags.Contains(mat.Flags[i]))
                    includes.Add(mat.Flags[i].ToString().Split(".")[^1]);
            }

            return includes;
        }

        public List<string> CombineShaderDirectives(List<string> directives, NbShaderMode mode)
        {
            List<string> includes = directives.ToList();

            //General Directives are provided here
            if (mode.HasFlag(NbShaderMode.DEFFERED))
                includes.Add("_D_DEFERRED_RENDERING");

            if (mode.HasFlag(NbShaderMode.LIT))
                includes.Add("_D_LIGHTING");

            return includes;
        }

        public int CalculateShaderHash(List<string> includes)
        {
            //Directive ordering
            //1: General directives
            //2: Material directives
            //3: Config unique name

            string hash = "";

            for (int i = 0; i < includes.Count; i++)
                hash += includes[i].ToString();

            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }


        public GLSLShaderConfig CreateShaderConfig(GLSLShaderSource vs, GLSLShaderSource fs,
                                                GLSLShaderSource gs, GLSLShaderSource tcs,
                                                GLSLShaderSource tes, List<string> directives,
            NbShaderMode mode, string name, bool isGeneric = false)
        {
            List<string> finaldirectives = CombineShaderDirectives(directives, mode);
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs, fs, gs, tcs, tes, finaldirectives, mode);
            shader_conf.Name = name;
            return shader_conf;
        }

        public NbShader CompileMaterialShader(MeshMaterial mat)
        {
            if (mat.ShaderConfig == null)
            {
                Log("Missing Shader Configuration on Material. Nothing to compile",
                    LogVerbosityLevel.WARNING);
                return null;
            }

            List<string> matdirectives = GetMaterialShaderDirectives(mat);
            List<string> hashdirectives = new();
            hashdirectives = CombineShaderDirectives(hashdirectives, mat.ShaderConfig.ShaderMode);
            hashdirectives.AddRange(matdirectives);

            hashdirectives.Add(mat.ShaderConfig.Name);
            int hash = CalculateShaderHash(hashdirectives);
            NbShader shader = GetShaderByHash(hash);

            //Create New Shader if it doesn't exist
            if (shader == null)
            {
                if (!Platform.Graphics.GraphicsAPI.CompileShader(out shader, mat))
                    return null;
                //Attach UBO binding Points
                renderSys.Renderer.AttachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
                renderSys.Renderer.AttachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
                renderSys.Renderer.AttachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESHGROUP", 2);

                shader.Hash = hash;

                //Register New Shader
                RegisterEntity(shader);
            }

            //Attach Shader to material
            AttachShaderToMaterial(mat, shader);

            return shader;
        }

        #endregion

        #region IO

        public void SerializeScene(SceneGraph g, string filepath)
        {
            StreamWriter sw = new(filepath);
            Newtonsoft.Json.JsonTextWriter writer = new Newtonsoft.Json.JsonTextWriter(sw);
            Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
            writer.Formatting = Newtonsoft.Json.Formatting.Indented;

            writer.WriteStartObject();

            //Step A: Export SceneGraph
            List<GLSLShaderSource> sources = new();
            List<GLSLShaderConfig> configs = new();
            List<MeshMaterial> materials = new();
            List<NbTexture> textures = new();
            List<NbMeshData> meshes = new();

            foreach (SceneGraphNode node in g.Nodes)
            {
                if (node.HasComponent<MeshComponent>())
                {
                    MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;

                    if (mc.Mesh == null)
                        continue;
                    else if (mc.Mesh.Material == null)
                        continue;

                    //Save Config
                    var shader_config = mc.Mesh.Material.ShaderConfig;
                    if (!configs.Contains(shader_config))
                        configs.Add(shader_config);

                    //Save Mesh
                    if (!meshes.Contains(mc.Mesh.Data))
                        meshes.Add(mc.Mesh.Data);

                    //Save Material
                    if (!materials.Contains(mc.Mesh.Material))
                        materials.Add(mc.Mesh.Material);

                    foreach (NbSampler sampler in mc.Mesh.Material.Samplers)
                    {
                        NbTexture tex = sampler.GetTexture();
                        if (!textures.Contains(tex))
                            textures.Add(tex);
                    }


                    var shader_sources = mc.Mesh.Material.ShaderConfig.Sources;
                    foreach (GLSLShaderSource s in shader_sources.Values)
                    {
                        if (s.SourceFilePath == "")
                            continue;
                        if (!sources.Contains(s))
                            sources.Add(s);
                    }
                }
            }

            //Step B: Export Shader Configurations
            writer.WritePropertyName("SHADER_CONFIGS");
            writer.WriteStartArray();
            foreach (GLSLShaderConfig conf in configs)
                IO.NbSerializer.Serialize(conf, writer);
            writer.WriteEndArray();


            //Step C: Export Materials
            writer.WritePropertyName("MATERIALS");
            writer.WriteStartArray();
            foreach (MeshMaterial mat in materials)
                IO.NbSerializer.Serialize(mat, writer);
            writer.WriteEndArray();

            //Step C: Export Materials
            writer.WritePropertyName("TEXTURES");
            writer.WriteStartArray();
            foreach (NbTexture tex in textures)
                IO.NbSerializer.Serialize(tex, writer);
            writer.WriteEndArray();

            //Step D: Export Materials
            writer.WritePropertyName("MESHES");
            writer.WriteStartArray();
            foreach (NbMeshData mesh in meshes)
                IO.NbSerializer.Serialize(mesh, writer);
            writer.WriteEndArray();

            //Step E: Export SceneGraph
            writer.WritePropertyName("SCENEGRAPH");
            IO.NbSerializer.Serialize(g.Root, writer);

            writer.WriteEndObject();
            writer.Close();
        }

        public void DeserializeScene(string filepath)
        {
            StreamReader sr = new(filepath);
            Newtonsoft.Json.JsonTextReader reader = new Newtonsoft.Json.JsonTextReader(sr);
            var serializer = new Newtonsoft.Json.JsonSerializer();

            Newtonsoft.Json.Linq.JObject ob = serializer.Deserialize<Newtonsoft.Json.Linq.JObject>(reader);

            //Parse Shader Sources
            List<GLSLShaderConfig> configs = new();
            
            foreach (var s in ob["SHADER_CONFIGS"])
            {
                configs.Add((GLSLShaderConfig) IO.NbDeserializer.Deserialize(s));    
            }

            //SceneGraphNode root = null;
            //ImportScene(root);
            Console.WriteLine("DESERIALIZATION FINISHED!");
        }


        #endregion



        #region AssetDisposal

        public void DisposeSceneGraphNode(SceneGraphNode node)
        {
            //Remove from SceneGraph
            sceneMgmtSys.ActiveSceneGraph.RemoveNode(node);
            
            //Mesh Node Disposal
            if (node.HasComponent<MeshComponent>())
            {
                MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;
                if (mc.InstanceID >= 0)
                    renderSys.Renderer.RemoveRenderInstance(ref mc.Mesh, mc);
            }

            //Light Node Disposal
            if (node.HasComponent<LightComponent>())
            {
                LightComponent lc = node.GetComponent<LightComponent>() as LightComponent;
                if (lc.InstanceID >= 0)
                    renderSys.Renderer.RemoveLightRenderInstance(ref lc.Mesh, lc);
            }

            if (node.HasComponent<AnimComponent>())
            {
                AnimComponent ac = node.GetComponent<AnimComponent>() as AnimComponent;
                ac.AnimationDict.Clear();
                animationSys.AnimationGroups.Remove(ac.AnimGroup);
                foreach (Animation anim in ac.AnimGroup.Animations)
                {
                    animationSys.AnimMgr.Remove(anim);
                }

                //TODO: Remove animation data objects as well
            }

            DestroyEntity(node);
            
        }

        public void RecursiveSceneGraphNodeDispose(SceneGraphNode node)
        {
            foreach (SceneGraphNode child in node.Children)
                RecursiveSceneGraphNodeDispose(child);
            DisposeSceneGraphNode(node);
        }

        #endregion



        #region StateQueries

        public SceneGraph GetActiveSceneGraph()
        {
            //TODO: This should return the visible scenegraph
            //Ideally we would like to have multiple scenegraphs
            //With different objects on each one and just render the
            //visible one
            return sceneMgmtSys.ActiveSceneGraph;
        }

        public override void OnRenderUpdate(double dt)
        {
            throw new NotImplementedException();
        }

        public override void OnFrameUpdate(double dt)
        {
            throw new NotImplementedException();
        }

        #endregion



    }
}
