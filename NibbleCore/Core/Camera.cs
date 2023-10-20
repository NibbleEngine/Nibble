using System;
using NbCore;
using NbCore.Common;
using OpenTK.Mathematics;
using static NbCore.Frustum;

namespace NbCore
{
    public struct CameraPos
    {
        public NbVector3 PosImpulse;
        public NbVector2 Rotation;

        public void Reset()
        {
            PosImpulse = new NbVector3(0.0f);
            Rotation = new NbVector2(0.0f);
        }
    }

    public enum CameraMovementTypeEnum
    {
        FREE_CAM,
        ORBIT_CAM
    }

    public class Camera : Entity
    {
        //Base Coordinate System
        public static NbVector3 BaseRight = new(1.0f, 0.0f, 0.0f);
        public static NbVector3 BaseFront = new(0.0f, 0.0f, -1.0f);
        public static NbVector3 BaseUp = new(0.0f, 1.0f, 0.0f);

        //Current Vectors
        public NbVector3 Position = new(0.0f, 0.0f, 0.0f);
        public NbVector3 Right;
        public NbVector3 Front;
        public NbVector3 Up;
        public float pitch = 0.0f;
        public float yaw = -90.0f;

        //Movement Time

        public static float SpeedScale = 0.001f;
        public static float SensitivityScale = 0.001f;
        public bool isActive = false;

        //Matrices
        public NbMatrix4 projMat;
        public NbMatrix4 projMatInv;
        public NbMatrix4 lookMat;
        public NbMatrix4 lookMatInv;
        public NbMatrix4 cameraRotMat;
        public NbMatrix4 viewMat = NbMatrix4.Identity();
        //That's for object cam
        public NbMatrix4 rotMat = NbMatrix4.Identity();
        public NbVector3 rotAngles = new NbVector3(0.0f);
        public int type;
        public bool culling;
        
        //Camera Frustum Planes
        private readonly Frustum extFrustum = new();
        public NbVector4[] frPlanes = new NbVector4[6];

        public Camera(int mode, bool cull) : base(EntityType.Camera)
        {
            //Set fov on init
            Box _box = new Box(1.0f, 1.0f, 1.0f, new NbVector3(1.0f), true);
            _box.Dispose();
            type = mode;
            culling = cull;

            //calcCameraOrientation(ref Front, ref Right, ref Up, 0, 0);

            //Set Orientation to the basis
            Right = BaseRight;
            Up = BaseUp;
            Front = BaseFront;

        }

        public void UpdateViewMatrixOrbit()
        {
            lookMat = NbMatrix4.LookAt(Position, NbVector3.Zero, BaseUp);

            //TODO: Calculate cameraRotMat

        }

        public void UpdateViewMatrixFree()
        {
            lookMat = NbMatrix4.LookAt(Position, Position + Front, BaseUp);
            
            cameraRotMat = NbMatrix4.CreateRotationX(Math.Radians(System.Math.Clamp(pitch, -89, 89)));
            cameraRotMat *= NbMatrix4.CreateRotationY(Math.Radians(-yaw - 90));
        }

        public void updateViewMatrix()
        {
            if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.FREE_CAM)
                UpdateViewMatrixFree();
            else if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.ORBIT_CAM)
                UpdateViewMatrixOrbit();

            //Calculate Projection Matrix
            NbVector2i viewport_size = NbRenderState.engineRef.GetSystem<Systems.RenderingSystem>().GetViewportSize();
            float aspect = (float)viewport_size.X / viewport_size.Y;
            //float aspect = 1.6f;
            CameraSettings settings = NbRenderState.settings.CamSettings;

            if (type == 0)
            {
                projMat = NbMatrix4.CreatePerspectiveFieldOfView(Math.Radians(settings.FOV), aspect, settings.zNear, settings.zFar);
            }
            else
            {
                //Create orthographic projection
                projMat = NbMatrix4.CreateOrthographic(viewport_size.X, viewport_size.Y, settings.zNear, settings.zFar);
                //Create scale matrix based on the fov
                NbMatrix4 scaleMat = NbMatrix4.CreateScale(0.8f * Math.Radians(settings.FOV));
            }

