using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    public class AnimationManager : NbEntityManager<Animation>
    {
        public bool AddAnimation(Animation o)
        {
            return base.Add(o);
        }

        public bool HasAnimation(ulong id)
        {
            return Contains(id);
        }

        public Animation GetAnimation(ulong id)
        {
            return Get(id) as Animation;
        }


        public override void CleanUp()
        {
            foreach (Animation anim in Entities)
                anim.Dispose();
            
            base.CleanUp();
        }

    }
}
