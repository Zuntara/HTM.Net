using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using DeepEqual.Syntax;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net
{
    /// <summary>
    /// Specifies parameters to be used as a configuration for a given <see cref="TemporalMemory"/> or <see cref="SpatialPooler"/> 
    /// See also:
    /// <see cref="Connections"/>
    /// <see cref="ComputeCycle"/>
    /// </summary>
    [Serializable]
    public class Parameters : Persistable
    {
        private static readonly ParametersMap DEFAULTS_ALL;
        private static readonly ParametersMap DEFAULTS_TEMPORAL;
        private static readonly ParametersMap DEFAULTS_SPATIAL;
        private static readonly ParametersMap DEFAULTS_ENCODER;
        private static readonly ParametersMap DEFAULTS_KNN;
        private static readonly ParametersMap DEFAULTS_CLASSIFIER;

        static Parameters()
        {
            ParametersMap defaultParams = new ParametersMap();

            /////////// Universal Parameters ///////////

            defaultParams.Add(KEY.SEED, 42);
            defaultParams.Add(KEY.RANDOM, new MersenneTwister((int)defaultParams[KEY.SEED]));

            /////////// Temporal Memory Parameters ///////////
            ParametersMap defaultTemporalParams = new ParametersMap();
            defaultTemporalParams.Add(KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
            defaultTemporalParams.Add(KEY.CELLS_PER_COLUMN, 32);
            defaultTemporalParams.Add(KEY.ACTIVATION_THRESHOLD, 13);
            defaultTemporalParams.Add(KEY.LEARNING_RADIUS, 2048);
            defaultTemporalParams.Add(KEY.MIN_THRESHOLD, 10);
            defaultTemporalParams.Add(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
            defaultTemporalParams.Add(KEY.MAX_SYNAPSES_PER_SEGMENT, 255);
            defaultTemporalParams.Add(KEY.MAX_SEGMENTS_PER_CELL, 255);
            defaultTemporalParams.Add(KEY.INITIAL_PERMANENCE, 0.21);
            defaultTemporalParams.Add(KEY.CONNECTED_PERMANENCE, 0.5);
            defaultTemporalParams.Add(KEY.PERMANENCE_INCREMENT, 0.10);
            defaultTemporalParams.Add(KEY.PERMANENCE_DECREMENT, 0.10);
            defaultTemporalParams.Add(KEY.PREDICTED_SEGMENT_DECREMENT, 0.0);
            defaultTemporalParams.Add(KEY.TM_VERBOSITY, 0);
            defaultTemporalParams.Add(KEY.LEARN, true);
            DEFAULTS_TEMPORAL = defaultTemporalParams;
            defaultParams.AddAll(DEFAULTS_TEMPORAL);

            //////////// Spatial Pooler Parameters ///////////
            ParametersMap defaultSpatialParams = new ParametersMap();
            defaultSpatialParams.Add(KEY.INPUT_DIMENSIONS, new int[] { 64 });
            defaultSpatialParams.Add(KEY.POTENTIAL_RADIUS, 16);
            defaultSpatialParams.Add(KEY.POTENTIAL_PCT, 0.5);
            defaultSpatialParams.Add(KEY.GLOBAL_INHIBITION, false);
            defaultSpatialParams.Add(KEY.INHIBITION_RADIUS, 0);
            defaultSpatialParams.Add(KEY.LOCAL_AREA_DENSITY, -1.0);
            defaultSpatialParams.Add(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 10.0);
            defaultSpatialParams.Add(KEY.STIMULUS_THRESHOLD, 0.0);
            defaultSpatialParams.Add(KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            defaultSpatialParams.Add(KEY.SYN_PERM_ACTIVE_INC, 0.1);
            defaultSpatialParams.Add(KEY.SYN_PERM_CONNECTED, 0.10);
            defaultSpatialParams.Add(KEY.SYN_PERM_BELOW_STIMULUS_INC, 0.01);
            defaultSpatialParams.Add(KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            defaultSpatialParams.Add(KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.001);
            defaultSpatialParams.Add(KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.001);
            defaultSpatialParams.Add(KEY.DUTY_CYCLE_PERIOD, 1000);
            defaultSpatialParams.Add(KEY.MAX_BOOST, 10.0);
            defaultSpatialParams.Add(KEY.WRAP_AROUND, true);
            defaultSpatialParams.Add(KEY.SP_VERBOSITY, 0);
            defaultSpatialParams.Add(KEY.LEARN, true);
            defaultSpatialParams.Add(KEY.SP_PARALLELMODE, false);   // default off
            DEFAULTS_SPATIAL = defaultSpatialParams;
            defaultParams.AddAll(DEFAULTS_SPATIAL);

            ///////////  Encoder Parameters ///////////
            ParametersMap defaultEncoderParams = new ParametersMap();
            defaultEncoderParams.Add(KEY.N, 500);
            defaultEncoderParams.Add(KEY.W, 21);
            defaultEncoderParams.Add(KEY.MIN_VAL, 0.0);
            defaultEncoderParams.Add(KEY.MAX_VAL, 1000.0);
            defaultEncoderParams.Add(KEY.RADIUS, 21.0);
            defaultEncoderParams.Add(KEY.RESOLUTION, 1.0);
            defaultEncoderParams.Add(KEY.PERIODIC, false);
            defaultEncoderParams.Add(KEY.CLIP_INPUT, false);
            defaultEncoderParams.Add(KEY.FORCED, false);
            defaultEncoderParams.Add(KEY.FIELD_NAME, "UNSET");
            defaultEncoderParams.Add(KEY.FIELD_TYPE, "int");
            defaultEncoderParams.Add(KEY.ENCODER, "ScalarEncoder");
            defaultEncoderParams.Add(KEY.FIELD_ENCODING_MAP, new EncoderSettingsList());
            DEFAULTS_ENCODER = defaultEncoderParams;
            defaultParams.AddAll(DEFAULTS_ENCODER);

            ///////////  Classifier Parameters ///////////
            ParametersMap defaultClassifierParams = new ParametersMap();
            defaultClassifierParams.Add(KEY.AUTO_CLASSIFY, false);
            defaultClassifierParams.Add(KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier));
            defaultClassifierParams.Add(KEY.CLASSIFIER_ALPHA, 0.001);
            defaultClassifierParams.Add(KEY.CLASSIFIER_STEPS, new[] { 1 });

            DEFAULTS_CLASSIFIER = defaultClassifierParams;
            defaultParams.AddAll(DEFAULTS_CLASSIFIER);

            ////////////////// KNNClassifier Defaults ///////////////////
            ParametersMap defaultKNNParams = new ParametersMap();
            defaultKNNParams.Add(KEY.K, 1);
            defaultKNNParams.Add(KEY.EXACT, false);
            defaultKNNParams.Add(KEY.DISTANCE_NORM, 2.0);
            defaultKNNParams.Add(KEY.DISTANCE_METHOD, DistanceMethod.Norm);
            defaultKNNParams.Add(KEY.DISTANCE_THRESHOLD, .0);
            defaultKNNParams.Add(KEY.DO_BINARIZATION, false);
            defaultKNNParams.Add(KEY.BINARIZATION_THRESHOLD, 0.5);
            defaultKNNParams.Add(KEY.USE_SPARSE_MEMORY, true);
            defaultKNNParams.Add(KEY.SPARSE_THRESHOLD, 0.1);
            defaultKNNParams.Add(KEY.RELATIVE_THRESHOLD, false);
            defaultKNNParams.Add(KEY.NUM_WINNERS, 0);
            defaultKNNParams.Add(KEY.NUM_SVD_SAMPLES, -1);
            defaultKNNParams.Add(KEY.NUM_SVD_DIMS, null);
            defaultKNNParams.Add(KEY.FRACTION_OF_MAX, -1.0);
            defaultKNNParams.Add(KEY.MAX_STORED_PATTERNS, -1);
            defaultKNNParams.Add(KEY.REPLACE_DUPLICATES, false);
            defaultKNNParams.Add(KEY.KNN_CELLS_PER_COL, 0);
            DEFAULTS_KNN = defaultKNNParams;
            defaultParams.AddAll(DEFAULTS_KNN);

            DEFAULTS_ALL = defaultParams;
        }

        /// <summary>
        /// Constant values representing configuration parameters for the <see cref="TemporalMemory"/>
        /// </summary>
        [Serializable]
        public sealed class KEY
        {
            /////////// Universal Parameters ///////////
            /// <summary>
            /// Total number of columns
            /// </summary>
            public static readonly KEY COLUMN_DIMENSIONS = new KEY("columnDimensions", typeof(int[]));
            /// <summary>
            /// Total number of cells per column
            /// </summary>
            public static readonly KEY CELLS_PER_COLUMN = new KEY("cellsPerColumn", typeof(int), 1, null);
            /// <summary>
            /// Learning variable
            /// </summary>
            public static readonly KEY LEARN = new KEY("learn", typeof(bool));
            /// <summary>
            /// Random Number Generator
            /// </summary>
            public static readonly KEY RANDOM = new KEY("random", typeof(IRandom));
            /// <summary>
            /// Seed for random number generator
            /// </summary>
            public static readonly KEY SEED = new KEY("seed", typeof(int));

            /////////// Temporal Memory Parameters ///////////
            /**
             * If the number of active connected synapses on a segment
             * is at least this threshold, the segment is said to be active.
             */
            public static readonly KEY ACTIVATION_THRESHOLD = new KEY("activationThreshold", typeof(int), 0, null);
            /**
             * Radius around cell from which it can
             * sample to form distal {@link DistalDendrite} connections.
             */
            public static readonly KEY LEARNING_RADIUS = new KEY("learningRadius", typeof(int), 0, null);
            /**
             * If the number of synapses active on a segment is at least this
             * threshold, it is selected as the best matching
             * cell in a bursting column.
             */
            public static readonly KEY MIN_THRESHOLD = new KEY("minThreshold", typeof(int), 0, null);
            /**
             * The maximum number of synapses added to a segment during learning.
             */
            public static readonly KEY MAX_NEW_SYNAPSE_COUNT = new KEY("maxNewSynapseCount", typeof(int));
            /**
             * The maximum number of synapses that can be added to a segment.
             */
            public static readonly KEY MAX_SYNAPSES_PER_SEGMENT = new KEY("maxSynapsesPerSegment", typeof(int));
            /**
             * The maximum number of {@link Segment}s a {@link Cell} can have.
             */
            public static readonly KEY MAX_SEGMENTS_PER_CELL = new KEY("maxSegmentsPerCell", typeof(int));
            /**
             * Initial permanence of a new synapse
             */
            public static readonly KEY INITIAL_PERMANENCE = new KEY("initialPermanence", typeof(double), 0.0, 1.0);
            /**
             * If the permanence value for a synapse
             * is greater than this value, it is said
             * to be connected.
             */
            public static readonly KEY CONNECTED_PERMANENCE = new KEY("connectedPermanence", typeof(double), 0.0, 1.0);
            /**
             * Amount by which permanence of synapses
             * are incremented during learning.
             */
            public static readonly KEY PERMANENCE_INCREMENT = new KEY("permanenceIncrement", typeof(double), 0.0, 1.0);
            /**
             * Amount by which permanences of synapses
             * are decremented during learning.
             */
            public static readonly KEY PERMANENCE_DECREMENT = new KEY("permanenceDecrement", typeof(double), 0.0, 1.0);
            /**
             * Amount by which active permanences of synapses of previously 
             * predicted but inactive segments are decremented.
             */
            public static readonly KEY PREDICTED_SEGMENT_DECREMENT = new KEY("predictedSegmentDecrement", typeof(double), 0.0, 9.0);
            /** Remove this and add Logging (slf4j) */
            public static readonly KEY TM_VERBOSITY = new KEY("tmVerbosity", typeof(int), 0, 10);


            /////////// Spatial Pooler Parameters ///////////
            public static readonly KEY INPUT_DIMENSIONS = new KEY("inputDimensions", typeof(int[]));
            public static readonly KEY POTENTIAL_RADIUS = new KEY("potentialRadius", typeof(int));
            public static readonly KEY POTENTIAL_PCT = new KEY("potentialPct", typeof(double)); //TODO add range here?
            public static readonly KEY GLOBAL_INHIBITION = new KEY("globalInhibition", typeof(bool));
            public static readonly KEY INHIBITION_RADIUS = new KEY("inhibitionRadius", typeof(int), 0, null);
            public static readonly KEY LOCAL_AREA_DENSITY = new KEY("localAreaDensity", typeof(double)); //TODO add range here?
            public static readonly KEY NUM_ACTIVE_COLUMNS_PER_INH_AREA = new KEY("numActiveColumnsPerInhArea", typeof(double));//TODO add range here?
            public static readonly KEY STIMULUS_THRESHOLD = new KEY("stimulusThreshold", typeof(double)); //TODO add range here?
            public static readonly KEY SYN_PERM_INACTIVE_DEC = new KEY("synPermInactiveDec", typeof(double), 0.0, 1.0);
            public static readonly KEY SYN_PERM_ACTIVE_INC = new KEY("synPermActiveInc", typeof(double), 0.0, 1.0);
            public static readonly KEY SYN_PERM_CONNECTED = new KEY("synPermConnected", typeof(double), 0.0, 1.0);
            public static readonly KEY SYN_PERM_BELOW_STIMULUS_INC = new KEY("synPermBelowStimulusInc", typeof(double), 0.0, 1.0);
            public static readonly KEY SYN_PERM_TRIM_THRESHOLD = new KEY("synPermTrimThreshold", typeof(double), 0.0, 1.0);
            public static readonly KEY MIN_PCT_OVERLAP_DUTY_CYCLES = new KEY("minPctOverlapDutyCycles", typeof(double));//TODO add range here?
            public static readonly KEY MIN_PCT_ACTIVE_DUTY_CYCLES = new KEY("minPctActiveDutyCycles", typeof(double));//TODO add range here?
            public static readonly KEY DUTY_CYCLE_PERIOD = new KEY("dutyCyclePeriod", typeof(int));//TODO add range here?
            public static readonly KEY MAX_BOOST = new KEY("maxBoost", typeof(double)); //TODO add range here?
            public static readonly KEY WRAP_AROUND = new KEY("wrapAround", typeof(bool));
            public static readonly KEY SP_VERBOSITY = new KEY("spVerbosity", typeof(int), 0, 10);
            /// <summary>
            /// If defined this will initialize and run the spatial pooler multithreaded, this will 
            /// make the network less deterministic because the random generator will return different values on different places,
            /// even when a seed is used. (can be a problem for unit tests)
            /// </summary>
            public static readonly KEY SP_PARALLELMODE = new KEY("spParallelMode", typeof(bool));

            ///////////// SpatialPooler / Network Parameter(s) /////////////
            /// <summary>
            /// Number of cycles to send through the SP before forwarding data to the rest of the network.
            /// </summary>
            public static readonly KEY SP_PRIMER_DELAY = new KEY("sp_primer_delay", typeof(int?));

            ///////////// Encoder Parameters //////////////
            /// <summary>
            /// number of bits in the representation (must be &gt;= w)
            /// </summary>
            public static readonly KEY N = new KEY("n", typeof(int));
            /// <summary>
            /// The number of bits that are set to encode a single value - the "width" of the output signal
            /// </summary>
            public static readonly KEY W = new KEY("w", typeof(int));
            /// <summary>
            /// The minimum value of the input signal.
            /// </summary>
            public static readonly KEY MIN_VAL = new KEY("minVal", typeof(double));
            /// <summary>
            /// The maximum value of the input signal.
            /// </summary>
            public static readonly KEY MAX_VAL = new KEY("maxVal", typeof(double));
            /**
             * inputs separated by more than, or equal to this distance will have non-overlapping
             * representations
             */
            public static readonly KEY RADIUS = new KEY("radius", typeof(double));
            /** inputs separated by more than, or equal to this distance will have different representations */
            public static readonly KEY RESOLUTION = new KEY("resolution", typeof(double));
            /**
             * If true, then the input value "wraps around" such that minval = maxval
             * For a periodic value, the input must be strictly less than maxval,
             * otherwise maxval is a true upper bound.
             */
            public static readonly KEY PERIODIC = new KEY("periodic", typeof(bool));
            /** 
             * if true, non-periodic inputs smaller than minval or greater
             * than maxval will be clipped to minval/maxval 
             */
            public static readonly KEY CLIP_INPUT = new KEY("clipInput", typeof(bool));
            /** 
             * If true, skip some safety checks (for compatibility reasons), default false 
             * Mostly having to do with being able to set the window size &lt; 21 
             */
            public static readonly KEY FORCED = new KEY("forced", typeof(bool));
            /// <summary>
            /// Name of the field being encoded
            /// </summary>
            public static readonly KEY FIELD_NAME = new KEY("fieldName", typeof(string));
            /// <summary>
            /// Primitive type of the field, used to auto-configure the type of encoder
            /// </summary>
            public static readonly KEY FIELD_TYPE = new KEY("fieldType", typeof(string));
            /// <summary>
            /// Encoder name
            /// </summary>
            public static readonly KEY ENCODER = new KEY("encoderType", typeof(string));
            /** Designates holder for the Multi Encoding Map */
            public static readonly KEY FIELD_ENCODING_MAP = new KEY("fieldEncodings", typeof(EncoderSettingsList));
            public static readonly KEY CATEGORY_LIST = new KEY("categoryList", typeof(IList));

            /// <summary>
            /// Network Layer indicator for auto classifier generation
            /// </summary>
            public static readonly KEY AUTO_CLASSIFY = new KEY("hasClassifiers", typeof(bool));
            public static readonly KEY AUTO_CLASSIFY_TYPE = new KEY("defaultClassifierType", typeof(IClassifier));
            public static readonly KEY CLASSIFIER_ALPHA = new KEY("classifierAlpha", typeof(double));
            public static readonly KEY CLASSIFIER_STEPS = new KEY("classifierSteps", typeof(int[]));


            // How many bits to use if encoding the respective date fields.
            // e.g. Tuple(bits to use:int, radius:double)
            public static readonly KEY DATEFIELD_SEASON = new KEY("season", typeof(BitsTuple));
            /// <summary>
            /// Day of week
            /// </summary>
            public static readonly KEY DATEFIELD_DOFW = new KEY("dayOfWeek", typeof(BitsTuple));
            public static readonly KEY DATEFIELD_WKEND = new KEY("weekend", typeof(BitsTuple));
            public static readonly KEY DATEFIELD_HOLIDAY = new KEY("holiday", typeof(BitsTuple));
            /// <summary>
            /// Hours of week
            /// </summary>
            public static readonly KEY DATEFIELD_HOW = new KEY("dayOfWeek", typeof(BitsTuple));
            /// <summary>
            /// Time of day
            /// </summary>
            public static readonly KEY DATEFIELD_TOFD = new KEY("timeOfDay", typeof(BitsTuple));
            public static readonly KEY DATEFIELD_CUSTOM = new KEY("customDays", typeof(Tuple)); // e.g. Tuple(bits:int, List<String>:"mon,tue,fri")
            public static readonly KEY DATEFIELD_PATTERN = new KEY("formatPattern", typeof(string));
            public static readonly KEY DATEFIELD_FORMATTER = new KEY("dateFormatter", typeof(DateTimeFormatInfo));

            // -------------------------------
            // Anomaly parameters
            // -------------------------------
            // initialization
            public static readonly KEY ANOMALY_KEY_MODE = new KEY("mode", typeof(Anomaly.Mode));
            public static readonly KEY ANOMALY_KEY_LEARNING_PERIOD = new KEY("claLearningPeriod", typeof(int));
            public static readonly KEY ANOMALY_KEY_ESTIMATION_SAMPLES = new KEY("estimationSamples", typeof(int));
            public static readonly KEY ANOMALY_KEY_USE_MOVING_AVG = new KEY("useMovingAverage", typeof(bool));
            public static readonly KEY ANOMALY_KEY_WINDOW_SIZE = new KEY("windowSize", typeof(int));
            public static readonly KEY ANOMALY_KEY_IS_WEIGHTED = new KEY("isWeighted", typeof(bool));
            // config
            public static readonly KEY ANOMALY_KEY_DIST = new KEY("distribution", typeof(Statistic));
            public static readonly KEY ANOMALY_KEY_MVG_AVG = new KEY("movingAverage", typeof(MovingAverage));
            public static readonly KEY ANOMALY_KEY_HIST_LIKE = new KEY("historicalLikelihoods", typeof(double[]));
            public static readonly KEY ANOMALY_KEY_HIST_VALUES = new KEY("historicalValues", typeof(double[]));
            public static readonly KEY ANOMALY_KEY_TOTAL = new KEY("total", typeof(double));
            // Computational argument keys
            public static readonly KEY ANOMALY_KEY_MEAN = new KEY("mean", typeof(double));
            public static readonly KEY ANOMALY_KEY_STDEV = new KEY("stdev", typeof(double));
            public static readonly KEY ANOMALY_KEY_VARIANCE = new KEY("variance", typeof(double));

            ///////////// KNNClassifier Parameters //////////////
            /** The number of nearest neighbors used in the classification of patterns. <b>Must be odd</b> */
            public static readonly KEY K = new KEY("k", typeof(int));
            /** If true, patterns must match exactly when assigning class labels */
            public static readonly KEY EXACT = new KEY("exact", typeof(bool));
            /** When distance method is "norm", this specifies the p value of the Lp-norm */
            public static readonly KEY DISTANCE_NORM = new KEY("distanceNorm", typeof(double));
            /** 
             * The method used to compute distance between input patterns and prototype patterns.
             * see({@link DistanceMethod}) 
             */
            public static readonly KEY DISTANCE_METHOD = new KEY("distanceMethod", typeof(DistanceMethod));
            /** 
             * A threshold on the distance between learned
             * patterns and a new pattern proposed to be learned. The distance must be
             * greater than this threshold in order for the new pattern to be added to
             * the classifier's memory
             */
            public static readonly KEY DISTANCE_THRESHOLD = new KEY("distanceThreshold", typeof(double));
            /** If True, then scalar inputs will be binarized. */
            public static readonly KEY DO_BINARIZATION = new KEY("doBinarization", typeof(bool));
            /** If doBinarization is True, this specifies the threshold for the binarization of inputs */
            public static readonly KEY BINARIZATION_THRESHOLD = new KEY("binarizationThreshold", typeof(double));
            /** If True, classifier will use a sparse memory matrix */
            public static readonly KEY USE_SPARSE_MEMORY = new KEY("useSparseMemory", typeof(bool));
            /** 
             * If useSparseMemory is True, input variables whose absolute values are 
             * less than this threshold will be stored as zero
             */
            public static readonly KEY SPARSE_THRESHOLD = new KEY("sparseThreshold", typeof(double));
            /** Flag specifying whether to multiply sparseThreshold by max value in input */
            public static readonly KEY RELATIVE_THRESHOLD = new KEY("relativeThreshold", typeof(bool));
            /** Number of elements of the input that are stored. If 0, all elements are stored */
            public static readonly KEY NUM_WINNERS = new KEY("numWinners", typeof(int));
            /** 
             * Number of samples the must occur before a SVD
             * (Singular Value Decomposition) transformation will be performed. If 0,
             * the transformation will never be performed
             */
            public static readonly KEY NUM_SVD_SAMPLES = new KEY("numSVDSamples", typeof(int));
            /** 
             * Controls dimensions kept after SVD transformation. If "adaptive", 
             * the number is chosen automatically
             */
            public static readonly KEY NUM_SVD_DIMS = new KEY("numSVDDims", typeof(int?));
            /**
             * If numSVDDims is "adaptive", this controls the
             * smallest singular value that is retained as a fraction of the largest
             * singular value
             */
            public static readonly KEY FRACTION_OF_MAX = new KEY("fractionOfMax", typeof(double));
            /**
             * Limits the maximum number of the training
             * patterns stored. When KNN learns in a fixed capacity mode, the unused
             * patterns are deleted once the number of stored patterns is greater than
             * maxStoredPatterns. A value of -1 is no limit
             */
            public static readonly KEY MAX_STORED_PATTERNS = new KEY("maxStoredPatterns", typeof(int));
            /**
             * A boolean flag that determines whether,
             * during learning, the classifier replaces duplicates that match exactly,
             * even if distThreshold is 0. Should be TRUE for online learning
             */
            public static readonly KEY REPLACE_DUPLICATES = new KEY("replaceDuplicates", typeof(bool));
            /**
             * If >= 1, input is assumed to be organized into
             * columns, in the same manner as the temporal pooler AND whenever a new
             * prototype is stored, only the start cell (first cell) is stored in any
             * bursting column
             */
            public static readonly KEY KNN_CELLS_PER_COL = new KEY("cellsPerCol", typeof(int));

            private static readonly Map<string, KEY> fieldMap = new Map<string, KEY>();

            static KEY()
            {
                // TODO: load all readonly static fields into the fieldmap (reflection?)
                List<KEY> keys = GetParametersFromClass(typeof(Parameters.KEY));
                foreach (KEY key in keys)
                {
                    fieldMap.Add(key.GetFieldName(), key);
                }
            }

            private static List<KEY> GetParametersFromClass(Type type)
            {
                var publicFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public).ToList();
                var otherFields = type.GetFields().ToList();
                publicFields.AddRange(otherFields);
                var allFields = publicFields.Distinct().ToArray();

                List<KEY> keys = new List<KEY>();
                foreach (var fieldInfo in allFields)
                {
                    keys.Add((KEY)fieldInfo.GetValue(null));
                }
                return keys;
            }

            public static KEY GetKeyByFieldName(string fieldName)
            {
                if (fieldMap.ContainsKey(fieldName))
                {
                    return fieldMap[fieldName];
                }
                return null;
            }

            internal readonly string fieldName;
            private readonly Type fieldType;
            private readonly double? min;
            private readonly double? max;

            /**
             * Constructs a new KEY
             *
             * @param fieldName
             * @param fieldType
             */
            private KEY(string fieldName, Type fieldType)
                : this(fieldName, fieldType, null, null)
            {

            }

            /**
             * Constructs a new KEY with range check
             *
             * @param fieldName
             * @param fieldType
             * @param min
             * @param max
             */
            private KEY(string fieldName, Type fieldType, double? min, double? max)
            {
                this.fieldName = fieldName;
                this.fieldType = fieldType;
                this.min = min;
                this.max = max;
            }

            public Type GetFieldType()
            {
                return fieldType;
            }

            public string GetFieldName()
            {
                return fieldName;
            }

            public double? GetMin()
            {
                return min;
            }

            public double? GetMax()
            {
                return max;
            }

            public bool CheckRange(double? value)
            {
                if (value == null)
                {
                    throw new ArgumentException("checkRange argument can not be null");
                }
                return (min == null && max == null) ||
                       (min != null && max == null && min.GetValueOrDefault() <= value.GetValueOrDefault()) ||
                       (max != null && min == null && value.GetValueOrDefault() < value.GetValueOrDefault()) ||
                       (min != null && min.GetValueOrDefault() <= value.GetValueOrDefault() && max != null && value.GetValueOrDefault() < max.GetValueOrDefault());
            }

            #region Overrides of Object

            public override string ToString()
            {
                return GetFieldName();
            }



            public override bool Equals(object obj)
            {
                if (this == obj)
                    return true;
                if (obj == null)
                    return false;
                if (GetType() != obj.GetType())
                    return false;
                KEY other = (KEY)obj;

                return Equals(other);
            }

            #region Equality members

            private bool Equals(KEY other)
            {
                return string.Equals(fieldName, other.fieldName) && Equals(fieldType, other.fieldType) && min.Equals(other.min) && max.Equals(other.max);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (fieldName != null ? fieldName.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (fieldType != null ? fieldType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ min.GetHashCode();
                    hashCode = (hashCode * 397) ^ max.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(KEY left, KEY right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(KEY left, KEY right)
            {
                return !Equals(left, right);
            }

            #endregion

            #endregion
        }

        /// <summary>
        /// Save guard decorator around params map
        /// </summary>
        [Serializable]
        public class ParametersMap : Dictionary<KEY, object>
        {
            /**
             * Default serialvers
             */
            private const long serialVersionUID = 1L;

            public ParametersMap()
            {

            }

            public ParametersMap(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {

            }

            public new void Add(KEY key, object value)
            {
                if (value != null)
                {
                    if (!(value is Type) && !key.GetFieldType().IsInstanceOfType(value))
                    {
                        throw new ArgumentException(
                            $"Can not set Parameters Property '{key.GetFieldName()}' because of type mismatch. " +
                            $"The required type is class {key.GetFieldType()} and the given class is {value.GetType().Name}");
                    }
                    if ((value is Type) && !key.GetFieldType().IsAssignableFrom((Type)value))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Can not set Parameters Property '{0}' because of type mismatch. The required type is class {1}"
                            , key.GetFieldName(), key.GetFieldType()));
                    }
                    if (value is double? && !key.CheckRange((double?)value))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                            "Can not set Parameters Property '{0}' because of value '{1:0.00}' not in range. Range[{2:0.00}-{3:0.00}]"
                            , key.GetFieldName(), value, key.GetMin(), key.GetMax()));
                    }
                }
                if (base.ContainsKey(key))
                {
                    base[key] = value;
                }
                else
                {
                    base.Add(key, value);
                }

                //return value;
            }
        }

        /// <summary>
        /// Map of parameters to their values
        /// </summary>
        private ParametersMap paramMap = new ParametersMap();

        //TODO apply from container to parameters

        /// <summary>
        /// Returns the size of the internal parameter storage.
        /// </summary>
        public int Size()
        {
            return paramMap.Count;
        }

        /// <summary>
        /// Factory method. Return global <see cref="Parameters"/> object with default values
        /// </summary>
        /// <returns></returns>
        public static Parameters GetAllDefaultParameters()
        {
            return GetParameters(DEFAULTS_ALL);
        }

        /**
         * Factory method. Return temporal {@link Parameters} object with default values
         *
         * @return {@link Parameters} object
         */
        public static Parameters GetTemporalDefaultParameters()
        {
            return GetParameters(DEFAULTS_TEMPORAL);
        }

        /**
         * Factory method. Return spatial {@link Parameters} object with default values
         *
         * @return {@link Parameters} object
         */
        public static Parameters GetSpatialDefaultParameters()
        {
            return GetParameters(DEFAULTS_SPATIAL);
        }

        /**
         * Factory method. Return Encoder {@link Parameters} object with default values
         * @return
         */
        public static Parameters GetEncoderDefaultParameters()
        {
            return GetParameters(DEFAULTS_ENCODER);
        }

        /// <summary>
        /// Factory method. Return KNNClassifier {@link Parameters} object with default values
        /// </summary>
        /// <returns></returns>
        public static Parameters GetKnnDefaultParameters()
        {
            return GetParameters(DEFAULTS_KNN);
        }

        /**
         * Called internally to populate a {@link Parameters} object with the keys
         * and values specified in the passed in map.
         *
         * @return {@link Parameters} object
         */
        private static Parameters GetParameters(IDictionary<KEY, object> map)
        {
            Parameters result = new Parameters();
            foreach (KEY key in map.Keys)
            {
                result.SetParameterByKey(key, map[key]);
            }
            return result;
        }

        public static void ApplyParametersFromDescription(object descriptionObj, Parameters pInstance)
        {
            var props = descriptionObj.GetType().GetProperties();

            foreach (PropertyInfo propertyInfo in props)
            {
                var foundMapping = propertyInfo.GetCustomAttribute<ParameterMapping>();
                if (foundMapping == null) continue;
                string paramFieldName = foundMapping.FieldName;
                if (string.IsNullOrWhiteSpace(foundMapping.FieldName))
                {
                    // Get propertyName
                    paramFieldName = propertyInfo.Name;
                }

                // Lookup the property KEY
                KEY foundKey = KEY.GetKeyByFieldName(paramFieldName);
                if (foundKey != null)
                {
                    var parValue = propertyInfo.GetValue(descriptionObj);
                    pInstance.SetParameterByKey(foundKey, parValue);
                }
                else
                {
                    throw new InvalidOperationException("Parameter: " + paramFieldName + " not found on type " + descriptionObj.GetType().Name + "!");
                }
            }
        }

        /**
         * Constructs a new {@code Parameters} object.
         * It is private. Only allow instantiation with Factory methods.
         * This way we will never have erroneous Parameters with missing attributes
         */
        private Parameters()
        {
        }

        /**
         * Sets the fields specified by this {@code Parameters} on the specified
         * {@link Connections} object.
         *
         * @param cn
         */
        public void Apply(object cn)
        {
            BeanUtil beanUtil = BeanUtil.GetInstance();
            List<KEY> presentKeys = paramMap.Keys.ToList();
            lock (paramMap)
            {
                foreach (KEY key in presentKeys)
                {
                    beanUtil.SetSimpleProperty(cn, key.fieldName, GetParameterByKey(key));
                }
            }
        }

        /**
         * Copies the specified parameters into this {@code Parameters}
         * object over writing the intersecting keys and values.
         * @param p     the Parameters to perform a union with.
         * @return      this Parameters object combined with the specified
         *              Parameters object.
         */
        public Parameters Union(Parameters p)
        {
            foreach (KEY k in p.paramMap.Keys)
            {
                SetParameterByKey(k, p.GetParameterByKey(k));
            }
            return this;
        }

        /**
         * Returns a Set view of the keys in this {@code Parameter}s 
         * object
         * @return
         */
        public List<KEY> Keys()
        {
            List<KEY> retVal = paramMap.Keys.ToList();
            return retVal;
        }

        /**
         * Returns a separate instance of the specified {@code Parameters} object.
         * @return      a unique instance.
         */
        public Parameters Copy()
        {
            return new Parameters().Union(this);
        }

        /**
         * Returns an empty instance of {@code Parameters};
         * @return
         */
        public static Parameters Empty()
        {
            return new Parameters();
        }

        /// <summary>
        /// Set parameter by Key <see cref="KEY"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetParameterByKey(KEY key, object value)
        {
            paramMap.Add(key, value);
        }

        /**
         * Get parameter by Key{@link KEY}
         *
         * @param key
         * @return
         */
        public object GetParameterByKey(KEY key, object defaultValue = null)
        {
            if (paramMap.ContainsKey(key))
            {
                return paramMap[key];
            }
            return defaultValue;
        }

        /**
         * @param key IMPORTANT! This is a nuclear option, should be used with care. 
         * Will knockout key's parameter from map and compromise integrity
         */
        public void ClearParameter(KEY key)
        {
            paramMap.Remove(key);
        }

        /**
         * Convenience method to log difference this {@code Parameters} and specified
         * {@link Connections} object.
         *
         * @param cn
         * @return true if find it different
         */
        public bool LogDiff(object cn)
        {
            if (cn == null)
            {
                throw new ArgumentException("cn Object is required and can not be null");
            }
            bool result = false;
            BeanUtil beanUtil = BeanUtil.GetInstance();
            BeanUtil.InternalPropertyInfo[] properties = beanUtil.GetPropertiesInfoForBean(cn.GetType(), new Dictionary<Type, BeanUtil.InternalPropertyInfo[]>());
            for (int i = 0; i < properties.Length; i++)
            {
                BeanUtil.InternalPropertyInfo property = properties[i];
                string fieldName = property.GetName();
                KEY propKey = KEY.GetKeyByFieldName(property.GetName());
                if (propKey != null)
                {
                    object paramValue = this.GetParameterByKey(propKey);
                    object cnValue = beanUtil.GetSimpleProperty(cn, fieldName);

                    // KEY.POTENTIAL_RADIUS is defined as Math.min(cn.numInputs, potentialRadius) so just log...
                    if (propKey == KEY.POTENTIAL_RADIUS)
                    {
                        Console.WriteLine("Difference is OK: Property:" + fieldName + " is different - CN:" + cnValue + " | PARAM:" + paramValue);
                    }
                    else if ((paramValue != null && !paramValue.Equals(cnValue)) || (paramValue == null && cnValue != null))
                    {
                        result = true;
                        Console.WriteLine("Property:" + fieldName + " is different - CONNECTIONS:" + cnValue + " | PARAMETERS:" + paramValue);
                    }
                }
            }
            return result;
        }

        //TODO I'm not sure we need maintain implicit setters below. Kinda contradict unified access with KEYs

        /**
         * Returns the seeded random number generator.
         *
         * @param r the generator to use.
         */
        public void SetRandom(IRandom r)
        {
            paramMap.Add(KEY.RANDOM, r);
        }

        /**
         * Sets the number of <see cref="Column"/>.
         *
         * @param columnDimensions
         */
        public void SetColumnDimensions(int[] columnDimensions)
        {
            paramMap.Add(KEY.COLUMN_DIMENSIONS, columnDimensions);
        }

        /**
         * Sets the number of <see cref="Cell"/>s per <see cref="Column"/>
         *
         * @param cellsPerColumn
         */
        public void SetCellsPerColumn(int cellsPerColumn)
        {
            paramMap.Add(KEY.CELLS_PER_COLUMN, cellsPerColumn);
        }

        /**
         * <p>
         * Sets the activation threshold.
         * </p>
         * If the number of active connected synapses on a segment
         * is at least this threshold, the segment is said to be active.
         *
         * @param activationThreshold
         */
        public void SetActivationThreshold(int activationThreshold)
        {
            paramMap.Add(KEY.ACTIVATION_THRESHOLD, activationThreshold);
        }

        /**
         * Radius around cell from which it can
         * sample to form distal dendrite connections.
         *
         * @param learningRadius
         */
        public void SetLearningRadius(int learningRadius)
        {
            paramMap.Add(KEY.LEARNING_RADIUS, learningRadius);
        }

        /**
         * If the number of synapses active on a segment is at least this
         * threshold, it is selected as the best matching
         * cell in a bursting column.
         *
         * @param minThreshold
         */
        public void SetMinThreshold(int minThreshold)
        {
            paramMap.Add(KEY.MIN_THRESHOLD, minThreshold);
        }

        /**
         * The maximum number of synapses added to a segment during learning.
         *
         * @param maxSynapsesPerSegment
         */
        public void SetMaxSynapsesPerSegment(int maxSynapsesPerSegment)
        {
            paramMap.Add(KEY.MAX_SYNAPSES_PER_SEGMENT, maxSynapsesPerSegment);
        }

        /**
         * The maximum number of {@link Segment}s a {@link Cell} can have.
         *
         * @param maxSegmentsPerCell
         */
        public void SetMaxSegmentsPerCell(int maxSegmentsPerCell)
        {
            paramMap.Add(KEY.MAX_SEGMENTS_PER_CELL, maxSegmentsPerCell);
        }

        /**
         * The maximum number of synapses added to a segment during learning.
         *
         * @param maxNewSynapseCount
         */
        public void SetMaxNewSynapseCount(int maxNewSynapseCount)
        {
            paramMap.Add(KEY.MAX_NEW_SYNAPSE_COUNT, maxNewSynapseCount);
        }

        /**
         * Seed for random number generator
         *
         * @param seed
         */
        public void SetSeed(int seed)
        {
            paramMap.Add(KEY.SEED, seed);
        }

        /**
         * Initial permanence of a new synapse
         *
         * @param   initialPermanence
         */
        public void SetInitialPermanence(double initialPermanence)
        {
            paramMap.Add(KEY.INITIAL_PERMANENCE, initialPermanence);
        }

        /**
         * If the permanence value for a synapse
         * is greater than this value, it is said
         * to be connected.
         *
         * @param connectedPermanence
         */
        public void SetConnectedPermanence(double connectedPermanence)
        {
            paramMap.Add(KEY.CONNECTED_PERMANENCE, connectedPermanence);
        }

        /**
         * Amount by which permanences of synapses
         * are incremented during learning.
         *
         * @param permanenceIncrement
         */
        public void SetPermanenceIncrement(double permanenceIncrement)
        {
            paramMap.Add(KEY.PERMANENCE_INCREMENT, permanenceIncrement);
        }

        /**
         * Amount by which permanences of synapses
         * are decremented during learning.
         *
         * @param permanenceDecrement
         */
        public void SetPermanenceDecrement(double permanenceDecrement)
        {
            paramMap.Add(KEY.PERMANENCE_DECREMENT, permanenceDecrement);
        }

        ////////////////////////////// SPACIAL POOLER PARAMS //////////////////////////////////

        /**
         * A list representing the dimensions of the input
         * vector. Format is [height, width, depth, ...], where
         * each value represents the size of the dimension. For a
         * topology of one dimension with 100 inputs use 100, or
         * [100]. For a two dimensional topology of 10x5 use
         * [10,5].
         *
         * @param inputDimensions
         */
        public void SetInputDimensions(int[] inputDimensions)
        {
            paramMap.Add(KEY.INPUT_DIMENSIONS, inputDimensions);
        }

        /**
         * This parameter determines the extent of the input
         * that each column can potentially be connected to.
         * This can be thought of as the input bits that
         * are visible to each column, or a 'receptiveField' of
         * the field of vision. A large enough value will result
         * in 'global coverage', meaning that each column
         * can potentially be connected to every input bit. This
         * parameter defines a square (or hyper square) area: a
         * column will have a max square potential pool with
         * sides of length 2 * potentialRadius + 1.
         *
         * @param potentialRadius
         */
        public void SetPotentialRadius(int potentialRadius)
        {
            paramMap.Add(KEY.POTENTIAL_RADIUS, potentialRadius);
        }

        /**
         * The inhibition radius determines the size of a column's local
         * neighborhood. of a column. A cortical column must overcome the overlap
         * score of columns in his neighborhood in order to become actives. This
         * radius is updated every learning round. It grows and shrinks with the
         * average number of connected synapses per column.
         *
         * @param inhibitionRadius the local group size
         */
        public void SetInhibitionRadius(int inhibitionRadius)
        {
            paramMap.Add(KEY.INHIBITION_RADIUS, inhibitionRadius);
        }

        /**
         * The percent of the inputs, within a column's
         * potential radius, that a column can be connected to.
         * If set to 1, the column will be connected to every
         * input within its potential radius. This parameter is
         * used to give each column a unique potential pool when
         * a large potentialRadius causes overlap between the
         * columns. At initialization time we choose
         * ((2*potentialRadius + 1)^(# inputDimensions) *
         * potentialPct) input bits to comprise the column's
         * potential pool.
         *
         * @param potentialPct
         */
        public void SetPotentialPct(double potentialPct)
        {
            paramMap.Add(KEY.POTENTIAL_PCT, potentialPct);
        }

        /**
         * If true, then during inhibition phase the winning
         * columns are selected as the most active columns from
         * the region as a whole. Otherwise, the winning columns
         * are selected with respect to their local
         * neighborhoods. Using global inhibition boosts
         * performance x60.
         *
         * @param globalInhibition
         */
        public void SetGlobalInhibition(bool globalInhibition)
        {
            paramMap.Add(KEY.GLOBAL_INHIBITION, globalInhibition);
        }

        /**
         * The desired density of active columns within a local
         * inhibition area (the size of which is set by the
         * internally calculated inhibitionRadius, which is in
         * turn determined from the average size of the
         * connected potential pools of all columns). The
         * inhibition logic will insure that at most N columns
         * remain ON within a local inhibition area, where N =
         * localAreaDensity * (total number of columns in
         * inhibition area).
         *
         * @param localAreaDensity
         */
        public void SetLocalAreaDensity(double localAreaDensity)
        {
            paramMap.Add(KEY.LOCAL_AREA_DENSITY, localAreaDensity);
        }

        /**
         * An alternate way to control the density of the active
         * columns. If numActivePerInhArea is specified then
         * localAreaDensity must be less than 0, and vice versa.
         * When using numActivePerInhArea, the inhibition logic
         * will insure that at most 'numActivePerInhArea'
         * columns remain ON within a local inhibition area (the
         * size of which is set by the internally calculated
         * inhibitionRadius, which is in turn determined from
         * the average size of the connected receptive fields of
         * all columns). When using this method, as columns
         * learn and grow their effective receptive fields, the
         * inhibitionRadius will grow, and hence the net density
         * of the active columns will *decrease*. This is in
         * contrast to the localAreaDensity method, which keeps
         * the density of active columns the same regardless of
         * the size of their receptive fields.
         *
         * @param numActiveColumnsPerInhArea
         */
        public void SetNumActiveColumnsPerInhArea(double numActiveColumnsPerInhArea)
        {
            paramMap.Add(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, numActiveColumnsPerInhArea);
        }

        /**
         * This is a number specifying the minimum number of
         * synapses that must be on in order for a columns to
         * turn ON. The purpose of this is to prevent noise
         * input from activating columns. Specified as a percent
         * of a fully grown synapse.
         *
         * @param stimulusThreshold
         */
        public void SetStimulusThreshold(double stimulusThreshold)
        {
            paramMap.Add(KEY.STIMULUS_THRESHOLD, stimulusThreshold);
        }

        /**
         * The amount by which an inactive synapse is
         * decremented in each round. Specified as a percent of
         * a fully grown synapse.
         *
         * @param synPermInactiveDec
         */
        public void SetSynPermInactiveDec(double synPermInactiveDec)
        {
            paramMap.Add(KEY.SYN_PERM_INACTIVE_DEC, synPermInactiveDec);
        }

        /**
         * The amount by which an active synapse is incremented
         * in each round. Specified as a percent of a
         * fully grown synapse.
         *
         * @param synPermActiveInc
         */
        public void SetSynPermActiveInc(double synPermActiveInc)
        {
            paramMap.Add(KEY.SYN_PERM_ACTIVE_INC, synPermActiveInc);
        }

        /**
         * The default connected threshold. Any synapse whose
         * permanence value is above the connected threshold is
         * a "connected synapse", meaning it can contribute to
         * the cell's firing.
         *
         * @param synPermConnected
         */
        public void SetSynPermConnected(double synPermConnected)
        {
            paramMap.Add(KEY.SYN_PERM_CONNECTED, synPermConnected);
        }

        /**
         * Sets the increment of synapse permanences below the stimulus
         * threshold
         *
         * @param synPermBelowStimulusInc
         */
        public void SetSynPermBelowStimulusInc(double synPermBelowStimulusInc)
        {
            paramMap.Add(KEY.SYN_PERM_BELOW_STIMULUS_INC, synPermBelowStimulusInc);
        }

        /**
         * @param synPermTrimThreshold
         */
        public void SetSynPermTrimThreshold(double synPermTrimThreshold)
        {
            paramMap.Add(KEY.SYN_PERM_TRIM_THRESHOLD, synPermTrimThreshold);
        }

        /**
         * A number between 0 and 1.0, used to set a floor on
         * how often a column should have at least
         * stimulusThreshold active inputs. Periodically, each
         * column looks at the overlap duty cycle of
         * all other columns within its inhibition radius and
         * sets its own internal minimal acceptable duty cycle
         * to: minPctDutyCycleBeforeInh * max(other columns'
         * duty cycles).
         * On each iteration, any column whose overlap duty
         * cycle falls below this computed value will  get
         * all of its permanence values boosted up by
         * synPermActiveInc. Raising all permanences in response
         * to a sub-par duty cycle before  inhibition allows a
         * cell to search for new inputs when either its
         * previously learned inputs are no longer ever active,
         * or when the vast majority of them have been
         * "hijacked" by other columns.
         *
         * @param minPctOverlapDutyCycles
         */
        public void SetMinPctOverlapDutyCycles(double minPctOverlapDutyCycles)
        {
            paramMap.Add(KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, minPctOverlapDutyCycles);
        }

        /**
         * A number between 0 and 1.0, used to set a floor on
         * how often a column should be activate.
         * Periodically, each column looks at the activity duty
         * cycle of all other columns within its inhibition
         * radius and sets its own internal minimal acceptable
         * duty cycle to:
         * minPctDutyCycleAfterInh *
         * max(other columns' duty cycles).
         * On each iteration, any column whose duty cycle after
         * inhibition falls below this computed value will get
         * its internal boost factor increased.
         *
         * @param minPctActiveDutyCycles
         */
        public void SetMinPctActiveDutyCycles(double minPctActiveDutyCycles)
        {
            paramMap.Add(KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, minPctActiveDutyCycles);
        }

        /**
         * The period used to calculate duty cycles. Higher
         * values make it take longer to respond to changes in
         * boost or synPerConnectedCell. Shorter values make it
         * more unstable and likely to oscillate.
         *
         * @param dutyCyclePeriod
         */
        public void SetDutyCyclePeriod(int dutyCyclePeriod)
        {
            paramMap.Add(KEY.DUTY_CYCLE_PERIOD, dutyCyclePeriod);
        }

        /**
         * The maximum overlap boost factor. Each column's
         * overlap gets multiplied by a boost factor
         * before it gets considered for inhibition.
         * The actual boost factor for a column is number
         * between 1.0 and maxBoost. A boost factor of 1.0 is
         * used if the duty cycle is &gt;= minOverlapDutyCycle,
         * maxBoost is used if the duty cycle is 0, and any duty
         * cycle in between is linearly extrapolated from these
         * 2 end points.
         *
         * @param maxBoost
         */
        public void SetMaxBoost(double maxBoost)
        {
            paramMap.Add(KEY.MAX_BOOST, maxBoost);
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            StringBuilder result = new StringBuilder("{\n");
            StringBuilder spatialInfo = new StringBuilder();
            StringBuilder temporalInfo = new StringBuilder();
            StringBuilder otherInfo = new StringBuilder();

            foreach (KEY key in paramMap.Keys)
            {
                if (DEFAULTS_SPATIAL.ContainsKey(key))
                {
                    BuildParamStr(spatialInfo, key);
                }
                else if (DEFAULTS_TEMPORAL.ContainsKey(key))
                {
                    BuildParamStr(temporalInfo, key);
                }
                else
                {
                    BuildParamStr(otherInfo, key);
                }
            }
            if (spatialInfo.Length > 0)
            {
                result.Append("\tSpatial: {\n").Append(spatialInfo).Append("\t}\n");
            }
            if (temporalInfo.Length > 0)
            {
                result.Append("\tTemporal: {\n").Append(temporalInfo).Append("\t}\n");
            }
            if (otherInfo.Length > 0)
            {
                result.Append("\tOther: {\n").Append(otherInfo).Append("\t}\n");
            }
            return result.Append("}").ToString();
        }

        private void BuildParamStr(StringBuilder spatialInfo, KEY key)
        {
            object value = GetParameterByKey(key);
            if (value is int[])
            {
                value = ArrayUtils.IntArrayToString(value);
            }
            spatialInfo.Append("\t\t").Append(key.GetFieldName()).Append(":").Append(value).Append("\n");
        }

        public override int GetHashCode()
        {
            IRandom rnd = (IRandom)paramMap.Get(KEY.RANDOM);
            paramMap.Remove(KEY.RANDOM);
            int hc = paramMap.GetArrayHashCode();
            paramMap.Add(KEY.RANDOM, rnd);

            return hc;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Parameters other = (Parameters)obj;
            if (paramMap == null)
            {
                if (other.paramMap != null)
                    return false;
            }
            else
            {
                Type[] classArray = new Type[] { typeof(Object) };
                try
                {
                    foreach (KEY key in paramMap.Keys)
                    {
                        if (paramMap.Get(key) == null || other.paramMap.Get(key) == null) continue;

                        Type thisValueClass = paramMap.Get(key).GetType();
                        Type otherValueClass = other.paramMap.Get(key).GetType();
                        bool isSpecial = IsSpecial(key, thisValueClass);
                        if (!isSpecial && (thisValueClass.GetMethod("Equals", classArray).DeclaringType != thisValueClass ||
                            otherValueClass.GetMethod("Equals", classArray).DeclaringType != otherValueClass))
                        {
                            continue;
                        }
                        else if (isSpecial)
                        {
                            if (typeof(int[]).IsAssignableFrom(thisValueClass))
                            {
                                if (!Arrays.AreEqual((int[])paramMap.Get(key), (int[])other.paramMap.Get(key))) return false;
                            }
                            else if (key == KEY.FIELD_ENCODING_MAP)
                            {
                                if (!paramMap.Get(key).IsDeepEqual(other.paramMap.Get(key)))
                                {
                                    return false;
                                }
                            }
                        }
                        else if (!other.paramMap.ContainsKey(key) || !paramMap.Get(key).Equals(other.paramMap.Get(key)))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception e) { return false; }
            }
            return true;
        }

        /**
             * Returns a flag indicating whether the type is an equality
             * special case.
             * @param key       the {@link KEY}
             * @param klazz     the class of the type being considered.
             * @return
             */
        private bool IsSpecial(KEY key, Type klazz)
        {
            if (typeof(int[]).IsAssignableFrom(klazz) || key == KEY.FIELD_ENCODING_MAP)
            {

                return true;
            }
            return false;
        }
    }

    public class ParameterMapping : Attribute
    {
        public string FieldName { get; set; }

        public ParameterMapping()
        {

        }

        public ParameterMapping(string fieldName)
        {
            FieldName = fieldName;
        }
    }
}