using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Encoders
{
    /**
     * Tuple to contain the results of an {@link Encoder}'s decoded
     * values.
     */
 [Serializable]
    public class DecodeResult : DecodeTuple<Map<string, RangeList>, List<string>>
    {
        /**
    * Constructs a new {@code Decode}
    * @param m		Map of field names to {@link RangeList} object
    * @param l		List of comma-separated descriptions for each list of ranges.
    */
        public DecodeResult(Map<string, RangeList> m, List<string> l)
            : base(m, l)
        {
            
        }

        /**
         * Returns the Map of field names to {@link RangeList} object
         * @return
         */
        public Map<string, RangeList> GetFields()
        {
            return fields;
        }

        /**
         * Returns the List of comma-separated descriptions for each list of ranges.
         * @return
         */
        public List<string> GetDescriptions()
        {
            return fieldDescriptions;
        }

        /**
         * Returns the {@link RangeList} associated with the specified field.
         * @param fieldName		the name of the field
         * @return
         */
        public RangeList GetRanges(string fieldName)
        {
            return fields[fieldName];
        }

        /**
         * Returns a specific range ({@link MinMax}) for the specified field.
         * @param fieldName		the name of the field
         * @param index			the index of the range to return
         * @return
         */
        public MinMax GetRange(string fieldName, int index)
        {
            return fields[fieldName].GetRange(index);
        }
    }
}