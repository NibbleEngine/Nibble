using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using Microsoft.CodeAnalysis.Emit;
using System.Runtime.Loader;
using NbCore.Common;


namespace NbCore.Systems
{
    public class ScriptingSystem : EngineSystem
    {
        public static Dictionary<ulong, AssemblyLoadContext> _appCtxDict = new();
        public Dictionary<NbScriptAsset, List<ScriptComponent>> ScriptComponentMap;
        public Dictionary<ulong, NbScriptAsset> ScriptMap;
        
        public ScriptingSystem() : base(EngineSystemEnum.SCRIPTING_SYSTEM) 
        {
            ScriptMap = new();
            ScriptComponentMap = new();
        }

        public void RegisterEntity(NbScriptAsset script)
        {
            Callbacks.Assert(script != null, "Script should not be null");
            ScriptMap[script.Hash] = script;
            ScriptComponentMap[script] = new();
        }

        public void Remove(ScriptComponent sc)
        {
            Callbacks.Assert(sc.Asset != null, "The Script should not be null");

            //Unload Assembly Context
            if (sc.Script != null)
            {
                _appCtxDict[sc.Script.Hash].Unload();
                _appCtxDict.Remove(sc.Script.Hash);
            }
            
            if (sc.Asset != null)
            {
                ScriptComponentMap[sc.Asset].Remove(sc);
            }
        }
        
        public void Remove(NbScriptAsset script)
        {
            if (ScriptMap.ContainsKey(script.Hash))
            {
                ScriptMap.Remove(script.Hash);
                foreach (ScriptComponent sc in ScriptComponentMap[script])
                    Remove(sc);
                ScriptComponentMap.Remove(script);
            }
        }

        public int GetRegisteredComponents()
        {
            int count = 0;
            foreach (NbScriptAsset script in ScriptComponentMap.Keys)
            {
                count += ScriptComponentMap[script].Count;
            }
            return count;
        }

        public void RegisterEntity(ScriptComponent sc)
        {
            if (ScriptMap.ContainsKey(sc.Asset.Hash))
            {
                if (ScriptComponentMap[sc.Asset].Contains(sc))
                {
                    Log(string.Format("Script Component already registered"), LogVerbosityLevel.INFO);
                    return;
                }
                else
                {
                    ScriptComponentMap[sc.Asset].Add(sc);
                }
            }
            else
            {
                Log(string.Format("Script not registered. Unable to register component"), LogVerbosityLevel.WARNING);
            }
        }
        
        public void RegisterEntity(SceneGraphNode e)
        {
            if (!e.HasComponent<ScriptComponent>())
            {
                Log(string.Format("Entity {0} should have a script component", e.ID), LogVerbosityLevel.INFO);
                return;
            }

            foreach (ScriptComponent sc in e.GetComponents<ScriptComponent>())
            {
                RegisterEntity(sc);
            }

        }
        
        public override void CleanUp()
        {
            ScriptComponentMap.Clear();
            ScriptMap.Clear();
        }
        
        public override void OnFrameUpdate(double dt)
        {
            foreach (NbScriptAsset asset in ScriptComponentMap.Keys)
            {
                foreach(ScriptComponent sc in ScriptComponentMap[asset])
                {
                    //The scriptcomponent should be only supported on SceneGraphNodes
                    sc.Script.OnFrameUpdate((SceneGraphNode) sc.RefEntity, dt);
                }
            }
        }

        public override void OnRenderUpdate(double dt)
        {
            foreach (NbScriptAsset asset in ScriptComponentMap.Keys)
            {
                foreach (ScriptComponent sc in ScriptComponentMap[asset])
                {
                    //The scriptcomponent should be only supported on SceneGraphNodes
                    sc.Script.OnRenderUpdate((SceneGraphNode)sc.RefEntity, dt);
                }
            }
        }

        public NbScript CompileScript(string scriptpath, int id)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(File.ReadAllText(scriptpath));
            // Detect the file location for the library that defines the object type
            var nbcoreRefLocation = Assembly.GetExecutingAssembly().Location;

            //Adding basic references
            List<PortableExecutableReference> refs = new List<PortableExecutableReference>();
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Console.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
            refs.Add(MetadataReference.CreateFromFile(nbcoreRefLocation));

            // A single, immutable invocation to the compiler
            // to produce a library
            ulong hash = NbHasher.Hash(scriptpath + id);

            if (_appCtxDict.ContainsKey(hash))
            {
                _appCtxDict[hash].Unload();
                _appCtxDict.Remove(hash);
            }

            AssemblyLoadContext _ctx = new AssemblyLoadContext(hash.ToString(), true);
            _appCtxDict[hash] = _ctx;

            var compilation = CSharpCompilation.Create(hash.ToString())
              .WithOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                             optimizationLevel: OptimizationLevel.Release,
                                             allowUnsafe: true))
              .AddReferences(refs.ToArray())
              .AddSyntaxTrees(tree);
            MemoryStream ms = new MemoryStream();
            EmitResult compilationResult = compilation.Emit(ms);
            if (compilationResult.Success)
            {
                // Load the assembly
                ms.Seek(0, SeekOrigin.Begin);
                Assembly asm = _ctx.LoadFromStream(ms);

                // Invoke the RoslynCore.Helper.CalculateCircleArea method passing an argument
                Type script_type = null;
                foreach (Type t in asm.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(NbScript)))
                    {
                        script_type = t;
                        break;
                    }
                }

                NbScript main_ob = (NbScript) asm.CreateInstance(script_type.FullName, false, BindingFlags.Default, null, new object[] { RenderState.engineRef },
                    null, null);
                main_ob.Hash = hash;
                ms.Close();
                return main_ob;
            }
            else
            {
                foreach (Diagnostic codeIssue in compilationResult.Diagnostics)
                {
                    string issue = $"ID: {codeIssue.Id}, Message: {codeIssue.GetMessage()}," +
                        $" Location: {codeIssue.Location.GetLineSpan()}," +
                        $" Severity: {codeIssue.Severity}";
                    Log(issue, LogVerbosityLevel.WARNING);
                }
                return null;
            }
        }
    }
}
