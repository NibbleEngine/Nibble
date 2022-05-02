using System;
using System.Collections.Generic;
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
using System.Runtime.CompilerServices;

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
        //private EntityRegistrySystem registrySys;
        //public TransformationSystem transformSys;
        //public ActionSystem actionSys;
        //public AnimationSystem animationSys;
        //public SceneManagementSystem sceneMgmtSys;
        //public RenderingSystem renderSys; //TODO: Try to make it private. Noone should have a reason to access it
        private readonly RequestHandler reqHandler;

        private Dictionary<Type, EngineSystem> _engineSystemMap = new(); //TODO fill up

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
            RenderingSystem renderSys = new(); //Init renderManager of the engine
            EntityRegistrySystem registrySys = new EntityRegistrySystem();
            ActionSystem actionSys = new ActionSystem();
            AnimationSystem animationSys = new AnimationSystem();
            TransformationSystem transformSys = new TransformationSystem();
            SceneManagementSystem sceneMgmtSys = new SceneManagementSystem();
            ScriptingSystem scriptMgmtSys = new ScriptingSystem();
            
            SetEngine(this);
            AddSystem(renderSys);
            AddSystem(registrySys);
            AddSystem(actionSys);
            AddSystem(animationSys);
            AddSystem(transformSys);
            AddSystem(sceneMgmtSys);
            AddSystem(scriptMgmtSys);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddSystem<T>(T system) where T: EngineSystem
        {
            system.SetEngine(this);
            _engineSystemMap[typeof(T)] = system;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetSystem<T>() where T : EngineSystem
        {
            return (T) _engineSystemMap[typeof(T)];
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
            GetSystem<RenderingSystem>().FontMgr.addFont(f);
            return f;
        }

        public void ImportScene(SceneGraphNode scene)
        {
            RegisterSceneGraphTree(scene, true);
            RequestEntityTransformUpdate(scene);

            scene.SetParent(GetActiveSceneGraph().Root);
            scene.Root = scene.Parent;

            //Post Import procedures
            GetSystem<RenderingSystem>().SubmitOpenMeshGroups();
            
            //Invoke Event
            NewSceneEvent?.Invoke(GetActiveSceneGraph());
        }

        //Entity Registration Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEntityRegistered(Entity e)
        {
            return GetSystem<EntityRegistrySystem>().IsRegistered(e);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(NbShader shader)
        {
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(shader))
            {
                GetSystem<RenderingSystem>().ShaderMgr.AddShader(shader);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(NbMesh mesh)
        {
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(mesh))
            {
                GetSystem<RenderingSystem>().RegisterEntity(mesh);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(NbTexture tex)
        {
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(tex))
            {
                GetSystem<RenderingSystem>().TextureMgr.Add(tex.Path, tex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterEntity(MeshMaterial mat)
        {
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(mat))
            {
                GetSystem<RenderingSystem>().MaterialMgr.AddMaterial(mat);

                //Register Textures as well
                foreach (NbSampler sampl in mat.Samplers)
                {
                    if (sampl.Texture != null)
                    {
                        RegisterEntity(sampl.Texture);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RegisterSceneGraphTree(SceneGraphNode e, bool recurse = true)
        {
            //Add Entity to main registry
            if (RegisterEntity(e))
            {
                GetSystem<SceneManagementSystem>().AddNode(e);

                if (recurse)
                {
                    foreach (SceneGraphNode child in e.Children)
                        RegisterSceneGraphTree(child, recurse);
                }
            }
        }
        
        public bool RegisterEntity(SceneGraphNode e)
        {
            //Add Entity to main registry
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    GetSystem<TransformationSystem>().RegisterEntity(e);

                //Register to rendering System
                if (e.HasComponent<MeshComponent>())
                {
                    //Register mesh, material and the corresponding shader if necessary
                    MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;

                    RegisterEntity(mc.Mesh);
                    RegisterEntity(mc.Mesh.Material);

                    GetSystem<RenderingSystem>().RegisterEntity(mc); //Register Mesh to Rendering System
                }

                if (e.HasComponent<AnimComponent>())
                {
                    AnimComponent ac = e.GetComponent<AnimComponent>() as AnimComponent;
                    //Iterate to all Animations
                    foreach (Animation anim in ac.AnimGroup.Animations)
                    {
                        RegisterEntity(anim);
                    }

                    GetSystem<AnimationSystem>().RegisterEntity(ac);
                }

                if (e.HasComponent<ScriptComponent>())
                {
                    GetSystem<ScriptingSystem>().RegisterEntity(e);
                }

                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RegisterEntity(NbScript e)
        {
            //Add Entity to main registry
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(e))
            {
                GetSystem<ScriptingSystem>().RegisterEntity(e);
                return true;
            }
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RegisterEntity(Entity e)
        {
            //Add Entity to main registry
            if (GetSystem<EntityRegistrySystem>().RegisterEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    GetSystem<TransformationSystem>().RegisterEntity(e);

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(NbScript script)
        {
            //Remove from main registry
            if (GetSystem<EntityRegistrySystem>().DeleteEntity(script))
            {
                GetSystem<ScriptingSystem>().Remove(script);
            }
            script.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(MeshMaterial mat)
        {
            //Remove from main registry
            if (GetSystem<EntityRegistrySystem>().DeleteEntity(mat))
            {
                GetSystem<RenderingSystem>().MaterialMgr.Remove(mat);
            }
            mat.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(Entity e)
        {
            //Remove from main registry
            if (GetSystem<EntityRegistrySystem>().DeleteEntity(e))
            {
                //Register to transformation System
                if (e.HasComponent<TransformComponent>())
                    GetSystem<TransformationSystem>().DeleteEntity(e);
            }

            e.Dispose();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RequestEntityTransformUpdate(SceneGraphNode node)
        {
            GetSystem<TransformationSystem>().RequestEntityUpdate(node);
            foreach (SceneGraphNode child in node.Children)
                RequestEntityTransformUpdate(child);
        }

        #region SceneManagement

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearActiveSceneGraph()
        {
            GetSystem<SceneManagementSystem>().ClearSceneGraph(GetSystem<SceneManagementSystem>().ActiveSceneGraph);
        }

        #endregion


        #region EngineQueries

        //Asset Getter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetEntityByID(ulong id)
        {
            return GetSystem<EntityRegistrySystem>().GetEntity(id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NbTexture GetTexture(string name)
        {
            return GetSystem<RenderingSystem>().TextureMgr.Get(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NbMesh GetMesh(ulong hash)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.Mesh).Find(x => ((NbMesh)x).Hash == hash) as NbMesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MeshMaterial GetMaterialByName(string name)
        {
            return GetSystem<RenderingSystem>().MaterialMgr.GetByName(name);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneGraphNode GetSceneNodeByName(string name)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.SceneNode).Find(x=>((SceneGraphNode) x).Name == name) as SceneGraphNode;
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
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(etype).Find(x=>((SceneGraphNode) x).Name == name && ((SceneGraphNode) x).Type == type) as SceneGraphNode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GLSLShaderSource GetShaderSourceByFilePath(string path)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.ShaderSource)
                .Find(x => ((GLSLShaderSource)x).SourceFilePath == FileUtils.FixPath(path)) as GLSLShaderSource;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GLSLShaderConfig GetShaderConfigByName(string name)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.ShaderConfig)
                .Find(x => ((GLSLShaderConfig)x).Name == name) as GLSLShaderConfig;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GLSLShaderConfig GetShaderConfigByHash(ulong hash)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.ShaderConfig)
                .Find(x => ((GLSLShaderConfig)x).Hash == hash) as GLSLShaderConfig;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NbShader GetShaderByHash(ulong hash)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.Shader)
                .Find(x => ((NbShader)x).Hash == hash) as NbShader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NbScript GetScriptByHash(ulong hash)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(EntityType.Script)
                .Find(x => ((NbScript)x).Hash == hash) as NbScript;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEntityListCount(EntityType type)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(type).Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetShaderSourceCount()
        {
            return GetEntityListCount(EntityType.ShaderSource);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLightCount()
        {
            return GetEntityListCount(EntityType.LightComponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<Entity> GetEntityTypeList(EntityType type)
        {
            return GetSystem<EntityRegistrySystem>().GetEntityTypeList(type);
        }

        #endregion

        public void init(int width, int height)
        {
            //Initialize the render manager
            GetSystem<RenderingSystem>().init(width, height);
            
            Log("Initialized", LogVerbosityLevel.INFO);
        }
        
        

        //Main Rendering Routines

        private void rt_ResizeViewport(int w, int h)
        {
            GetSystem<RenderingSystem>().Resize(w, h);
        }

#if DEBUG

        private void rt_SpecularTestScene()
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            //Import.NMS.Palettes.set_palleteColors();

            //Clear Systems
            GetSystem<ActionSystem>().CleanUp();
            GetSystem<AnimationSystem>().CleanUp();

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
                    Data = (new Sphere(new NbVector3(), 2.0f, 40)).geom.GetMeshData()
                }
                
            };

            NbShader shader = null;
            GLSLShaderConfig conf;

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
            conf = GetShaderConfigByName("UberShader");

            shader = CreateShader(conf);
            EngineRef.CompileShader(shader);
            mat.AttachShader(shader);
            
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
            foreach (EngineSystem sys in _engineSystemMap.Values)
            {
                sys.CleanUp();
            }
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
                Mesh = GetMesh(NbHasher.Hash("default_cross"))
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
                Mesh = GetMesh(NbHasher.Hash("default_cross"))
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
                    Data = seg.geom.GetMeshData(),
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
                    Hash = NbHasher.Hash(name) ^ (ulong) DateTime.Now.GetHashCode(),
                    Type = NbMeshType.Light,
                    MetaData = ls.geom.GetMetaData(),
                    Material = GetMaterialByName("lightMat"),
                    Data = ls.geom.GetMeshData()
                }
            };
            
            n.AddComponent<MeshComponent>(mc);
            ls.Dispose();

            //Add Light Component
            LightComponent lc = new()
            {
                Mesh = EngineRef.GetMesh(NbHasher.Hash("default_light_sphere")),
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

        public NbTexture CreateTexture(string filepath,
            NbTextureWrapMode wrapmode, NbTextureFilter minFilter, NbTextureFilter magFilter, bool keepData = false)
        {
            byte[] data = File.ReadAllBytes(filepath);
            return CreateTexture(data, filepath, wrapmode, minFilter, magFilter, keepData);
        }
        
        public NbTexture CreateTexture(byte[] data, string name,
            NbTextureWrapMode wrapmode, NbTextureFilter minFilter, NbTextureFilter magFilter,
            bool keepData = false)
        {
            //TODO: Possibly move that to a separate rendering thread
            NbTexture tex = new(name, data);
            Platform.Graphics.GraphicsAPI.GenerateTexture(tex);
            Platform.Graphics.GraphicsAPI.setupTextureParameters(tex, wrapmode, magFilter, minFilter, 8.0f);
            Platform.Graphics.GraphicsAPI.UploadTexture(tex);
            if (!keepData)
                tex.Data.Data = null;
            //renderSys.TextureMgr.AddTexture(tex);
            return tex;
        }

        public List<string> GetMaterialShaderDirectives(MeshMaterial mat)
        {
            List<string> includes = new();
            List<MaterialFlagEnum> mat_flags = mat.GetFlags();
            for (int i = 0; i < mat_flags.Count; i++)
            {
                if (MeshMaterial.supported_flags.Contains(mat_flags[i]))
                    includes.Add(mat_flags[i].ToString().Split(".")[^1]);
            }

            return includes;
        }

        public List<string> CreateShaderDirectivesFromMode(NbShaderMode mode)
        {
            List<string> includes = new();

            //General Directives are provided here
            if (mode.HasFlag(NbShaderMode.DEFFERED))
                includes.Add("_D_DEFERRED_RENDERING");
            if (mode.HasFlag(NbShaderMode.SKINNED))
                includes.Add("_D_SKINNED");
            if (mode.HasFlag(NbShaderMode.LIT))
                includes.Add("_D_LIGHTING");

            return includes;
        }

        public ulong CalculateShaderHash(NbShader shader)
        {
            return CalculateShaderHash(shader.GetShaderConfig(), shader.directives);
        }

        public ulong CalculateShaderHash(GLSLShaderConfig conf, List<string> extradirectives = null)
        {
            ulong hash = conf.Hash;
            if (extradirectives != null)
            {
                for (int i = 0; i < extradirectives.Count;i++)
                {
                    hash = NbHasher.CombineHash(hash, NbHasher.Hash(extradirectives[i]));
                }
            }

            return hash;
        }

        public GLSLShaderConfig CreateShaderConfig(GLSLShaderSource vs, GLSLShaderSource fs,
                                                GLSLShaderSource gs, GLSLShaderSource tcs,
                                                GLSLShaderSource tes, NbShaderMode mode,
                                                string name, bool isGeneric = false)
        {

            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs, fs, gs, tcs, tes, mode);
            shader_conf.Name = name;
            shader_conf.IsGeneric = isGeneric;
            return shader_conf;
        }

        public NbScript CreateScript(string filepath)
        {
            NbScript script = GetSystem<ScriptingSystem>().CompileScript(filepath);
            if (script != null)
            {
                RegisterEntity(script);
            }
            return script;
        }

        public NbShader CreateShader(GLSLShaderConfig conf, List<string> extradirectives = null)
        {
            NbShader shader = new(conf);
            if (extradirectives != null)
                shader.directives = new(extradirectives);
            return shader;
        }

        public void SetMaterialShader(MeshMaterial mat, GLSLShaderConfig conf)
        {
            //Calculate requested shader hash
            ulong shader_hash = CalculateShaderHash(conf, GetMaterialShaderDirectives(mat));

            NbShader new_shader = GetShaderByHash(shader_hash);

            if (new_shader == null)
            {
                //Create new Shader
                new_shader = RenderState.engineRef.CreateShader(conf, 
                    GetMaterialShaderDirectives(mat));
                
                CompileShader(new_shader);
            }
            
            mat.AttachShader(new_shader);
        }


        public bool CompileShader(NbShader shader)
        {
            if (shader.GetShaderConfig() == null)
            {
                Log("Missing Shader Configuration on Material. Nothing to compile",
                    LogVerbosityLevel.ERROR);
                return false;
            }

            if (!Platform.Graphics.GraphicsAPI.CompileShader(shader))
                return false;
            
            shader.Hash = CalculateShaderHash(shader);
            
            shader.IsUpdated?.Invoke(shader);
            //Register New Shader
            RegisterEntity(shader);
            return true;
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
            List<GLSLShaderConfig> configs = new();
            List<MeshMaterial> materials = new();
            List<NbTexture> textures = new();
            List<string> scripts = new();
            List<NbMesh> meshes = new();
            List<NbMeshData> mesh_data = new();

            foreach (SceneGraphNode node in g.Nodes)
            {
                if (node == g.Root)
                    continue;
                
                if (node.HasComponent<MeshComponent>())
                {
                    MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;

                    if (mc.Mesh == null)
                        continue;
                    else if (mc.Mesh.Material == null)
                        continue;

                    //Save Config
                    var shader_config = mc.Mesh.Material.Shader.GetShaderConfig();
                    if (!configs.Contains(shader_config) && !shader_config.IsGeneric)
                    {
                        configs.Add(shader_config);
                    }
                        
                    //Save Mesh
                    if (!meshes.Contains(mc.Mesh))
                        meshes.Add(mc.Mesh);

                    //Save Mesh Data
                    if (!mesh_data.Contains(mc.Mesh.Data))
                        mesh_data.Add(mc.Mesh.Data);

                    //Save Materiald
                    if (!materials.Contains(mc.Mesh.Material) && !mc.Mesh.Material.IsGeneric)
                        materials.Add(mc.Mesh.Material);

                    foreach (NbSampler sampler in mc.Mesh.Material.Samplers)
                    {
                        NbTexture tex = sampler.Texture;
                        if (tex == null)
                            continue;
                        if (!textures.Contains(tex))
                            textures.Add(tex);
                    }

                    
                }

                if (node.HasComponent<ScriptComponent>())
                {
                    ScriptComponent sc = node.GetComponent<ScriptComponent>() as ScriptComponent;
                    
                    if (sc.SourcePath != "" && !scripts.Contains(sc.SourcePath))
                    {
                        scripts.Add(sc.SourcePath);
                    }
                
                }
            }

            //Step B: Export Shader Configurations
            writer.WritePropertyName("SHADER_CONFIGS");
            writer.WriteStartArray();
            foreach (GLSLShaderConfig conf in configs)
                IO.NbSerializer.Serialize(conf, writer);
            writer.WriteEndArray();

            writer.WritePropertyName("TEXTURES");
            writer.WriteStartArray();
            foreach (NbTexture tex in textures)
                IO.NbSerializer.Serialize(tex, writer);
            writer.WriteEndArray();

            writer.WritePropertyName("SCRIPTS");
            writer.WriteStartArray();
            foreach (string script_path in scripts)
                IO.NbSerializer.Serialize(script_path, writer);
            writer.WriteEndArray();

            writer.WritePropertyName("MATERIALS");
            writer.WriteStartArray();
            foreach (MeshMaterial mat in materials)
                IO.NbSerializer.Serialize(mat, writer);
            writer.WriteEndArray();

            writer.WritePropertyName("MESH_DATA");
            writer.WriteStartArray();
            foreach (NbMeshData data in mesh_data)
                IO.NbSerializer.Serialize(data, writer);
            writer.WriteEndArray();

            writer.WritePropertyName("MESHES");
            writer.WriteStartArray();
            foreach (NbMesh mesh in meshes)
                IO.NbSerializer.Serialize(mesh, writer);
            writer.WriteEndArray();

            
            writer.WritePropertyName("SCENEGRAPH");
            IO.NbSerializer.Serialize(g.Root, writer);
            
            writer.WriteEndObject();
            writer.Close();
        }

        public void OpenScene(string filepath)
        {
            //Clear Scene
            EngineRef.ClearActiveSceneGraph();

            StreamReader sr = new(filepath);
            Newtonsoft.Json.JsonTextReader reader = new Newtonsoft.Json.JsonTextReader(sr);
            var serializer = new Newtonsoft.Json.JsonSerializer();
            Newtonsoft.Json.Linq.JObject ob = serializer.Deserialize<Newtonsoft.Json.Linq.JObject>(reader);
            reader.Close();
            
            //Parse Shader Sources
            Dictionary<ulong,NbMeshData> meshes = new();

            foreach (var s in ob["SHADER_CONFIGS"])
            {
                GLSLShaderConfig conf = (GLSLShaderConfig)IO.NbDeserializer.Deserialize(s);
                
                if (GetShaderConfigByHash(conf.Hash) == null)
                    RegisterEntity(conf);
            }
                
            foreach (var s in ob["TEXTURES"])
            {
                NbTexture tex = (NbTexture)IO.NbDeserializer.Deserialize(s);
                RegisterEntity(tex);
            }

            foreach (var s in ob["MESH_DATA"])
            {
                NbMeshData meshdata = (NbMeshData) IO.NbDeserializer.Deserialize(s);
                GetSystem<RenderingSystem>().MeshDataMgr.Add(meshdata.Hash, meshdata);
            }

            foreach (var s in ob["MATERIALS"])
            {
                MeshMaterial mat = (MeshMaterial)IO.NbDeserializer.Deserialize(s);
                RegisterEntity(mat);
            }

            foreach (var s in ob["MESHES"])
            {
                NbMesh mesh = (NbMesh) IO.NbDeserializer.Deserialize(s);
                RegisterEntity(mesh);
            }

            
            SceneGraphNode root = (SceneGraphNode) IO.NbDeserializer.Deserialize(ob["SCENEGRAPH"]);

            //Register shit and add to shit
            
            foreach (SceneGraphNode child in root.Children)
                ImportScene(child);

            root.Dispose(); //Root is not imported
            //SceneGraphNode root = null;
            //ImportScene(root);
            Console.WriteLine("DESERIALIZATION FINISHED!");
        }


        #endregion



        #region AssetDisposal

        public void DisposeSceneGraphNode(SceneGraphNode node)
        {
            //Remove from SceneGraph
            GetSystem<SceneManagementSystem>().ActiveSceneGraph.RemoveNode(node);
            
            //Mesh Node Disposal
            if (node.HasComponent<MeshComponent>())
            {
                MeshComponent mc = node.GetComponent<MeshComponent>() as MeshComponent;
                if (mc.InstanceID >= 0)
                    GetSystem<RenderingSystem>().Renderer.RemoveRenderInstance(ref mc.Mesh, mc);
            }

            //Light Node Disposal
            if (node.HasComponent<LightComponent>())
            {
                LightComponent lc = node.GetComponent<LightComponent>() as LightComponent;
                if (lc.InstanceID >= 0)
                    GetSystem<RenderingSystem>().Renderer.RemoveLightRenderInstance(ref lc.Mesh, lc);
            }

            if (node.HasComponent<AnimComponent>())
            {
                AnimComponent ac = node.GetComponent<AnimComponent>() as AnimComponent;
                ac.AnimationDict.Clear();
                GetSystem<AnimationSystem>().AnimationGroups.Remove(ac.AnimGroup);
                foreach (Animation anim in ac.AnimGroup.Animations)
                {
                    GetSystem<AnimationSystem>().AnimMgr.Remove(anim);
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
            return GetSystem<SceneManagementSystem>().ActiveSceneGraph;
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


