using OpenTK.Mathematics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NbCore
{
    [NbSerializable]
    public struct NbVector4
    {
        internal Vector4 _Value;

        public NbVector4(NbVector4 vec)
        {
            _Value = vec._Value;
        }

        public NbVector4(System.Numerics.Vector4 vec)
        {
            _Value.X = vec.X;
            _Value.Y = vec.Y;
            _Value.Z = vec.Z;
            _Value.W = vec.W;
        }

        public NbVector4(NbVector3 vec, float w)
        {
            _Value = new Vector4(vec._Value, w);
        }

        public NbVector4(float x = 0.0f, float y = 0.0f, float z = 0.0f, float w = 0.0f)
        {
            _Value.X = x;
            _Value.Y = y;
            _Value.Z = z;
            _Value.W = w;
        }
        //Methods

        public override string ToString()
        {
            return $"{_Value.X} {_Value.Y} {_Value.Z} {_Value.W}";
        }

        //Exposed Properties
        public NbVector3 Xyz
        {
            get => new NbVector3()
            {
                _Value = _Value.Xyz
            };

            set =>_Value.Xyz = value._Value;
            
        }

        public float this[int i]
        {
            get => _Value[i];
            set => _Value[i] = value;
        }
        
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
        
        public float W
        {
            get => _Value.W;
            set => _Value.W = value;
        }

        public float Length => _Value.Length;

        public void Normalize()
        {
            _Value.Normalize();
        }
        
        public static NbVector4 operator +(NbVector4 a)
        {
            return a;
        }
        
        public static NbVector4 operator -(NbVector4 a)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(-a._Value.X,
                    -a._Value.Y,-a._Value.Z,-a._Value.W)
            };
            return n;
        }
        
        public static NbVector4 operator +(NbVector4 a, NbVector4 b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a._Value.X + b._Value.X,
                    a._Value.Y + b._Value.Y,
                    a._Value.Z + b._Value.Z,
                    a._Value.W + b._Value.W)
            };
            return n;
        }

        public static NbVector4 operator *(NbVector4 a, NbMatrix4 mat)
        {
            Vector4 res = a._Value * mat._Value;
            return new(res.X, res.Y, res.Z, res.W);
        }
        
        public static NbVector4 operator *(NbVector4 v, float a)
        {
            return new NbVector4()
            {
                _Value = v._Value * a
            };
        }
        
        public static NbVector4 operator *(float a, NbVector4 v)
        {
            return v * a;
        }
        
        public static float Dot(NbVector4 a, NbVector4 b)
        {
            return Vector4.Dot(a._Value, b._Value);
        }
        
        public static NbVector4 operator -(NbVector4 a, NbVector4 b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a._Value.X - b._Value.X,
                    a._Value.Y - b._Value.Y,
                    a._Value.Z - b._Value.Z,
                    a._Value.W - b._Value.W)
            };
            return n;
        }

        public static NbVector4 operator -(float a, NbVector4 b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a - b._Value.X,
                    a - b._Value.Y,
                    a - b._Value.Z,
                    a - b._Value.W)
            };
            return n;
        }

        public static NbVector4 operator -(NbVector4 a, float b)
        {
            NbVector4 n = new()
            {
                _Value = new Vector4(a._Value.X - b,
                                     a._Value.Y - b,
                                     a._Value.Z - b,
                                     a._Value.W - b)
            };
            return n;
        }

        public static NbVector4 Transform(NbVector4 vec, NbQuaternion q)
        {
            return new NbVector4()
            {
                _Value = Vector4.Transform(vec._Value, q._Value)
            };
        }

        public static NbVector4 Transform(NbVector4 vec, NbMatrix4 mat)
        {
            return new NbVector4()
            {
                _Value = Vector4.TransformRow(vec._Value, mat._Value)
            };
        }

        public static NbVector4 Transform(NbMatrix4 mat, NbVector4 vec)
        {
            return new NbVector4()
            {
                _Value = Vector4.TransformColumn(mat._Value, vec._Value)
            };
        }

        public float[] ToArray()
        {
            float[] fmat = new float[4];
            fmat[0] = X;
            fmat[1] = Y;
            fmat[2] = Z;
            fmat[3] = W;

            return fmat;
        }

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
            writer.WritePropertyName("W");
            writer.WriteValue(_Value.W);
            writer.WriteEndObject();
        }

        public static NbVector4 Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            return new()
            {
                X = token.Value<float>("X"),
                Y = token.Value<float>("Y"),
                Z = token.Value<float>("Z"),
                W = token.Value<float>("W")
            };
        }

    }



}