using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Math;
using Newtonsoft.Json;

namespace NbCore
{
    [NbSerializable]
    public struct ImposterData
    {
        public NbVector3 Color;
        public float Width;
        public float Height;
        public int ImageID;
        public bool IsUpdated;

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Color");
            IO.NbSerializer.Serialize(Color, writer);
            writer.WritePropertyName("Width");
            writer.WriteValue(Width);
            writer.WritePropertyName("Height");
            writer.WriteValue(Height);
            writer.WritePropertyName("ImageID");
            writer.WriteValue(ImageID);
            writer.WriteEndObject();
        }

        public static ImposterData Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            return new()
            {
                Color = (NbVector3)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Color")),
                Width = token.Value<int>("Width"),
                Height = token.Value<float>("Height"),
                ImageID = token.Value<int>("ImageID"),
            };
        }

    }

    [NbSerializable]
    public class ImposterComponent : MeshComponent
    {
        //Exposed Light Properties
        public ImposterData Data;
        
        public ImposterComponent() : base()
        {
            Data = new()
            {
                Color = new NbVector3(1.0f),
                Width = 1.0f,
                Height = 1.0f,
                ImageID = 0,
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

        public new void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Mesh"); //I am not sure I need this in the serialized version
            writer.WriteValue(Mesh.Hash.ToString());
            writer.WritePropertyName("Data");
            IO.NbSerializer.Serialize(Data, writer);
            writer.WriteEndObject();
        }

        public new static ImposterComponent Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            string mesh_hash = token.Value<string>("Mesh");
            ulong hash = ulong.Parse(mesh_hash);

            ImposterComponent lc = new()
            {
                Mesh = Common.RenderState.engineRef.GetMesh(hash),
                Data = (ImposterData) IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Data"))
            };

            return lc;
        }

    }
}
