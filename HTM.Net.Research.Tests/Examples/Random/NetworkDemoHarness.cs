using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.Random
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
        /// <param name="map">if called more than once to set up encoders for more
        /// than one field, this should be the map itself returned
        /// from the first call to {@code #setupMap(Map, int, int, double, double, double, double, Boolean, Boolean, Boolean, String, String, String)}</param>
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
        public static EncoderSettingsList SetupMap(
                        EncoderSettingsList map,
                        int n, int w, double min, double max, double radius, double resolution, bool? periodic,
                        bool? clip, bool? forced, string fieldName, FieldMetaType? fieldType, string encoderType)
        {

            if (map == null)
            {
                map = new EncoderSettingsList();
            }
            EncoderSetting inner;

            if (!map.TryGetValue(fieldName, out inner))
            {
                map.Add(fieldName, inner = new EncoderSetting());
            }

            inner.n = n;
            inner.w = w;
            inner.minVal = min;
            inner.maxVal = max;
            inner.radius = radius;
            inner.resolution = resolution;

            if (periodic != null) inner.periodic = periodic;
            if (clip != null) inner.clipInput = clip;
            if (forced != null) inner.forced = forced;
            if (fieldName != null)
            {
                inner.fieldName = fieldName;
                inner.name = fieldName;
            }
            if (fieldType != null) inner.fieldType = fieldType;
            if (encoderType != null) inner.encoderType = encoderType;

            return map;
        }

        /**
         * Returns the Hot Gym encoder setup.
         * @return
         */
        public static EncoderSettingsList GetRandomDataFieldEncodingMap()
        {
            EncoderSettingsList fieldEncodings = SetupMap(
                    null,
                    0, // n
                    0, // w
                    0, 0, 0, 0, null, null, null,
                    "Date", FieldMetaType.DateTime, "DateEncoder");
            for (int i = 0; i < 6; i++)
            {
                fieldEncodings = SetupMap(
                    fieldEncodings,
                    25,
                    3,
                    1, 45, 0, 1, null, null, null,
                    $"Number {(i+1)}", FieldMetaType.Integer, "RandomDistributedScalarEncoder");
            }
            fieldEncodings = SetupMap(
                    fieldEncodings,
                    25,
                    3,
                    1, 45, 0, 1, null, null, null,
                    "Bonus", FieldMetaType.Integer, "RandomDistributedScalarEncoder");

            fieldEncodings["Date"].dayOfWeek = new Tuple(1, 1.0); // Day of week
            //fieldEncodings["Date"].timeOfDay = new Tuple(5, 4.0); // Time of day
            fieldEncodings["Date"].formatPattern = "dd/MM/YY";

            return fieldEncodings;
        }

        /**
         * Returns the Hot Gym encoder setup.
         * @return
         */
        public static EncoderSettingsList GetNetworkDemoFieldEncodingMap()
        {
            EncoderSettingsList fieldEncodings = SetupMap(
                    null,
                    0, // n
                    0, // w
                    0, 0, 0, 0, null, null, null,
                    "timestamp", FieldMetaType.DateTime, "DateEncoder");
            fieldEncodings = SetupMap(
                    fieldEncodings,
                    50,
                    21,
                    0, 100, 0, 0.1, null, true, null,
                    "consumption", FieldMetaType.Float, "ScalarEncoder");

            fieldEncodings["timestamp"].timeOfDay = new Tuple(21, 9.5); // Time of day
            fieldEncodings["timestamp"].formatPattern = "MM/dd/YY HH:mm";

            return fieldEncodings;
        }

        /**
         * Returns Encoder parameters and meta information for the "Hot Gym" encoder
         * @return
         */
        public static Parameters GetNetworkDemoTestEncoderParams()
        {
            EncoderSettingsList fieldEncodings = GetNetworkDemoFieldEncodingMap();

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
            p.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);

            p.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.8);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.0001); // 0.07
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.0005); // 0.01
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);

            p.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 20);
            p.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.21);
            p.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.1);
            p.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.1);
            p.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 9); 
            p.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 12); 

            p.SetParameterByKey(Parameters.KEY.CLIP_INPUT, true);
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        /**
         * Returns Encoder parameters and meta information for the "Hot Gym" encoder
         * @return
         */
        public static Parameters GetRandomDataFieldEncodingParams()
        {
            EncoderSettingsList fieldEncodings = GetRandomDataFieldEncodingMap();

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        /**
         * Parameters and meta information for the "dayOfWeek" encoder
         * @return
         */
        public static EncoderSettingsList GetDayDemoFieldEncodingMap()
        {
            EncoderSettingsList fieldEncodings = SetupMap(
                    null,
                    8, // n
                    3, // w
                    0.0, 8.0, 0, 1, true, null, true,
                    "dayOfWeek", FieldMetaType.Integer, "ScalarEncoder");
            return fieldEncodings;
        }

        /**
         * Returns Encoder parameters for the "dayOfWeek" test encoder.
         * @return
         */
        public static Parameters GetDayDemoTestEncoderParams()
        {
            EncoderSettingsList fieldEncodings = GetDayDemoFieldEncodingMap();

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
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 20 });
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
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.1);
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