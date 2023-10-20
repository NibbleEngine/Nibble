using System;
using Newtonsoft.Json;
using System.Reflection;
using System.IO;
using System.Linq;

namespace NbCore.Common
{
    public enum ApplicationMode
    {
        EDIT,
        GAME
    }

    

    public enum ViewSettingsEnum
    {
        ViewInfo = 1,
        ViewLights = 2,
        ViewLightVolumes = 4,
        ViewJoints = 8,
        ViewLocators = 16,
        ViewCollisions = 32,
        ViewBoundHulls = 64,
        ViewGizmos = 128,
        EmulateActions = 256
    }

    public struct CameraSettings
    {
        public int FOV;
        public float Sensitivity;
        public float Speed;
        public float zNear;
        public float zFar;

        public CameraSettings(int fov, float sens,
            float speed, float near, float far)
        {
            FOV = fov;
            Sensitivity = sens;
            Speed = speed;
            zNear = near;
            zFar = far;
        }
    }

    public struct ViewSettings
    {
        public bool ViewInfo;
        public bool ViewLights;
        public bool ViewLightVolumes;
        public bool ViewJoints;
        public bool ViewLocators;
        public bool ViewCollisions;
        public bool ViewBoundHulls;
        public bool ViewGizmos;
        public bool EmulateActions;
        public int SettingsMask;
        
        
        //Use the settings mask when serializing the struct to the settings file
        public ViewSettings(int settings_mask)
        {
            SettingsMask = settings_mask;
            ViewInfo = (settings_mask & (int) ViewSettingsEnum.ViewInfo) != 0;
            ViewLights = (settings_mask & (int)ViewSettingsEnum.ViewLights) != 0;
            ViewLightVolumes = (settings_mask & (int)ViewSettingsEnum.ViewLightVolumes) != 0;
            ViewJoints = (settings_mask & (int)ViewSettingsEnum.ViewJoints) != 0;
            ViewLocators = (settings_mask & (int)ViewSettingsEnum.ViewLocators) != 0;
            ViewCollisions = (settings_mask & (int)ViewSettingsEnum.ViewCollisions) != 0;
            ViewBoundHulls = (settings_mask & (int)ViewSettingsEnum.ViewBoundHulls) != 0;
            ViewGizmos = (settings_mask & (int)ViewSettingsEnum.ViewGizmos) != 0;
            EmulateActions = (settings_mask & (int)ViewSettingsEnum.EmulateActions) != 0;
        }

    }

    public class RenderSettings
    {
        public int FPS = 60;
        public float HDRExposure = 0.005f;

        public NbVector3 BackgroundColor = new NbVector3(0.3f, 0.3f, 0.3f);

        //Set Full rendermode by default
        [JsonIgnore]
        public NbPolygonRenderMode RenderMode 
        {
            get {
                if (RenderWireFrame)
                    return NbPolygonRenderMode.Line;
                return NbPolygonRenderMode.Fill;
            }
        }

        public bool UseVSync = true;
        public bool UseTextures = true;
        public bool UseLighting = true;

        //Test Settings
#if (DEBUG)
        [JsonIgnore]
        public float testOpt1 = 0.0f;
        [JsonIgnore]
        public float testOpt2 = 0.0f;
        [JsonIgnore]
        public float testOpt3 = 0.0f;
#endif
        
        //Properties
        public bool UseFXAA = true;
        public bool UseToneMapping = true;
        //Bloom Settings
        public bool UseBLOOM = true;
        public float BloomIntensity = 0.05f;
        public float BloomFilterRadius = 0.005f;

        [JsonIgnore]
        public bool UseFrustumCulling = true;

        [JsonIgnore]
        public bool LODFiltering = true;

        [JsonIgnore]
        public bool RenderWireFrame = false;

        [JsonIgnore]
        public bool ToggleAnimations = true;

    }


    public class EngineSettings
    {
        public RenderSettings RenderSettings = new();
        public ViewSettings ViewSettings = new();
        public CameraSettings CamSettings = new CameraSettings(90, 1.0f, 1.0f, 0.05f, 30000f);
        public int TickRate = 60;
        public bool EnableShaderCompilationLog = true;
        public LogVerbosityLevel LogVerbosity = LogVerbosityLevel.INFO;

        public static EngineSettings GenerateDefaults()
        {
            return new EngineSettings();
        }

