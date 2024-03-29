﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OpenTK;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;
using NbCore;
using NbCore.Input;
using NbCore.Utils;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using System.ComponentModel;
using System.Diagnostics.Contracts;
using Newtonsoft.Json;
using System.Resources;
using System.Reflection;
using System.IO;
//using System.Drawing;
using SixLabors.ImageSharp;
using System.Linq;
using NbCore.Plugins;

namespace NbCore.Common
{
    
    public static class RenderState
    {
        //Keep the view rotation Matrix
        public static NbMatrix4 rotMat = NbMatrix4.Identity();

        //Keep the view rotation Angles (in degrees)
        public static NbVector3 rotAngles = new NbVector3(0.0f);

        //App Settings
        public static Settings settings = new Settings();

        //Engine Reference
        public static Engine engineRef;

        //Keep the main camera global
        public static Camera activeCam;
        //Item Counter
        public static int itemCounter = 0;
        //Status
        public static string StatusString = "";

        public static bool enableShaderCompilationLog = true;
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

    public interface ISettings
    {
        public static ISettings GenerateDefaults()
        {
            return null;
        }

        public void SaveToFile(string filename)
        {
            var jsonobject = JsonConvert.SerializeObject(this);
            File.WriteAllText(filename, jsonobject);
        }
    }


    public class RenderSettings
    {
        public int FPS = 60;
        public bool UseVSync = false;
        public float HDRExposure = 0.005f;
        
        //Set Full rendermode by default
        [JsonIgnore]
        public PolygonMode RENDERMODE 
        {
            get {
                if (RenderWireFrame)
                    return PolygonMode.Line;
                return PolygonMode.Fill;
            }
        }

        [JsonIgnore]
        public Color clearColor = new(new SixLabors.ImageSharp.PixelFormats.Rgba32(33,33,33,255));
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
        public bool UseBLOOM = true;

        [JsonIgnore]
        public bool UseFrustumCulling = true;

        [JsonIgnore]
        public bool LODFiltering = true;

        [JsonIgnore]
        public bool RenderWireFrame = false;

        [JsonIgnore]
        public bool ToggleAnimations = true;

    }


    public class EngineSettings : ISettings
    {
        public RenderSettings RenderSettings = new();
        public ViewSettings ViewSettings = new();
        public bool EnableShaderCompilationLog = true;
        public LogVerbosityLevel LogVerbosity = LogVerbosityLevel.INFO;

        public static EngineSettings GenerateDefaults()
        {
            EngineSettings settings = new EngineSettings();
            return settings;
        }

    }   
    
    //Get rid of that class
    public class Settings
    {
        //Public Settings
        public RenderSettings renderSettings = new RenderSettings();
        public ViewSettings viewSettings = new ViewSettings(31);
        public CameraSettings camSettings = new CameraSettings(90, 1.0f, 1.0f, 0.05f, 30000f);

        //Private Settings
        public LogVerbosityLevel LogVerbosity;

        //Methods
        public static Settings generateDefaultSettings()
        {
            Settings settings = new Settings();

            return settings;
        }

        public static Settings loadFromDisk()
        {
            //Load jsonstring
            Settings settings;
            if (File.Exists("settings.json"))
            {
                string jsonstring = File.ReadAllText("settings.json");
                settings =  JsonConvert.DeserializeObject<Settings>(jsonstring);
            } else
            {
                //Generate Settings
                //Generating new settings file
                settings = generateDefaultSettings();
                saveToDisk(settings);
            }
            
            return settings;
        }

        public static void saveToDisk(Settings settings)
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
    public delegate Image GetBitMapResourceCallback(string resourceName);
    public delegate string GetTextResourceCallback(string resourceName);
    public delegate object GetResourceWithTypeCallback(string resourceName, out string resourceType);
    

    public static class Callbacks
    {
        public static UpdateStatusCallback updateStatus = null;
        public static ShowInfoMsg showInfo = null;
        public static ShowErrorMsg showError = null;
        public static NbLogger Logger = null;
        public static AssertCallback Assert = null;
        public static SendRequestCallback issueRequestToGLControl = null;
        public static GetResourceCallback getResource = null;
        public static GetResourceFromAssemblyCallback getResourceFromAssembly = null;
        public static GetBitMapResourceCallback getBitMapResource = null;
        public static GetTextResourceCallback getTextResource = null;
        public static GetResourceWithTypeCallback getResourceWithType = null;
        
        
        public static void SetDefaultCallbacks()
        {
            Assert = DefaultAssert;
            getResource = DefaultGetResource;
            getResourceFromAssembly = DefaultGetResourceFromAssembly;
            getTextResource = DefaultGetTextResource;
            getBitMapResource = DefaultGetBitMapResource;
        }

        public static void DefaultAssert(bool status, string msg)
        {
            if (!status)
            {
                string trace = new System.Diagnostics.StackTrace().ToString();
                Logger.Log(Assembly.GetCallingAssembly(), trace, LogVerbosityLevel.ERROR);
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
                Logger.Log(Assembly.GetCallingAssembly(), string.Format("Unable to Fetch Resource {0}", resource_name), 
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

        public static Image DefaultGetBitMapResource(string resource_name)
        {
            byte[] data = DefaultGetResource(resource_name);

            if (data != null)
            {
                Image im = Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(data);
                return im;
            }

            return null;
        }

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
