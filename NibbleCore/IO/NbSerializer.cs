using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;

namespace NbCore
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public class NbSerializable : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public class NbDeserializable : Attribute
    {

    }
}

namespace NbCore.IO
{
    public static class NbDeserializer
    {
        public static object Deserialize(Newtonsoft.Json.Linq.JToken token)
        {
            Type type = Type.GetType(token.Value<string>("ObjectType"));
            
            if (type.IsDefined(typeof(NbDeserializable), false))
            {
                //Call custom Deserializer
                MethodInfo c_deserializer = type.GetMethod("Deserialize");

                if (c_deserializer != null)
                {
                    return c_deserializer.Invoke(null, new object[] { token });
                }
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
                else if (field.FieldType == typeof(int))
                    val = token.Value<int>(field.Name);
                else if (field.FieldType == typeof(float))
                    val = token.Value<float>(field.Name);
                else if (field.FieldType == typeof(long))
                    val = token.Value<long>(field.Name);
                else if (field.FieldType == typeof(ulong))
                    val = token.Value<ulong>(field.Name);
                else
                    val = null;

                field.SetValue(instance, val);
            }


            return instance;
        }
    }

    public static class NbSerializer
    {
        public static void WriteDictionary(FieldInfo field, object ob, JsonTextWriter writer)
        {
            Type keyType = field.FieldType.GetGenericArguments()[0];
            Type valueType = field.FieldType.GetGenericArguments()[1];

            writer.WriteStartArray();

            foreach (DictionaryEntry kvp in (IDictionary)field.GetValue(ob))
            {
                Serialize(kvp.Value, writer);
            }

            writer.WriteEndArray();
        }

        public static void WriteField(FieldInfo field, object ob, JsonTextWriter writer)
        {

            if (field.FieldType.IsGenericType)
            {
                if (field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    WriteDictionary(field, ob, writer);
                } else if (field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var collection = (IEnumerable) field.GetValue(ob);

                    writer.WriteStartArray();
                    //Iterate in list
                    foreach (var item in collection)
                        Serialize(item, writer);
                    writer.WriteEndArray();
                }
            }
            else if (field.FieldType.IsEnum || 
                     field.FieldType == typeof(string) ||
                     field.FieldType == typeof(long) ||
                     field.FieldType == typeof(ulong) ||
                     field.FieldType == typeof(int) ||
                     field.FieldType == typeof(uint) ||
                     field.FieldType == typeof(float) ||
                     field.FieldType == typeof(double) ||
                     field.FieldType == typeof(bool))
                writer.WriteValue(field.GetValue(ob));
            else if (field.GetType().IsClass)
                Serialize(field.GetValue(ob), writer);
            
        }


        public static void Serialize<T>(T ob, JsonTextWriter writer)
        {
            //Iterate in object types fields
            Type type = ob.GetType();

            if (type.IsDefined(typeof(NbSerializable), false))
            {
                //Call custom Serializer
                MethodInfo c_serializer = type.GetMethod("Serialize");

                if (c_serializer != null)
                {
                    c_serializer.Invoke(ob, new object[] { writer });
                    return;
                }
            }

            //Precheck that the class has at least one serializable property
            FieldInfo[] fields = type.GetFields();
            bool has_serializable_fields = false;
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<NbSerializable>() != null)
                {
                    has_serializable_fields = true;
                    break;
                }
            }

            if (!has_serializable_fields)
                return;

            writer.WriteStartObject();
            writer.WritePropertyName("ObjectType");
            writer.WriteValue(type.FullName);
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<NbSerializable>() is null)
                    continue;

                writer.WritePropertyName(field.Name);
                WriteField(field, ob, writer);
            }

            writer.WriteEndObject();
        }

    }
}
