using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    public class MultiEncoder : Encoder<object>
    {
        protected Map<int,string> indexToCategory = new Map<int, string>();

        protected List<Tuple> categoryList;

        protected int width;

        protected const char CATEGORY_DELIMITER = ';';

        /**
         * Constructs a new {@code MultiEncoder}
         */
        private MultiEncoder() { }

        /**
         * Returns a builder for building MultiEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code MultiEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        public void Init()
        {
        }


        public override void SetFieldStats(string fieldName, Map<string, double> fieldStatistics)
        {
            foreach (var t in GetEncoders(this))
            {
                string name = t.GetName();
                IEncoder encoder = t.GetEncoder();
                encoder.SetFieldStats(name, fieldStatistics);
            }
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(object input, int[] output)
        {
            foreach (var t in GetEncoders(this))
            {
                string name = t.GetName();
                IEncoder encoder = t.GetEncoder();
                int offset = t.GetOffset();

                int[] tempArray = new int[encoder.GetWidth()];

                try
                {
                    object o = GetInputValue(input, name);
                    encoder.EncodeIntoArrayUntyped(o, tempArray);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("state not allowed", e);
                }

                Array.Copy(tempArray, 0, output, offset, tempArray.Length);
            }
        }

        public int[] EncodeField(string fieldName, object value)
        {
            foreach (EncoderTuple t in GetEncoders(this))
            {
                string name = t.GetName();
                IEncoder<object> encoder = t.GetEncoder<IEncoder<object>>();

                if (name.Equals(fieldName))
                {
                    return encoder.Encode(value);
                }
            }
            return new int[] { };
        }

        public List<int[]> EncodeEachField(object input)
        {
            List<int[]> encodings = new List<int[]>();

            foreach (var t in GetEncoders(this))
            {
                string name = t.GetName();
                IEncoder<object> encoder = t.GetEncoder<IEncoder<object>>();

                encodings.Add(encoder.Encode(GetInputValue(input, name)));
            }

            return encodings;
        }

        public void AddEncoder(string name, IEncoder child)
        {
            base.AddEncoder(this, name, child, width);

            foreach (Tuple d in child.GetDescription())
            {
                Tuple dT = d;
                description.Add(new Tuple(dT.Get(0), (int)dT.Get(1) + GetWidth()));
            }
            width += child.GetWidth();
        }

        /**
         * Configures this {@code MultiEncoder} using the specified settings map.
         * 
         * @param fieldEncodings
         */
        public void AddMultipleEncoders(Map<string, Map<string, object>> fieldEncodings)
        {
            MultiEncoderAssembler.Assemble(this, fieldEncodings);
        }

        /**
         * Open up for internal Network API use.
         * Returns an {@link Encoder.Builder} which corresponds to the specified name.
         * @param encoderName
         * @return
         */
        public IBuilder GetBuilder(string encoderName)
        {
            switch (encoderName)
            {
                case "CategoryEncoder":
                    return CategoryEncoder.GetBuilder();
                case "CoordinateEncoder":
                    return CoordinateEncoder.GetBuilder();
                case "GeospatialCoordinateEncoder":
                    return GeospatialCoordinateEncoder.GetGeobuilder();
                case "LogEncoder":
                    return LogEncoder.GetBuilder();
                case "PassThroughEncoder":
                    return PassThroughEncoder<int[]>.GetBuilder();
                case "ScalarEncoder":
                    return ScalarEncoder.GetBuilder();
                case "AdaptiveScalarEncoder":
                    return AdaptiveScalarEncoder.GetAdaptiveBuilder();
                case "SparsePassThroughEncoder":
                    return SparsePassThroughEncoder.GetSparseBuilder();
                case "SDRCategoryEncoder":
                    return SDRCategoryEncoder.GetBuilder();
                case "RandomDistributedScalarEncoder":
                    return RandomDistributedScalarEncoder.GetBuilder();
                case "DateEncoder":
                    return DateEncoder.GetBuilder();
                case "DeltaEncoder":
                    return DeltaEncoder.GetDeltaBuilder();
                case "SDRPassThroughEncoder":
                    return SDRPassThroughEncoder.GetSptBuilder();
                default:
                    throw new ArgumentException("Invalid encoder: " + encoderName);
            }
        }

        public void SetValue(IBuilder builder, string param, object value)
        {
            switch (param.ToLower())
            {
                case "n":
                    builder.N((int)value);
                    break;
                case "w":
                    builder.W((int)value);
                    break;
                case "minval":
                    builder.MinVal(TypeConverter.Convert<double>(value));
                    break;
                case "maxval":
                    builder.MaxVal(TypeConverter.Convert<double>(value));
                    break;
                case "radius":
                    builder.Radius(TypeConverter.Convert<double>(value));
                    break;
                case "resolution":
                    builder.Resolution(TypeConverter.Convert<double>(value));
                    break;
                case "periodic":
                    builder.Periodic((bool)value);
                    break;
                case "clipinput":
                    builder.ClipInput((bool)value);
                    break;
                case "forced":
                    builder.Forced((bool)value);
                    break;
                case "fieldname":
                case "name":
                    builder.Name((string)value);
                    break;
                case "categorylist":
                    if (value is string)
                    {
                        string strVal = (string)value;
                        if (strVal.IndexOf(CATEGORY_DELIMITER) == -1)
                        {
                            throw new ArgumentException("Category field not delimited with '" + CATEGORY_DELIMITER + "' character.");
                        }
                        value = strVal.Split(CATEGORY_DELIMITER).ToList();
                    }
                    if (builder is CategoryEncoder.Builder)
                    {
                        ((CategoryEncoder.Builder)builder).CategoryList((List<string>)value);
                    }
                    else {
                        ((SDRCategoryEncoder.Builder)builder).CategoryList((List<string>)value);
                    }

                    break;
                default:
                    throw new ArgumentException("Invalid parameter: " + param);
            }
        }

        public override int GetWidth()
        {
            return width;
        }

        public override int GetN()
        {
            return width;
        }

        /// <summary>
        /// Returns w (width of the output signal)
        /// </summary>
        /// <returns></returns>
        public override int GetW()
        {
            return width;
        }

        public override string GetName()
        {
            if (name == null) return "";
            else return name;
        }

        public override bool IsDelta()
        {
            return false;
        }

        public override void SetLearning(bool learningEnabled)
        {
            foreach (var t in GetEncoders(this))
            {
                var encoder = t.GetEncoder();
                encoder.SetLearningEnabled(learningEnabled);
            }
        }

        public override List<S> GetBucketValues<S>(Type returnType)
        {
            return null;
        }

        /**
         * Returns a {@link EncoderBuilder} for constructing {@link MultiEncoder}s
         *
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         *
         */
        public class Builder : BuilderBase
        {

            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new MultiEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                //Call init
                ((MultiEncoder)encoder).Init();

                return (MultiEncoder)encoder;
            }
        }
    }
}