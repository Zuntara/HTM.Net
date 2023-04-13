using System;
using System.Runtime.Serialization;
using HTM.Net.Util;

namespace HTM.Net.Swarming.HyperSearch.Variables;

[Serializable]
//[JsonConverter(typeof(KwArgsJsonConverter))]
public class KWArgsModel : Map<string, object>
{
    public KWArgsModel()
    {
        // for deserialization
    }

    protected KWArgsModel(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {

    }

    /*
    kwArgs: new KWArgsModel
    {
        {  "maxVal" , new PermuteInt(100, 300, 1)},
        {  "n" , new PermuteInt(13, 500, 1)},
        {  "w" , 7},
        {  "minVal" , 0},
    }
    */
    // w, n, minval, maxval
    public KWArgsModel(bool populate = false)
    {
        if (populate)
        {
            Add("maxVal", null);
            Add("n", null);
            Add("w", null);
            Add("minVal", null);
        }
    }
}