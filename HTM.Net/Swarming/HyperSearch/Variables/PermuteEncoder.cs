using System;
using System.Diagnostics;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Swarming.HyperSearch.Variables;

/// <summary>
/// A permutation variable that defines a field encoder. This serves as
/// a container for the encoder constructor arguments.
/// </summary>
[Serializable]
public class PermuteEncoder : PermuteVariable
{
    [DoNotApply]
    public string Name { get { return this["name"] as string; } set { this["name"] = value; } }
    [DoNotApply]
    public string FieldName { get { return this["fieldName"] as string; } set { this["fieldName"] = value; } }
    [DoNotApply]
    public string EncoderType { get { return (string)this["encoderType"]; } set { this["encoderType"] = value; } }
    [DoNotApply]
    public bool ClassifierOnly { get { return (bool)(this["classifierOnly"] ?? false); } set { this["classifierOnly"] = value; } }
    [DoNotApply]
    public object MaxVal { get { return this["maxVal"]; } set { this["maxVal"] = value; } } // int or permuteint
    [DoNotApply]
    public object Radius { get { return this["radius"]; } set { this["radius"] = value; } } // float or permutefloat
    [DoNotApply]
    public object N { get { return this["n"]; } set { this["n"] = value; } } // int or permuteint
    [DoNotApply]
    public object W { get { return this["w"]; } set { this["w"] = value; } } // int or permuteint
    [DoNotApply]
    public object MinVal { get { return this["minVal"]; } set { this["minVal"] = value; } } // int or permuteint
    [DoNotApply]
    public bool ClipInput { get { return (bool)(this["clipInput"] ?? false); } set { this["clipInput"] = value; } }

    [JsonProperty]
    public KWArgsModel KwArgs { get; set; }


    [Obsolete("Don' use")]
    [JsonConstructor]
    public PermuteEncoder()
    {
        KwArgs = new KWArgsModel();
    }

    public PermuteEncoder(string fieldName, string encoderType, string name = null, KWArgsModel kwArgs = null)
    {
        // Possible values in kwArgs include: w, n, minVal, maxVal, etc.
        this.KwArgs = kwArgs ?? new KWArgsModel();

        this.FieldName = fieldName;
        if (name == null)
        {
            name = fieldName;
        }

        this.Name = name;
        this.EncoderType = encoderType;
    }

    #region Overrides of Object

    public override string ToString()
    {
        string suffix = "";
        //for (key, value in this.kwArgs.items())
        foreach (var pair in KwArgs)
        {
            suffix += string.Format("{0}={1}, ", pair.Key, pair.Value);
        }

        return string.Format("PermuteEncoder(fieldName={0}, encoderClass={1}, name={2}, {3})",
            FieldName, EncoderType, Name, suffix);
    }

    #endregion

    public object this[string key]
    {
        get
        {
            if (KwArgs.ContainsKey(key)) return KwArgs[key];
            return null;
        }
        set
        {
            if (KwArgs.ContainsKey(key)) KwArgs[key] = value;
            else KwArgs[key] = value;
        }
    }

    public override void SetState(VarState varState)
    {
        throw new NotSupportedException();
    }

    public override VarState GetState()
    {
        throw new NotSupportedException();
    }

    ///// <summary>
    ///// Return a dict that can be used to construct this encoder. This dict
    ///// can be passed directly to the addMultipleEncoders() method of the
    ///// multi encoder.
    ///// </summary>
    ///// <param name="encoderName">name of the encoder</param>
    ///// <param name="flattenedChosenValues">
    ///// dict of the flattened permutation variables. Any
    ///// variables within this dict whose key starts
    ///// with encoderName will be substituted for
    ///// encoder constructor args which are being
    ///// permuted over.
    ///// </param>
    ///// <returns></returns>
    //public PermuteEncoder getEncoderFlattened_old(string encoderName, Map<string, object> flattenedChosenValues)
    //{
    //    Map<string, object> encoder = new Map<string, object>();
    //    //encoder.Add("fieldname", this.fieldName);
    //    //encoder.Add("name", this.name);
    //    //encoder = dict(fieldname = this.fieldName,name = this.name);

    //    // Get the position of each encoder argument
    //    //for (encoderArg, value in this.kwArgs.iteritems())
    //    foreach (var pair in this.kwArgs)
    //    {
    //        var encoderArg = pair.Key;
    //        var value = pair.Value;
    //        // If a permuted variable, get its chosen value.
    //        if (value is PermuteVariable)
    //        {
    //            value = flattenedChosenValues[string.Format("{0}:{1}", encoderName, encoderArg)];
    //        }

    //        encoder[encoderArg] = value;
    //    }

