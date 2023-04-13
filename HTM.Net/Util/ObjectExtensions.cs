using System;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Util;

public static class ObjectExtensions
{
    public static T EnsureType<T>(this object obj)
    {
        if (obj is T)
        {
            return (T)obj;
        }

        if (obj is JObject j)
        {
            return j.ToObject<T>();
        }

        throw new InvalidCastException($"Cannot cast {obj.GetType().Name} to {typeof(T).Name}");
    }
}