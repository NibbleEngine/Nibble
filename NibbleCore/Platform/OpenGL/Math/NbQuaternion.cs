using System.Diagnostics.Contracts;
using System.Xml;
using OpenTK.Mathematics;

namespace NbCore
{
    public struct NbQuaternion
    {
        internal Quaternion _Value;

        public NbQuaternion(NbQuaternion q)
        {
            _Value = q._Value;
        }

        public NbQuaternion(float X, float Y, float Z, float W)
        {
            _Value = new Quaternion(X, Y, Z, W);
        }

        public override string ToString()
        {
            return $"{_Value.X} {_Value.Y} {_Value.Z} {_Value.W}";
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
        
        public float W
        {
            get => _Value.W;
            set => _Value.W = value;
        }

        public NbQuaternion Conjugate()
        {
            NbQuaternion nq = new NbQuaternion(this);
            nq.Conjugate();
            return nq;
        }

        public static NbQuaternion FromEulerAngles(float x, float y, float z, string order)
        {
            //Assume that inputs are in radians already

            var c1 = (float) System.Math.Cos(x / 2);
            var c2 = (float) System.Math.Cos(y / 2);
            var c3 = (float) System.Math.Cos(z / 2);

            var s1 = (float) System.Math.Sin(x / 2);
            var s2 = (float) System.Math.Sin(y / 2);
            var s3 = (float) System.Math.Sin(z / 2);

            NbQuaternion n = new();

            switch (order)
            {
                case "XYZ":
                    n.X = s1 * c2 * c3 + c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 - s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 + s1 * s2 * c3;
                    n.W = c1 * c2 * c3 - s1 * s2 * s3;
                    break;
                
                case "YXZ":
                    n.X = s1 * c2 * c3 + c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 - s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 - s1 * s2 * c3;
                    n.W = c1 * c2 * c3 + s1 * s2 * s3;
                    break;
                
                case "ZXY":
                    n.X = s1 * c2 * c3 - c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 + s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 + s1 * s2 * c3;
                    n.W = c1 * c2 * c3 - s1 * s2 * s3;
                    break;

                case "ZYX":
                    n.X = s1 * c2 * c3 - c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 + s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 - s1 * s2 * c3;
                    n.W = c1 * c2 * c3 + s1 * s2 * s3;
                    break;

                case "YZX":
                    n.X = s1 * c2 * c3 + c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 + s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 - s1 * s2 * c3;
                    n.W = c1 * c2 * c3 - s1 * s2 * s3;
                    break;

                case "XZY":
                    n.X = s1 * c2 * c3 - c1 * s2 * s3;
                    n.Y = c1 * s2 * c3 - s1 * c2 * s3;
                    n.Z = c1 * c2 * s3 + s1 * s2 * c3;
                    n.W = c1 * c2 * c3 + s1 * s2 * s3;
                    break;
                default:
                    throw new System.Exception("Not Supported Euler Order");
            }
            
            return n;
        }

        public static NbQuaternion operator *(NbQuaternion q1, NbQuaternion q2)
        {
            return new NbQuaternion()
            {
                _Value = Quaternion.Multiply(q1._Value, q2._Value)
            };
        }

        public NbVector3 ToEulerAngles()
        {
            return ToEulerAngles(this);
        }

        public static NbVector3 ToEulerAngles(NbQuaternion q)
        {
            Vector3 v = q._Value.ToEulerAngles();
            return new(){X = v.X, Y = v.Y, Z = v.Z};
        }

        public static NbQuaternion FromMatrix(NbMatrix4 mat)
        {
            Quaternion q = mat._Value.ExtractRotation();
            return new()
            {
                X = q.X,
                Y = q.Y,
                Z = q.Z,
                W = q.W
            };
        }

        public static NbQuaternion FromAxis(NbVector3 axis, float angle)
        {
            return new NbQuaternion()
            {
                _Value = Quaternion.FromAxisAngle(axis._Value, angle)
            };
        }

        public static void ToEulerAngles(NbQuaternion q, out NbVector3 v)
        {
            Quaternion.ToEulerAngles(q._Value, out var vt);
            v = new NbVector3(vt.X, vt.Y, vt.Z);
        }

        public static NbQuaternion Slerp(NbQuaternion a, NbQuaternion b, float c)
        {
            return new NbQuaternion()
            {
                _Value = Quaternion.Slerp(a._Value, b._Value, c)
            };
        }
        
    }
}