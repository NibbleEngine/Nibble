using NbCore.Math;
using NbCore.Common;


namespace NbCore
{

    //Light attributes are saved starting from the first uniform of the MeshInstance struct
    //In particular:
    //First uniform (x: fov. y: intensity, z: falloff, w:)

    public class NbImposterBufferManager : NbMeshBufferManager
    {
        public static void AddRenderInstance(ref ImposterComponent lc, TransformData td)
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

            //Imposter Attributes
            SetInstanceData(lc);

            mesh.InstanceCount++;
        }
        
        public static void SetInstanceData(ImposterComponent ic)
        {
            SetInstanceColor(ic.Mesh, ic.InstanceID, ic.Data.Color);
            SetInstanceWidth(ic);
            SetInstanceHeight(ic);
            SetInstanceImageID(ic);
        }

        public static void SetInstanceWidth(ImposterComponent ic)
        {
            ic.Mesh.InstanceDataBuffer[ic.InstanceID].uniforms[0, 0] = ic.Data.Width;
        }

        public static void SetInstanceHeight(ImposterComponent ic)
        {
            ic.Mesh.InstanceDataBuffer[ic.InstanceID].uniforms[0, 1] = ic.Data.Height;
        }

        public static void SetInstanceImageID(ImposterComponent ic)
        {
            ic.Mesh.InstanceDataBuffer[ic.InstanceID].uniforms[0, 3] = ic.Data.ImageID;
        }

    }
}
