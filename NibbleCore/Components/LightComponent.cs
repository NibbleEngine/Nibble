using System;
using NbCore;
using NbCore.Platform.Graphics;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public class LightComponent : MeshComponent
    {
        //Exposed Light Properties
        public NbLightData Data;
        
        public NbVector3 Direction;
        //Light Projection + View Matrices
        public NbMatrix4[] lightSpaceMatrices;
        public NbMatrix4 lightProjectionMatrix;

        public LightComponent() : base()
        {
            Data = new()
            {
                Color = new NbVector3(1.0f),
                InnerCutOff = -1.0f,
                OutterCutOff = -1.0f,
                Intensity = 1.0f,
                IsRenderable = true,
                Falloff = ATTENUATION_TYPE.QUADRATIC,
                Falloff_radius = 1.0f,
                LightType = LIGHT_TYPE.POINT,
                IsUpdated = true
            };
        }


        public override Component Clone()
        {
            throw new NotImplementedException();
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (InstanceID >= 0)
                    GraphicsAPI.RemoveLightRenderInstance(ref Mesh, this);

                base.Dispose(disposing);
            }
        }

        public new void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Mesh");
            writer.WriteValue(Mesh.Hash.ToString());
            writer.WritePropertyName("Direction");
            IO.NbSerializer.Serialize(Direction, writer);
            writer.WritePropertyName("LightData");
            IO.NbSerializer.Serialize(Data, writer);
            writer.WriteEndObject();
        }

        public new static LightComponent Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string mesh_hash = token.Value<string>("Mesh");
            ulong hash = ulong.Parse(mesh_hash);

            LightComponent lc = new()
            {
                Mesh = NbRenderState.engineRef.GetMesh(hash),
                Direction = (NbVector3) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Direction")),
                Data = (NbLightData) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("LightData"))
            };

            return lc;
        }


    }
}
