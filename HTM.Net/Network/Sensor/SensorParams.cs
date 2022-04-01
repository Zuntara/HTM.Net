using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    [Serializable]
    public class SensorParams : Dictionary<string, object>
    {
        [Obsolete("Only for deserialization")]
        public SensorParams(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        private SensorParams(Func<Keys.Args> keySet, params object[] values)
            : this(keySet().Get(), values)
        {
            
        }

        private SensorParams(string[] keys, params object[] values)
        {
            if (keys.Length != values.Length)
            {
                throw new ArgumentException("keys and values must have the same length");
            }

            for (int i = 0; i < keys.Length; i++)
            {
                Add(keys[i], values[i]);
            }
        }

        public bool HasKey(string key)
        {
            return ContainsKey(key);
        }

        public string[] GetKeys()
        {
            return base.Keys.ToArray();
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
        public static SensorParams Create(string[] keys, params object[] values)
        {
            return new SensorParams(keys, values);
        }

        protected bool Equals(SensorParams other)
        {
            string keys = string.Join(",", GetKeys());
            string otherKeys = string.Join(",", other.GetKeys());
            return keys.Equals(otherKeys);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SensorParams)obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(SensorParams left, SensorParams right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SensorParams left, SensorParams right)
        {
            return !Equals(left, right);
        }

        /**
         * Convenience class to use as handle way to specify an expected
         * String array of key values for each of the 3 known input configuration
         * types.
         */
        [Serializable]
        public class Keys
        {
            public class Args
            {
                public static readonly Args U = new Args(new string[] { "FILE", "URI" });
                public static readonly Args P = new Args(new string[] { "FILE", "PATH" });
                public static readonly Args O = new Args(new string[] { "NAME", "ONSUB" });
                public static readonly Args D = new Args(new string[] { "CONN_STRING", "QUERY" });

                private readonly string[] _arr;

                private Args(string[] s)
                {
                    _arr = s;
                }

                public string[] Get() { return _arr; }
            }

            public static Args Uri()
            {
                return Args.U;
            }
            public static Args Path()
            {
                return Args.P;
            }
            public static Args Obs()
            {
                return Args.O;
            }
            public static Args Db()
            {
                return Args.D;
            }
        }
    }

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
    public class SensorParams2 : NamedTuple
    {
        /**
         * Convenience class to use as handle way to specify an expected
         * String array of key values for each of the 3 known input configuration
         * types.
         */
        [Serializable]
        public class Keys
        {
            public class Args
            {
                public static readonly Args U = new Args(new string[] { "FILE", "URI" });
                public static readonly Args P = new Args(new string[] { "FILE", "PATH" });
                public static readonly Args O = new Args(new string[] { "NAME", "ONSUB" });
                public static readonly Args D = new Args(new string[] { "CONN_STRING", "QUERY" });

                private readonly string[] _arr;

                private Args(string[] s)
                {
                    _arr = s;
                }

                public string[] Get() { return _arr; }
            }

            public static Args Uri()
            {
                return Args.U;
            }
            public static Args Path()
            {
                return Args.P;
            }
            public static Args Obs()
            {
                return Args.O;
            }
            public static Args Db()
            {
                return Args.D;
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
        private SensorParams2(Func<Keys.Args> keySet, params object[] values)
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
        private SensorParams2(string[] keys, params object[] values)
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
        public static SensorParams2 Create(Func<Keys.Args> keySet, params object[] values)
        {
            return new SensorParams2(keySet, values);
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
        public static SensorParams2 Create(string[] keys, params object[]  values)
        {
            return new SensorParams2(keys, values);
        }

        //public static void main(String[] args)
        //{
        //    Object[] n = { "rec-center-hourly", ResourceLocator.locate("rec-center-hourly") };
        //    SensorParams parms = SensorParams.create(Keys::uri, n);
        //    assert(parms != null);
        //}
    }
}