using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace NbCore
{
    public class JointComponent : Component
    {
        [NbSerializable]
        public int JointIndex;
        
        public override Component Clone()
        {
            return new JointComponent()
            {
                JointIndex = JointIndex,
            };
        }

        public override void CopyFrom(Component c)
        {
            JointComponent c2 = (JointComponent)c;
            
            JointIndex = c2.JointIndex;            
        }
    }
}
