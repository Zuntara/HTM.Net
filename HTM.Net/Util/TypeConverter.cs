namespace HTM.Net.Util
{
    public class TypeConverter
    {
        public static T Convert<T>(object o)
        {
            return (T)System.Convert.ChangeType(o, typeof(T));
        }
    }
}