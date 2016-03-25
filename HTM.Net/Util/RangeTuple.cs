using System.Collections.Generic;

namespace HTM.Net.Util
{
    /// <summary>
    /// Subclasses the {@link Tuple} utility class to constrain 
    /// the number of arguments and argument types to those specifically
    /// related to the {@link Encoder} functionality.
    /// </summary>
    /// <typeparam name="TL"></typeparam>
    /// <typeparam name="TS"></typeparam>
    public class RangeTuple<TL, TS> : Tuple
        where TL : List<MinMax>
    {
        protected TL l;
        protected string desc;

        /**
         * Instantiates a {@code RangeTuple}
         * @param l
         * @param s
         */
        public RangeTuple(TL l, string s) : base(l, s)
        {

            this.l = l;
            this.desc = s;
        }
    }
}