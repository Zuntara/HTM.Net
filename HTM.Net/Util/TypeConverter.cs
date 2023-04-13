using System.ComponentModel;
using System;

namespace HTM.Net.Util
{
    public class TypeConverter
    {
        public static T Convert<T>(object o)
        {
            if (o == null) return default(T);
            if (typeof(T).Name.StartsWith("Nullable"))
            {
                var converter = new NullableConverter(typeof(T));

                if (o.GetType() == converter.UnderlyingType)
                {
                    return (T)converter.ConvertFrom(o);
                }
                object o2 = System.Convert.ChangeType(o, converter.UnderlyingType);
                return (T)converter.ConvertFrom(o2);
            }
            return (T)System.Convert.ChangeType(o, typeof(T));
        }

        public static object Convert(object o, Type destinationType)
        {
            if (o == null) return null;
            if (destinationType.Name.StartsWith("Nullable"))
            {
                var converter = new NullableConverter(destinationType);

                if (o.GetType() == converter.UnderlyingType)
                {
                    return converter.ConvertFrom(o);
                }
                object o2 = System.Convert.ChangeType(o, converter.UnderlyingType);
                return converter.ConvertFrom(o2);
            }
            return System.Convert.ChangeType(o, destinationType);
        }
    }
}