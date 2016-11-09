using System;
using System.Globalization;
using System.Linq;
using HTM.Net.Encoders;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net
{
    /**
     * Public values for the field data types
     * 
     */

    [Serializable]
    public enum FieldMetaType
    {
        String,
        DateTime,
        Integer,
        Float,
        Boolean,
        List,
        Coord,
        Geo,
        /// <summary>
        /// Sparse Array (i.e. 0, 2, 3)
        /// </summary>
        SparseArray,
        /// <summary>
        /// Dense Array (i.e. 1, 1, 0, 1)
        /// </summary>
        DenseArray
    }

    public class FieldMetaTypeHelper
    {
        //STRING("string"),
        //DATETIME("datetime"),
        //INTEGER("int"),
        //FLOAT("float"),
        //BOOLEAN("bool"),
        //LIST("list"),
        //COORD("coord"),
        //GEO("geo"),
        ///** Sparse Array (i.e. 0, 2, 3) */
        //SARR("sarr"),
        ///** Dense Array (i.e. 1, 1, 0, 1) */
        //DARR("darr");

        /**
         * String representation to be used when a display
         * String is required.
         */
        private string displayString;

        /** Private constructor */
        internal FieldMetaTypeHelper(string s)
        {
            this.displayString = s;
        }

        /**
         * Returns the {@link Encoder} matching this field type.
         * @return
         */
        public IEncoder NewEncoder(FieldMetaType type)
        {
            switch (type)
            {
                case FieldMetaType.List:
                case FieldMetaType.String: return SDRCategoryEncoder.GetBuilder().Build();
                case FieldMetaType.DateTime: return DateEncoder.GetBuilder().Build();
                case FieldMetaType.Boolean: return ScalarEncoder.GetBuilder().Build();
                case FieldMetaType.Coord: return CoordinateEncoder.GetBuilder().Build();
                case FieldMetaType.Geo: return GeospatialCoordinateEncoder.GetGeobuilder().Build();
                case FieldMetaType.Integer:
                case FieldMetaType.Float: return RandomDistributedScalarEncoder.GetBuilder().Build();
                case FieldMetaType.DenseArray:
                case FieldMetaType.SparseArray: return SDRPassThroughEncoder.GetSptBuilder().Build();
                default: return null;
            }
        }

        /**
         * Returns the input type for the {@code FieldMetaType} that this is...
         * @param input
         * @param enc
         * @return
         */
        public T DecodeType<T>(FieldMetaType type, string input, IEncoder enc)
        {
            switch (type)
            {
                case FieldMetaType.List:
                case FieldMetaType.String: return ChangeType<T>(input);
                case FieldMetaType.DateTime: return ChangeType<T>(((DateEncoder)enc).Parse(input));
                case FieldMetaType.Boolean: return ChangeType<T>(bool.Parse(input) == true ? 1 : 0);
                case FieldMetaType.Coord:
                case FieldMetaType.Geo:
                    {
                        string[] parts = input.Split(';'); // [\\s]*\\;[\\s]*
                        return ChangeType<T>(new Tuple(double.Parse(parts[0], NumberFormatInfo.InvariantInfo), double.Parse(parts[1], NumberFormatInfo.InvariantInfo), double.Parse(parts[2], NumberFormatInfo.InvariantInfo)));
                    }
                case FieldMetaType.Integer:
                case FieldMetaType.Float: return ChangeType<T>(double.Parse(input, NumberFormatInfo.InvariantInfo)); //return ChangeType<T>(input);
                case FieldMetaType.SparseArray:
                case FieldMetaType.DenseArray:
                    {
                        return ChangeType<T>(input.Replace("[", "").Replace("]", "") // [\\s]*\\;[\\s]*
                            .Split(',').Select(s => Convert.ToInt32(s)).ToArray());
                    }
                default: return default(T);
            }
        }

        private TType ChangeType<TType>(object input)
        {
            if (input is IConvertible)
            {
                return (TType)Convert.ChangeType(input, typeof(TType));
            }
            return (TType)input;
        }

        /**
         * Returns the display string
         * @return the display string
         */
        public string Display()
        {
            return displayString;
        }

        /**
         * Parses the specified String and returns a {@link FieldMetaType}
         * representing the passed in value.
         * 
         * @param s  the type in string form
         * @return the FieldMetaType indicated or the default: {@link FieldMetaType#FLOAT}.
         */
        public static FieldMetaType FromString(object s)
        {
            string val = s.ToString().ToLower().Trim();
            switch (val)
            {
                case "char":
                case "string":
                case "category":
                    {
                        return FieldMetaType.String;
                    }
                case "date":
                case "date time":
                case "datetime":
                case "time":
                    {
                        return FieldMetaType.DateTime;
                    }
                case "int":
                case "integer":
                case "long":
                    {
                        return FieldMetaType.Integer;
                    }
                case "double":
                case "float":
                case "number":
                case "numeral":
                case "num":
                case "scalar":
                case "floating point":
                    {
                        return FieldMetaType.Float;
                    }
                case "bool":
                case "boolean":
                    {
                        return FieldMetaType.Boolean;
                    }
                case "list":
                    {
                        return FieldMetaType.List;
                    }
                case "geo":
                    {
                        return FieldMetaType.Geo;
                    }
                case "coord":
                    {
                        return FieldMetaType.Coord;
                    }
                case "sarr":
                    {
                        return FieldMetaType.SparseArray;
                    }
                case "darr":
                    {
                        return FieldMetaType.DenseArray;
                    }
                default: return FieldMetaType.Float;
            }
        }
    }
}