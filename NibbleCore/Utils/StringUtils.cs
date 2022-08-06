using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NbCore.Utils
{
    public static class StringUtils
    {
        //Parse a string of n and terminate if it is a null terminated string
        public static String read_string(BinaryReader br, int n)
        {
            string s = "";
            bool exit = true;
            long off = br.BaseStream.Position;
            while (exit)
            {
                Char c = br.ReadChar();
                if (c == 0)
                    exit = false;
                else
                    s += c;
            }

            br.BaseStream.Seek(off + 0x80, SeekOrigin.Begin);
            return s;
        }
    }

}
