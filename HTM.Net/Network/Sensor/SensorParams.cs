using System;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
        /**
     * <p>
     * Implementation of named parameter tuples that is tightly
     * keyed to only known keys in order to assist with proper
     * formation and auto creation within a <see cref="Network"/>.
     * </p>
     * <p>
     * To retrieve a {@code Keys.Args} from this {@code SensorParams}
     * for the purpose of construction follow this pattern for usage:
     * <p>
     * <pre>
     *  Object[] n = { "rec-center-hourly", ResourceLocator.locate("rec-center-hourly") };
        SensorParams parms = SensorParams.create(Keys::uri, n); // May be (Keys::path, Keys::obs) also
     * </pre>
     * 
     * 
     * @author David Ray
     */
    [Serializable]
    public class SensorParams : NamedTuple
    {
        /**
         * Convenience class to use as handle way to specify an expected
         * String array of key values for each of the 3 known input configuration
         * types.
         */
        public class Keys
        {
            public class Args
            {
                public static readonly Args U = new Args(new string[] { "FILE", "URI" });
                public static readonly Args P = new Args(new string[] { "FILE", "PATH" });
                public static readonly Args O = new Args(new string[] { "NAME", "ONSUB" });

                private readonly string[] _arr;

                private Args(string[] s)
                {
                    this._arr = s;
                }

                public string[] Get() { return _arr; }
            }

            public static Keys.Args Uri()
            {
                return Keys.Args.U;
            }
            public static Keys.Args Path()
            {
                return Keys.Args.P;
            }
            public static Keys.Args Obs()
            {
                return Keys.Args.O;
            }
        }

        /**
         * Takes a String array of keys (via {@link Supplier#get()} and a varargs 
         * array of their values, creating key/value pairs. In this case, the keys are 
         * all predetermined to be one of the {@link Keys.Args} types which specify the 
         * keys which are to be used.
         * 
         * @param keySet       a Supplier yielding a particular set of String keys
         * @param values       the values correlated with the specified keys.
         */
        private SensorParams(Func<Keys.Args> keySet, params object[] values)
            : base(keySet().Get(), values)
        {
            
        }

        /**
         * Takes a String array of keys (via {@link Supplier#get()} and a varargs 
         * array of their values, creating key/value pairs. In this case, the keys are 
         * all predetermined to be one of the {@link Keys.Args} types which specify the 
         * keys which are to be used.
         * 
         * @param keys         a String array of keys
         * @param values       the values correlated with the specified keys.
         */
        private SensorParams(string[] keys, params object[] values)
            : base(keys, values)
        {
            
        }

        /**
         * Factory method to create a {@code SensorParams} object which indicates
         * information used to connect a {@link Sensor} to its source.
         * 
         * @param keySet       a Supplier yielding a particular set of String keys
         * @param values       the values correlated with the specified keys.
         * 
         * @return  a SensorParams configuration
         */
        public static SensorParams Create(Func<Keys.Args> keySet, params object[] values)
        {
            return new SensorParams(keySet, values);
        }

        /**
         * Factory method to create a {@code SensorParams} object which indicates
         * information used to connect a {@link Sensor} to its source.
         * 
         * @param keys         a String array of keys
         * @param values       the values correlated with the specified keys.
         * 
         * @return  a SensorParams configuration
         */
        public static SensorParams Create(string[] keys, params object[]  values)
        {
            return new SensorParams(keys, values);
        }

        //public static void main(String[] args)
        //{
        //    Object[] n = { "rec-center-hourly", ResourceLocator.locate("rec-center-hourly") };
        //    SensorParams parms = SensorParams.create(Keys::uri, n);
        //    assert(parms != null);
        //}
    }
}