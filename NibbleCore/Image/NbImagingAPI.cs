using NbCore.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NbCore.Image
{
    public enum NbUncompressedImageFormat
    {
        PNG,
        JPEG,
        BMP
    }

    public static class NbImagingAPI
    {
        private static bool Initialized = false;
        private const string ImagingLibrary = "SixLabors.ImageSharp";
        private static Dictionary<string, Type> ImagingLibraryTypes = new();
        private static Dictionary<string, MethodInfo> ImagingLibraryMethods = new();
        private static Assembly image_lib = null;

        //Private Methods
        private static MethodInfo FindMethod(Type owner, string methodName, Type[] paramTypes, bool isGeneric=false)
        {
            MethodInfo[] methods = owner.GetMethods().Where(x => x.Name == methodName && x.IsGenericMethod == isGeneric).ToArray();

            MethodInfo method = null;
            foreach (MethodInfo m in methods)
            {
                ParameterInfo[] mparams = m.GetParameters();
                if (mparams.Length != paramTypes.Length)
                    continue;

                bool sigCheck = true;
                for (int i = 0; i < mparams.Length; i++)
                {
                    sigCheck &= mparams[i].ParameterType == paramTypes[i];
                }

                if (sigCheck)
                    method = m;
            }

            return method;
        }

        private static void Init()
        {
            image_lib = Assembly.Load(ImagingLibrary);


            //Fetch Types
            Type[] types = image_lib.GetTypes();

            foreach (Type type in types)
            {
                if (type.Name == "Image")
                    ImagingLibraryTypes["Image"] = type;
                else if (type.Name == "Image`1" && type.IsGenericType)
                    ImagingLibraryTypes["Image<T>"] = type;
                else if (type.Name == "Bgra32")
                    ImagingLibraryTypes["BGRA32"] = type;
                else if (type.Name == "Rgba32")
                    ImagingLibraryTypes["RGBA32"] = type;
                else if (type.Name == "BmpEncoder")
                    ImagingLibraryTypes["BmpEncoder"] = type;
                else if (type.Name == "JpegEncoder")
                    ImagingLibraryTypes["JpegEncoder"] = type;
                else if (type.Name == "PngEncoder")
                    ImagingLibraryTypes["PngEncoder"] = type;
            }

            //Setup Generic Types
            ImagingLibraryTypes["Image<Rgba32>"] = ImagingLibraryTypes["Image<T>"].MakeGenericType(ImagingLibraryTypes["RGBA32"]);
            ImagingLibraryTypes["Image<Bgra32>"] = ImagingLibraryTypes["Image<T>"].MakeGenericType(ImagingLibraryTypes["BGRA32"]);

            //Fetch Methods 
            //Image.Load<T>(byte[] data)
            MethodInfo LoadMethod = FindMethod(ImagingLibraryTypes["Image"], "Load", new Type[] { typeof(byte[]) }, true);
            MethodInfo LoadPixelDataMethod = FindMethod(ImagingLibraryTypes["Image"], "LoadPixelData", new Type[] { typeof(byte[]), typeof(int), typeof(int) }, true);

            //Setup Generics
            ImagingLibraryMethods["Image.Load<Rgba32>"] = LoadMethod.MakeGenericMethod(ImagingLibraryTypes["RGBA32"]);
            ImagingLibraryMethods["Image.Load<Bgra32>"] = LoadMethod.MakeGenericMethod(ImagingLibraryTypes["BGRA32"]);
            ImagingLibraryMethods["Image.LoadPixelData<Bgra32>"] = LoadPixelDataMethod.MakeGenericMethod(ImagingLibraryTypes["BGRA32"]);
            ImagingLibraryMethods["Image.LoadPixelData<Rgba32>"] = LoadPixelDataMethod.MakeGenericMethod(ImagingLibraryTypes["RGBA32"]);

            //Save Methods
            ImagingLibraryMethods["Image<Rgba32>.Save"] = ImagingLibraryTypes["Image<Rgba32>"].GetMethod("Save");
            ImagingLibraryMethods["Image<Bgra32>.Save"] = ImagingLibraryTypes["Image<Bgra32>"].GetMethod("Save");

            Initialized = true;
        }

        private static object ImageLoad(byte[] data, NbTextureInternalFormat fmt)
        {
            MethodInfo method = null;

            switch (fmt)
            {
                case NbTextureInternalFormat.RGBA8:
                    method = ImagingLibraryMethods["Image.Load<Rgba32>"];
                    break;
                case NbTextureInternalFormat.BGRA8:
                    method = ImagingLibraryMethods["Image.Load<Bgra32>"];
                    break;
            }

            object image = null;
            try
            {
                image = method.Invoke(null, new object[] { data });
            }
            catch (Exception ex)
            {
                Callbacks.Log(Assembly.GetCallingAssembly().GetName().Name, ex.Message, LogVerbosityLevel.HIDEBUG);
            }

            return image;
        }

        private static object ImageLoad(byte[] data, int width, int height, NbTextureInternalFormat fmt)
        {
            MethodInfo method = null;

            switch (fmt)
            {
                case NbTextureInternalFormat.RGBA8:
                    method = ImagingLibraryMethods["Image.LoadPixelData<Rgba32>"];
                    break;
                case NbTextureInternalFormat.BGRA8:
                    method = ImagingLibraryMethods["Image.LoadPixelData<Bgra32>"];
                    break;
            }

            object image = null;
            try
            {
                image = method.Invoke(null, new object[] { data, width, height });
            }
            catch (Exception ex)
            {
                Callbacks.Log(Assembly.GetCallingAssembly().GetName().Name, ex.Message, LogVerbosityLevel.HIDEBUG);
            }
            
            return image;
        }

        private static object ImageLoad(NbTextureData tex)
        {
            return ImageLoad(tex.Data, tex.Width, tex.Height, tex.pif);
        }

        private static NbTextureData GetTextureDataFromImage(object image)
        {
            Type ImageType = image.GetType();
            Type PixelType = ImageType.GetGenericArguments()[0];

            //Get Memory type
            var systemdll = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == "System.Private.CoreLib").FirstOrDefault();

            Type MemoryGeneric = systemdll.GetTypes().Where(x => x.Name == "Memory`1" && x.IsGenericType).FirstOrDefault();
            Type MemoryPixel = MemoryGeneric.MakeGenericType(PixelType);

            //Fetch Method DangerousTryGetSinglePixelMemory(out pixels) from ImageType;
            MethodInfo GetSinglePixelMemory = ImageType.GetMethod("DangerousTryGetSinglePixelMemory");
            object[] vParams = new object[] { null };
            GetSinglePixelMemory.Invoke(image, vParams);

            var pixels = MemoryPixel.GetMethod("ToArray").Invoke(vParams[0], new object[] { });
            Type ArrayType = pixels.GetType();
            int pixel_length = (int)ArrayType.GetProperty("Length").GetValue(pixels);

            //Get Single Value Method
            MethodInfo GetValueMethod = FindMethod(ArrayType, "GetValue", new Type[] { typeof(int) });
            
            byte[] pixel_data = new byte[pixel_length * 4];
            for (int i = 0; i < pixel_length; i++)
            {
                var pixel = GetValueMethod.Invoke(pixels, new object[] { i });
                pixel_data[4 * i + 0] = (byte) PixelType.GetField("R").GetValue(pixel);
                pixel_data[4 * i + 1] = (byte) PixelType.GetField("G").GetValue(pixel);
                pixel_data[4 * i + 2] = (byte) PixelType.GetField("B").GetValue(pixel);
                pixel_data[4 * i + 3] = (byte) PixelType.GetField("A").GetValue(pixel);
            }

            NbTextureData texdata = new()
            {
                Width = (int)ImageType.GetProperty("Width").GetValue(image),
                Height = (int)ImageType.GetProperty("Height").GetValue(image),
                Data = pixel_data,
                MipMapCount = 1,
                Depth = 1,
                target = NbTextureTarget.Texture2D,
                pif = NbTextureInternalFormat.RGBA8
            };

            return texdata;

        }

        private static void ImageSave(object image, string filepath)
        {
            MethodInfo method = image.GetType().GetMethod("Save");
            FileStream ms = new FileStream(filepath, FileMode.Create);
            
            //Identify encoder
            string ext = Path.GetExtension(filepath).Replace(".", string.Empty);
            TextInfo info = CultureInfo.CurrentCulture.TextInfo;
            string encoderName = info.ToTitleCase(ext) + "Encoder";

            Type encoderType = ImagingLibraryTypes[encoderName];

            method.Invoke(image, new object[] { ms, Activator.CreateInstance(encoderType) });
            ms.Close();
        }

        //Public Methods
        public static NbTextureData Load(byte[] pixeldata, int width, int height, NbTextureInternalFormat fmt)
        {
            if (!Initialized)
                Init();

            object image = ImageLoad(pixeldata, width, height, fmt);
            return GetTextureDataFromImage(image);
        }

        public static NbTextureData Load(byte[] image_data, NbTextureInternalFormat fmt)
        {
            if (!Initialized)
                Init();

            object image = ImageLoad(image_data, fmt);
            return GetTextureDataFromImage(image);
        }

        public static NbTextureData Load(string filepath)
        {
            if (!Initialized)
                Init();

            byte[] image_data = File.ReadAllBytes(filepath);
            //Identify encoder
            string ext = Path.GetExtension(filepath).Replace(".", string.Empty).ToUpper();
            NbTextureInternalFormat fmt = NbTextureInternalFormat.BGRA8;
            switch (ext)
            {
                case "BMP":
                    fmt = NbTextureInternalFormat.RGBA8; break;
                case "PNG":
                    fmt = NbTextureInternalFormat.BGRA8; break;
            }

            return Load(image_data, fmt);
        }

        public static void ImageSave(NbTextureData tex, string filepath)
        {
            if (!Initialized)
                Init();

            object image = ImageLoad(tex);
            ImageSave(image, filepath);
        }

    }
}
