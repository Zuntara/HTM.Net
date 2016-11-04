using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Encoders
{
    /**
     * Convenience subclass of {@link Tuple} to contain the list of
     * ranges expressed for a particular decoded output of an
     * {@link Encoder} by using tightly constrained types without 
     * the verbosity at the instantiation site.
     * 
     */
 [Serializable]
    public class RangeList : RangeTuple<List<MinMax>, string>
    {
        /**
    * Constructs and new {@code Ranges} object.
    * @param l		the {@link List} of {@link MinMax} objects which are the 
    * 				minimum and maximum postions of 1's
    * @param s
    */
        public RangeList(List<MinMax> l, string s)
            : base(l, s)
        {
            
        }

        /**
         * Returns a List of the {@link MinMax}es.
         * @return
         */
        public List<MinMax> GetRanges()
        {
            return l;
        }

        /**
         * Returns a comma-separated String containing the descriptions
         * for all of the {@link MinMax}es
         * @return
         */
        public string GetDescription()
        {
            return desc;
        }

        /**
         * Adds a {@link MinMax} to this list of ranges
         * @param mm
         */
        public void Add(MinMax mm)
        {
            l.Add(mm);
        }

        /**
         * Returns the specified {@link MinMax} 
         * 	
         * @param index		the index of the MinMax to return
         * @return			the specified {@link MinMax} 
         */
        public MinMax GetRange(int index)
        {
            return l[index];
        }

        /**
         * Sets the entire comma-separated description string
         * @param s
         */
        public void SetDescription(string s)
        {
            this.desc = s;
        }

        /**
         * Returns the count of ranges contained in this Ranges object
         * @return
         */
        public override int Count
        {
            get { return l.Count; }
        }
        
        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return l.ToString();
        }
    }
}