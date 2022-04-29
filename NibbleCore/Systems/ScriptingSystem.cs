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
        public static Dictionary<string, AssemblyLoadContext> _appCtxDict = new();
        
        public Dictionary<ulong, NbScript> ScriptMap;
        public Dictionary<ulong, ScriptComponent> EntityDataMap;
        
        public ScriptingSystem() : base(EngineSystemEnum.SCRIPTING_SYSTEM) 
        {
            ScriptMap = new();
            EntityDataMap = new();
        }

        public void RegisterEntity(NbScript script)
        {
            if (script != null)
            {
                ScriptMap[script.Hash] = script;
            }
        }
        
        public void Remove(NbScript script)
        {
            if (ScriptMap.ContainsKey(script.Hash))
            {
                ScriptMap.Remove(script.Hash);
            }
        }
        
        public void RegisterEntity(SceneGraphNode e)
        {
            if (!e.HasComponent<ScriptComponent>())
            {
                Log(string.Format("Entity {0} should have a script component", e.ID), LogVerbosityLevel.INFO);
                return;
            }

            if (EntityDataMap.ContainsKey(e.ID))
            {
                Log(string.Format("Entity {0} already registered", e.ID), LogVerbosityLevel.INFO);
                return;
            }
            
            ScriptComponent sc = e.GetComponent<ScriptComponent>() as ScriptComponent;
            EntityDataMap[e.ID] = sc;
        }
        
        public override void CleanUp()
        {
            EntityDataMap.Clear();
        }
        
        public override void OnFrameUpdate(double dt)
        {
            foreach (ScriptComponent sc in EntityDataMap.Values)
            {
                NbScript script = ScriptMap[sc.ScriptHash];
                //The scriptcomponent should be only supported on SceneGraphNodes
                script.OnFrameUpdate((SceneGraphNode) sc.RefEntity, dt);
            }
        }

        public override void OnRenderUpdate(double dt)
        {
            foreach (ScriptComponent sc in EntityDataMap.Values)
            {
                NbScript script = ScriptMap[sc.ScriptHash];
                //The scriptcomponent should be only supported on SceneGraphNodes
                script.OnRenderUpdate((SceneGraphNode)sc.RefEntity, dt);
            }
        }

        public NbScript CompileScript(string scriptpath)
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
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
            refs.Add(MetadataReference.CreateFromFile(nbcoreRefLocation));

            // A single, immutable invocation to the compiler
            // to produce a library
            string hash_name = NbHasher.Hash(scriptpath).ToString();

            if (_appCtxDict.ContainsKey(hash_name))
            {
                _appCtxDict[hash_name].Unload();
                _appCtxDict.Remove(hash_name);
            }

            AssemblyLoadContext _ctx = new AssemblyLoadContext(hash_name, true);
            _appCtxDict[hash_name] = _ctx;

            var compilation = CSharpCompilation.Create(hash_name)
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

                NbScript main_ob = (NbScript)asm.CreateInstance(script_type.FullName, false, BindingFlags.Default, null, new object[] { RenderState.engineRef },
                    null, null);
                main_ob.Hash = NbHasher.Hash(scriptpath);
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
