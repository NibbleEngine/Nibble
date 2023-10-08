using NbCore;
using System.Collections.Generic;
using System.IO;

namespace NbCore
{
    public class AnimNodeFrameData
    {
        public List<NbQuaternion> rotations = new();
        public List<NbVector3> translations = new();
        public List<NbVector3> scales = new();

        public void LoadRotations(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbQuaternion q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                    W = br.ReadSingle()
                };

                rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbVector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                NbVector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }

}
