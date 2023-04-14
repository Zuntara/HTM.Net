using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Model;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    public interface IEncoder
    {
        void SetN(int i);
        void SetW(int i);
        void SetMinVal(double minVal);
        void SetMaxVal(double maxVal);
        void SetRadius(double radius);
        void SetResolution(double resolution);
        void SetPeriodic(bool periodic);
        void SetClipInput(bool clipInput);
        void SetForced(bool forced);
        void SetName(string name);
        void SetFieldStats(string fieldName, Map<string, double> fieldStatistics);

        int GetWidth();
        List<double> GetScalars(string s);
        List<string> GetEncodedValues<TS>(TS inputData);
        int[] GetBucketIndices(string input);
        int[] GetBucketIndices(double input);
        List<EncoderResult> GetBucketInfo(int[] buckets);

        List<double> ClosenessScores(List<double> expValues, List<double> actValues, bool fractional);

        void SetLearningEnabled(bool learningEnabled);
        List<Tuple> GetDescription();
        void EncodeIntoArrayUntyped(object o, int[] tempArray);
        /// <summary>
        /// Returns the generic type of the encoder
        /// </summary>
        /// <returns></returns>
        Type GetEncoderType();

        HashSet<FieldMetaType> GetDecoderOutputFieldTypes();

        List<EncoderResult> TopDownCompute(int[] fieldOutput);
        Tuple Decode(int[] fieldOutput, string parentName);

        List<string> GetScalarNames(string parentFieldName);
    }

    public interface IEncoder<T> : IEncoder
    {
        int[] Encode(T inputData);
        void EncodeIntoArray(T inputData, int[] output);
    }

    /**
 * <pre>
 * An encoder takes a value and encodes it with a partial sparse representation
 * of bits.  The Encoder superclass implements:
 * - encode() - returns an array encoding the input; syntactic sugar
 *   on top of encodeIntoArray. If pprint, prints the encoding to the terminal
 * - pprintHeader() -- prints a header describing the encoding to the terminal
 * - pprint() -- prints an encoding to the terminal
 *
 * Methods/properties that must be implemented by subclasses:
 * - getDecoderOutputFieldTypes()   --  must be implemented by leaf encoders; returns
 *                                      [`nupic.data.fieldmeta.FieldMetaType.XXXXX`]
 *                                      (e.g., [nupic.data.fieldmetaFieldMetaType.float])
 * - getWidth()                     --  returns the output width, in bits
 * - encodeIntoArray()              --  encodes input and puts the encoded value into the output array,
 *                                      which is a 1-D array of length returned by getWidth()
 * - getDescription()               --  returns a list of (name, offset) pairs describing the
 *                                      encoded output
 * </pre>
 *
 * <P>
 * Typical usage is as follows:
 * <PRE>
 * CategoryEncoder.Builder builder =  ((CategoryEncoder.Builder)CategoryEncoder.builder())
 *      .w(3)
 *      .radius(0.0)
 *      .minVal(0.0)
 *      .maxVal(8.0)
 *      .periodic(false)
 *      .forced(true);
 *
 * CategoryEncoder encoder = builder.build();
 *
 * <b>Above values are <i>not</i> an example of "sane" values.</b>
 *
 * </PRE>
 */
    [Serializable]
    public abstract class Encoder<T> : Persistable, IEncoder<T>
    {

        [NonSerialized]
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(Encoder<T>));

        /** Value used to represent no data */
        public const double SENTINEL_VALUE_FOR_MISSING_DATA = double.NaN;
        protected List<Tuple> description = new List<Tuple>();

        /** The number of bits that are set to encode a single value - the
         * "width" of the output signal
         */
        protected int w = 0;
        /** number of bits in the representation (must be >= w) */
        protected int n = 0;
        /** the half width value */
        protected int halfWidth;
        /**
         * inputs separated by more than, or equal to this distance will have non-overlapping
         * representations
         */
        protected double radius = 0;
        /// <summary>
        /// inputs separated by more than, or equal to this distance will have different representations
        /// </summary>
        protected double resolution = 0;
        /// <summary>
        /// If true, then the input value "wraps around" such that minval = maxval
        /// For a periodic value, the input must be strictly less than maxval,
        /// otherwise maxval is a true upper bound.
        /// </summary>
        protected bool periodic = true;
        /// <summary>
        /// The minimum value of the input signal.
        /// </summary>
        protected double minVal = 0;
        /// <summary>
        /// The maximum value of the input signal.
        /// </summary>
        protected double maxVal = 0;
        /** if true, non-periodic inputs smaller than minval or greater
                than maxval will be clipped to minval/maxval */
        protected bool clipInput;
        /// <summary>
        /// if true, skip some safety checks (for compatibility reasons), default false
        /// </summary>
        protected bool forced;
        /// <summary>
        /// Encoder name - an optional string which will become part of the description
        /// </summary>
        protected string name = "";
        protected int padding;
        protected int nInternal;
        protected double rangeInternal;
        protected double range;
        protected bool encLearningEnabled;
        protected HashSet<FieldMetaType> flattenedFieldTypeList;
        protected Map<Tuple, List<FieldMetaType>> decoderFieldTypes;
        /**
         * This matrix is used for the topDownCompute. We build it the first time
         * topDownCompute is called
         */
        protected SparseObjectMatrix<int[]> topDownMapping;
        protected double[] topDownValues;
        protected IList bucketValues;
        protected Map<EncoderTuple, List<EncoderTuple>> encoders;
        protected List<string> scalarNames;


        protected Encoder() { }

        ///////////////////////////////////////////////////////////
        /**
         * Sets the "w" or width of the output signal
         * <em>Restriction:</em> w must be odd to avoid centering problems.
         * @param w
         */
        public virtual void SetW(int w)
        {
            this.w = w;
        }

        /// <summary>
        /// Returns w (width of the output signal)
        /// </summary>
        /// <returns></returns>
        public virtual int GetW()
        {
            return w;
        }

        /// <summary>
        /// Half the width
        /// </summary>
        /// <param name="hw"></param>
        public void SetHalfWidth(int hw)
        {
            this.halfWidth = hw;
        }

        /**
         * For non-periodic inputs, padding is the number of bits "outside" the range,
         * on each side. I.e. the representation of minval is centered on some bit, and
         * there are "padding" bits to the left of that centered bit; similarly with
         * bits to the right of the center bit of maxval
         *
         * @param padding
         */
        public void SetPadding(int padding)
        {
            this.padding = padding;
        }

        /**
         * For non-periodic inputs, padding is the number of bits "outside" the range,
         * on each side. I.e. the representation of minval is centered on some bit, and
         * there are "padding" bits to the left of that centered bit; similarly with
         * bits to the right of the center bit of maxval
         *
         * @return
         */
        public int GetPadding()
        {
            return padding;
        }

        /**
         * Sets rangeInternal
         * @param r
         */
        public void SetRangeInternal(double r)
        {
            this.rangeInternal = r;
        }

        /**
         * Returns the range internal value
         * @return
         */
        public double GetRangeInternal()
        {
            return rangeInternal;
        }

        /**
         * Sets the range
         * @param range
         */
        public void SetRange(double range)
        {
            this.range = range;
        }

        /**
         * Returns the range
         * @return
         */
        public double GetRange()
        {
            return range;
        }

        /**
         * nInternal represents the output area excluding the possible padding on each side
         *
         * @param n
         */
        public void SetNInternal(int n)
        {
            this.nInternal = n;
        }

        /**
         * nInternal represents the output area excluding the possible padding on each
         * side
         * @return
         */
        public int GetNInternal()
        {
            return nInternal;
        }

        /**
         * This matrix is used for the topDownCompute. We build it the first time
         * topDownCompute is called
         *
         * @param sm
         */
        public void SetTopDownMapping(SparseObjectMatrix<int[]> sm)
        {
            topDownMapping = sm;
        }

        /**
         * Range of values.
         * @param values
         */
        public void SetTopDownValues(double[] values)
        {
            topDownValues = values;
        }

        /**
         * Returns the top down range of values
         * @return
         */
        public double[] GetTopDownValues()
        {
            return topDownValues;
        }

        /**
         * Return the half width value.
         * @return
         */
        public int GetHalfWidth()
        {
            return halfWidth;
        }

        /**
         * The number of bits in the output. Must be greater than or equal to w
         * @param n
         */
        public void SetN(int n)
        {
            this.n = n;
        }

        /**
         * Returns n
         * @return
         */
        public virtual int GetN()
        {
            return n;
        }

        /**
         * The minimum value of the input signal.
         * @param minVal
         */
        public void SetMinVal(double minVal)
        {
            this.minVal = minVal;
        }

        /**
         * Returns minval
         * @return
         */
        public double GetMinVal()
        {
            return minVal;
        }

        /**
         * The maximum value of the input signal.
         * @param maxVal
         */
        public void SetMaxVal(double maxVal)
        {
            this.maxVal = maxVal;
        }

        /**
         * Returns maxval
         * @return
         */
        public double GetMaxVal()
        {
            return maxVal;
        }

        /**
         * inputs separated by more than, or equal to this distance will have non-overlapping
         * representations
         *
         * @param radius
         */
        public void SetRadius(double radius)
        {
            this.radius = radius;
        }

        /**
         * Returns the radius
         * @return
         */
        public double GetRadius()
        {
            return radius;
        }

        /**
         * inputs separated by more than, or equal to this distance will have different
         * representations
         *
         * @param resolution
         */
        public void SetResolution(double resolution)
        {
            this.resolution = resolution;
        }

        /**
         * Returns the resolution
         * @return
         */
        public double GetResolution()
        {
            return resolution;
        }

        /**
         * If true, non-periodic inputs smaller than minval or greater
         * than maxval will be clipped to minval/maxval
         * @param b
         */
        public void SetClipInput(bool b)
        {
            this.clipInput = b;
        }

        /**
         * Returns the clip input flag
         * @return
         */
        public bool ClipInput()
        {
            return clipInput;
        }

        /**
         * If true, then the input value "wraps around" such that minval = maxval
         * For a periodic value, the input must be strictly less than maxval,
         * otherwise maxval is a true upper bound.
         *
         * @param b
         */
        public void SetPeriodic(bool b)
        {
            this.periodic = b;
        }

        /**
         * Returns the periodic flag
         * @return
         */
        public bool IsPeriodic()
        {
            return periodic;
        }

        /**
         * If true, skip some safety checks (for compatibility reasons), default false
         * @param b
         */
        public void SetForced(bool b)
        {
            this.forced = b;
        }

        /**
         * Returns the forced flag
         * @return
         */
        public bool IsForced()
        {
            return forced;
        }

        /**
         * An optional string which will become part of the description
         * @param name
         */
        public void SetName(string name)
        {
            this.name = name;
        }

        /**
         * Returns the optional name
         * @return
         */
        public virtual string GetName()
        {
            return name;
        }

        public Type GetEncoderType()
        {
            return typeof(T);
        }

        /**
         * Adds a the specified {@link Encoder} to the list of the specified
         * parent's {@code Encoder}s.
         *
         * @param parent	the parent Encoder
         * @param name		Name of the {@link Encoder}
         * @param e			the {@code Encoder}
         * @param offset	the offset of the encoded output the specified encoder
         * 					was used to encode.
         */
        public void AddEncoder(Encoder<T> parent, string name, IEncoder child, int offset)
        {
            if (encoders == null)
            {
                encoders = new Map<EncoderTuple, List<EncoderTuple>>();
            }

            EncoderTuple key = GetEncoderTuple(parent);
            // Insert a new Tuple for the parent if not yet added.
            if (key == null)
            {
                encoders.Add(key = new EncoderTuple("", this, 0), new List<EncoderTuple>());
            }

            List<EncoderTuple> childEncoders = null;
            if ((childEncoders = encoders[key]) == null)
            {
                encoders.Add(key, childEncoders = new List<EncoderTuple>());
            }
            childEncoders.Add(new EncoderTuple(name, child, offset));
        }

        /**
         * Returns the {@link Tuple} containing the specified {@link Encoder}
         * @param e		the Encoder the return value should contain
         * @return		the {@link Tuple} containing the specified {@link Encoder}
         */
        public EncoderTuple GetEncoderTuple(IEncoder e)
        {
            if (encoders == null)
            {
                encoders = new Map<EncoderTuple, List<EncoderTuple>>();
            }

            //for (EncoderTuple tuple : encoders.keySet())
            foreach (EncoderTuple tuple in encoders.Keys)
            {
                if (tuple.GetEncoder().Equals(e))
                {
                    return tuple;
                }
            }

            return null;
        }

        /**
         * Returns the list of child {@link Encoder} {@link Tuple}s
         * corresponding to the specified {@code Encoder}
         *
         * @param e		the parent {@link Encoder} whose child Encoder Tuples are being returned
         * @return		the list of child {@link Encoder} {@link Tuple}s
         */
        public List<EncoderTuple> GetEncoders(IEncoder e)
        {
            EncoderTuple tuple = GetEncoderTuple(e);
            Dictionary<EncoderTuple, List<EncoderTuple>> encoders = GetEncoders();
            if (tuple != null && encoders.ContainsKey(tuple))
            {
                return encoders[tuple];
            }

            return new List<EncoderTuple>();
        }

        /// <summary>
        /// Returns the list of <see cref="IEncoder"/>s
        /// </summary>
        /// <returns></returns>
        public Map<EncoderTuple, List<EncoderTuple>> GetEncoders()
        {
            if (encoders == null)
            {
                encoders = new Map<EncoderTuple, List<EncoderTuple>>();
            }

            return encoders;
        }

        /**
         * Sets the encoder flag indicating whether learning is enabled.
         *
         * @param	encLearningEnabled	true if learning is enabled, false if not
         */
        public void SetLearningEnabled(bool encLearningEnabled)
        {
            this.encLearningEnabled = encLearningEnabled;
        }

        /**
         * Returns a flag indicating whether encoder learning is enabled.
         */
        public bool IsEncoderLearningEnabled()
        {
            return encLearningEnabled;
        }

        /**
         * Returns the list of all field types of the specified {@link Encoder}.
         *
         * @return	List<FieldMetaType>
         */
        public List<FieldMetaType> GetFlattenedFieldTypeList(Encoder<T> e)
        {
            if (decoderFieldTypes == null)
            {
                decoderFieldTypes = new Map<Tuple, List<FieldMetaType>>();
            }

            EncoderTuple key = GetEncoderTuple(e);
            List<FieldMetaType> fieldTypes = null;
            if ((fieldTypes = decoderFieldTypes[key]) == null)
            {
                decoderFieldTypes.Add(key, fieldTypes = new List<FieldMetaType>());
            }
            return fieldTypes;
        }

        /**
         * Returns the list of all field types of a parent {@link Encoder} and all
         * leaf encoders flattened in a linear list which does not retain any parent
         * child relationship information.
         *
         * @return	List<FieldMetaType>
         */
        public HashSet<FieldMetaType> GetFlattenedFieldTypeList()
        {
            return flattenedFieldTypeList;
        }

        /**
         * Sets the list of flattened {@link FieldMetaType}s
         *
         * @param l		list of {@link FieldMetaType}s
         */
        public void SetFlattenedFieldTypeList(HashSet<FieldMetaType> l)
        {
            flattenedFieldTypeList = l;
        }

        /// <summary>
        /// Returns the names of the fields
        /// </summary>
        /// <returns>list of names</returns>
        public List<string> GetScalarNames()
        {
            return scalarNames;
        }

        /// <summary>
        /// Sets the names of the fields
        /// </summary>
        /// <param name="names"></param>
        public void SetScalarNames(List<string> names)
        {
            this.scalarNames = names;
        }
        ///////////////////////////////////////////////////////////


        /**
         * Should return the output width, in bits.
         */
        public abstract int GetWidth();

        /**
         * Returns true if the underlying encoder works on deltas
         */
        public abstract bool IsDelta();

        /**
         * Encodes inputData and puts the encoded value into the output array,
         * which is a 1-D array of length returned by {@link #getW()}.
         *
         * Note: The output array is reused, so clear it before updating it.
         * @param inputData Data to encode. This should be validated by the encoder.
         * @param output 1-D array of same length returned by {@link #getW()}
         *
         * @return
         */
        public abstract void EncodeIntoArray(T inputData, int[] output);

        public abstract void EncodeIntoArrayUntyped(object o, int[] tempArray);

        /**
         * Set whether learning is enabled.
         * @param 	learningEnabled		flag indicating whether learning is enabled
         */
        public virtual void SetLearning(bool learningEnabled)
        {
            SetLearningEnabled(learningEnabled);
        }

        /**
         * This method is called by the model to set the statistics like min and
         * max for the underlying encoders if this information is available.
         * @param	fieldName			fieldName name of the field this encoder is encoding, provided by
         *     							{@link MultiEncoder}
         * @param	fieldStatistics		fieldStatistics dictionary of dictionaries with the first level being
         *     							the fieldName and the second index the statistic ie:
         *     							fieldStatistics['pounds']['min']
         */
        public virtual void SetFieldStats(string fieldName, Map<string, double> fieldStatistics) { }

        /**
         * Convenience wrapper for {@link #encodeIntoArray(double, int[])}
         * @param inputData		the input scalar
         *
         * @return	an array with the encoded representation of inputData
         */
        public int[] Encode(T inputData)
        {
            int[] output = new int[GetN()];
            EncodeIntoArray(inputData, output);
            return output;
        }

        /**
         * Return the field names for each of the scalar values returned by
         * .
         * @param parentFieldName	parentFieldName The name of the encoder which is our parent. This name
         *     						is prefixed to each of the field names within this encoder to form the
         *      					keys of the dict() in the retval.
         *
         * @return
         */
        public List<string> GetScalarNames(string parentFieldName)
        {
            List<string> names = new List<string>();
            if (GetEncoders() != null && GetEncoders().Count > 0)
            {
                List<EncoderTuple> encoders = GetEncoders(this);
                //for (Tuple tuple : encoders)
                foreach (var tuple in encoders)
                {
                    List<string> subNames = ((IEncoder)tuple.Item3).GetScalarNames(GetName());
                    List<string> hierarchicalNames = new List<string>();
                    if (parentFieldName != null)
                    {
                        //for (String name : subNames)
                        foreach (string name in subNames)
                        {
                            hierarchicalNames.Add($"{parentFieldName}.{name}");
                        }
                    }

                    names.AddRange(hierarchicalNames);
                }
            }
            else
            {
                if (parentFieldName != null)
                {
                    names.Add(GetName());
                }
                else
                {
                    names.Add((string)GetEncoderTuple(this).Get(0));
                }
            }

            return names;
        }

        /**
         * Returns a sequence of field types corresponding to the elements in the
         * decoded output field array.  The types are defined by {@link FieldMetaType}
         *
         * @return
         */
        public virtual HashSet<FieldMetaType> GetDecoderOutputFieldTypes()
        {
            if (GetFlattenedFieldTypeList() != null)
            {
                return new HashSet<FieldMetaType>(GetFlattenedFieldTypeList());
            }

            HashSet<FieldMetaType> retVal = new HashSet<FieldMetaType>();
            foreach (var t in GetEncoders(this))
            {
                HashSet<FieldMetaType> subTypes = ((Encoder<T>)t.Item2).GetDecoderOutputFieldTypes();
                foreach (FieldMetaType subType in subTypes)
                {
                    retVal.Add(subType);
                }
            }
            SetFlattenedFieldTypeList(retVal);
            return retVal;
        }

        /**
         * Gets the value of a given field from the input record
         * @param inputObject	input object
         * @param fieldName		the name of the field containing the input object.
         */
        public object GetInputValue(object inputObject, string fieldName)
        {
            if (typeof(IDictionary<string, object>).IsAssignableFrom(inputObject.GetType()))
            {
                //if (Map.class.isAssignableFrom(inputObject.getClass())) {

                IDictionary<string, object> map = (IDictionary<string, object>)inputObject;
                if (!map.ContainsKey(fieldName))
                {
                    throw new InvalidOperationException("Unknown field name '" + fieldName +
                        "' known fields are: " + Arrays.ToString(map.Keys) + ". ");
                }
                if (map.GetType().FullName.Contains("InputMap"))
                {
                    var getMethod = map.GetType().GetMethod("Get");
                    getMethod = getMethod.MakeGenericMethod(typeof(T));
                    return getMethod.Invoke(map, new object[] { fieldName }); // map[fieldName];
                }
                return map[fieldName];
            }
            return null;
        }

        /**
         * Returns an {@link TDoubleList} containing the sub-field scalar value(s) for
         * each sub-field of the inputData. To get the associated field names for each of
         * the scalar values, call getScalarNames().
         *
         * For a simple scalar encoder, the scalar value is simply the input unmodified.
         * For category encoders, it is the scalar representing the category string
         * that is passed in.
         *
         * TODO This is not correct for DateEncoder:
         *
         * For the datetime encoder, the scalar value is the
         * the number of seconds since epoch.
         *
         * The intent of the scalar representation of a sub-field is to provide a
         * baseline for measuring error differences. You can compare the scalar value
         * of the inputData with the scalar value returned from topDownCompute() on a
         * top-down representation to evaluate prediction accuracy, for example.
         *
         * @param <S>  the specifically typed input object
         *
         * @return
         */
        public virtual List<double> GetScalars(string d)
        {
            List<double> retVals = new List<double>();
            //double inputData = (double)d;
            List<EncoderTuple> encoders = GetEncoders(this);
            if (encoders != null)
            {
                foreach (EncoderTuple t in encoders)
                {
                    List<double> values = t.GetEncoder().GetScalars(d);
                    retVals.AddRange(values);
                }
            }
            return retVals;
        }

        /**
         * Returns the input in the same format as is returned by topDownCompute().
         * For most encoder types, this is the same as the input data.
         * For instance, for scalar and category types, this corresponds to the numeric
         * and string values, respectively, from the inputs. For datetime encoders, this
         * returns the list of scalars for each of the sub-fields (timeOfDay, dayOfWeek, etc.)
         *
         * This method is essentially the same as getScalars() except that it returns
         * strings
         * @param <S> 	The input data in the format it is received from the data source
         *
         * @return A list of values, in the same format and in the same order as they
         * are returned by topDownCompute.
         *
         * @return	list of encoded values in String form
         */
        public List<string> GetEncodedValues<S>(S inputData)
        {
            List<string> retVals = new List<string>();
            Dictionary<EncoderTuple, List<EncoderTuple>> encoders = GetEncoders();
            if (encoders != null && encoders.Count > 0)
            {
                foreach (EncoderTuple t in encoders.Keys)
                {
                    retVals.AddRange(t.GetEncoder().GetEncodedValues(inputData));
                }
            }
            else {
                retVals.Add(inputData.ToString());
            }

            return retVals;
        }

        /**
         * Returns an array containing the sub-field bucket indices for
         * each sub-field of the inputData. To get the associated field names for each of
         * the buckets, call getScalarNames().
         * @param  	input 	The data from the source. This is typically a object with members.
         *
         * @return 	array of bucket indices
         */
        public virtual int[] GetBucketIndices(string input)
        {
            List<int> l = new List<int>();
            Dictionary<EncoderTuple, List<EncoderTuple>> encoders = GetEncoders();
            if (encoders != null && encoders.Count > 0)
            {
                foreach (EncoderTuple t in encoders.Keys)
                {
                    l.AddRange(t.GetEncoder().GetBucketIndices(input));
                }
            }
            else {
                throw new InvalidOperationException("Should be implemented in base classes that are not " +
                    "containers for other encoders");
            }
            return l.ToArray();
        }

        /**
         * Returns an array containing the sub-field bucket indices for
         * each sub-field of the inputData. To get the associated field names for each of
         * the buckets, call getScalarNames().
         * @param  	input 	The data from the source. This is typically a object with members.
         *
         * @return 	array of bucket indices
         */
        public virtual int[] GetBucketIndices(double input)
        {
            List<int> l = new List<int>();
            Dictionary<EncoderTuple, List<EncoderTuple>> encoders = GetEncoders();
            if (encoders != null && encoders.Count > 0)
            {
                foreach (EncoderTuple t in encoders.Keys)
                {
                    l.AddRange(t.GetEncoder().GetBucketIndices(input));
                }
            }
            else {
                throw new InvalidOperationException("Should be implemented in base classes that are not " +
                    "containers for other encoders");
            }
            return l.ToArray();
        }

        /**
         * Return a pretty print string representing the return values from
         * getScalars and getScalarNames().
         * @param scalarValues 	input values to encode to string
         * @param scalarNames 	optional input of scalar names to convert. If None, gets
         *                  	scalar names from getScalarNames()
         *
         * @return string representation of scalar values
         */
        public string ScalarsToStr(List<string> scalarValues, List<string> scalarNames)
        {
            if (scalarNames == null || !scalarNames.Any())
            {
                scalarNames = GetScalarNames("");
            }

            StringBuilder desc = new StringBuilder();
            foreach (var t in ArrayUtils.Zip(scalarNames, scalarValues))
            {
                if (desc.Length > 0)
                {
                    desc.Append(string.Format(", {0}:{1:#.00}", t.Get(0), t.Get(1)));
                }
                else {
                    desc.Append(string.Format("{0}:{1:#.00}", t.Get(0), t.Get(1)));
                }
            }
            return desc.ToString();
        }

        /**
         * This returns a list of tuples, each containing (name, offset).
         * The 'name' is a string description of each sub-field, and offset is the bit
         * offset of the sub-field for that encoder.
         *
         * For now, only the 'multi' and 'date' encoders have multiple (name, offset)
         * pairs. All other encoders have a single pair, where the offset is 0.
         *
         * @return		list of tuples, each containing (name, offset)
         */
        public virtual List<Tuple> GetDescription()
        {
            return description;
        }


        /**
         * Return a description of the given bit in the encoded output.
         * This will include the field name and the offset within the field.
         * @param bitOffset  	Offset of the bit to get the description of
         * @param formatted     If True, the bitOffset is w.r.t. formatted output,
         *                     	which includes separators
         *
         * @return tuple(fieldName, offsetWithinField)
         */
        public Tuple EncodedBitDescription(int bitOffset, bool formatted)
        {
            //Find which field it's in
            List<Tuple> description = GetDescription();
            int len = description.Count;
            string prevFieldName = null;
            int prevFieldOffset = -1;
            int offset = -1;
            for (int i = 0; i < len; i++)
            {
                Tuple t = description[i];//(name, offset)
                if (formatted)
                {
                    offset = ((int)t.Item2) + 1;
                    if (bitOffset == offset - 1)
                    {
                        prevFieldName = "separator";
                        prevFieldOffset = bitOffset;
                    }
                }
                if (bitOffset < offset) break;
            }
            // Return the field name and offset within the field
            // return (fieldName, bitOffset - fieldOffset)
            int width = formatted ? GetDisplayWidth() : GetWidth();

            if (prevFieldOffset == -1 || bitOffset > GetWidth())
            {
                throw new InvalidOperationException("Bit is outside of allowable range: " +
                    string.Format("[0 - %d]", width));
            }
            return new Tuple(prevFieldName, bitOffset - prevFieldOffset);
        }

        /**
         * Pretty-print a header that labels the sub-fields of the encoded
         * output. This can be used in conjunction with {@link #pprint(int[], String)}.
         * @param prefix
         */
        public void PPrintHeader(string prefix)
        {
            LOGGER.Info(prefix == null ? "" : prefix);

            List<Tuple> description = GetDescription();
            description.Add(new Tuple("end", GetWidth()));

            int len = description.Count - 1;
            for (int i = 0; i < len; i++)
            {
                string name = (string)description[i].Item1;
                int width = (int)description[i + 1].Item2;

                string formatStr = string.Format("{0} %%-%ds |", width);
                StringBuilder pname = new StringBuilder(name);
                if (name.Length > width) pname.Length = (width);

                LOGGER.Info(string.Format(formatStr, pname));
            }

            len = GetWidth() + (description.Count - 1) * 3 - 1;
            StringBuilder hyphens = new StringBuilder();
            for (int i = 0; i < len; i++) hyphens.Append("-");
            LOGGER.Info(new StringBuilder(prefix).Append(hyphens).ToString());
        }

        /**
         * Pretty-print the encoded output using ascii art.
         * @param output
         * @param prefix
         */
        public void PPrint(int[] output, string prefix)
        {
            LOGGER.Info(prefix == null ? "" : prefix);

            List<Tuple> description = GetDescription();
            description.Add(new Tuple("end", GetWidth()));

            int len = description.Count - 1;
            for (int i = 0; i < len; i++)
            {
                int offset = (int)description[i].Item2;
                int nextOffset = (int)description[i + 1].Item2;

                LOGGER.Info(
                        string.Format("{0} |",
                                ArrayUtils.BitsToString(
                                        ArrayUtils.Sub(output, ArrayUtils.Range(offset, nextOffset))
                                )
                        )
                );
            }
        }

        /**
         * Takes an encoded output and does its best to work backwards and generate
         * the input that would have generated it.
         *
         * In cases where the encoded output contains more ON bits than an input
         * would have generated, this routine will return one or more ranges of inputs
         * which, if their encoded outputs were ORed together, would produce the
         * target output. This behavior makes this method suitable for doing things
         * like generating a description of a learned coincidence in the SP, which
         * in many cases might be a union of one or more inputs.
         *
         * If instead, you want to figure the *most likely* single input scalar value
         * that would have generated a specific encoded output, use the topDownCompute()
         * method.
         *
         * If you want to pretty print the return value from this method, use the
         * decodedToStr() method.
         *
         *************
         * OUTPUT EXPLAINED:
         *
         * fieldsMap is a {@link Map} where the keys represent field names
         * (only 1 if this is a simple encoder, > 1 if this is a multi
         * or date encoder) and the values are the result of decoding each
         * field. If there are  no bits in encoded that would have been
         * generated by a field, it won't be present in the Map. The
         * key of each entry in the dict is formed by joining the passed in
         * parentFieldName with the child encoder name using a '.'.
         *
         * Each 'value' in fieldsMap consists of a {@link Tuple} of (ranges, desc),
         * where ranges is a list of one or more {@link MinMax} ranges of
         * input that would generate bits in the encoded output and 'desc'
         * is a comma-separated pretty print description of the ranges.
         * For encoders like the category encoder, the 'desc' will contain
         * the category names that correspond to the scalar values included
         * in the ranges.
         *
         * The fieldOrder is a list of the keys from fieldsMap, in the
         * same order as the fields appear in the encoded output.
         *
         * Example retvals for a scalar encoder:
         *
         *   {'amount':  ( [[1,3], [7,10]], '1-3, 7-10' )}
         *   {'amount':  ( [[2.5,2.5]],     '2.5'       )}
         *
         * Example retval for a category encoder:
         *
         *   {'country': ( [[1,1], [5,6]], 'US, GB, ES' )}
         *
         * Example retval for a multi encoder:
         *
         *   {'amount':  ( [[2.5,2.5]],     '2.5'       ),
         *   'country': ( [[1,1], [5,6]],  'US, GB, ES' )}
         * @param encoded      		The encoded output that you want decode
         * @param parentFieldName 	The name of the encoder which is our parent. This name
         *      					is prefixed to each of the field names within this encoder to form the
         *    						keys of the {@link Map} returned.
         *
         * @returns Tuple(fieldsMap, fieldOrder)
         */
        public virtual Tuple Decode(int[] encoded, string parentFieldName)
        {
            Map<string, RangeList> fieldsMap = new Map<string, RangeList>();
            List<string> fieldsOrder = new List<string>();

            string parentName = parentFieldName == null || !parentFieldName.Any() ?
                GetName() : string.Format("{0}.{1} ", parentFieldName, GetName());

            List<EncoderTuple> encoders = GetEncoders(this);
            int len = encoders.Count;
            for (int i = 0; i < len; i++)
            {
                Tuple threeFieldsTuple = encoders[i];
                int nextOffset = 0;
                if (i < len - 1)
                {
                    nextOffset = (int)encoders[i + 1].Get(2);
                }
                else {
                    nextOffset = GetW();
                }

                int[] fieldOutput = ArrayUtils.Sub(encoded, ArrayUtils.Range((int)threeFieldsTuple.Get(2), nextOffset));

                Tuple result = ((IEncoder)threeFieldsTuple.Get(1)).Decode(fieldOutput, parentName);

                foreach (var tuplePair in (Map<string, RangeList>)result.Get(0))
                {
                    fieldsMap.Add(tuplePair.Key, tuplePair.Value);
                }

                //fieldsMap.AddRange((Dictionary<string, Tuple>)result.Item1);
                fieldsOrder.AddRange((List<string>)result.Get(1));
            }

            return new Tuple(fieldsMap, fieldsOrder);
        }

        /**
         * Return a pretty print string representing the return value from decode().
         *
         * @param decodeResults
         * @return
         */
        public string DecodedToStr(Tuple decodeResults)
        {
            StringBuilder desc = new StringBuilder();
            Map<string, RangeList> fieldsDict = (Map<string, RangeList>)decodeResults.Item1;
            List<string> fieldsOrder = (List<string>)decodeResults.Item2;
            foreach (string fieldName in fieldsOrder)
            {
                Tuple ranges = fieldsDict[fieldName];
                if (desc.Length > 0)
                {
                    desc.Append(", ").Append(fieldName).Append(":");
                }
                else {
                    desc.Append(fieldName).Append(":");
                }
                desc.Append("[").Append(ranges.Item2).Append("]");
            }
            return desc.ToString();
        }

        /**
         * Returns a list of items, one for each bucket defined by this encoder.
         * Each item is the value assigned to that bucket, this is the same as the
         * EncoderResult.value that would be returned by getBucketInfo() for that
         * bucket and is in the same format as the input that would be passed to
         * encode().
         *
         * This call is faster than calling getBucketInfo() on each bucket individually
         * if all you need are the bucket values.
         *
         * @param	returnType 		class type parameter so that this method can return encoder
         * 							specific value types
         *
         * @return  list of items, each item representing the bucket value for that
         *          bucket.
         */
        public abstract List<TS> GetBucketValues<TS>(Type returnType);

        /**
         * Returns a list of {@link EncoderResult}s describing the inputs for
         * each sub-field that correspond to the bucket indices passed in 'buckets'.
         * To get the associated field names for each of the values, call getScalarNames().
         * @param buckets 	The list of bucket indices, one for each sub-field encoder.
         *              	These bucket indices for example may have been retrieved
         *              	from the getBucketIndices() call.
         *
         * @return A list of {@link EncoderResult}s. Each EncoderResult has
         */
        public virtual List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            //Concatenate the results from bucketInfo on each child encoder
            List<EncoderResult> retVals = new List<EncoderResult>();
            int bucketOffset = 0;
            foreach (EncoderTuple encoderTuple in GetEncoders(this))
            {
                int nextBucketOffset = -1;
                List<EncoderTuple> childEncoders = null;
                if ((childEncoders = GetEncoders(encoderTuple.GetEncoder())).Count != 0)
                {
                    nextBucketOffset = bucketOffset + childEncoders.Count;
                }
                else {
                    nextBucketOffset = bucketOffset + 1;
                }
                int[] bucketIndices = ArrayUtils.Sub(buckets, ArrayUtils.Range(bucketOffset, nextBucketOffset));
                List<EncoderResult> values = encoderTuple.GetEncoder().GetBucketInfo(bucketIndices);

                retVals.AddRange(values);

                bucketOffset = nextBucketOffset;
            }

            return retVals;
        }

        /**
         * Returns a list of EncoderResult named tuples describing the top-down
         * best guess inputs for each sub-field given the encoded output. These are the
         * values which are most likely to generate the given encoded output.
         * To get the associated field names for each of the values, call
         * getScalarNames().
         * @param encoded The encoded output. Typically received from the topDown outputs
         *              from the spatial pooler just above us.
         *
         * @returns A list of EncoderResult named tuples. Each EncoderResult has
         *        three attributes:
         *
         *        -# value:         This is the best-guess value for the sub-field
         *                          in a format that is consistent with the type
         *                          specified by getDecoderOutputFieldTypes().
         *                          Note that this value is not necessarily
         *                          numeric.
         *
         *        -# scalar:        The scalar representation of this best-guess
         *                          value. This number is consistent with what
         *                          is returned by getScalars(). This value is
         *                          always an int or float, and can be used for
         *                          numeric comparisons.
         *
         *        -# encoding       This is the encoded bit-array
         *                          that represents the best-guess value.
         *                          That is, if 'value' was passed to
         *                          encode(), an identical bit-array should be
         *                          returned.
         */
        public virtual List<EncoderResult> TopDownCompute(int[] encoded)
        {
            List<EncoderResult> retVals = new List<EncoderResult>();

            List<EncoderTuple> encoders = GetEncoders(this);
            int len = encoders.Count;
            for (int i = 0; i < len; i++)
            {
                int offset = (int)encoders[i].Get(2);
                IEncoder encoder = (IEncoder)encoders[i].Get(1);

                int nextOffset;
                if (i < len - 1)
                {
                    //Encoders = List<Encoder> : Encoder = EncoderTuple(name, encoder, offset)
                    nextOffset = (int)encoders[i + 1].Get(2);
                }
                else {
                    nextOffset = GetW();
                }

                int[] fieldOutput = ArrayUtils.Sub(encoded, ArrayUtils.Range(offset, nextOffset));
                List<EncoderResult> values = encoder.TopDownCompute(fieldOutput);

                retVals.AddRange(values);
            }

            return retVals;
        }

        public virtual List<double> ClosenessScores(List<double> expValues, List<double> actValues, bool fractional)
        {
            List<double> retVal = new List<double>();

            //Fallback closenss is a percentage match
            List<EncoderTuple> encoders = GetEncoders(this);
            if (encoders == null || encoders.Count < 1)
            {
                double err = Math.Abs(expValues[0] - actValues[0]);
                double closeness = -1;
                if (fractional)
                {
                    double denom = Math.Max(expValues[0], actValues[0]);
                    if (denom == 0)
                    {
                        denom = 1.0;
                    }

                    closeness = 1.0 - err / denom;
                    if (closeness < 0)
                    {
                        closeness = 0;
                    }
                }
                else {
                    closeness = err;
                }

                retVal.Add(closeness);
                return retVal;
            }

            int scalarIdx = 0;
            foreach (EncoderTuple res in GetEncoders(this))
            {
                List<double> values = res.GetEncoder().ClosenessScores(
                    expValues.SubList(scalarIdx, expValues.Count), actValues.SubList(scalarIdx, actValues.Count), fractional);

                scalarIdx += values.Count;
                retVal.AddRange(values);
            }

            return retVal;
        }

        /**
         * Returns an array containing the sum of the right
         * applied multiplications of each slice to the array
         * passed in.
         *
         * @param encoded
         * @return
         */
        public int[] RightVecProd(SparseObjectMatrix<int[]> matrix, int[] encoded)
        {
            int[] retVal = new int[matrix.GetMaxIndex() + 1];
            for (int i = 0; i < retVal.Length; i++)
            {
                int[] slice = matrix.GetObject(i);
                for (int j = 0; j < slice.Length; j++)
                {
                    retVal[i] += (slice[j] * encoded[j]);
                }
            }
            return retVal;
        }

        /**
         * Calculate width of display for bits plus blanks between fields.
         *
         * @return	width
         */
        public int GetDisplayWidth()
        {
            return GetWidth() + GetDescription().Count - 1;
        }



        /**
         * Base class for {@link Encoder} builders
         * @param <T>
         */
        public abstract class BuilderBase : IBuilder
        {
            protected int n;
            protected int w;
            protected double minVal;
            protected double maxVal;
            protected double radius;
            protected double resolution;
            protected bool periodic;
            protected bool clipInput;
            protected bool forced;
            protected string name;

            protected IEncoder encoder;

            public virtual IEncoder Build()
            {
                if (encoder == null)
                {
                    throw new InvalidOperationException("Subclass did not instantiate builder type " +
                        "before calling this method!");
                }
                encoder.SetN(n);
                encoder.SetW(w);
                encoder.SetMinVal(minVal);
                encoder.SetMaxVal(maxVal);
                encoder.SetRadius(radius);
                encoder.SetResolution(resolution);
                encoder.SetPeriodic(periodic);
                encoder.SetClipInput(clipInput);
                encoder.SetForced(forced);
                encoder.SetName(name);

                return (IEncoder)encoder;
            }

            public IBuilder N(int n)
            {
                this.n = n;
                return this;
            }
            public IBuilder W(int w)
            {
                this.w = w;
                return this;
            }
            public virtual IBuilder MinVal(double minVal)
            {
                this.minVal = minVal;
                return this;
            }
            public virtual IBuilder MaxVal(double maxVal)
            {
                this.maxVal = maxVal;
                return this;
            }
            public virtual IBuilder Radius(double radius)
            {
                this.radius = radius;
                return this;
            }
            public virtual IBuilder Resolution(double resolution)
            {
                this.resolution = resolution;
                return this;
            }
            public virtual IBuilder Periodic(bool periodic)
            {
                this.periodic = periodic;
                return this;
            }
            public virtual IBuilder ClipInput(bool clipInput)
            {
                this.clipInput = clipInput;
                return this;
            }
            public IBuilder Forced(bool forced)
            {
                this.forced = forced;
                return this;
            }
            public virtual IBuilder Name(string name)
            {
                this.name = name;
                return this;
            }
        }
    }

    public interface IBuilder
    {
        IBuilder N(int n);
        IBuilder W(int w);
        IBuilder MinVal(double minVal);
        IBuilder MaxVal(double value);
        IBuilder Radius(double value);
        IBuilder Resolution(double value);
        IBuilder Periodic(bool value);
        IBuilder ClipInput(bool value);
        IBuilder Forced(bool value);
        IBuilder Name(string value);

        IEncoder Build();
    }
}