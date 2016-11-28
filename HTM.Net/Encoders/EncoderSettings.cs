using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    [Serializable]
    public class EncoderSettingsList : Map<string, EncoderSetting>
    {
        public EncoderSettingsList()
        {

        }

        public EncoderSettingsList(IDictionary<string, EncoderSetting> otherList)
        {
            this.AddAll(otherList);
        }

        public EncoderSetting For(string encoderName)
        {
            return this.Where(k => k.Key.Equals(encoderName, StringComparison.InvariantCultureIgnoreCase))
                    .Select(k => k.Value).SingleOrDefault();
        }
    }

    [Serializable]
    public class EncoderSetting
    {
        private static Dictionary<string, PropertyInfo> _allKeyProps;

        static EncoderSetting()
        {
            _allKeyProps = typeof(EncoderSetting).GetProperties()
                .Where(p => p.Name != nameof(Keys) && p.Name != nameof(AllKeys) && p.Name != "Item")
                .ToDictionary(k => k.Name.ToLower(), v => v);
        }

        /// <summary>
        /// Returns all keys
        /// </summary>
        public List<string> AllKeys
        {
            get { return _allKeyProps.Keys.ToList(); }
        }

        /// <summary>
        /// Returns all non empty keys
        /// </summary>
        public List<string> Keys
        {
            get { return _allKeyProps.Where(p => p.Value.GetValue(this) != null).Select(p => p.Key).ToList(); }
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
            return !string.IsNullOrWhiteSpace(encoderType);
        }
        public bool HasType()
        {
            return !string.IsNullOrWhiteSpace(type);
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
                key = key.ToLower();
                if (!_allKeyProps.ContainsKey(key)) throw new ArgumentException("Key does not exist.");

                return _allKeyProps[key].GetValue(this);
            }
            set
            {
                key = key.ToLower();
                if (!_allKeyProps.ContainsKey(key)) throw new ArgumentException("Key does not exist.");

                Type destType = _allKeyProps[key].PropertyType;

                _allKeyProps[key].SetValue(this, TypeConverter.Convert(value, destType));
            }
        }

        public string name { get; set; }
        public string fieldName { get; set; }
        public FieldMetaType? fieldType { get; set; }
        public int? n { get; set; }
        public int? w { get; set; }
        public double? minVal { get; set; }
        public double? maxVal { get; set; }
        public double? radius { get; set; }
        public double? resolution { get; set; }
        public double? numBuckets { get; set; }
        public bool? forced { get; set; }
        public bool? periodic { get; set; }
        public bool? clipInput { get; set; }
        public bool? runDelta { get; set; }
        public string space { get; set; }
        public IList categoryList { get; set; }
        public bool? classifierOnly { get; set; }
        public string encoderType { get; set; }
        public string type { get; set; }

        public Tuple dayOfWeek { get; set; }
        public Tuple timeOfDay { get; set; }
        public string formatPattern { get; set; }

        public int? timestep { get; set; }
        public int? scale { get; set; }

        public EncoderSetting Clone()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, this);
            ms.Position = 0;
            EncoderSetting obj = (EncoderSetting)formatter.Deserialize(ms);
            return obj;
        }
    }
}
