using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Math;
using NbCore.Platform.Graphics;
using Newtonsoft.Json;

namespace NbCore
{
    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }

    [NbSerializable]
    public struct LightData
    {
        public NbVector3 Color;
        public float FOV;
        public float Intensity;
        public bool IsRenderable;
        public ATTENUATION_TYPE Falloff;
        public float Falloff_radius;
        public LIGHT_TYPE LightType;
        public bool IsUpdated;

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Color");
            IO.NbSerializer.Serialize(Color, writer);
            writer.WritePropertyName("FOV");
            writer.WriteValue(FOV);
            writer.WritePropertyName("Intensity");
            writer.WriteValue(Intensity);
            writer.WritePropertyName("IsRenderable");
            writer.WriteValue(IsRenderable);
            writer.WritePropertyName("Falloff");
            writer.WriteValue(Falloff.ToString());
            writer.WritePropertyName("FalloffRadius");
            writer.WriteValue(Falloff_radius);
            writer.WritePropertyName("LightType");
            writer.WriteValue(LightType.ToString());
            writer.WriteEndObject();
        }

        public static LightData Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            return new()
            {
                Color = (NbVector3)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Color")),
                FOV = token.Value<float>("FOV"),
                Intensity = token.Value<float>("Intensity"),
                IsRenderable = token.Value<bool>("IsRenderable"),
                Falloff = (ATTENUATION_TYPE)Enum.Parse(typeof(ATTENUATION_TYPE), token.Value<string>("Falloff")),
                Falloff_radius = token.Value<float>("FalloffRadius"),
                LightType = (LIGHT_TYPE)Enum.Parse(typeof(LIGHT_TYPE), token.Value<string>("LightType"))
            };
        }

    }

    [NbSerializable]
    public class LightComponent : MeshComponent
    {
        //Exposed Light Properties
        public LightData Data;
        
        public NbVector3 Direction;
        //Light Projection + View Matrices
        public NbMatrix4[] lightSpaceMatrices;
        public NbMatrix4 lightProjectionMatrix;

        public LightComponent() : base()
        {
            Data = new()
            {
                Color = new NbVector3(1.0f),
                FOV = 360.0f,
                Intensity = 1.0f,
                IsRenderable = true,
                Falloff = ATTENUATION_TYPE.QUADRATIC,
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
                Mesh = Common.RenderState.engineRef.GetMesh(hash),
                Direction = (NbVector3) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Direction")),
                Data = (LightData) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("LightData"))
            };

            return lc;
        }


    }
}
