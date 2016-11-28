using System;

namespace HTM.Net.Util
{
    public static class StringExtentions
    {
        public static bool EqualsIgnoreCase(this string me, string other)
        {
            return me.Equals(other, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}