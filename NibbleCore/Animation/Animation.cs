using MathNet.Numerics.Distributions;
using NbCore.Math;
using System;

namespace NbCore
{
    public enum AnimationType
    {
        OneShot,
        Loop
    }
    
    public class Animation : Entity
    {
        public AnimationData animData; //Static Animation Data
        public int ActiveFrameIndex = 0;
        public float AnimationTime = 0.0f;
        public float FrameDuration = 0.0f;
        //private float LERP_coeff = 0.0f;
        public bool loaded = false;
        public bool IsPlaying = false;
        public bool Override = false; //Used to manually manipulate animation
        
        //Constructors
        public Animation() : base(EntityType.Animation)
        {
            
        }
        public Animation(Animation anim) : base(EntityType.Animation)
        {
            CopyFrom(anim);
        }

        public long Hash
        {
            get { return animData.MetaData.GetHashCode(); }
        }

        public void SetFrame(int frame_id)
        {
            if (frame_id != ActiveFrameIndex)
            {
                ActiveFrameIndex = frame_id;
            }   
        }

        public void CopyFrom(Animation anim)
        {
            base.CopyFrom(anim);
        }

        public override Animation Clone()
        {
            Animation ad = new();
            ad.CopyFrom(this);
            
            return ad;
        }

        public void Update(float dt)
        {
            FrameDuration = 1.0f / 60.0f; //Seconds
            FrameDuration /= animData.MetaData.Speed;
            
            AnimationTime += dt;
            AnimationTime %= FrameDuration;
        }

        public void Progress() 
        {
            int activeFrameCount = (animData.MetaData.FrameEnd == 0 ? animData.FrameCount : System.Math.Min(animData.MetaData.FrameEnd, animData.FrameCount)) - (animData.MetaData.FrameStart != 0 ? animData.MetaData.FrameStart : 0);
            //Assuming a fixed frequency of 60 fps for the animations
            
            if ((animData.MetaData.AnimType == AnimationType.OneShot) && (ActiveFrameIndex == activeFrameCount - 1))
            {
                IsPlaying = false;
                ActiveFrameIndex = 0;
            }
            else
            {
                //Advance frames
                ActiveFrameIndex = (ActiveFrameIndex + 1) % activeFrameCount;
            }
            
        }

        public NbVector3 GetNodeTranslation(string node)
        {
            //Fetch prevFrame stuff
            NbVector3 prev_p = animData.GetNodeTranslation(node, ActiveFrameIndex);
            return prev_p;
        }

        public NbQuaternion GetNodeRotation(string node)
        {
            //Fetch prevFrame stuff
            NbQuaternion prev_q = animData.GetNodeRotation(node, ActiveFrameIndex);
            return prev_q;
        }

        public NbVector3 GetNodeScale(string node)
        {
            //Fetch prevFrame stuff
            NbVector3 prev_s = animData.GetNodeScale(node, ActiveFrameIndex);

            return prev_s;
        }

        public NbMatrix4 GetNodeTransform(Animation a, string node)
        {
            //Fetch ActiveFrame stuff
            NbQuaternion q = a.animData.GetNodeRotation(node, a.ActiveFrameIndex);
            NbVector3 p = a.animData.GetNodeTranslation(node, a.ActiveFrameIndex);
            NbVector3 s = a.animData.GetNodeScale(node, a.ActiveFrameIndex);

            return NbMatrix4.CreateFromQuaternion(q) * NbMatrix4.CreateTranslation(p);
            //return NbMatrix4.CreateTranslation(p) * NbMatrix4.CreateFromQuaternion(q);
            //return NbMatrix4.CreateFromQuaternion(q);
        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void ApplyNodeTransform(TransformController tc, string node)
        {
            //Fetch prevFrame stuff
            NbQuaternion q = animData.GetNodeRotation(node, ActiveFrameIndex);
            NbVector3 p = animData.GetNodeTranslation(node, ActiveFrameIndex);
            NbVector3 s = animData.GetNodeScale(node, ActiveFrameIndex);

            //Convert transforms
            tc.AddFutureState(p, q, s);
        }

    }

}