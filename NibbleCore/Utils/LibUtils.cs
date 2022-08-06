using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NbCore.Utils
{
    public static class LibUtils
    {

        public static void LoadNibbleDependencies()
        {

        }

        public static Assembly LoadAssembly(object sender, ResolveEventArgs args)
        {
            Assembly result = null;
            if (args != null && !string.IsNullOrEmpty(args.Name))
            {

                string[] searchpaths = new[]
                {
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullName),
                    Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullName), "Plugins"),
                    Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().FullName), "Lib")
                };


                //Build potential fullpath to the loading assembly
                var assemblyName = args.Name.Split(new string[] { "," }, StringSplitOptions.None)[0];
                var assemblyExtension = "dll";
                var assemblyFullName = string.Format("{0}.{1}", assemblyName, assemblyExtension);

                string assemblyPath = GetAssemblyPath(assemblyFullName, searchpaths);
                
                if (assemblyPath != "")
                    result = GetAssembly(assemblyPath);

                if (result != null)
                    return result;
            }

            Common.Callbacks.Log(Assembly.GetExecutingAssembly().GetName().Name, $"Unable to load assembly {args.Name}", LogVerbosityLevel.ERROR);
            return args.RequestingAssembly;
        }

        public static Assembly GetAssembly(string filepath)
        {
            try
            {
                //First try to load using the assembly name just in case its a system dll    
                return Assembly.LoadFile(filepath);
            }
            catch (FileNotFoundException ex)
            {
                Common.Callbacks.Log(Assembly.GetExecutingAssembly().GetName().Name, $"Unable to load assembly {filepath}, Error: {ex.Message}", LogVerbosityLevel.WARNING);
                return null;
            }
        }

        public static string GetAssemblyPath(string name, string[] searchpaths)
        {
            foreach (string searchpath in searchpaths)
            {
                var path = Path.Join(searchpath, name);

                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return "";
        }

        public static AssemblyName GetAssemblyName(string name, string[] searchpaths)
        {
            //Fetch AssemblyName
            AssemblyName aName = null;

            foreach (string searchpath in searchpaths)
            {
                var path = Path.Join(searchpath, name);

                if (File.Exists(path))
                {
                    return AssemblyName.GetAssemblyName(path);
                }
                else
                {
                    Common.Callbacks.Log(Assembly.GetExecutingAssembly().GetName().Name, $"Unable to find assembly {path}", LogVerbosityLevel.WARNING);
                }
            }

            return null;

        }


        public static void LoadAssembly(string name, string[] searchpaths)
        {
            string assemblyPath = GetAssemblyPath(name, searchpaths);
            
            if (assemblyPath == "")
                return;

            Assembly test = GetAssembly(assemblyPath);
            if (test == null)
                return;

            //FetchAssembly
            AppDomain.CurrentDomain.Load(test.GetName());
            Common.Callbacks.Log(Assembly.GetExecutingAssembly().GetName().Name, $"Loaded Assembly {test.GetName()}", LogVerbosityLevel.INFO);

            //Load Referenced Assemblies
            AssemblyName[] l = test.GetReferencedAssemblies();
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (AssemblyName a2 in l)
            {
                var asm = loadedAssemblies.FirstOrDefault(a => a.FullName == a2.FullName);

                if (asm == null)
                {
                    LoadAssembly(a2.Name + ".dll", searchpaths);
                }
            }

        }

    }
}
