﻿using System;
using System.Collections.Generic;
using NbCore.Text;

namespace NbCore.Managers
{
    public class FontManager
    {
        public Dictionary<string, Font> FontMap = new();

        public FontManager()
        {
            
        }

        public void addFont(Font f)
        {
            if (FontMap.ContainsKey(f.Name))
                FontMap[f.Name].Dispose();
            FontMap[f.Name] = f;
        }
            

        public Font getFont(string fontName)
        {
            if (!FontMap.ContainsKey(fontName))
            {
                Console.WriteLine("FontManager does not contain font");
                return null;
            }

            return FontMap[fontName];
        }

        public void cleanup()
        {
            foreach (Font f in FontMap.Values)
                f.Dispose();
            FontMap.Clear();
        }

    }

    
}
