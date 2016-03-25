using System.Collections.Generic;
using HTM.Net.Encoders;

namespace HTM.Net.Util
{
    /**
     * Subclass of Tuple to specifically contain the results of an
     * {@link Encoder}'s {@link Encoder#encode(double)}
     * call.
     * 
     * @param <M>	the fieldsMap
     * @param <L>	the fieldsOrder
     */
    public class DecodeTuple<TM, TL> : Tuple
        where TM: Map<string, RangeList>
        where TL: List<string>
    {
        protected TM fields;
	    protected TL fieldDescriptions;

        public DecodeTuple(TM m, TL l)
            : base(m, l)
        {
            fields = m;
            fieldDescriptions = l;
        }
    }
}