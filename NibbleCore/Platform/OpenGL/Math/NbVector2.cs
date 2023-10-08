using OpenTK.Mathematics;

namespace NbCore
{
    public struct NbVector2
    {
        internal Vector2 _Value;
        
        public float X
        {
            get => _Value.X;
            set => _Value.X = value;
        }
        
        public float Y
        {
            get => _Value.Y;
            set => _Value.Y = value;
        }

        public NbVector2(float v)
        {
            _Value = new Vector2(v);
        }
        
        public NbVector2(float a, float b)
        {
            _Value = new Vector2(a,b);
        }

        public void Normalize()
        {
            _Value.Normalize();
        }

        public NbVector2 Normalized()
        {
            return new NbVector2()
            {
                _Value = _Value.Normalized()
            };
        }

        public static NbVector2 operator *(NbVector2 a, float b)
        {
            return new NbVector2()
            {
                _Value = a._Value * b
            };
        }

        public static NbVector2 operator +(NbVector2 a, NbVector2 b)
        {
            return new NbVector2()
            {
                _Value = a._Value + b._Value
            };
        }

        public static NbVector2 operator -(NbVector2 a, NbVector2 b)
        {
            return new NbVector2()
            {
                _Value = a._Value - b._Value
            };
        }

    }
}