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
            SetInstanceFOV(lc);
            SetInstanceIntensity(lc);
            SetInstanceRenderable(lc);
            SetInstanceColor(lc.Mesh, lc.InstanceID, lc.Data.Color);
            SetInstanceFalloff(lc);
            SetInstanceLightType(lc);
        }

        public static void SetInstanceFOV(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 0] = lc.Data.FOV;
        }

        public static void SetInstanceIntensity(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 1] = lc.Data.Intensity;
        }

        public static void SetInstanceRenderable(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 2] = lc.Data.IsRenderable ? 1.0f : 0.0f;
        }

        public static void SetInstanceFalloff(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[0, 3] = (float) lc.Data.Falloff;
        }

        public static void SetInstanceLightType(LightComponent lc)
        {
            lc.Mesh.InstanceDataBuffer[lc.InstanceID].uniforms[1, 0] = (float) lc.Data.LightType;
        }

    }
}
