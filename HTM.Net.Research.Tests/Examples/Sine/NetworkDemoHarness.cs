using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.Sine
{
    /**
 * Encapsulates setup methods which are shared among various tests
 * in the {@link org.numenta.nupic.network} package.
 * 
 * @author cogmission
 */
    public class NetworkDemoHarness
    {
        /// <summary>
        /// Sets up an Encoder Mapping of configurable values.
        /// </summary>
        /// <param name="map">
        /// if called more than once to set up encoders for more than one field, this should be the map itself returned
        /// from the first call to {@code #setupMap(Map, int, int, double,double, double, double, Boolean, Boolean, Boolean, String, String, String)}
        /// </param>
        /// <param name="n">the total number of bits in the output encoding</param>
        /// <param name="w">the number of bits to use in the representation</param>
        /// <param name="min">the minimum value (if known i.e. for the ScalarEncoder)</param>
        /// <param name="max">the maximum value (if known i.e. for the ScalarEncoder)</param>
        /// <param name="radius">see {@link Encoder}</param>
        /// <param name="resolution">see {@link Encoder}</param>
        /// <param name="periodic">such as hours of the day or days of the week, which repeat in cycles</param>
        /// <param name="clip">whether the outliers should be clipped to the min and max values</param>
        /// <param name="forced">use the implied or explicitly stated ratio of w to n bits rather than the "suggested" number</param>
        /// <param name="fieldName">the name of the encoded field</param>
        /// <param name="fieldType">the data type of the field</param>
        /// <param name="encoderType">the Camel case class name minus the .class suffix</param>
        /// <returns></returns>
        public static Map<string, Map<string, object>> SetupMap(
                Map<string, Map<string, object>> map,
                int n, int w, double min, double max, double radius, double resolution, bool? periodic,
                bool? clip, bool? forced, string fieldName, string fieldType, string encoderType)
        {

            if (map == null)
            {
                map = new Map<string, Map<string, object>>();
            }
            Map<string, object> inner = null;
            if (!map.TryGetValue(fieldName, out inner))
            {
                map.Add(fieldName, inner = new Map<string, object>());
            }

            inner.Add("n", n);
            inner.Add("w", w);
            inner.Add("minVal", min);
            inner.Add("maxVal", max);
            inner.Add("radius", radius);
            inner.Add("resolution", resolution);

            if (periodic != null) inner.Add("periodic", periodic);
            if (clip != null) inner.Add("clipInput", clip);
            if (forced != null) inner.Add("forced", forced);
            if (fieldName != null)
            {
                inner.Add("fieldName", fieldName);
                inner.Add("name", fieldName);
            }
            if (fieldType != null) inner.Add("fieldType", fieldType);
            if (encoderType != null) inner.Add("encoderType", encoderType);

            return map;
        }

        /**
         * Returns the Hot Gym encoder setup.
         * @return
         */
        public static Map<string, Map<string, object>> GetNetworkDemoFieldEncodingMap()
        {
            //Map<string, Map<string, object>> fieldEncodings = SetupMap(
            //        null,
            //        0, // n
            //        0, // w
            //        0, 0, 0, 0, null, null, null,
            //        "timestamp", "datetime", "DateEncoder");
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                    null,
                    50,
                    21,
                    -10.0, 10.0, 0, 0.1, null, true, null,
                    "sinedata", "float", "ScalarEncoder");

            //fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_TOFD.GetFieldName(), new BitsTuple(21, 9.5)); // Time of day
            //fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_DOFW.GetFieldName(), new BitsTuple(11, 1)); // day of week
            //fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_WKEND.GetFieldName(), new BitsTuple(0, 1)); // Weekend
            //fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_SEASON.GetFieldName(), new BitsTuple(3, 91.5)); // Season
            //fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_PATTERN.GetFieldName(), "dd/MM/yyyy HH:mm");

            return fieldEncodings;
        }

        /**
         * Returns Encoder parameters and meta information for the "Hot Gym" encoder
         * @return
         */
        public static Parameters GetNetworkDemoTestEncoderParams()
        {
            Map<string, Map<string, object>> fieldEncodings = GetNetworkDemoFieldEncodingMap();

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 128 });
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 4);
            p.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 20.0);

            p.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.7);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.0001); // 0.07
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.0005); // 0.01
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);

            p.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 5);
            p.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.21);
            p.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.1);
            p.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.1);
            p.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 9); 
            p.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 6); 

            p.SetParameterByKey(Parameters.KEY.CLIP_INPUT, true);
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }



        /**
         * Parameters and meta information for the "dayOfWeek" encoder
         * @return
         */
        public static Map<string, Map<string, object>> GetDayDemoFieldEncodingMap()
        {
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                    null,
                    8, // n
                    3, // w
                    0.0, 8.0, 0, 1, true, null, true,
                    "dayOfWeek", "number", "ScalarEncoder");
            return fieldEncodings;
        }

        /**
         * Returns Encoder parameters for the "dayOfWeek" test encoder.
         * @return
         */
        public static Parameters GetDayDemoTestEncoderParams()
        {
            Map<string, Map<string, object>> fieldEncodings = GetDayDemoFieldEncodingMap();

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        /**
         * Returns the default parameters used for the "dayOfWeek" encoder and algorithms.
         * @return
         */
        public static Parameters GetParameters()
        {
            Parameters parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 8 });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 20 });
            parameters.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 6);

            //SpatialPooler specific
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 12);//3
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);//0.5
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 5.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLE, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLE, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 10);
            parameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);
            parameters.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 0);

            //Temporal Memory specific
            parameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            parameters.SetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE, 0.8);
            parameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 5);
            parameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 6);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 4);

            return parameters;
        }


    }
}