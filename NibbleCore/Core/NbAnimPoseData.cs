using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    //Animation Classes
    public class NbAnimPoseData
    {
        public AnimationData animData;
        public int FrameStart;
        public int FrameEnd;

        public NbAnimPoseData()
        {

        }

        public NbAnimPoseData(NbAnimPoseData apd)
        {
            animData = apd.animData;
            FrameStart = apd.FrameStart;
            FrameEnd = apd.FrameEnd;
        }

    }
}
