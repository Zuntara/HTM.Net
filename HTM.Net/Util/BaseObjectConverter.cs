using System;
using System.Linq;
using Newtonsoft.Json;

namespace HTM.Net.Util;

public class BaseObjectConverter<T> : JsonConverter<T>
    where T : class, new()
{
    public override bool CanWrite => true;

    public override bool CanRead => true;

    public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
    {
        var origHandling = serializer.TypeNameHandling;
        serializer.TypeNameHandling = TypeNameHandling.Objects;

        writer.WriteStartObject();

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

        reader.Read(); // start object

        do
        {
            string propertyName = (string)reader.Value;
            reader.Read();

            var prop = props.Single(p => p.Name == propertyName);

            if (reader.Value != null
                || reader.TokenType == JsonToken.StartArray
                || reader.TokenType == JsonToken.StartObject)
            {
                object deSer = serializer.Deserialize(reader, prop.PropertyType);
                reader.Read();
                prop.SetValue(obj, deSer);
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