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
        public readonly ObjectManager<AnimationData> AnimDataMgr = new();
        //Organize animations in groups
        public List<NbAnimationGroup> AnimationGroups = new();
        
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
                    Log("Animation Data already registered.", Common.LogVerbosityLevel.INFO);
            }
        }

        public override void OnRenderUpdate(double dt)
        {
            foreach (NbAnimationGroup group in AnimationGroups)
            {
                foreach (Animation a in group.Animations)
                {
                    SceneComponent sc = group.AnimationRoot.GetComponent<SceneComponent>() as SceneComponent;

                    //if (a.IsPlaying)
                    {
                        //Update data to the meshgroups
                        foreach (var node in a.animData.Nodes)
                        {
                            SceneGraphNode joint;
                            if (node.EndsWith("JNT"))
                            {
                                string temp_name = node;
                                if (node.Contains("End"))
                                    temp_name = temp_name.Replace("JNT", "");
                                joint = sc.JointNodes[temp_name];
                            }
                            else
                            {
                                joint = sc.GetNodeByName(node);
                            }
                            
                            if (joint == null)
                                continue;
                            
                            if (joint.Type == SceneNodeType.JOINT)
                            {
                                //TransformControllers are only available on dynamic entities....
                                TransformController tc = EngineRef.transformSys.GetEntityTransformController(joint);
                                JointComponent jc = joint.GetComponent<JointComponent>() as JointComponent;
                                int actualJointIndex = jc.JointIndex;
                                NbMatrix4 invBindMatrix = group.RefMeshGroup.JointBindingDataList[actualJointIndex].invBindMatrix;
                                NbMatrix4 nodeTransform = invBindMatrix * tc.GetActor().WorldTransformMat;
                                MathUtils.insertMatToArray16(group.RefMeshGroup.GroupTBO1Data, 16 * actualJointIndex, nodeTransform);
                            } 
                        }

                    }
                }
                
            }
        }

        public override void OnFrameUpdate(double dt)
        {
            foreach (NbAnimationGroup group in AnimationGroups)
            {
                foreach (Animation a in group.Animations)
                {
                    if (a.IsPlaying)
                        a.Update((float)dt);

                    if (a.IsUpdated)
                    {
                        //Update data to the meshgroups
                        foreach (string node in a.animData.Nodes)
                        {
                            NbVector3 nodePosition = a.GetNodeTranslation(node);
                            NbQuaternion nodeRotation = a.GetNodeRotation(node);
                            NbVector3 nodeScale = a.GetNodeScale(node);

                            //Get Node
                            SceneComponent sc = group.AnimationRoot.GetComponent<SceneComponent>() as SceneComponent;
                            SceneGraphNode joint = sc.JointNodes[node];
                            if (joint != null && joint.Type == SceneNodeType.JOINT)
                            {
                                TransformController tc = EngineRef.transformSys.GetEntityTransformController(joint);
                                //Add future state
                                tc.AddFutureState(nodePosition, nodeRotation, nodeScale);
                            }
                                
                        }
                        a.IsUpdated = false;
                    }
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
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            List<Animation> ad_list = ac.getActiveAnimations();
          
            foreach (Animation ad in ad_list)
                ad.IsPlaying = false;
        }

        public static void StopActiveLoopAnimations(Entity anim_model)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
            List<Animation> ad_list = ac.getActiveAnimations();

            foreach (Animation ad in ad_list)
            {
                if (ad.animData.MetaData.AnimType == AnimationType.Loop)
                    ad.IsPlaying = false;
            }
                
        }

        public static int queryAnimationFrame(Entity anim_model, string Anim)
        {
            AnimComponent ac = anim_model.GetComponent<AnimComponent>() as AnimComponent;
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
