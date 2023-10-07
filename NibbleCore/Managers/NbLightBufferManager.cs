using NbCore.Math;
using NbCore.Common;


namespace NbCore
{

    //Light attributes are saved starting from the first uniform of the MeshInstance struct
    //In particular:
    //First uniform (x: fov. y: intensity, z: falloff, w:)

    public class NbLightBufferManager : NbMeshBufferManager
    {
        public static void AddRenderInstance(ref LightComponent lc, TransformData td)
        {
            NbMesh mesh = lc.Mesh;

            if (lc.InstanceID >= 0)
            {
                Callbacks.Assert(false, "Non negative renderInstanceID on a non visible mesh. This should not happen");
                return;
            }

            lc.InstanceID = GetNextMeshInstanceID(ref mesh);

            //Store Component
            mesh.ComponentDict[lc.InstanceID] = lc;

            //Uplod worldMat to the meshVao
            NbMatrix4 actualWorldMat = td.WorldTransformMat;
            NbMatrix4 actualWorldMatInv = (actualWorldMat).Inverted();
            SetInstanceWorldMat(mesh, lc.InstanceID, actualWorldMat);
            SetInstanceWorldMatInv(mesh, lc.InstanceID, actualWorldMatInv);
            SetInstanceNormalMat(mesh, lc.InstanceID, NbMatrix4.Transpose(actualWorldMatInv));

            //LightAttributes
            SetInstanceData(lc);

            mesh.InstanceCount++;
        }
        
        public static void SetInstanceData(LightComponent lc)
        {
            SetInstanceInnerCutoff(lc);
            SetInstanceOutterCutoff(lc);
            SetInstanceIntensity(lc);
            SetInstanceRenderable(lc);
            SetInstanceColor(lc.Mesh, lc.InstanceID, lc.Data.Color);
            SetInstanceFalloff(lc);
            SetInstanceFalloffRadius(lc);
            SetInstanceLightType(lc);
            SetInstanceDirection(lc);
        }

        public static void SetInstanceInnerCutoff(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 0] = (float) System.Math.Cos(MathUtils.radians(lc.Data.InnerCutOff));
        }

        public static void SetInstanceOutterCutoff(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 1] = (float)System.Math.Cos(MathUtils.radians(lc.Data.OutterCutOff));
        }

        public static void SetInstanceIntensity(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 2] = lc.Data.Intensity;
        }

        public static void SetInstanceRenderable(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[1, 0] = lc.Data.IsRenderable ? 1.0f : 0.0f;
        }

        public static void SetInstanceFalloff(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 3] = (float) lc.Data.Falloff;
        }

        public static void SetInstanceLightType(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[2, 3] = lc.Data.LightType == LIGHT_TYPE.POINT ? 0.0f : 1.0f;
        }

        public static void SetInstanceFalloffRadius(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[1, 1] = lc.Data.Falloff_radius;
        }

        public static void SetInstanceDirection(LightComponent lc)
        {
            NbMatrix4 rotX = NbMatrix4.CreateRotationX(MathUtils.radians(lc.Data.Direction.X));
            NbMatrix4 rotY = NbMatrix4.CreateRotationY(MathUtils.radians(lc.Data.Direction.Y));
            NbMatrix4 rotZ = NbMatrix4.CreateRotationZ(MathUtils.radians(lc.Data.Direction.Z));

            NbVector4 endPoint = NbVector4.Transform(new NbVector4(1.0f, 0.0f, 0.0f, 0.0f), rotZ * rotX * rotY);

            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[2, 0] = endPoint.X;
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[2, 1] = endPoint.Y;
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[2, 2] = endPoint.Z;
        }


    }
}
