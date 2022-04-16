using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public static class NbHasher
    {
        public static ulong CombineHash(ulong seed1, ulong seed2)
        {
            //Function taken from boost lib
            //Ref : https://stackoverflow.com/questions/4948780/magic-number-in-boosthash-combine
            return seed1 ^ (seed2 + 0x9e3779b9 + (seed1 << 6) + (seed1 >> 2));
        }

        public static ulong Hash(string str)
        {
            ulong hash = 0;
            
            for (int i=0;i<str.Length;i++)
            {
                hash += str[i];
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }

            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);

            return hash;
        }

    }
}
