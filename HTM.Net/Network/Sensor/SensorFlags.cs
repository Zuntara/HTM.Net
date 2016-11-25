namespace HTM.Net.Network.Sensor
{
    /**
     * <p>
     * Designates field type and processing information. One of the 3 row 
     * information types such as Field Name, {@link FieldMetaType} and these
     * {@code SensorFlags}.
     * </p>
     * <p>
     * <ul>
     *      <li><b>R</b> - <em>"reset"</em> -       Specify that a reset should be inserted into the 
     *                                              model when this field evaluates to true. This is 
     *                                              used to manually insert resets. "1" equals reset,
     *                                              "0" equals no reset.
     *                              
     *      <li><b>S</b> - <em>"sequence"</em> -    Specify that a reset should be inserted into the 
     *                                              model when this field changes. This is used when 
     *                                              you have a field that identifies sequences and 
     *                                              you want to insert resets between each sequence.
     *                        
     *      <li><b>T</b> - <em>"timestamp"</em> -   This identifies a date/time field that should be 
     *                                              used as the timestamp for aggregation and other 
     *                                              time-related functions.
     *                              
     *      <li><b>C</b> - <em>"category"</em> -    This indicates that the category encoder should be used.
     *      
     *      <li><b>L</b> - <em>"learn"</em> -       If "1" then learn, if "0" then stop learning.   
     *      
     *      <li><b>B</b> - <em>"blank"</em> -       Blank meaning do nothing (space filler)
     * </ul>
     * 
     * 
     */

    public enum SensorFlags
    {
        /// <summary>
        /// Blank
        /// </summary>
        B = 0,
        Blank = B,
        /// <summary>
        /// Reset
        /// </summary>
        R = 1,
        Reset = R,
        /// <summary>
        /// Sequence
        /// </summary>
        S = 2,
        Sequence = S,
        /// <summary>
        /// Timestamp
        /// </summary>
        T = 3,
        Timestamp = T,
        /// <summary>
        /// Category
        /// </summary>
        C = 4,
        Category = C,
        /// <summary>
        /// Learn
        /// </summary>
        L = 5,
        Learn = L

        //R("reset"), S("sequence"), T("timestamp"), C("category"), L("learn"), B("blank");
    }

    public class SensorFlagsHelper
    {
        /** Flag description */
        private string description;

        public SensorFlagsHelper(string desc)
        {
            this.description = desc;
        }

        /**
         * Returns the description associated with a particular flag.
         * 
         * @return
         */
        public string Description()
        {
            return description;
        }

        /**
         * Returns the flag indicator which specifies special processing 
         * or hints. (see this class' doc)
         * 
         * @param o
         * @return
         */
        public static SensorFlags FromString(object o)
        {
            string val = o.ToString().ToLower();
            switch (val)
            {
                case "r":
                case "reset":
                    return SensorFlags.R;
                case "s":
                case "sequence":
                    return SensorFlags.S;
                case "t":
                case "timestamp":
                    return SensorFlags.T;
                case "c":
                case "category":
                    return SensorFlags.C;
                case "l":
                case "learn":
                    return SensorFlags.L;
                default: return SensorFlags.B;
            }
        }
    }
}