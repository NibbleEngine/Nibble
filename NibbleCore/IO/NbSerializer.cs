using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace NbCore
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public class NbSerializable : Attribute
    {

    }
}

namespace NbCore.IO
{
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

        public static void Serialize<T>(T ob, string filepath)
        {
            StreamWriter sw = new(filepath);
            JsonTextWriter writer = new JsonTextWriter(sw);
            writer.Formatting = Formatting.Indented;
            Serialize(ob, writer);
            writer.Close();
        }

        public static void Serialize<T>(T ob, JsonTextWriter writer)
        {
            //Iterate in object types fields
            Type type = ob.GetType();

            if (type.IsDefined(typeof(NbSerializable), false))
            {
                //Call custom Serializer
                MethodInfo c_serializer = type.GetMethod("Serialize", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);

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
