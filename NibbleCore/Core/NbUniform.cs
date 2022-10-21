using NbCore.Math;
using Newtonsoft.Json;
using System.CodeDom;
using System.Runtime.CompilerServices;

namespace NbCore
{

    unsafe public class MyRef2<T>
    {
        private T* _ref;
        public ref T Ref
        {
            get
            {
                return ref *_ref;
            }
        }
            
        public MyRef2()
        {
            _ref = null;
        }

        public void SetRef(ref T r)
        {
            _ref = (T*)Unsafe.AsPointer(ref r);
        }
    }

    public class MyRef<T>
    {
        T Ref { get; set; }
    }

    [NbSerializable]
    public class NbUniform
    {
        public string Name = "Uniform"; //Uniform custom name
        private MyRef2<NbVector4> ValuesRef = new MyRef2<NbVector4>();
        public NbUniformState State;
        public bool IsBound = false;
        
        public NbUniform() 
        {
            NbVector4 vec = new();
            ValuesRef.SetRef(ref vec);
        }
        
        public NbUniform(string name, NbVector4 values)
        {
            Name = name;
            ValuesRef.SetRef(ref values);
            State = new()
            {
                ShaderBinding = "",
                ShaderLocation = -1,
                Type = NbUniformType.Float
            };
        }

        public void SetX(float x)
        {
            ValuesRef.Ref.X = x;
        }

        public void Bind(ref NbVector4 vec)
        {
            ValuesRef.SetRef(ref vec);
            IsBound = true;
        }

        public void UnBind()
        {
            NbVector4 v = new();
            ValuesRef.SetRef(ref v);
            IsBound = false;
        }
        
        public NbVector4 Values
        {
            get { return ValuesRef.Ref; }
        }

        public void Serialize(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(GetType().ToString());
            writer.WritePropertyName("Name");
            writer.WriteValue(Name);
            writer.WritePropertyName("State");
            IO.NbSerializer.Serialize(State, writer);
            writer.WritePropertyName("Values");
            IO.NbSerializer.Serialize(Values, writer);
            
            writer.WriteEndObject();
        }

        public static NbUniform Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            NbUniform uf = new()
            {
                Name = token.Value<string>("Name"),
                State = (NbUniformState)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("State"))
            };

            NbVector4 vec = (NbVector4)IO.NbDeserializer.Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>("Values"));
            uf.Bind(ref vec);
            return uf;
        }

        

    }

}