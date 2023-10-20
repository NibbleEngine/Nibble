using System;
using Newtonsoft.Json;

namespace NbCore
{
    //TODO: Make sure that every entity (previous model) uses this component by default

    [NbSerializable]
    public unsafe class TransformComponent : Component {
        
        public NbTransformData Data;
        public bool IsControllable = false;

        public TransformComponent(NbTransformData data): base()
        {
            Data = data;
        }

        public override Component Clone()
        {
            //Use the same Data reference to the clone as well (not sure if this is correct)
            TransformComponent n = new TransformComponent(Data);
            
            return n;
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().FullName);
            writer.WritePropertyName("Data");
            IO.NbSerializer.Serialize(Data, writer);
            writer.WriteEndObject();
        }

        public static TransformComponent Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbTransformData data = IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Data")) as NbTransformData;
            return new TransformComponent(data);
        }


    }
}
