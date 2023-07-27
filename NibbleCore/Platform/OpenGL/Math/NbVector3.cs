using OpenTK.Mathematics;
using Newtonsoft.Json;

namespace NbCore.Math
{
    [NbSerializable]
    public struct NbVector3
    {
        internal Vector3 _Value;

        public NbVector3(NbVector3 vec)
        {
            _Value = vec._Value;
        }
        
        public NbVector3(float x = 0.0f)
        {
            _Value = new Vector3(x);
        }
        
        public NbVector3(float x, float y, float z)
        {
            _Value.X = x;
            _Value.Y = y;
            _Value.Z = z;
        }
        //Methods
        public void Normalize()
        {
            _Value.Normalize();
        }

        public NbVector3 Cross(NbVector3 a)
        {
            return new NbVector3()
            {
                _Value = Vector3.Cross(_Value, a._Value)
            };
        }

        public float Dot(NbVector3 a)
        {
            return Vector3.Dot(_Value, a._Value);
        }

        public override string ToString()
        {
            return $"{_Value.X} {_Value.Y} {_Value.Z}";
        }

        //Exposed Properties
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
        
        public float Z
        {
            get => _Value.Z;
            set => _Value.Z = value;
        }
        
        public static NbVector3 operator +(NbVector3 a)
        {
            return a;
        }
        
        public static NbVector3 operator -(NbVector3 a)
        {
            NbVector3 n = new()
            {
                _Value = -a._Value
            };
            return n;
        }
        
        public static NbVector3 operator +(NbVector3 a, NbVector3 b)
        {
            NbVector3 n = new()
            {
                _Value = a._Value + b._Value
            };
            return n;
        }
        
        public static NbVector3 operator -(NbVector3 a, NbVector3 b)
        {
            NbVector3 n = new()
            {
                _Value = a._Value - b._Value
            };
            return n;
        }

        public static NbVector3 operator *(NbVector3 v, float a)
        {
            return new NbVector3()
            {
                _Value = v._Value * a
            };
        }
        
        public static NbVector3 operator *(float a, NbVector3 v)
        {
            return v * a;
        }

        public static NbVector3 Transform(NbVector3 vec, NbQuaternion q)
        {
            return new NbVector3()
            {
                _Value = Vector3.Transform(vec._Value, q._Value)
            };
        }

        public static NbVector3 Lerp(NbVector3 a, NbVector3 b, float blend)
        {
            return new NbVector3()
            {
                _Value = Vector3.Lerp(a._Value, b._Value, blend)
            };
        }

        public NbVector3 Normalized()
        {
            return new NbVector3()
            {
                _Value = Vector3.Normalize(_Value)
            };
        }
        
        public float Length => _Value.Length;

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("X");
            writer.WriteValue(_Value.X);
            writer.WritePropertyName("Y");
            writer.WriteValue(_Value.Y);
            writer.WritePropertyName("Z");
            writer.WriteValue(_Value.Z);
            writer.WriteEndObject();
        }

        public static NbVector3 Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            float x = token.Value<float>("X");
            float y = token.Value<float>("Y");
            float z = token.Value<float>("Z");

            return new NbVector3(x, y, z);
        }
    }
}