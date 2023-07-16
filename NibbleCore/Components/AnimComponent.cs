using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public NbAnimationGroup AnimGroup = new();
        public Dictionary<string, Animation> AnimationDict = new();

        //Default Constructor
        public AnimComponent()
        {

        }

        public Animation getAnimation(string Name)
        {
            if (!AnimationDict.ContainsKey(Name))
                return null;
            return AnimationDict[Name];
        }

        public List<Animation> getActiveAnimations()
        {
            List<Animation> animList = new();
            
            foreach (Animation ad in AnimGroup.Animations)
            {
                if (ad.IsPlaying)
                    animList.Add(ad);
            }
                
            return animList;
        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //TODO: Copy Animations

        }

        public override Component Clone()
        {
            AnimComponent ac = new();

            //Copy Animations
            foreach (Animation ad in AnimGroup.Animations)
            {
                Animation clone = new Animation(ad);
                ac.AnimGroup.Animations.Add(clone);
            }
                
            return ac;
        }

        protected AnimComponent(AnimComponent input)
        {
            this.copyFrom(input);
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //TODO: Should we do something with the animgroup here?
                AnimationDict.Clear();
                base.Dispose(disposing);
            }
            
        }

    }
}
