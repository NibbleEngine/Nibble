
namespace NbCore
{
    public class NbTransformData
    {
        [NbSerializable] public float TransX = 0.0f;
        [NbSerializable] public float TransY = 0.0f;
        [NbSerializable] public float TransZ = 0.0f;
        [NbSerializable] public float RotX = 0.0f;
        [NbSerializable] public float RotY = 0.0f;
        [NbSerializable] public float RotZ = 0.0f;
        [NbSerializable] public float ScaleX = 1.0f;
        [NbSerializable] public float ScaleY = 1.0f;
        [NbSerializable] public float ScaleZ = 1.0f;


        public static NbTransformData CreateFromMatrix(NbMatrix4 transform)
        {
            NbTransformData td = new();
            td.SetFromMatrix(transform);
            return td;
        }

        public void SetFromMatrix(NbMatrix4 transform)
        {
            localTranslation = NbMatrix4.ExtractTranslation(transform);
            localRotation = NbMatrix4.ExtractRotation(transform);
            localScale = NbMatrix4.ExtractScale(transform);
            LocalTransformMat = transform;
        }

        //Raw values 
        public NbVector3 localTranslation
        {
            get
            {
                return new(TransX, TransY, TransZ);
            }

            set
            {
                TransX = value.X;
                TransY = value.Y;
                TransZ = value.Z;
            }
        }

        public NbQuaternion localRotation
        {
            get
            {
                return NbQuaternion.FromEulerAngles(Math.Radians(RotX),
                                                  Math.Radians(RotY),
                                                  Math.Radians(RotZ), "XYZ");
            }

            set
            {
                NbVector3 res;
                NbQuaternion.ToEulerAngles(value, out res);
                RotX = Math.Degrees(res.X);
                RotY = Math.Degrees(res.Y);
                RotZ = Math.Degrees(res.Z);
            }
        }

        public NbVector4 WorldPosition
        {
            get
            {
                return WorldTransformMat[3];
            }

        }

        public NbVector3 localScale
        {
            get => new(ScaleX, ScaleY, ScaleZ);

            set
            {
                ScaleX = value.X;
                ScaleY = value.Y;
                ScaleZ = value.Z;
            }
        }

        //Keep Original Values
        private float OldTransX = 0.0f;
        private float OldTransY = 0.0f;
        private float OldTransZ = 0.0f;
        private float OldRotX = 0.0f;
        private float OldRotY = 0.0f;
        private float OldRotZ = 0.0f;
        private float OldScaleX = 1.0f;
        private float OldScaleY = 1.0f;
        private float OldScaleZ = 1.0f;

        public NbMatrix4 LocalTransformMat;
        public NbMatrix4 WorldTransformMat;

        public NbMatrix4 InverseTransformMat;

        private NbTransformData parent;
        public bool IsUpdated;
        public bool IsActive; //TODO: Not sure if this is neede anywhere

        public NbTransformData()
        {
            //Rest Properties
            LocalTransformMat = NbMatrix4.Identity();
            WorldTransformMat = NbMatrix4.Identity();
            InverseTransformMat = NbMatrix4.Identity();
            IsUpdated = false;
            IsActive = true; //by default
        }
        
        public void SetParentData(NbTransformData data)
        {
            parent = data;
        }

        public void ClearParentData()
        {
            parent = null;
        }

        public void RecalculateTransformMatrices()
        {
            LocalTransformMat = NbMatrix4.CreateScale(localScale) *
                                NbMatrix4.CreateFromQuaternion(localRotation) *
                                NbMatrix4.CreateTranslation(localTranslation);

            if (parent != null)
                WorldTransformMat = LocalTransformMat * parent.WorldTransformMat;
            else
                WorldTransformMat = LocalTransformMat;
            IsUpdated = true;
        }

        public void StoreAsOldTransform()
        {
            OldTransX = TransX;
            OldTransY = TransY;
            OldTransZ = TransZ;
            OldRotX = RotX;
            OldRotY = RotY;
            OldRotZ = RotZ;
            OldScaleX = ScaleX;
            OldScaleY = ScaleY;
            OldScaleZ = ScaleZ;
        }

        public void ResetTransform()
        {
            TransX = OldTransX;
            TransY = OldTransY;
            TransZ = OldTransZ;
            RotX = OldRotX;
            RotY = OldRotY;
            RotZ = OldRotZ;
            ScaleX = OldScaleX;
            ScaleY = OldScaleY;
            ScaleZ = OldScaleZ;
        }

        
    }
}
