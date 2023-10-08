using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public class TwosComplement
    {
        static public int toInt(uint val, int bits)
        {
            int mask = 1 << (bits - 1);
            return (int)(-(val & mask) + (val & ~mask));
        }
    }

}
