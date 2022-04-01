using System;

namespace HTM.Net.Util;

[Serializable]
public static class TimeUtils
{
    private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static int CurrentTimeMillis()
    {
        return (int)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
    }
}