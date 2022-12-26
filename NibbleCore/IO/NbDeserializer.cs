using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace NbCore.IO
{
    public static class NbDeserializer
    {
        public static JObject DeserializeToToken(string filepath)
        {
            StreamReader sr = new(filepath);
            JsonTextReader reader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();
            JObject ob = serializer.Deserialize<JObject>(reader);
            reader.Close();
            return ob;
        }

        public static object Deserialize(JToken token)
        {
            Type type = Type.GetType(token.Value<string>("ObjectType"));
            
            if (type.IsDefined(typeof(NbSerializable), false))
            {
                //Call custom Deserializer
                MethodInfo c_deserializer = type.GetMethod("Deserialize");

                Common.Callbacks.Assert(c_deserializer != null,
                    $"Missing Deserialize method on serializable class {type}");

                return c_deserializer.Invoke(null, new object[] { token });
            }

            object instance = Activator.CreateInstance(type);
            
            //Parse fields from the token and pass them to instance
            FieldInfo[] fields = type.GetFields();
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<NbSerializable>() == null)
                    continue;

                object val;
                if (field.FieldType.IsEnum)
                {
                    int enum_val = token.Value<int>(field.Name);
                    val = Enum.ToObject(field.FieldType, enum_val);
                }
                else if (field.FieldType == typeof(string))
                    val = token.Value<string>(field.Name);
                else if (field.FieldType == typeof(bool))
                    val = token.Value<bool>(field.Name);
                else if (field.FieldType == typeof(int))
                    val = token.Value<int>(field.Name);
                else if (field.FieldType == typeof(uint))
                    val = token.Value<uint>(field.Name);
                else if (field.FieldType == typeof(long))
                    val = token.Value<long>(field.Name);
                else if (field.FieldType == typeof(ulong))
                    val = token.Value<ulong>(field.Name);
                else if (field.FieldType == typeof(float))
                    val = token.Value<float>(field.Name);
                else //If its not a basic type is probably another object
                    val = Deserialize(token.Value<Newtonsoft.Json.Linq.JToken>(field.Name));

                field.SetValue(instance, val);
            }


            return instance;
        }
    }

    
}
