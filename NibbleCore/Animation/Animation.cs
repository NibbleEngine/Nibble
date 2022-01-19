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
        public SceneGraphNode AnimationRoot; //Reference node to be able to search for joints
        public NbMeshGroup RefMeshGroup = null; //Referenced group of animated meshes
        private int prevFrameIndex = 0;
        public int ActiveFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float animationTime = 0.0f;
        private float prevFrameTime = 0.0f;
        private float nextFrameTime = 0.0f;
        private float LERP_coeff = 0.0f;
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

        public void SetFrame(int frame_id)
        {
            prevFrameIndex = frame_id;
            nextFrameIndex = frame_id;
            ActiveFrameIndex = frame_id;
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

        public void Update(float dt) //time in milliseconds
        {
            animationTime += dt;
            Progress();
        }

        private void Progress() 
        {
            //Override frame based on the GUI
            if (Override)
                return;
            //TODO: The imgui panel for animation should set all these values
            /*
            {
                //Find frames
                prevFrameIndex = activeFrameIndex;
                nextFrameIndex = activeFrameIndex;
                LERP_coeff = 0.0f;
                return;
            }
            */

            int activeFrameCount = (animData.MetaData.FrameEnd == 0 ? animData.FrameCount : System.Math.Min(animData.MetaData.FrameEnd, animData.FrameCount)) - (animData.MetaData.FrameStart != 0 ? animData.MetaData.FrameStart : 0);
            //Assuming a fixed frequency of 60 fps for the animations
            float activeAnimDuration = activeFrameCount / 60.0f; // In ms TOTAL
            float activeAnimInterval = activeAnimDuration / (activeFrameCount - 1); // Per frame time

            if (animationTime > activeAnimDuration)
            {
                if ((animData.MetaData.AnimType == AnimationType.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    prevFrameTime = 0.0f;
                    nextFrameTime = 0.0f;
                    IsPlaying = false;
                    return;
                }
                else
                {
                    animationTime %= activeAnimDuration; //Clamp to correct time span

                    //Properly calculate previous and nextFrameTimes
                    prevFrameIndex = (int) System.Math.Floor(animationTime / activeAnimInterval);
                    nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                    prevFrameTime = activeAnimInterval * prevFrameIndex;
                    nextFrameTime = prevFrameTime + activeAnimInterval;
                }
                    
            }


            if (animationTime > nextFrameTime)
            {
                //Progress animation
                prevFrameIndex = nextFrameIndex;
                ActiveFrameIndex = prevFrameIndex;
                prevFrameTime = nextFrameTime;
                
                nextFrameIndex = (prevFrameIndex + 1) % activeFrameCount;
                nextFrameTime = prevFrameTime + activeAnimInterval;
            }

            LERP_coeff = (animationTime - prevFrameTime) / activeAnimInterval;

            //Console.WriteLine("AnimationTime {0} PrevAnimationTime {1} NextAnimationTime {2} LERP Coeff {3}",
            //    animationTime, prevFrameTime, nextFrameTime, LERP_coeff);

        }

        public NbVector3 GetNodeTranslation(string node)
        {
            //Fetch prevFrame stuff
            NbVector3 prev_p = animData.GetNodeTranslation(node, prevFrameIndex);
            //Fetch nextFrame stuff
            NbVector3 next_p = animData.GetNodeTranslation(node, nextFrameIndex);
            //Interpolate
            NbVector3 p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);

            return p;
        }

        public NbQuaternion GetNodeRotation(string node)
        {
            //Fetch prevFrame stuff
            NbQuaternion prev_q = animData.GetNodeRotation(node, prevFrameIndex);
            //Fetch nextFrame stuff
            NbQuaternion next_q = animData.GetNodeRotation(node, nextFrameIndex);
            //Interpolate
            NbQuaternion q = NbQuaternion.Slerp(next_q, prev_q, LERP_coeff);

            return q;
        }

        public NbVector3 GetNodeScale(string node)
        {
            //Fetch prevFrame stuff
            NbVector3 prev_s = animData.GetNodeScale(node, prevFrameIndex);

            //Fetch nextFrame stuff
            NbVector3 next_s = animData.GetNodeScale(node, nextFrameIndex);

            //Interpolate
            NbVector3 s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            return s;
        }

        public NbMatrix4 GetNodeTransform(Animation a, string node)
        {
            //Fetch prevFrame stuff
            NbQuaternion prev_q = a.animData.GetNodeRotation(node, a.prevFrameIndex);
            NbVector3 prev_p = a.animData.GetNodeTranslation(node, a.prevFrameIndex);
            NbVector3 prev_s = a.animData.GetNodeScale(node, a.prevFrameIndex);

            //Fetch nextFrame stuff
            NbQuaternion next_q = a.animData.GetNodeRotation(node, a.nextFrameIndex);
            NbVector3 next_p = a.animData.GetNodeTranslation(node, a.nextFrameIndex);
            NbVector3 next_s = a.animData.GetNodeScale(node, a.nextFrameIndex);

            //Interpolate
            NbQuaternion q = NbQuaternion.Slerp(next_q, prev_q, a.LERP_coeff);
            NbVector3 p = next_p * a.LERP_coeff + prev_p * (1.0f - a.LERP_coeff);
            NbVector3 s = next_s * a.LERP_coeff + prev_s * (1.0f - a.LERP_coeff);

            return NbMatrix4.CreateFromQuaternion(q) * NbMatrix4.CreateTranslation(p);
            //return NbMatrix4.CreateTranslation(p) * NbMatrix4.CreateFromQuaternion(q);
            //return NbMatrix4.CreateFromQuaternion(q);
        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void ApplyNodeTransform(TransformController tc, string node)
        {
            //Fetch prevFrame stuff
            NbQuaternion prev_q = animData.GetNodeRotation(node, prevFrameIndex);
            NbVector3 prev_p = animData.GetNodeTranslation(node, prevFrameIndex);
            NbVector3 prev_s = animData.GetNodeScale(node, prevFrameIndex);

            //Fetch nextFrame stuff
            NbQuaternion next_q = animData.GetNodeRotation(node, nextFrameIndex);
            NbVector3 next_p = animData.GetNodeTranslation(node, nextFrameIndex);
            NbVector3 next_s = animData.GetNodeScale(node, nextFrameIndex);

            //Interpolate
            NbQuaternion q = NbQuaternion.Slerp(next_q, prev_q, LERP_coeff);
            NbVector3 p = next_p * LERP_coeff + prev_p * (1.0f - LERP_coeff);
            NbVector3 s = next_s * LERP_coeff + prev_s * (1.0f - LERP_coeff);

            //Convert transforms
            tc.AddFutureState(p, q, s);
        }

    }

}