using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Util
{
    public static class ListExtentions
    {
        public static List<T> SubList<T>(this List<T> list, int fromIdx, int count)
        {
            return list.Skip(fromIdx).Take(count).ToList();
        }

        public static void Shuffle<T>(this IList<T> list, IRandom rnd)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = (rnd.NextInt(n) % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}