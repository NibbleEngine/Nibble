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
        public AnimationManager AnimMgr = new();
        public readonly ObjectManager<AnimationData> AnimDataMgr = new();

        public AnimationSystem() : base(EngineSystemEnum.ANIMATION_SYSTEM)
        {
            
        }

        public override void CleanUp()
        {
            AnimMgr.CleanUp();
        }

        public void RegisterEntity(Animation e)
        {
            if (e.Type != EntityType.Animation)
            {
                Log($"Unable to register {e.Type} entity", Common.LogVerbosityLevel.WARNING);
                return;
            }



            
            AnimMgr.Add(e);
            if (!AnimDataMgr.Add((ulong) e.animData.MetaData.GetHashCode(), e.animData))
                Log("Animation Data already registered.", Common.LogVerbosityLevel.INFO);
        }

        public override void OnRenderUpdate(double dt)
        {
            foreach (Animation a in AnimMgr.Entities)
            {
                //if (a.IsPlaying)
                {
                    //Update data to the meshgroups
                    foreach (var pair in a.animData.NodeIndexMap)
                    {
                        string node = pair.Key;
                        
                        //Get Node
                        SceneComponent sc = a.AnimationRoot.GetComponent<SceneComponent>() as SceneComponent;
                        SceneGraphNode joint = sc.GetNodeByName(node);
                        TransformController tc = EngineRef.transformSys.GetEntityTransformController(joint);
                        
                        int actualJointIndex = a.animData.NodeIndexMap[node];
                        NbMatrix4 invBindMatrix = a.RefMeshGroup.JointBindingDataList[actualJointIndex].invBindMatrix;
                        NbMatrix4 nodeTransform = invBindMatrix * tc.GetActor().WorldTransformMat;
                        MathUtils.insertMatToArray16(a.RefMeshGroup.GroupTBO1Data, 16 * actualJointIndex, nodeTransform);
                    
                    }

                }
            }
        }

        public override void OnFrameUpdate(double dt)
        {
            foreach (Animation a in AnimMgr.Entities)
            {
                if (a.Override)
                {

                }
                else if (a.IsPlaying)
                {
                    a.Update(a.animData.MetaData.Speed * (float) dt);

                    //Update data to the meshgroups
                    foreach (var pair in a.animData.NodeIndexMap)
                    {
                        string node = pair.Key;
                        NbVector3 nodePosition = a.GetNodeTranslation(node);
                        NbQuaternion nodeRotation = a.GetNodeRotation(node);
                        NbVector3 nodeScale = a.GetNodeScale(node);

                        //Get Node
                        SceneComponent sc = a.AnimationRoot.GetComponent<SceneComponent>() as SceneComponent;
                        SceneGraphNode joint = sc.GetNodeByName(node);
                        TransformController tc = EngineRef.transformSys.GetEntityTransformController(joint);

                        //Add future state
                        tc.AddFutureState(nodePosition, nodeRotation, nodeScale);
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