        public static EngineSettings loadFromDisk()
        {
            //Load jsonstring
            EngineSettings settings;
            if (File.Exists("settings.json"))
            {
                string jsonstring = File.ReadAllText("settings.json");
                settings = JsonConvert.DeserializeObject<EngineSettings>(jsonstring);
            }
            else
            {
                //Generate Settings
                //Generating new settings file
                settings = GenerateDefaults();
                saveToDisk(settings);
            }

            return settings;
        }

        public static void saveToDisk(EngineSettings settings)
        {
            //Test Serialize object
            string jsonstring = JsonConvert.SerializeObject(settings);
            File.WriteAllText("settings.json", jsonstring);
        }

    }   
    
    //Delegates - Function Types for Callbacks
    public delegate void UpdateStatusCallback(string msg);
    //public delegate void OpenAnimCallback(string filepath, Model animScene);
    //public delegate void OpenPoseCallback(string filepath, Model animScene);
    public delegate void ShowInfoMsg(string msg, string caption);
    public delegate void ShowErrorMsg(string msg, string caption);
    public delegate void LogCallback(object sender, string msg, LogVerbosityLevel level);
    public delegate void AssertCallback(bool status, string msg);
    public delegate void SendRequestCallback(ref ThreadRequest req);
    public delegate byte[] GetResourceCallback(string resourceName);
    public delegate byte[] GetResourceFromAssemblyCallback(Assembly assembly, string resourceName);
    //public delegate BMPImage GetBitMapResourceCallback(string resourceName);
    public delegate string GetTextResourceCallback(string resourceName);
    public delegate object GetResourceWithTypeCallback(string resourceName, out string resourceType);
    

    public static class Callbacks
    {
        public static UpdateStatusCallback updateStatus = null;
        public static ShowInfoMsg showInfo = null;
        public static ShowErrorMsg showError = null;
        public static LogCallback Log = null;
        public static AssertCallback Assert = null;
        public static SendRequestCallback issueRequestToGLControl = null;
        public static GetResourceCallback getResource = null;
        public static GetResourceFromAssemblyCallback getResourceFromAssembly = null;
        //public static GetBitMapResourceCallback getBitMapResource = null;
        public static GetTextResourceCallback getTextResource = null;
        public static GetResourceWithTypeCallback getResourceWithType = null;
        
        public static void SetDefaultCallbacks()
        {
            Assert = DefaultAssert;
            Log = DefaultLog;
            getResource = DefaultGetResource;
            getResourceFromAssembly = DefaultGetResourceFromAssembly;
            getTextResource = DefaultGetTextResource;
            //getBitMapResource = DefaultGetBitMapResource;
        }

        public static void DefaultLog(object sender, string msg, LogVerbosityLevel lvl)
        {
            Console.WriteLine($"{sender.ToString().ToUpper()} - {lvl.ToString().ToUpper()} - {msg}");
        }

        public static void DefaultAssert(bool status, string msg)
        {
            if (!status)
            {
                string trace = new System.Diagnostics.StackTrace().ToString();
                Log(Assembly.GetCallingAssembly(), trace, LogVerbosityLevel.ERROR);
                throw new Exception(msg);
            }
        }

        //Resource Handler
        public static byte[] DefaultGetResourceFromAssembly(Assembly assembly, string resource_name)
        {
            byte[] data = null; //output data
            string[] resources = assembly.GetManifestResourceNames();

            //for (int i=0;i<resources.Length;i++)
            //    Console.WriteLine((resources[i]));
            
            try
            {
                string res_name = resources.First(s => s.EndsWith(resource_name));
                BinaryReader _textStreamReader = new(assembly.GetManifestResourceStream(res_name));
                data = _textStreamReader.ReadBytes((int) _textStreamReader.BaseStream.Length);
            } catch
            {
                Log(Assembly.GetCallingAssembly(), string.Format("Unable to Fetch Resource {0}", resource_name), 
                    LogVerbosityLevel.ERROR);
            }
            
            return data;
        }
        
        public static byte[] DefaultGetResource(string resource_name)
        {
            // Determine path
            var assembly = Assembly.GetExecutingAssembly();
            return DefaultGetResourceFromAssembly(assembly, resource_name);
        }

        //public static BMPImage DefaultGetBitMapResource(string resource_name)
        //{
        //    byte[] data = DefaultGetResource(resource_name);
            
        //    if (data != null)
        //    {
        //        return new BMPImage(data);
        //    }

        //    return null;
        //}

        public static string DefaultGetTextResource(string resource_name)
        {
            byte[] data = DefaultGetResource(resource_name);

            if (data != null)
            {
                MemoryStream ms = new(data);
                StreamReader tr = new(ms);
                return tr.ReadToEnd();
            }

            return "";
        }
    }
}

