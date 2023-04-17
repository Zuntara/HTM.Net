using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using HTM.Net.Util;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders;

[Serializable]
public class EncoderSettingsList : Map<string, EncoderSetting>
{
    public EncoderSettingsList()
    {

    }

    public EncoderSettingsList(IDictionary<string, EncoderSetting> otherList)
    {
        foreach (var encoderSetting in otherList)
        {
            Add(encoderSetting.Key, encoderSetting.Value.Clone());
        }
    }

    public EncoderSettingsList(Map<string, Map<string, object>> otherList)
    {
        foreach (var pair in otherList)
        {
            EncoderSetting setting = new EncoderSetting(pair.Value);

            Add(pair.Key, setting);
        }
    }

    protected EncoderSettingsList(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {

    }

    public EncoderSetting For(string encoderName)
    {
        return this.Where(k => k.Key.Equals(encoderName, StringComparison.InvariantCultureIgnoreCase))
            .Select(k => k.Value).SingleOrDefault();
    }

    public Map<string, Map<string, object>> AsMap()
    {
        Map<string, Map<string, object>> map = new Map<string, Map<string, object>>();

        foreach (var kvp in this)
        {
            map[kvp.Key] = kvp.Value.ToMap();
        }

        return map;
    }

    public static implicit operator Map<string, Map<string, object>>(EncoderSettingsList list)
    {
        return list.AsMap();
    }
}

[Serializable]
public class EncoderSetting
{
    private static Dictionary<string, PropertyInfo> _allKeyProps;

    static EncoderSetting()
    {
        _allKeyProps = typeof(EncoderSetting).GetProperties()
            .Where(p => 
                p.Name != nameof(Keys) 
                && p.Name != nameof(AllKeys) 
                && p.Name != nameof(type)
                && p.Name != "Item")
            .ToDictionary(k => k.Name, v => v);
    }

    public EncoderSetting()
    {
    }

    public EncoderSetting(Map<string, object> mapValues)
    {
        foreach (var pair in mapValues)
        {
            this[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// Returns all keys
    /// </summary>
    [JsonIgnore]
    public List<string> AllKeys
    {
        get { return _allKeyProps.Keys.ToList(); }
    }

    /// <summary>
    /// Returns all non empty keys
    /// </summary>
    [JsonIgnore]
    public List<string> Keys
    {
        get
        {
            return _allKeyProps
                .Where(p => p.Value.GetValue(this) != null)
                .Select(p => p.Key)
                .ToList();
        }
    }

    public bool HasName()
    {
        return !string.IsNullOrWhiteSpace(name);
    }
    public bool HasFieldName()
    {
        return !string.IsNullOrWhiteSpace(fieldName);
    }
    public bool HasEncoderType()
    {
        return encoderType != EncoderTypes.None;
    }
    public bool HasType()
    {
        return type != EncoderTypes.None;
    }
    public bool HasN()
    {
        return n.HasValue;
    }
    public bool HasW()
    {
        return w.HasValue;
    }
    public bool HasForced()
    {
        return forced.HasValue;
    }
    public bool HasCategoryList()
    {
        return categoryList != null;
    }
    public bool HasFieldType()
    {
        return fieldType.HasValue;
    }
    public bool HasSpace()
    {
        return !string.IsNullOrWhiteSpace(space);
    }

    public object this[string key]
    {
        get
        {
            if (!_allKeyProps.ContainsKey(key)) throw new ArgumentException("Key does not exist.");

            return _allKeyProps[key].GetValue(this);
        }
        set
        {
            if (!_allKeyProps.ContainsKey(key)) throw new ArgumentException($"Key does not exist. ({key})");

            Type destType = _allKeyProps[key].PropertyType;

            _allKeyProps[key].SetValue(this, TypeConverter.Convert(value, destType));
        }
    }

    public string name { get; set; }
    /// <summary>
    /// Name of the field being encoded
    /// </summary>
    public string fieldName { get; set; }
    /// <summary>
    /// Primitive type of the field, used to auto-configure the type of encoder
    /// </summary>
    public FieldMetaType? fieldType { get; set; }
    /// <summary>
    /// number of bits in the representation (must be &gt;= w)
    /// </summary>
    public int? n { get; set; }
    /// <summary>
    /// The number of bits that are set to encode a single value - the "width" of the output signal
    /// </summary>
    public int? w { get; set; }
    /// <summary>
    /// The minimum value of the input signal.
    /// </summary>
    public double? minVal { get; set; }
    /// <summary>
    /// The maximum value of the input signal.
    /// </summary>
    public double? maxVal { get; set; }
    /// <summary>
    /// inputs separated by more than, or equal to this distance will have non-overlapping representations
    /// </summary>
    public double? radius { get; set; }
    /// <summary>
    /// inputs separated by more than, or equal to this distance will have different representations
    /// </summary>
    public double? resolution { get; set; }
    /// <summary>
    /// If true, then the input value "wraps around" such that minval = maxval
    /// For a periodic value, the input must be strictly less than maxval,
    /// otherwise maxval is a true upper bound.
    /// </summary>
    public bool? periodic { get; set; }
    public double? numBuckets { get; set; }
    /// <summary>
    /// If true, skip some safety checks (for compatibility reasons), default false
    /// Mostly having to do with being able to set the window size &lt; 21 
    /// </summary>
    public bool? forced { get; set; }
    /// <summary>
    /// if true, non-periodic inputs smaller than minval or greater
    /// than maxval will be clipped to minval/maxval
    /// </summary>
    public bool? clipInput { get; set; }
    public bool? runDelta { get; set; }
    public string space { get; set; }
    public List<string> categoryList { get; set; }
    public bool? classifierOnly { get; set; }
    /// <summary>
    /// Encoder name
    /// </summary>
    public EncoderTypes? encoderType { get; set; }
    public EncoderTypes type { get; set; }

    public DayOfWeekTuple DayOfWeek { get; set; }
    public TimeOfDayTuple TimeOfDay { get; set; }
    public WeekendTuple Weekend { get; set; }
    public SeasonTuple Season { get; set; }
    public HolidayTuple Holiday { get; set; }
    public string formatPattern { get; set; }

    public int? timestep { get; set; }
    public int? scale { get; set; }

    public EncoderSetting Clone()
    {
        return new EncoderSetting(ToMap());
    }

    public EncoderTypes GetEncoderType()
    {
        return encoderType ?? type;
    }

    public Map<string, object> ToMap()
    {
        Map<string, object> mapped = new Map<string, object>();

        foreach (var key in Keys)
        {
            mapped[key] = this[key];
        }

        return mapped;
    }
}