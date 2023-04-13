using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace HTM.Net.Util;

public class BaseObjectConverter<T> : JsonConverter<T>
    where T : class, new()
{
    private FieldAttributes? Access { get; }

    public BaseObjectConverter()
    {
    }

    public BaseObjectConverter(FieldAttributes? access = null)
    {
        Access = access;
    }

    public override bool CanWrite => true;

    public override bool CanRead => true;

    public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
    {
        var origHandling = serializer.TypeNameHandling;
        serializer.TypeNameHandling = TypeNameHandling.Objects;

        writer.WriteStartObject();

        if (Access == FieldAttributes.Private)
        {
            value.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .ToList()
                .ForEach(pi =>
                {
                    writer.WritePropertyName(pi.Name);
                    serializer.Serialize(writer, pi.GetValue(value));
                });
        }

        value.GetType().GetProperties()
            .ToList()
            .ForEach(pi =>
            {
                writer.WritePropertyName(pi.Name);
                serializer.Serialize(writer, pi.GetValue(value));
            });

        writer.WriteEndObject();

        serializer.TypeNameHandling = origHandling;
    }

    public override T ReadJson(JsonReader reader, Type objectType,
        T existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var origHandling = serializer.TypeNameHandling;
        serializer.TypeNameHandling = TypeNameHandling.Objects;
        T obj = existingValue ?? new T();

        var props = obj.GetType().GetProperties().ToList();
        var fields = new List<FieldInfo>();
        if (Access == FieldAttributes.Private)
        {
            fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).ToList();
        }

        reader.Read(); // start object

        do
        {
            string propertyName = (string)reader.Value;
            reader.Read();

            var prop = props.SingleOrDefault(p => p.Name == propertyName);
            var field = fields.SingleOrDefault(f => f.Name == propertyName);

            if (reader.Value != null
                || reader.TokenType == JsonToken.StartArray
                || reader.TokenType == JsonToken.StartObject)
            {
                object deSer = serializer.Deserialize(reader, prop?.PropertyType ?? field?.FieldType);
                reader.Read();
                
                prop?.SetValue(obj, deSer);
                field?.SetValue(obj, deSer);
            }
            else
            {
                reader.Read();
            }
        } while (reader.TokenType == JsonToken.PropertyName);

        serializer.TypeNameHandling = origHandling;
        return obj;
    }
}