namespace HTM.Net.Util
{
    public class TypeConverter
    {
        public static T Convert<T>(object o)
        {
            if (o == null) return default(T);
            return (T)System.Convert.ChangeType(o, typeof(T));
        }
    }
}