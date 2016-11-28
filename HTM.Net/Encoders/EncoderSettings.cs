using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    public class EncoderSettingsList : Map<string, EncoderSetting>
    {
        public EncoderSetting For(string encoderName)
        {
            return this.Where(k => k.Key.Equals(encoderName, StringComparison.InvariantCultureIgnoreCase))
                    .Select(k => k.Value).SingleOrDefault();
        }
    }

    public class EncoderSetting
    {
        private static Dictionary<string, PropertyInfo> _allKeyProps;

        static EncoderSetting()
        {
            _allKeyProps = typeof (EncoderSetting).GetProperties().ToDictionary(k => k.Name.ToLower(), v => v);
        }

        public List<string> Keys
        {
            get
            {
                List<string> keys = new List<string> ();
                if(HasName()) keys.Add("name");
                if(HasFieldName()) keys.Add("fieldName");
                if(HasEncoderType()) keys.Add("encoderType");
                if(HasType()) keys.Add("type");
                if(HasN()) keys.Add("n");
                if(HasW()) keys.Add("w");
                return keys;
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


        public object this[string key]
        {
            get
            {
                key = key.ToLower();
                if(!_allKeyProps.ContainsKey(key)) throw new ArgumentException("Key does not exist.");

                return _allKeyProps[key].GetValue(this);
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
        public bool? forced { get; set; }
        public bool? periodic { get; set; }
        public bool? clipInput { get; set; }
        public IList categoryList { get; set; }

        public string encoderType { get; set; }
        public string type { get; set; }

        public Tuple dayOfWeek { get; set; }
        public Tuple timeOfDay { get; set; }
        public string formatPattern { get; set; }

        public int? timestep { get; set; }
        public int? scale { get; set; }
    }
}
