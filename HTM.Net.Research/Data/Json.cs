using Newtonsoft.Json;

namespace HTM.Net.Research.Data;

public static class Json
{
    public static string Serialize(object instance)
    {
        return JsonConvert.SerializeObject(instance, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
    }

    public static T Deserialize<T>(string jsonString)
    {
        T obj = JsonConvert.DeserializeObject<T>(jsonString, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
        return obj;
    }
}