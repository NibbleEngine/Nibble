using System;
using System.Collections.Generic;
using System.IO;
using NbCore.Common;
using System.Reflection;


namespace NbCore
{
    public delegate void SourceUpdatedEventHandler();
    
    [NbSerializable]
    public class NbShaderSource : Entity
    {
        public string Name = "";
        public ulong Hash;
        public NbShaderTextType SourceType;
        private List<NbShaderSource> _dynamicTextParts = new();
        private List<string> _staticTextParts = new();
        [NbSerializable]
        public string SourceFilePath = ""; //Path of the file where the source is fetched from
        public string SourceText = ""; //Source Text as fetched from the source file
        public string ResolvedText = ""; //Source Text after processing all dependencies
        [NbSerializable]
        public bool HasWatcher;
        private FileSystemWatcher _watcher;
        private HashSet<string> _watchFiles = new();
        private DateTime LastReadTime;
        public bool Processed = false;
        public bool Resolved = false;

        public SourceUpdatedEventHandler IsUpdated;

        //Keep source texts that the current text refers to
        public HashSet<NbShaderSource> ReferencedSources = new();
        //Keep source texts that reference this source
        public HashSet<NbShaderSource> ReferencedBySources = new();
        //Keeps track of all the Shaders that the current source is used by
        public HashSet<NbShaderConfig> ReferencedByConfigs = new(); 
        
        //Static random generator used in temp file name generation
        private static readonly Random rand_gen = new(999991);

        public NbShaderSource() : base(EntityType.ShaderSource)
        {
            SourceType = NbShaderTextType.Static;
        }

        public NbShaderSource(string text) : base(EntityType.ShaderSource)
        {
            SourceType = NbShaderTextType.Static;
            SourceText = text;
            Name = "Shader_" + RenderState.engineRef.GetShaderSourceCount();
            Hash = (ulong) DateTime.Now.Ticks;
            //Automatically register to engine
            RenderState.engineRef.RegisterEntity(this);
        }

        public NbShaderSource(string filepath, bool watchFile) : base(EntityType.ShaderSource)
        {
            SourceType = NbShaderTextType.Dynamic;
            HasWatcher = watchFile;
            SourceFilePath = Utils.FileUtils.FixPath(filepath);
            SourceText = File.ReadAllText(SourceFilePath);
            LastReadTime = DateTime.Now;
            if (watchFile)
            {
                addFileWatcher(SourceFilePath);
            }
            Hash = NbHasher.Hash(SourceFilePath);
            //Automatically register to engine
            RenderState.engineRef.RegisterEntity(this);
        }

        private void addFileWatcher(string filepath)
        {
            FileSystemWatcher fw = new FileSystemWatcher();
            fw.Changed += file_changed;
            fw.Path = System.IO.Path.GetDirectoryName(filepath);
            fw.Filter = System.IO.Path.GetFileName(filepath);
            fw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            fw.EnableRaisingEvents = true;
            _watcher = fw;
        }

        public void Process()
        {
            _dynamicTextParts.Clear();
            _staticTextParts.Clear();
            
            if (SourceType == NbShaderTextType.Static)
            {
                Processed = true;
                return;
            }
            else //Dynamic Sources
            {
                //Parse source file
                StringReader sr = new StringReader(SourceText);
                string dirpath = System.IO.Path.GetDirectoryName(SourceFilePath);
                string line;
                string[] split;
                string staticpart = "";
                while ((line = sr.ReadLine()) != null)
                {
                    //string line = sr.ReadLine();
                    string original_line = line;
                    line = line.TrimStart(new char[] { ' ' });

                    //Check for preprocessor directives
                    if (line.StartsWith("#include"))
                    {
                        //Save static part
                        if (staticpart != "")
                        {
                            _staticTextParts.Add(staticpart);
                            staticpart = "";
                        }

                        split = line.Split(' ');

                        if (split.Length != 2)
                            throw new ApplicationException("Wrong Usage of #include directive");

                        //get included filepath
                        string npath = split[1].Trim('"');
                        npath = System.IO.Path.Combine(dirpath, npath);
                        //Add dynamic source
                        //Check if Shader Source exists for this path
                        NbShaderSource ss = RenderState.engineRef.GetShaderSourceByFilePath(npath);
                        if (ss == null)
                        {
                            Console.WriteLine($"Loading new dynamic source {npath}");
                            ss = new NbShaderSource(npath, true);
                        }
                        if (!ss.Processed)
                            ss.Process();
                        
                        ReferencedSources.Add(ss);
                        ss.ReferencedBySources.Add(this);
                        
                        _dynamicTextParts.Add(ss);
                        _staticTextParts.Add("[FETCH_DYNAMIC]");
                    }
                    else
                    {
                        staticpart += original_line + '\n';
                    }
                }
                sr.Close();

                //Save last static part
                if (staticpart != "")
                {
                    _staticTextParts.Add(staticpart);
                }
            }

            Processed = true;
        }

