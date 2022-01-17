using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Managers
{
    public class AnimationManager : EntityManager<Animation>
    {
        public bool AddAnimation(Animation o)
        {
            return base.Add(o);
        }

        public bool HasAnimation(long id)
        {
            return Contains(id);
        }

        public Animation GetAnimation(long id)
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
