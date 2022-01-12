using System;
using System.Collections.Generic;
using System.Text;
using NbCore;

namespace NbCore
{
    public class ReferenceComponent : Component
    {
        public string Reference;
        
        public ReferenceComponent()
        {

        }
        
        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }
}
