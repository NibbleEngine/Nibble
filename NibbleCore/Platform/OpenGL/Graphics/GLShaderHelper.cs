using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Generic;
using System.Linq;
using NbCore;
using NbCore.Math;
using NbCore.Common;
using System.Windows;
using NbCore.Utils;


namespace NbCore.Platform.Graphics.OpenGL { 

    
    

    

    public static class GLShaderHelper
    {
        static public string NumberLines(string s)
        {
            if (s == "")
                return s;
                
            string n_s = "";
            string[] split = s.Split('\n');

            for (int i = 0; i < split.Length; i++)
                n_s += (i + 1).ToString() + ": " + split[i] + "\n";
            
            return n_s;
        }

        //Shader Compilation

        public static void throwCompilationError(string log)
        {
            //Lock execution until the file is available
            string log_file = "shader_compilation_log.out";

            if (!File.Exists(log_file))
                File.Create(log_file);

            while (!FileUtils.IsFileReady(log_file))
            {
                Console.WriteLine("Log File not ready yet");
            };
            
            StreamWriter sr = new StreamWriter(log_file);
            sr.Write(log);
            sr.Close();
            Console.WriteLine(log);
            Callbacks.Assert(false, "Shader Compilation Failed. Check Log");
        }
    }
}