            //Calculate final matrices
            viewMat = lookMat * projMat;
            lookMatInv = lookMat.Inverted();
            projMatInv = projMat.Inverted();

            updateFrustumPlanes();
        }

        public static void UpdateCameraDirectionalVectors(Camera cam)
        {
            TransformController t_controller = NbRenderState.engineRef.GetSystem<Systems.TransformationSystem>().GetEntityTransformController(cam);
            cam.Position = t_controller.Position;

            if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.FREE_CAM)
            {
                cam.Front.X = (float)System.Math.Cos(Math.Radians(System.Math.Clamp(cam.pitch, -89, 89))) * (float)System.Math.Cos(Math.Radians(cam.yaw));
                cam.Front.Y = (float)System.Math.Sin(Math.Radians(System.Math.Clamp(cam.pitch, -89, 89)));
                cam.Front.Z = (float)System.Math.Cos(Math.Radians(System.Math.Clamp(cam.pitch, -89, 89))) * (float)System.Math.Sin(Math.Radians(cam.yaw));
                cam.Front.Normalize();

                //NbQuaternion q = t_controller.Rotation;
                //cam.Front = NbVector3.Transform(new NbVector3(-1.0f, 0.0f, 0.0f), q);
                //cam.Front.Normalize();
            } else if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.ORBIT_CAM)
            {
                cam.Front = NbVector3.Zero - cam.Position;
                cam.Front.Normalize();
            }

            cam.Right = cam.Front.Cross(BaseUp).Normalized();
            cam.Up = cam.Right.Cross(cam.Front).Normalized();

            //Console.WriteLine($"Camera Up {cam.Up.X} {cam.Up.Y} {cam.Up.Z}");
            //Console.WriteLine($"Camera Front {cam.Front.X} {cam.Front.Y} {cam.Front.Z}");
            //Console.WriteLine($"Camera Right {cam.Right.X} {cam.Right.Y} {cam.Right.Z}");

        }

        public void Reset()
        {
            yaw = -90f; pitch = 0;
            NbVector3 newPosition = NbVector3.Zero;
            if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.ORBIT_CAM)
                newPosition = new NbVector3(1.0f);
            NbQuaternion yaw_q = NbQuaternion.FromAxis(BaseUp, Math.Radians(yaw));
            NbQuaternion pitch_q = NbQuaternion.FromAxis(BaseRight, Math.Radians(pitch));
            NbQuaternion new_rot = pitch_q * yaw_q;

            //Move Camera based on the impulse

            //Calculate Next State 
            NbQuaternion currentRotation = new_rot;
            NbVector3 currentScale = new(1.0f);

            TransformController t_controller = NbRenderState.engineRef.GetSystem<Systems.TransformationSystem>().GetEntityTransformController(this);
            t_controller.AddFutureState(newPosition, currentRotation, currentScale);
        }

        public static void CalculateNextCameraStateFree(Camera cam, CameraPos target)
        {
            TransformController t_controller = NbRenderState.engineRef.GetSystem<Systems.TransformationSystem>().GetEntityTransformController(cam);

            //Calculate actual camera speed

            if (cam.yaw > 360) cam.yaw = 0;
            if (cam.yaw < -360) cam.yaw = 0;

            cam.pitch -= 0.1f * NbRenderState.settings.CamSettings.Sensitivity * target.Rotation.Y;
            cam.yaw += 0.1f * NbRenderState.settings.CamSettings.Sensitivity * target.Rotation.X;

            NbQuaternion yaw_q = NbQuaternion.FromAxis(BaseUp, Math.Radians(cam.yaw));
            NbQuaternion pitch_q = NbQuaternion.FromAxis(BaseRight, Math.Radians(cam.pitch));
            NbQuaternion new_rot = pitch_q * yaw_q;

            //Console.WriteLine("Mouse Displacement {0} {1}",
            //                target.Rotation.X, target.Rotation.Y);

            //Console.WriteLine(string.Format("Camera Rotation {0} {1} {2} {3}",
            //                    new_rot.X, new_rot.Y, new_rot.Z, new_rot.W),
            //                    LogVerbosityLevel.INFO);

            //Move Camera based on the impulse

            //Calculate Next State 
            NbVector3 currentPosition = t_controller.FuturePosition;
            NbQuaternion currentRotation = new_rot;
            NbVector3 currentScale = new(1.0f);

            NbVector3 offset = new();
            offset += SpeedScale * NbRenderState.settings.CamSettings.Speed * target.PosImpulse.X * cam.Right;
            offset += SpeedScale * NbRenderState.settings.CamSettings.Speed * target.PosImpulse.Z * cam.Front;
            offset += SpeedScale * NbRenderState.settings.CamSettings.Speed * target.PosImpulse.Y * cam.Up;

            //Console.WriteLine(string.Format("Camera offset {0} {1} {2}",
            //                    offset.X, offset.Y, offset.Z));

            currentPosition += offset;
            //There is no need to update rotation for the camera. Pitch/Yaw is all we need
            //Quaternion rall = Quaternion.FromEulerAngles(cam.pitch, cam.yaw, 0.0f);
            //currentRotation = rall;

            t_controller.AddFutureState(currentPosition, currentRotation, currentScale);
        }

        public static void CalculateNextCameraStateOrbit(Camera cam, CameraPos target)
        {
            TransformController t_controller = NbRenderState.engineRef.GetSystem<Systems.TransformationSystem>().GetEntityTransformController(cam);

            //Update position 
            NbVector3 currentPosition = t_controller.FuturePosition;
            currentPosition += SpeedScale * NbRenderState.settings.CamSettings.Speed * target.PosImpulse.Z * cam.Front;

            //Apply rotation on position
            NbQuaternion rot_x = NbQuaternion.FromAxis(cam.Right, 
                -0.002f * NbRenderState.settings.CamSettings.Sensitivity * target.Rotation.Y);
            NbQuaternion rot_y = NbQuaternion.FromAxis(BaseUp,
                -0.002f * NbRenderState.settings.CamSettings.Sensitivity * target.Rotation.X);
            currentPosition = NbVector3.Transform(currentPosition, rot_x * rot_y);

            t_controller.AddFutureState(currentPosition, t_controller.FutureRotation, t_controller.FutureScale);
        }

        public static void CalculateNextCameraState(Camera cam, CameraPos target)
        {
            if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.FREE_CAM)
                CalculateNextCameraStateFree(cam, target);
            else if (NbRenderState.settings.CamSettings.CamType == CameraMovementTypeEnum.ORBIT_CAM)
                CalculateNextCameraStateOrbit(cam, target);
        }

        /*
        public void updateTarget(CameraPos target, float interval)
        {
            //Interval is the update interval of the movement defined in the control camera timer
            
            //Cache current Position + Orientation
            PrevPosition = Position;
            PrevDirection = Direction;

            //Rotate Direction
            Quaternion rx = Quaternion.FromAxisAngle(Up, -target.Rotation.X * Sensitivity);
            Quaternion ry = Quaternion.FromAxisAngle(Right, -target.Rotation.Y * Sensitivity); //Looks OK
            //Quaternion rz = Quaternion.FromAxisAngle(Front, 0.0f); //Looks OK

            TargetDirection = Direction * rx * ry;

            float actual_speed = (float) Math.Pow(Speed, SpeedPower);
            
            float step = 0.00001f;
            Vector3 offset = new();
            offset += step * actual_speed * target.PosImpulse.X * Right;
            offset += step * actual_speed * target.PosImpulse.Y * Front;
            offset += step * actual_speed * target.PosImpulse.Z * Up;

            //Update final vector
            TargetPosition += offset;

            //Calculate Time for movement
            
            //Console.WriteLine("TargetPos {0} {1} {2}",
            //    TargetPosition.X, TargetPosition.Y, TargetPosition.Z);
            //Console.WriteLine("PrevPos {0} {1} {2}",
            //    PrevPosition.X, PrevPosition.Y, PrevPosition.Z);
            //Console.WriteLine("TargetRotation {0} {1} {2} {3}",
            //    TargetDirection.X, TargetDirection.Y, TargetDirection.Z, TargetDirection.W);
            //Console.WriteLine("PrevRotation {0} {1} {2} {3}",
            //    PrevDirection.X, PrevDirection.Y, PrevDirection.Z, PrevDirection.W);
            

            float eff_speed = interval * actual_speed / 1000.0f;
            t_pos_move = (TargetPosition - PrevPosition).Length / eff_speed;
            t_rot_move = (TargetDirection - PrevDirection).Length / eff_speed;
            t_start = 0.0f; //Reset time_counter

            //Console.WriteLine("t_pos {0}, t_rot {1}", t_pos_move, t_rot_move);

        }

        */

        /*
        public void Move(double dt)
        {
            
            //calculate interpolation coeff
            t_start += (float) dt;
            float pos_lerp_coeff, rot_lerp_coeff;

            pos_lerp_coeff = t_start / (float) Math.Max(t_pos_move, 1e-4);
            pos_lerp_coeff = MathUtils.clamp(pos_lerp_coeff, 0.0f, 1.0f);
            
            rot_lerp_coeff = t_start / (float)Math.Max(t_rot_move, 1e-4);
            rot_lerp_coeff = MathUtils.clamp(rot_lerp_coeff, 0.0f, 1.0f);
            
            
            //Interpolate Quaternions/Vectors
            Direction = PrevDirection * (1.0f - rot_lerp_coeff) +
                        TargetDirection * rot_lerp_coeff;
            Position = PrevPosition * (1.0f - pos_lerp_coeff) +
                    TargetPosition * pos_lerp_coeff;

            //Update Base Axis
            Quaternion newFront = MathUtils.conjugate(Direction) * new Quaternion(BaseFront, 0.0f) * Direction;
            Front = newFront.Xyz.Normalized();
            Right = Vector3.Cross(Front, BaseUp).Normalized();
            Up = Vector3.Cross(Right, Front).Normalized();
        }
        */



        public void updateFrustumPlanes()
        {
            //extFrustum.CalculateFrustum(viewMat); // New Method
            NbMatrix4 cpy = viewMat;
            //cpy.Transpose();
            
            
            //Front Plane
            frPlanes[(int)ClippingPlane.Front] = new NbVector4(cpy.M13, cpy.M23, cpy.M33, cpy.M43);

            //Back Plane
            frPlanes[(int)ClippingPlane.Back] = new NbVector4(-cpy.M13 + cpy.M14,
                                                                -cpy.M23 + cpy.M24,
                                                                -cpy.M33 + cpy.M34,
                                                                -cpy.M43 + cpy.M44);

            //Left Plane
            frPlanes[(int)ClippingPlane.Left] = new NbVector4(cpy.M14 + cpy.M11,
                                                                cpy.M24 + cpy.M21,
                                                                cpy.M34 + cpy.M31,
                                                                cpy.M44 + cpy.M41);

            //Right Plane
            frPlanes[(int)ClippingPlane.Right] = new NbVector4(-cpy.M11 + cpy.M14,
                                                                -cpy.M21 + cpy.M24,
                                                                -cpy.M31 + cpy.M34,
                                                                -cpy.M41 + cpy.M44);

            //Top Plane
            frPlanes[(int)ClippingPlane.Top] = new NbVector4(-cpy.M12 + cpy.M14,
                                                                -cpy.M22 + cpy.M24,
                                                                -cpy.M32 + cpy.M34,
                                                                -cpy.M42 + cpy.M44);

            //Bottom Plane
            frPlanes[(int)ClippingPlane.Bottom] = new NbVector4(cpy.M14 + cpy.M12,
                                                                cpy.M24 + cpy.M22,
                                                                cpy.M34 + cpy.M32,
                                                                cpy.M44 + cpy.M42);

            //Normalize planes (NOT SURE IF I NEED THAT)
            for (int i = 0; i < 6; i++)
            {
                float length = frPlanes[i].Xyz.Length;
                frPlanes[i].X /= length;
                frPlanes[i].Y /= length;
                frPlanes[i].Z /= length;
                frPlanes[i].W /= length;
            } 
            
        }

        public bool frustum_occlude(NbVector3 AABBMIN, NbVector3 AABBMAX, NbMatrix4 transform)
        {
            if (!NbRenderState.settings.RenderSettings.UseFrustumCulling)
                return true;

            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            NbVector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //Move sphere to object's root position
            bsh_center = (new NbVector4(bsh_center, 1.0f) * transform).Xyz;

            //This is not accurate for some fucking reason
            //return extFrustum.AABBVsFrustum(cand.Bbox, cand.worldMat * transform);

            //In the future I should add the original AABB as well, spheres look to work like a charm for now   
            return extFrustum.SphereVsFrustum(bsh_center, radius);
        }


        public bool frustum_occlude(NbMesh mesh, NbMatrix4 transform)
        {
            if (!culling) return true;

            NbVector4 v1, v2;

            v1 = new NbVector4(mesh.MetaData.AABBMIN, 1.0f);
            v2 = new NbVector4(mesh.MetaData.AABBMAX, 1.0f);

            return frustum_occlude(v1.Xyz, v2.Xyz, transform);
        }


        public override Camera Clone()
        {
            throw new NotImplementedException();
        }

    }

    public class Frustum
    {
        private readonly NbVector4[] _frustum = new NbVector4[6];
        public float[,] _frustum_points = new float[8, 3];

        public const int A = 0;
        public const int B = 1;
        public const int C = 2;
        public const int D = 3;

        public enum ClippingPlane : int
        {
            Right = 0,
            Left = 1,
            Bottom = 2,
            Top = 3,
            Back = 4,
            Front = 5
        }

        private static void NormalizePlane(float[,] frustum, int side)
        {
            float magnitude = 1.0f / (float)System.Math.Sqrt((frustum[side, 0] * frustum[side, 0]) + (frustum[side, 1] * frustum[side, 1])
                                                + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] *= magnitude;
            frustum[side, 1] *= magnitude;
            frustum[side, 2] *= magnitude;
            frustum[side, 3] *= magnitude;
        }

        public bool PointVsFrustum(NbVector4 point)
        {
            for (int i = 0; i < 6; i++)
            {
                if (NbVector4.Dot(_frustum[i], point) <= 0.0f)
                {
                    //Console.WriteLine("Point vs Frustum, Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p, x, y, z);
                    return false;
                }
            }
            return true;
        }

        public bool PointVsFrustum(NbVector3 location)
        {
            return PointVsFrustum(new NbVector4(location, 1.0f));
        }


        public bool AABBVsFrustum(NbVector3[] AABB)
        {
            //Transform points from local to model space
            NbVector4[] tr_AABB = new NbVector4[2];

            tr_AABB[0] = new NbVector4(AABB[0], 1.0f);
            tr_AABB[1] = new NbVector4(AABB[1], 1.0f);


            NbVector4[] verts = new NbVector4[8];
            verts[0] = new NbVector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[1] = new NbVector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[2] = new NbVector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[3] = new NbVector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[4] = new NbVector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[5] = new NbVector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[6] = new NbVector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);
            verts[7] = new NbVector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);


            //Check if all points are outside one of the planes
            for (int p = 0; p < 6; p++)
            {
                //Check all 8 points
                int i;
                for (i = 0; i < 8; i++)
                {
                    if (NbVector4.Dot(_frustum[p], verts[i]) > 0.0f)
                        return true;
                }

            }

            return false;
        }


        public bool SphereVsFrustum(NbVector4 center, float radius)
        {
            float d = 0;
            for (int p = 0; p < 6; p++)
            {
                d = NbVector4.Dot(_frustum[p], center);
                if (d <= -radius)
                {
                    //Console.WriteLine("Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p,
                    //    x, y, z);
                    return false;
                }
            }
            return true;
        }

        public bool SphereVsFrustum(NbVector3 location, float radius)
        {
            return SphereVsFrustum(new NbVector4(location, 1.0f), radius);
        }

        public static bool VolumeVsFrustum(float x, float y, float z, float width, float height, float length)
        {
            /* TO BE REPAIRED
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            */
            return true;
        }

        public bool VolumeVsFrustum(NbVector3 location, float width, float height, float length)
        {
            return VolumeVsFrustum(location.X, location.Y, location.Z, width, height, length);
        }

        public bool CubeVsFrustum(float x, float y, float z, float size)
        {
            /*
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            */
            return true;
        }

        public float distanceFromPlane(int id, NbVector4 point)
        {
            return NbVector4.Dot(_frustum[id], point) / _frustum[id].Length;
        }


        public void CalculateFrustum(NbMatrix4 mvp)
        {
            //Front Plane
            _frustum[(int)ClippingPlane.Front] = new NbVector4(mvp.M13, mvp.M23, mvp.M33, mvp.M43);

            //Back Plane
            _frustum[(int)ClippingPlane.Back] = new NbVector4(-mvp.M13 + mvp.M14, 
                                                              -mvp.M23 + mvp.M24, 
                                                              -mvp.M33 + mvp.M34,
                                                              -mvp.M43 + mvp.M44);

            //Left Plane
            _frustum[(int)ClippingPlane.Left] = new NbVector4(mvp.M14 + mvp.M11, 
                                                              mvp.M24 + mvp.M21,
                                                              mvp.M34 + mvp.M31,
                                                              mvp.M44 + mvp.M41);

            //Right Plane
            _frustum[(int)ClippingPlane.Right] = new NbVector4(-mvp.M11 + mvp.M14, 
                                                               -mvp.M21 + mvp.M24,
                                                               -mvp.M31 + mvp.M34,
                                                               -mvp.M41 + mvp.M44);

            //Top Plane
            _frustum[(int)ClippingPlane.Top] = new NbVector4(-mvp.M12 + mvp.M14, 
                                                             -mvp.M22 + mvp.M24,
                                                             -mvp.M32 + mvp.M34,
                                                             -mvp.M42 + mvp.M44);

            //Bottom Plane
            _frustum[(int)ClippingPlane.Bottom] = new NbVector4(mvp.M14 + mvp.M12,
                                                                mvp.M24 + mvp.M22,
                                                                mvp.M34 + mvp.M32,
                                                                mvp.M44 + mvp.M42);

            //Normalize planes (NOT SURE IF I NEED THAT)
            for (int i = 0; i < 6; i++)
                _frustum[i].Normalize();

        }


        float[] solvePlaneSystem(int p1, int p2, int p3)
        {
            //Setup Matrix
            var A = MathNet.Numerics.LinearAlgebra.Matrix<float>.Build.DenseOfArray(new float[,]
            {
                { _frustum[p1].X, _frustum[p1].Y, _frustum[p1].Z },
                { _frustum[p2].X, _frustum[p2].Y, _frustum[p2].Z },
                { _frustum[p3].X, _frustum[p3].Y, _frustum[p3].Z }
            });

            //Setup Right Hand Side
            var b = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.Dense(new float[]
            { _frustum[p1].W, _frustum[p2].W, _frustum[p3].W });

            var x = A.Solve(b);

            float[] ret_x = new float[3];
            ret_x[0] = x[0];
            ret_x[1] = x[1];
            ret_x[2] = x[2];

            return ret_x;

        }

    }



}