        public void Resolve()
        {
            if (!Processed)
                Process();

            ResolvedText = "";

            if (SourceType == NbShaderTextType.Static)
            {
                ResolvedText += SourceText;
            }
            else
            {
                int dynamicPartId = 0;
                for (int i = 0; i < _staticTextParts.Count; i++)
                {
                    if (_staticTextParts[i] == "[FETCH_DYNAMIC]")
                    {
                        if (!_dynamicTextParts[dynamicPartId].Resolved)
                            _dynamicTextParts[dynamicPartId].Resolve();
                        ResolvedText += _dynamicTextParts[dynamicPartId].ResolvedText;
                        dynamicPartId++;
                    }
                    else
                        ResolvedText += _staticTextParts[i];
                }
            }

            Resolved = true;
        }

        public void GetReferencedShaderSources(ref List<NbShaderSource> sources)
        {
            sources.Add(this);
            foreach (NbShaderSource childSource in _dynamicTextParts)
                childSource.GetReferencedShaderSources(ref sources);
        }
        
        private void file_changed(object sender, FileSystemEventArgs e)
        {
            FileSystemWatcher fw = (FileSystemWatcher)sender;
            string path = System.IO.Path.Combine(fw.Path, fw.Filter);
            
            lock (_watchFiles)
            {
                if (_watchFiles.Count > 0)
                {
                    Console.WriteLine($"reload in process..");
                    return;
                }
                else
                {
                    Console.WriteLine($"Adding File..");
                    _watchFiles.Add(path);
                }   
            }

            int openfiletries = 0;
            while (true)
            {
                try
                {
                    string NewSourceText = File.ReadAllText(SourceFilePath);

                    if (NewSourceText == SourceText)
                        Console.WriteLine($"Same Source, nothing to do...");
                    else
                    {
                        Console.WriteLine($"Reloading {path} Change: {e.ChangeType}");
                        SourceText = NewSourceText;
                        Console.WriteLine(NewSourceText);
                        Process();
                        Resolve(); //Recalculate ShaderText
                        //Check if the buffer is not flushed yet
                        if (ResolvedText == "")
                            Console.Write("asdasdasd");
                        
                        //Re-resolve all parent sources
                        foreach (NbShaderSource ps in ReferencedBySources)
                        {
                            ps.Resolved = false;
                            ps.Resolve();
                        };

                        IsUpdated?.Invoke(); //Invoke Source Updated Event
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception {ex.Message}");
                }
                finally
                {
                    openfiletries++;
                }
            }
            
            lock (_watchFiles)
            {
                _watchFiles.Clear();
            }
            Console.WriteLine($"Shader Reload Complete.");
        }

        private string Parser(string path, bool initWatchers)
        {
            //Make sure that the input file is indeed a file
            StreamReader sr;
            string[] split;
            string relpath = "";
            string text = "";
            string tmp_file = "tmp_" + rand_gen.Next().ToString();
            Console.WriteLine("Using temp file {0}", tmp_file);
            bool use_tmp_file = false;
            if (path.EndsWith(".glsl"))
            {
                string execPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                //string execPath = "G:\\Projects\\Model Viewer C#\\Model Viewer\\Viewer_Unit_Tests\\bin\\Debug";
                path = System.IO.Path.Combine(execPath, path);
                Console.WriteLine(path);
                //Check if file exists
                if (!File.Exists(path))
                {
                    //Because of shader files coming either in raw or path format, I should check for resources in
                    //the local Shaders folder as well
                    string basename = System.IO.Path.GetFileName(path);
                    string dirname = System.IO.Path.GetDirectoryName(path);
                    path = System.IO.Path.Combine(dirname, "Shaders", basename);
                    if (!File.Exists(path))
                        throw new ApplicationException("Preprocessor: File not found. Check the input filepath");
                }

                //Add filewatcher
                if (initWatchers)
                    addFileWatcher(path);

                split = System.IO.Path.GetDirectoryName(path).Split(System.IO.Path.PathSeparator);
                relpath = split[split.Length - 1];

                //FileStream fs = new FileStream(path, FileMode.Open);
                sr = new StreamReader(path);
            }
            else
            {
                //Shader has been provided in a raw string
                //Save it to a temp file
                File.WriteAllText(tmp_file, path);
                sr = new StreamReader(tmp_file);
                use_tmp_file = true;
            }

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                //string line = sr.ReadLine();
                string outline = line;
                line = line.TrimStart(new char[] { ' ' });

                //Check for preprocessor directives
                if (line.StartsWith("#include"))
                {
                    split = line.Split(' ');

                    if (split.Length != 2)
                        throw new ApplicationException("Wrong Usage of #include directive");

                    //get included filepath
                    string npath = split[1].Trim('"');
                    npath = npath.TrimStart('/');
                    npath = System.IO.Path.Combine(relpath, npath);
                    outline = Parser(npath, initWatchers);
                }
                //Skip Comments
                else if (line.StartsWith("///")) continue;

                //Finally append the parsed text
                text += outline + '\n';
                //sw.WriteLine(outline);
            }
            //CLose readwrites

            sr.Close();
            if (use_tmp_file)
            {
                File.Delete(tmp_file);
            }
            return text;
        }

        public override NbShaderSource Clone()
        {
            throw new NotImplementedException();
        }

        private bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _dynamicTextParts.Clear();
                    _staticTextParts.Clear();
                }

                disposed = true;
                base.Dispose(disposing);
            }
        }
    
    
        public static NbShaderSource Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            //Parse path
            string filepath = token.Value<string>("SourceFilePath");
            bool haswatcher = token.Value<bool>("HasWatcher");

            return new NbShaderSource(filepath, haswatcher);
        }
    
    }

}
