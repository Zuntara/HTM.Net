using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Swarming.HyperSearch.Variables;

public class KwArgsJsonConverter : JsonConverter
{
    public override bool CanWrite
    {
        get { return false; }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        Debug.WriteLine("Reading KWArgs");

        var jObject = JObject.Load(reader);

        KWArgsModel retVal = new KWArgsModel();

        foreach (JToken arrItem in jObject.Values())
        {
            if (arrItem.Type == JTokenType.Integer)
            {
                retVal.Add(arrItem.Path, arrItem.Value<int>());
            }
            if (arrItem.Type == JTokenType.Object)
            {
                JObject obj = (JObject)arrItem;

                var children = arrItem.Children<JProperty>();
                if (children.Any(c => c.Name == "Type"))
                {
                    string type = obj.Value<string>("Type");
                    var childObj = Activator.CreateInstance(Type.GetType(type));

                    serializer.Populate(obj.CreateReader(), childObj);
                    retVal.Add(arrItem.Path, childObj);
                }
            }
        }

        //JsonReader reader2 = new JTokenReader(jObj);
        //serializer.Populate(reader2, retVal);

        return retVal;

        throw new NotImplementedException();
    }

    public override bool CanConvert(Type objectType)
    {
        throw new NotImplementedException();
    }
}