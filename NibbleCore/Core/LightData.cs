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
        LINEAR_SQRT,
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
        public NbVector3 Direction;
        public float InnerCutOff;
        public float OutterCutOff;
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
            writer.WritePropertyName("Direction");
            IO.NbSerializer.Serialize(Direction, writer);
            writer.WritePropertyName("InnerCutOff");
            writer.WriteValue(InnerCutOff);
            writer.WritePropertyName("OutterCutOff");
            writer.WriteValue(OutterCutOff);
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
                Direction = (NbVector3)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Direction")),
                InnerCutOff = token.Value<float>("InnerCutOff"),
                OutterCutOff = token.Value<float>("OutterCutOff"),
                Intensity = token.Value<float>("Intensity"),
                IsRenderable = token.Value<bool>("IsRenderable"),
                Falloff = (ATTENUATION_TYPE)Enum.Parse(typeof(ATTENUATION_TYPE), token.Value<string>("Falloff")),
                Falloff_radius = token.Value<float>("FalloffRadius"),
                LightType = (LIGHT_TYPE)Enum.Parse(typeof(LIGHT_TYPE), token.Value<string>("LightType"))
            };
        }

    }

}
