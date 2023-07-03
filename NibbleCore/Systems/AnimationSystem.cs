using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;
using NbCore.Managers;
using NbCore.Math;

namespace NbCore.Systems
{
    public class AnimationSystem : EngineSystem
    {
        //Keep animations
        public AnimationManager AnimMgr = new();
        public readonly ObjectManager<ulong, AnimationData> AnimDataMgr = new();
        //Organize animations in groups
        public List<NbAnimationGroup> AnimationGroups = new();
        //Properties
        public static double updateInterval = 1.0 / 60; //Default Update interval of 60hz
        
        public AnimationSystem() : base(EngineSystemEnum.ANIMATION_SYSTEM)
        {
            
        }

        public override void CleanUp()
        {
            AnimMgr.CleanUp();
        }

        public void RegisterEntity(AnimComponent ac)
        {
            AnimationGroups.Add(ac.AnimGroup); //Store group
            foreach (Animation anim in ac.AnimGroup.Animations)
            {
                AnimMgr.Add(anim);
                
                if (!AnimDataMgr.Add((ulong)anim.animData.MetaData.GetHashCode(), anim.animData))
                    Log("Animation Data already registered.", LogVerbosityLevel.INFO);
            }
        }

        public override void OnRenderUpdate(double dt)
        {
            foreach (NbAnimationGroup group in AnimationGroups)
            {
                if (group.ActiveAnimation is null)
                    continue;
                
                group.ActiveAnimation.Update((float) dt);
                float interpolationCoeff = (float)(group.ActiveAnimation.AnimationTime / group.ActiveAnimation.FrameDuration);
                
                for (int i = 0; i < group.RefMeshGroup.JointCount; i++)
                {
                    NbMatrix4 prev = group.RefMeshGroup.PrevFrameJointData[i];
                    NbMatrix4 next = group.RefMeshGroup.NextFrameJointData[i];
                    group.RefMeshGroup.GroupTBO1Data[i] = (1.0f - interpolationCoeff) * prev + interpolationCoeff * next;

                }
            }
        }

        public void OnPostFrameUpdate()
        {
            foreach (NbAnimationGroup group in AnimationGroups)
            {
                if (group.ActiveAnimation is null)
                    continue;

                //Swap frame data
                NbMatrix4[] temp = group.RefMeshGroup.PrevFrameJointData;
                group.RefMeshGroup.PrevFrameJointData = group.RefMeshGroup.NextFrameJointData;
                group.RefMeshGroup.NextFrameJointData = temp;

                SceneComponent sc = group.AnimationRoot.GetComponent<SceneComponent>();
                
                foreach (SceneGraphNode joint in sc.JointNodes)
                {
                    JointComponent jc = joint.GetComponent<JointComponent>();
                    TransformComponent tc = joint.GetComponent<TransformComponent>();

                    int actualJointIndex = jc.JointIndex;

                    NbMatrix4 invBindMatrix = group.RefMeshGroup.JointBindingDataList[actualJointIndex].invBindMatrix;
                    group.RefMeshGroup.NextFrameJointData[actualJointIndex] = invBindMatrix * tc.Data.WorldTransformMat;

                }

                
            }
        }

        public override void OnFrameUpdate(double dt)
        {
            foreach (NbAnimationGroup group in AnimationGroups)
            {
                if (group.ActiveAnimation is null)
                    continue;
                
                if (group.ActiveAnimation.IsPlaying)
                {
                    group.ActiveAnimation.Progress();

                    SceneComponent sc = group.AnimationRoot.GetComponent<SceneComponent>();
                    

                    foreach (string anim_node in group.ActiveAnimation.animData.Nodes)
                    {
                        //TODO: THIS SHOULD NOT HAPPEN
                        if (!sc.HasNode(anim_node))
                            continue;

                        SceneGraphNode node = sc.NodeMap[anim_node];

                        if (node.Type == SceneNodeType.LOCATOR)
                            continue;
                        
                        TransformComponent tc = node.GetComponent<TransformComponent>();

                        NbVector3 nodePosition = group.ActiveAnimation.GetNodeTranslation(anim_node);
                        NbQuaternion nodeRotation = group.ActiveAnimation.GetNodeRotation(anim_node);
                        NbVector3 nodeScale = group.ActiveAnimation.GetNodeScale(anim_node);

                        tc.Data.localRotation = nodeRotation;
                        tc.Data.localScale = nodeScale;
                        tc.Data.localTranslation = nodePosition;

                       

                    }
                    EngineRef.RequestEntityTransformUpdate(group.AnimationRoot);
                }
                
            }
            
        }

        public static void StartAnimation(AnimComponent ac, string Anim)
        {
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                if (!ad.IsPlaying)
                    ad.IsPlaying = true;
            }
        }

        public static void StopActiveAnimations(SceneGraphNode anim_model)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>();
            List<Animation> ad_list = ac.getActiveAnimations();
          
            foreach (Animation ad in ad_list)
                ad.IsPlaying = false;
        }

        public static void StopActiveLoopAnimations(Entity anim_model)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>();
            List<Animation> ad_list = ac.getActiveAnimations();

            foreach (Animation ad in ad_list)
            {
                if (ad.animData.MetaData.AnimType == AnimationType.Loop)
                    ad.IsPlaying = false;
            }
                
        }

        public static int queryAnimationFrame(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>();
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return ad.ActiveFrameIndex;
            }
            return -1;
        }

        public static int queryAnimationFrameCount(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            Animation ad = ac.getAnimation(Anim);

            if (ad != null)
            {
                return ad.animData.FrameCount;
            }
            return -1;
        }

        
    }
}
