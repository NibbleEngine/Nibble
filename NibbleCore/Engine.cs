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

        //Rendering 
        public EngineRenderingState rt_State;

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

            //Set Start Status
            rt_State = EngineRenderingState.UNINITIALIZED;

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
                            Callbacks.Log("Unable to fetch required Nibble.dll version. Plugin not loaded",
                                LogVerbosityLevel.WARNING);
                            continue;
                        }

                        if (testNibbleVersion.Major != NibbleName.Version.Major)
                        {
                            Callbacks.Log($"Plugin incompatible with Nibble.dll Version {NibbleName.Version}. Plugin was build against : {testNibbleVersion}.",
                                LogVerbosityLevel.WARNING);
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

        
        public void DestroyEntity(Entity e)
        {
            //Remove from main registry
            if (registrySys.DeleteEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    transformSys.DeleteEntity(e);
            }
        }

        public void ImportScene(SceneGraphNode scene)
        {
            RegisterSceneGraphNode(scene);
            RequestEntityTransformUpdate(scene);

            scene.SetParent(GetActiveSceneGraph().Root);
            scene.Root = scene.Parent;
            
            //Post Import procedures
            renderSys.SubmitOpenMeshGroups();

            //Invoke Event
            NewSceneEvent?.Invoke(GetActiveSceneGraph());
        }


        public void RegisterEntity(Entity e)
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
                    RegisterEntity(mc.Material);
                    RegisterEntity(mc.Material.Shader);
                    
                    renderSys.RegisterEntity(e); //Register Mesh
                }

                if (e.HasComponent<AnimComponent>())
                {
                    AnimComponent ac = e.GetComponent<AnimComponent>() as AnimComponent;

                    //Iterate to all Animations
                    foreach (Animation anim in ac.Animations)
                    {
                        RegisterEntity(anim);
                        animationSys.RegisterEntity(anim);
                    }

                }

                if (e is SceneGraphNode)
                    sceneMgmtSys.AddNode(e as SceneGraphNode);
                
                //TODO Register to the rest systems if necessary
            }
        }

        public void RegisterSceneGraphNode(SceneGraphNode node)
        {
            RegisterEntity(node);
            foreach (SceneGraphNode child in node.Children)
                RegisterSceneGraphNode(child);
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

        #region ResourceManager

        public void InitializeResources()
        {
            AddDefaultShaders();
        }

        private void AddDefaultShaders()
        {
            //Local function
            void WalkDirectory(DirectoryInfo dir)
            {
                FileInfo[] files = dir.GetFiles("*.glsl");
                DirectoryInfo[] subdirs = dir.GetDirectories();

                if (subdirs.Length != 0)
                {
                    foreach (DirectoryInfo subdir in subdirs)
                        WalkDirectory(subdir);
                }

                if (files.Length != 0)
                {
                    foreach (FileInfo file in files)
                    {
                        //Convert filepath to single 
                        string filepath = Utils.FileUtils.FixPath(file.FullName);
                        //Add source file
                        Log($"Working On {filepath}", LogVerbosityLevel.INFO);
                        if (GetShaderSourceByFilePath(filepath) == null)
                        {
                            //Construction includes registration
                            GLSLShaderSource ss = new(filepath, true); 
                        }
                    }
                }
            }

            DirectoryInfo dirInfo = new("Shaders");
            WalkDirectory(dirInfo);

            //Now that all sources are loaded we can start processing all of them
            //Step 1: Process Shaders
            List<Entity> sourceList = GetEntityTypeList(EntityType.ShaderSource);
            int i = 0;
            while (i < sourceList.Count) //This way can account for new entries 
            {
                ((GLSLShaderSource) sourceList[i]).Process();
                i++;
            }
            
        }

        #endregion


        #region EngineQueries

        //Asset Setters
        public void AddTexture(Texture tex)
        {
            renderSys.TextureMgr.AddTexture(tex);
        }

        //Asset Getter
        public Texture GetTexture(string name)
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

        public GLSLShaderConfig GetShaderByHash(int hash)
        {
            return registrySys.GetEntityTypeList(EntityType.Shader)
                .Find(x => ((GLSLShaderConfig) x).Hash == hash) as GLSLShaderConfig;
        }

        public GLSLShaderConfig GetShaderByType(SHADER_TYPE typ)
        {
            return renderSys.ShaderMgr.GetGenericShader(typ);
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
            //Initialize Resource Manager
            InitializeResources();

            //Add Camera
            Camera cam = new(90, 0, true)
            {
                isActive = false
            };

            //Add Necessary Components to Camera
            TransformationSystem.AddTransformComponentToEntity(cam);
            TransformComponent tc = cam.GetComponent<TransformComponent>() as TransformComponent;
            tc.IsControllable = true;
            RegisterEntity(cam);
            
            //Set global reference to cam
            RenderState.activeCam = cam;

            //Set Camera Initial State
            TransformController tcontroller = transformSys.GetEntityTransformController(cam);
            tcontroller.AddFutureState(new NbVector3(), NbQuaternion.FromEulerAngles(0.0f, -3.14f/2.0f, 0.0f, "XYZ"), new NbVector3(1.0f));

            //Initialize the render manager
            renderSys.init(width, height);
            rt_State = EngineRenderingState.ACTIVE;

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

            //Clear RenderStats
            RenderStats.ClearStats();

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

            GLSLShaderConfig shader = null;

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
            shader = CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            renderSys.Renderer.AttachShaderToMaterial(mat, shader);

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
                Mesh = GetPrimitiveMesh((ulong) "default_cross".GetHashCode()),
                Material = GetMaterialByName("crossMat")
            };
            
            n.AddComponent<MeshComponent>(mc);

            return n;
        }
        
        public SceneGraphNode CreateMeshNode(string name, NbMesh mesh, MeshMaterial mat)
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
                Mesh = mesh,
                Material = mat
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
                Mesh = GetPrimitiveMesh((ulong)"default_cross".GetHashCode()),
                Material = GetMaterialByName("crossMat")
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
                    MetaData = seg.geom.GetMetaData()
                },
                Material = Common.RenderState.engineRef.GetMaterialByName("jointMat")
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
                    Data = ls.geom.GetData()
                },
                Material = GetMaterialByName("lightMat")
            };
            
            n.AddComponent<MeshComponent>(mc);
            ls.Dispose();

            //Add Light Component
            LightComponent lc = new()
            {
                Mesh = EngineRef.renderSys.GeometryMgr.GetPrimitiveMesh((ulong)"default_light_sphere".GetHashCode()),
                Material = EngineRef.renderSys.MaterialMgr.GetByName("lightMat"),
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

        public Texture AddTexture(string filepath)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return AddTexture(data, Path.GetFileName(filepath));
        }
        
        public Texture AddTexture(byte[] data, string name)
        {
            //TODO: Possibly move that to a separate rendering thread
            Texture tex = new();
            tex.Name = name;
            string ext = Path.GetExtension(name).ToUpper();
            tex.textureInit(data, ext); //Manually load data
            renderSys.TextureMgr.AddTexture(tex);
            return tex;
        }

        public void CompileShader(GLSLShaderConfig shader)
        {
            renderSys.Renderer.CompileShader(shader);
        }

        public GLSLShaderConfig CreateShader(GLSLShaderSource vs, GLSLShaderSource fs,
                                                GLSLShaderSource gs, GLSLShaderSource tcs,
                                                GLSLShaderSource tes, List<string> directives,
            SHADER_TYPE type, SHADER_MODE mode)
        {
            List<string> finaldirectives = renderSys.Renderer.CombineShaderDirectives(directives, mode);
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(type, vs, fs, gs, tcs, tes, finaldirectives, mode);
            renderSys.Renderer.CompileShader(shader_conf);
            return shader_conf;
        }

        public GLSLShaderConfig CompileMaterialShader(MeshMaterial mat, SHADER_MODE mode)
        {

            List<string> matdirectives = renderSys.Renderer.GetMaterialShaderDirectives(mat);
            List<string> hashdirectives = new();
            hashdirectives = renderSys.Renderer.CombineShaderDirectives(hashdirectives, mode);
            hashdirectives.AddRange(matdirectives);

            string vs_path = "Shaders/Simple_VS.glsl";
            vs_path = FileUtils.FixPath(vs_path);
            hashdirectives.Add(vs_path);

            string fs_path = "Shaders/Simple_FS.glsl";
            fs_path = FileUtils.FixPath(fs_path);
            hashdirectives.Add(fs_path);

            int hash = renderSys.Renderer.CalculateShaderHash(hashdirectives);

            //Check if a config exists
            GLSLShaderConfig conf = GetShaderByHash(hash);

            //Create New Shader if it doesn't exist
            if (conf == null)
            {
                GLSLShaderSource vs = GetShaderSourceByFilePath(vs_path);
                GLSLShaderSource fs = GetShaderSourceByFilePath(fs_path);

                conf = new()
                {
                    directives = matdirectives.ToList(),
                    ShaderMode = mode
                };

                //Add Sources
                conf.AddSource(NbShaderType.VertexShader, vs);
                conf.AddSource(NbShaderType.FragmentShader, fs);

                renderSys.Renderer.CompileShader(conf);

                //Attach UBO binding Points
                renderSys.Renderer.AttachUBOToShaderBindingPoint(conf, "_COMMON_PER_FRAME", 0);
                renderSys.Renderer.AttachSSBOToShaderBindingPoint(conf, "_COMMON_PER_MESH", 1);
                renderSys.Renderer.AttachSSBOToShaderBindingPoint(conf, "_COMMON_PER_MESHGROUP", 2);

                Callbacks.Assert(conf.Hash == hash, "Inconsistent Shader Hash Calculation");

            }

            return conf;
        }

        #endregion


        #region AssetDisposal

        public void DisposeSceneGraphNode(SceneGraphNode node)
        {
            DestroyEntity(node);
            
            //Mesh Node Disposal
            if (node.HasComponent<MeshComponent>())
            {
                MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;
                if (mc.InstanceID >= 0)
                    renderSys.Renderer.RemoveRenderInstance(ref mc.Mesh, mc);
            }

            //Mesh Node Disposal
            if (node.HasComponent<LightComponent>())
            {
                LightComponent lc = node.GetComponent<LightComponent>() as LightComponent;
                if (lc.InstanceID >= 0)
                    renderSys.Renderer.RemoveLightRenderInstance(ref lc.Mesh, lc);
            }

            node.Dispose();
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