    //    // Special treatment for DateEncoder timeOfDay and dayOfWeek stuff. In the
    //    //  permutations file, the class can be one of:
    //    //    DateEncoder.timeOfDay
    //    //    DateEncoder.dayOfWeek
    //    //    DateEncoder.season
    //    // If one of these, we need to intelligently set the constructor args.
    //    if (this.encoderClass.Contains("."))
    //    {
    //        // (encoder['type'], argName) = this.encoderClass.Split('.');
    //        string[] splitted = this.encoderClass.Split('.');
    //        encoder["type"] = splitted[0];
    //        string argName = splitted[1];
    //        Tuple<object, object> argValue = new Tuple<object, object>(encoder["w"], encoder["radius"]);
    //        encoder[argName] = argValue;
    //        encoder.Remove("w");
    //        encoder.Remove("radius");
    //    }
    //    else
    //    {
    //        encoder["type"] = this.encoderClass;
    //    }

    //    var args = new KWArgsModel();

    //    foreach (var pair in encoder)
    //    {
    //        if (pair.Key == "type") continue;
    //        if (pair.Key == "maxval") continue;
    //        if (pair.Key == "minval") continue;
    //        if (pair.Key == "n") continue;
    //        if (pair.Key == "w") continue;
    //        args.Add(pair.Key, pair.Value);
    //    }

    //    PermuteEncoder pe = new PermuteEncoder(fieldName, (string)encoder["type"], name, args);
    //    pe.maxval = encoder.Get("maxval", this.maxval);
    //    pe.minval = encoder.Get("minval", this.minval);
    //    pe.n = encoder.Get("n", this.n);
    //    pe.w = encoder.Get("w", this.w);
    //    return pe;
    //}

    /// <summary>
    /// Return a dict that can be used to construct this encoder. This dict
    /// can be passed directly to the addMultipleEncoders() method of the
    /// multi encoder.
    /// </summary>
    /// <param name="encoderName">name of the encoder</param>
    /// <param name="flattenedChosenValues">
    /// dict of the flattened permutation variables. Any
    /// variables within this dict whose key starts
    /// with encoderName will be substituted for
    /// encoder constructor args which are being
    /// permuted over.
    /// </param>
    /// <returns></returns>
    public EncoderSetting GetDict(string encoderName, Map<string, object> flattenedChosenValues)
    {
        EncoderSetting encoder = new EncoderSetting();
        encoder.fieldName = FieldName;
        encoder.name = Name;

        // Get the position of each encoder argument
        //for (encoderArg, value in this.kwArgs.iteritems())
        foreach (var pair in KwArgs)
        {
            var encoderArg = pair.Key;
            var value = pair.Value;
            // If a permuted variable, get its chosen value.
            if (value is PermuteVariable)
            {
                value = flattenedChosenValues[$"{encoderName}:{encoderArg}"];
            }

            encoder[encoderArg] = value;
        }

        // Special treatment for DateEncoder timeOfDay and dayOfWeek stuff. In the
        //  permutations file, the class can be one of:
        //    DateEncoder.timeOfDay
        //    DateEncoder.dayOfWeek
        //    DateEncoder.season
        // If one of these, we need to intelligently set the constructor args.
        if (EncoderType.Contains("."))
        {
            try
            {
                // (encoder['type'], argName) = this.encoderClass.Split('.');
                string[] splitted = EncoderType.Split('.');
                encoder.type = Enum.Parse<EncoderTypes>(splitted[0]);
                string argName = splitted[1];

                Tuple argValue = new Tuple(encoder.w ?? W, encoder.radius ?? Radius);
                encoder[argName] = argValue;
                encoder.w = null;
                encoder.radius = null;
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached) Debugger.Break();
                throw;
            }
        }
        else
        {
            encoder.type = Enum.Parse<EncoderTypes>(EncoderType);
        }

        //var args = new KWArgsModel();

        //foreach (var pair in encoder)
        //{
        //    if (pair.Key == "type") continue;
        //    if (pair.Key == "maxval") continue;
        //    if (pair.Key == "minval") continue;
        //    if (pair.Key == "n") continue;
        //    if (pair.Key == "w") continue;
        //    args.Add(pair.Key, pair.Value);
        //}

        //PermuteEncoder pe = new PermuteEncoder(fieldName, (string)encoder["type"], name, args);
        //pe.maxval = encoder.Get("maxval", this.maxval);
        //pe.minval = encoder.Get("minval", this.minval);
        //pe.n = encoder.Get("n", this.n);
        //pe.w = encoder.Get("w", this.w);
        //return pe;
        return encoder;
    }
}