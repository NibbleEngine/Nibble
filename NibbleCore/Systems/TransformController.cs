using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Systems;

namespace NbCore
{
    public unsafe class TransformController
    {
        //Previous
        public NbVector3 PrevPosition;
        public NbQuaternion PrevRotation;
        public NbVector3 PrevScale;
        
        //Next
        public NbVector3 NextPosition;
        public NbQuaternion NextRotation;
        public NbVector3 NextScale;

        //Current
        public NbVector3 Position;
        public NbQuaternion Rotation;
        public NbVector3 Scale;

        //Future
        public NbVector3 FuturePosition;
        public NbQuaternion FutureRotation;
        public NbVector3 FutureScale;

        private double Time = 0.0;
        private double InterpolationCoeff = 1.0f;
        private NbTransformData actorData = null;
        
        public TransformController(NbTransformData data)
        {
            SetActor(data);
        }
        
        public NbTransformData GetActor()
        {
            return actorData;
        }

        public void SetActor(NbTransformData data)
        {
            actorData = data;
            //Init States
            
            PrevPosition = actorData.localTranslation;
            PrevRotation = actorData.localRotation;
            PrevScale = actorData.localScale;

            NextPosition = actorData.localTranslation;
            NextRotation = actorData.localRotation;
            NextScale = actorData.localScale;

            FuturePosition = actorData.localTranslation;
            FutureRotation = actorData.localRotation;
            FutureScale = actorData.localScale;
        }

        public void ClearActor()
        {
            actorData = null;
        }

        public void AddFutureState(NbVector3 dp, NbQuaternion dr, NbVector3 ds)
        {
            FuturePosition = dp;
            FutureRotation = dr;
            FutureScale = ds;
        }
        
        public void Advance()
        {
            PrevPosition = Position;
            PrevRotation = Rotation;
            PrevScale = Scale;

            NextPosition = FuturePosition;
            NextRotation = FutureRotation;
            NextScale = FutureScale;

            Time = 0.0; //Reset Time
        }

        public void Update(double interval)
        {
            Time += interval;
            Time = System.Math.Min(Time, TransformationSystem.updateInterval);

            InterpolationCoeff = Time / TransformationSystem.updateInterval;
            CalculateState();
        }

        private void CalculateState()
        {
            //Interpolate between the two states
            Position = NbVector3.Lerp(PrevPosition, NextPosition, (float) InterpolationCoeff);
            Rotation = NbQuaternion.Slerp(PrevRotation, NextRotation, (float) InterpolationCoeff);
            Scale = NbVector3.Lerp(PrevScale, NextScale, (float) InterpolationCoeff);

            //Callbacks.Logger.Log(string.Format("Interpolated Position {0} {1} {2}",
            //                    Position.X, Position.Y, Position.Z, Time),
            //                    LogVerbosityLevel.INFO);
            
            ApplyStateToActor(); //Update Actor Data
        }

        private void ApplyStateToActor()
        {
            if (actorData != null)
            {
                actorData.localTranslation = Position;
                actorData.localRotation = Rotation;
                actorData.localScale = Scale;
                actorData.RecalculateTransformMatrices();
            }
        }
    }
}
