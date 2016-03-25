using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
 * Provides a central configuration path for {@link MultiEncoder}s, for use
 * both by the MultiEncoder itself, and the Network configuration performed
 * by the {@link HTMSensor}
 * 
 * @author cogmission
 *
 */
    public class MultiEncoderAssembler
    {

        /**
         * Uses the specified Map containing encoder settings to configure the
         * {@link MultiEncoder} passed in.
         * 
         * @param encoder           the {@link MultiEncoder} to configure.
         * @param encoderSettings   the Map containing MultiEncoder settings.
         */
        public static void Assemble(MultiEncoder encoder, Map<string, Map<string, object>> encoderSettings)
        {
            if (encoder is MultiEncoder)
            {
                if (encoderSettings == null || !encoderSettings.Any())
                {
                    throw new ArgumentException(
                        "Cannot initialize this Sensor's MultiEncoder with a null settings");
                }

                // Sort the encoders so that they end up in a controlled order
                List<string> sortedFields = new List<string>(encoderSettings.Keys);
                sortedFields.Sort();

                foreach (string field in sortedFields)
                {
                    Map<string, object> @params = encoderSettings[field];

                    if (!@params.ContainsKey("fieldName") && !@params.ContainsKey("fieldname"))
                    {
                        throw new ArgumentException("Missing fieldname for encoder " + field);
                    }
                    string fieldName = (string)(@params.Get("fieldName") ?? @params.Get("fieldname"));

                    if (!@params.ContainsKey("encoderType") && !@params.ContainsKey("type"))
                    {
                        throw new ArgumentException("Missing type for encoder " + field);
                    }

                    string encoderType = (string)(@params.Get("encoderType") ?? @params.Get("type"));
                    IBuilder builder = encoder.GetBuilder(encoderType);

                    if (encoderType.Equals("SDRCategoryEncoder"))
                    {
                        // Add mappings for category list
                        ConfigureCategoryBuilder(encoder, @params, builder);
                    }
                    else if (encoderType.Equals("DateEncoder"))
                    {
                        // Extract date specific mappings out of the map so that we can
                        // pre-configure the DateEncoder with its needed directives.
                        ConfigureDateBuilder(encoder, field, encoderSettings, (DateEncoder.Builder)builder);
                    }
                    else if (encoderType.Equals("GeospatialCoordinateEncoder"))
                    {
                        // Extract Geo specific mappings out of the map so that we can
                        // pre-configure the GeospatialCoordinateEncoder with its needed directives.
                        ConfigureGeoBuilder(encoder, field, encoderSettings, (GeospatialCoordinateEncoder.Builder)builder);
                    }
                    else {
                        foreach (string param in @params.Keys)
                        {
                            if (!param.Equals("fieldName") && !param.Equals("encoderType") && !param.Equals("type") &&
                                !param.Equals("fieldType") && !param.Equals("fieldEncodings"))
                            {

                                encoder.SetValue(builder, param, @params[param]);
                            }
                        }
                    }

                     encoder.AddEncoder(fieldName, builder.Build());
                }
            }
        }

        private static void ConfigureCategoryBuilder(MultiEncoder multiEncoder,
            Map<string, object> encoderSettings, IBuilder builder)
        {

            multiEncoder.SetValue(builder, "n", encoderSettings["n"]);
            multiEncoder.SetValue(builder, "w", encoderSettings["w"]);
            multiEncoder.SetValue(builder, "forced", encoderSettings.Get("forced", true));
            multiEncoder.SetValue(builder, "categoryList", encoderSettings.Get("categoryList"));
        }

        /**
         * Do special configuration for DateEncoder
         * @param encoderSettings
         */
        private static void ConfigureDateBuilder(MultiEncoder multiEncoder, string fieldName, Map<string, Map<string, object>> encoderSettings, DateEncoder.Builder b)
        {
            Map<string, object> dateEncoderSettings = GetEncoderMap(fieldName, encoderSettings, "DateEncoder");
            if (dateEncoderSettings == null)
            {
                throw new InvalidOperationException("Input requires missing DateEncoder settings mapping.");
            }

            foreach (string key in dateEncoderSettings.Keys)
            {
                if (!key.Equals("fieldname", StringComparison.InvariantCultureIgnoreCase) && !key.Equals("encoderType") && !key.Equals("type") &&
                    !key.Equals("fieldType") && !key.Equals("fieldEncodings"))
                {

                    if (!key.Equals("season") && !key.Equals("dayOfWeek") &&
                        !key.Equals("weekend") && !key.Equals("holiday") &&
                        !key.Equals("timeOfDay")
                            && !key.Equals("customDays") &&
                        !key.Equals("formatPattern") && !key.Equals("dateFormatter"))
                    {

                        multiEncoder.SetValue(b, key, dateEncoderSettings[key]);
                    }
                    else {
                        if (key.Equals("formatPattern"))
                        {
                            b.FormatPattern((string)dateEncoderSettings[key]);
                        }
                        else if (key.Equals("dateFormatter"))
                        {
                            b.Formatter((DateTimeFormatInfo)dateEncoderSettings[key]);
                        }
                        else {
                            SetDateFieldBits(b, dateEncoderSettings, key);
                        }
                    }
                }
            }
        }

        /**
         * Initializes the {@link DateEncoder.Builder} specified
         * @param b         the builder on which to set the mapping.
         * @param m         the map containing the values
         * @param key       the key to be set.
         */
        private static void SetDateFieldBits(DateEncoder.Builder b, Map<string, object> m, string key)
        {
            Tuple t = (Tuple)m[key];
            switch (key)
            {
                case "season":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.Season((int)t.Get(0), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else {
                            b.Season((int)t.Get(0));
                        }
                        break;
                    }
                case "dayOfWeek":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1)) > 0.0))
                        {
                            b.DayOfWeek((int)t.Get(0), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else {
                            b.DayOfWeek((int)t.Get(0));
                        }
                        break;
                    }
                case "weekend":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.Weekend((int)t.Get(0), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else {
                            b.Weekend((int)t.Get(0));
                        }
                        break;
                    }
                case "holiday":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.Holiday((int)t.Get(0), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else {
                            b.Holiday((int)t.Get(0));
                        }
                        break;
                    }
                case "timeOfDay":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.TimeOfDay((int)t.Get(0), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else {
                            b.TimeOfDay((int)t.Get(0));
                        }
                        break;
                    }
                case "customDays":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.CustomDays((int)t.Get(0), (List<string>)t.Get(1));
                        }
                        else {
                            b.CustomDays((int)t.Get(0));
                        }
                        break;
                    }

                default: break;
            }
        }

        /**
         * Specific configuration for GeospatialCoordinateEncoder builder
         * @param encoderSettings
         * @param builder
         */
        private static void ConfigureGeoBuilder(MultiEncoder multiEncoder,string fieldName, Map<string, Map<string, object>> encoderSettings, GeospatialCoordinateEncoder.Builder builder)
        {
            Map<string, object> geoEncoderSettings = GetEncoderMap(fieldName,encoderSettings, "GeospatialCoordinateEncoder");
            if (geoEncoderSettings == null)
            {
                throw new InvalidOperationException("Input requires missing GeospatialCoordinateEncoder settings mapping.");
            }

            foreach (string key in geoEncoderSettings.Keys)
            {
                if (!key.Equals("fieldName") && !key.Equals("encoderType") &&
                        !key.Equals("fieldType") && !key.Equals("fieldEncodings"))
                {

                    if (!key.Equals("scale") && !key.Equals("timestep"))
                    {
                        multiEncoder.SetValue(builder, key, geoEncoderSettings[key]);
                    }
                    else {
                        SetGeoFieldBits(builder, geoEncoderSettings, key);
                    }
                }
            }
        }

        /**
         * Initializes the {@link GeospatialCoordinateEncoder.Builder} specified
         * @param b         the builder on which to set the mapping.
         * @param m         the map containing the values
         * @param key       the key to be set.
         */
        private static void SetGeoFieldBits(GeospatialCoordinateEncoder.Builder b, Dictionary<string, object> m, string key)
        {
            object obj = m[key];
            if (obj is string)
            {
                string t = (string)m[key];
                switch (key)
                {
                    case "scale":
                        {
                            b.Scale(int.Parse(t));
                            break;
                        }
                    case "timestep":
                        {
                            b.Timestep(int.Parse(t));
                            break;
                        }
                    default: break;
                }
            }
            else {
                int t = (int)obj;
                switch (key)
                {
                    case "scale":
                        {
                            b.Scale(t);
                            break;
                        }
                    case "timestep":
                        {
                            b.Timestep(t);
                            break;
                        }
                    default: break;
                }
            }
        }

        /**
         * Extract the encoder settings out of the main map so that we can do
         * special initialization on it
         * @param encoderSettings
         * @return the settings map
         */
        private static Map<string, object> GetEncoderMap(string fieldName, Map<string, Map<string, object>> encoderSettings, string encoderType)
        {
            foreach (string key in encoderSettings.Keys.Where(k=>k.Equals(fieldName, StringComparison.InvariantCultureIgnoreCase)))
            {
                string keyType = null;
                if (encoderSettings[key].ContainsKey("encoderType"))
                {
                    if ((keyType = (string)encoderSettings[key]["encoderType"]) != null &&
                        keyType.Equals(encoderType))
                    {
                        // Remove the key from the specified map (extraction)
                        return (Map<string, object>)encoderSettings[key];
                    }
                }
                if (encoderSettings[key].ContainsKey("type"))
                {
                    if ((keyType = (string)encoderSettings[key]["type"]) != null &&
                        keyType.Equals(encoderType))
                    {
                        // Remove the key from the specified map (extraction)
                        return (Map<string, object>)encoderSettings[key];
                    }
                }
                
            }
            return null;
        }
    }
}