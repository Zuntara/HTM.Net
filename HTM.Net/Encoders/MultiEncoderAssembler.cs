using System;
using System.Collections.Generic;
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
    [Serializable]
    public class MultiEncoderAssembler
    {

        /**
         * Uses the specified Map containing encoder settings to configure the
         * {@link MultiEncoder} passed in.
         * 
         * @param encoder           the {@link MultiEncoder} to configure.
         * @param encoderSettings   the Map containing MultiEncoder settings.
         */
        public static MultiEncoder Assemble(MultiEncoder encoder, Map<string, Map<string, object>> encoderSettings)
        {
            if (encoderSettings == null || encoderSettings.Count == 0)
            {
                throw new ArgumentException(
                    "Cannot initialize this Sensor's MultiEncoder with a null or empty settings");
            }

            // Sort the encoders so that they end up in a controlled order
            List<string> sortedFields = new List<string>(encoderSettings.Keys);
            sortedFields.Sort();

            foreach (string field in sortedFields)
            {
                Map<string, object> @params = encoderSettings.Get(field);

                if (!@params.ContainsKey("fieldName"))
                {
                    throw new ArgumentException("Missing fieldname for encoder " + field);
                }

                string fieldName = (string)@params.Get("fieldName");

                if (!@params.ContainsKey("encoderType"))
                {
                    throw new ArgumentException("Missing type for encoder " + field);
                }

                EncoderTypes encoderType = (EncoderTypes)@params.Get("encoderType");
                IBuilder builder = encoder.GetBuilder(encoderType);

                if (encoderType == EncoderTypes.SDRCategoryEncoder)
                {
                    // Add mappings for category list
                    ConfigureCategoryBuilder(encoder,  @params, builder);
                }
                else if (encoderType == EncoderTypes.DateEncoder)
                {
                    // Extract date specific mappings out of the map so that we can
                    // pre-configure the DateEncoder with its needed directives.
                    ConfigureDateBuilder(encoder, encoderSettings, (DateEncoder.Builder)builder);
                }
                else if (encoderType == EncoderTypes.GeospatialCoordinateEncoder)
                {
                    // Extract Geo specific mappings out of the map so that we can
                    // pre-configure the GeospatialCoordinateEncoder with its needed directives.
                    ConfigureGeoBuilder(encoder, encoderSettings, (GeospatialCoordinateEncoder.Builder)builder);
                }
                else
                {
                    foreach (string param in @params.Keys)
                    {
                        if (!param.Equals("fieldName") && !param.Equals("encoderType") &&
                            !param.Equals("fieldType") && !param.Equals("fieldEncodings"))
                        {
                            encoder.SetValue(builder, param, @params.Get(param));
                        }
                    }
                }

                encoder.AddEncoder(fieldName, builder.Build());
            }

            return encoder;
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
        private static void ConfigureDateBuilder(
            MultiEncoder multiEncoder, Map<string, Map<string, object>> encoderSettings, DateEncoder.Builder b)
        {
            Map<string, object> dateEncoderSettings = GetEncoderMap(encoderSettings, EncoderTypes.DateEncoder);
            if (dateEncoderSettings == null)
            {
                throw new InvalidOperationException("Input requires missing DateEncoder settings mapping.");
            }

            foreach (string key in dateEncoderSettings.Keys)
            {
                if (!key.Equals("fieldName") && !key.Equals("encoderType") &&
                    !key.Equals("fieldType") && !key.Equals("fieldEncodings"))
                {

                    if (!key.Equals("season", StringComparison.InvariantCultureIgnoreCase)
                        && !key.Equals("dayOfWeek", StringComparison.InvariantCultureIgnoreCase) 
                        && !key.Equals("weekend", StringComparison.InvariantCultureIgnoreCase)
                        && !key.Equals("holiday", StringComparison.InvariantCultureIgnoreCase)
                        && !key.Equals("timeOfDay", StringComparison.InvariantCultureIgnoreCase)
                        && !key.Equals("customDays", StringComparison.InvariantCultureIgnoreCase) 
                        && !key.Equals("formatPattern"))
                    {
                        multiEncoder.SetValue(b, key, dateEncoderSettings[key]);
                    }
                    else
                    {
                        if (key.Equals("formatPattern"))
                        {
                            b.FormatPattern((string)dateEncoderSettings[key]);
                        }
                        else
                        {
                            SetDateFieldBits(b, dateEncoderSettings, Enum.Parse<DateEncoderSelection>(key, true));
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
        private static void SetDateFieldBits(DateEncoder.Builder b, Map<string, object> m, DateEncoderSelection key)
        {
            BaseDateTuple t = (BaseDateTuple)m[key.ToString()];
            switch (key)
            {
                case DateEncoderSelection.Season:
                    {
                        SeasonTuple seasonTuple = (SeasonTuple)t;
                        if (seasonTuple.Radius > 0.0)
                        {
                            b.Season((int)t.BitsToUse, seasonTuple.Radius);
                        }
                        else
                        {
                            b.Season((int)t.BitsToUse);
                        }
                        break;
                    }
                case DateEncoderSelection.DayOfWeek:
                    {
                        DayOfWeekTuple dayOfWeekTuple = (DayOfWeekTuple)t;
                        if (dayOfWeekTuple.Radius > 0.0)
                        {
                            b.DayOfWeek((int)t.BitsToUse, dayOfWeekTuple.Radius);
                        }
                        else
                        {
                            b.DayOfWeek((int)t.BitsToUse);
                        }
                        break;
                    }
                case DateEncoderSelection.Weekend:
                    {
                        WeekendTuple weekendTuple = (WeekendTuple)t;
                        if (weekendTuple.Radius > 0.0)
                        {
                            b.Weekend((int)t.BitsToUse, weekendTuple.Radius);
                        }
                        else
                        {
                            b.Weekend((int)t.BitsToUse);
                        }
                        break;
                    }
                case DateEncoderSelection.Holiday:
                    {
                        HolidayTuple holidayTuple = (HolidayTuple)t;
                        if (holidayTuple.Radius > 0.0)
                        {
                            b.Holiday((int)t.BitsToUse, holidayTuple.Radius);
                        }
                        else
                        {
                            b.Holiday((int)t.BitsToUse);
                        }
                        break;
                    }
                case DateEncoderSelection.TimeOfDay:
                    {
                        TimeOfDayTuple timeOfDayTuple = (TimeOfDayTuple)t;
                        if (timeOfDayTuple.Radius > 0.0)
                        {
                            b.TimeOfDay((int)t.BitsToUse, timeOfDayTuple.Radius);
                        }
                        else
                        {
                            b.TimeOfDay((int)t.BitsToUse);
                        }
                        break;
                    }
                case DateEncoderSelection.CustomDays:
                    {
                        CustomDaysTuple customDaysTuple = (CustomDaysTuple)t;
                        if (customDaysTuple.Days.Any())
                        {
                            b.CustomDays((int)t.BitsToUse, (List<DayOfWeek>)customDaysTuple.Days);
                        }
                        else
                        {
                            b.CustomDays((int)t.BitsToUse);
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
        private static void ConfigureGeoBuilder(MultiEncoder multiEncoder, Map<string, Map<string, object>> encoderSettings, GeospatialCoordinateEncoder.Builder builder)
        {
            Map<string, object> geoEncoderSettings = GetEncoderMap(encoderSettings, EncoderTypes.GeospatialCoordinateEncoder);
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
                    else
                    {
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
            else
            {
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
        private static Map<string, object> GetEncoderMap(Map<string, Map<string, object>> encoderSettings, EncoderTypes encoderType)
        {
            foreach (string key in encoderSettings.Keys)
            {
                EncoderTypes keyType;
                if ((keyType = (EncoderTypes)encoderSettings.Get(key).Get("encoderType")) != null &&
                    keyType.Equals(encoderType))
                {
                    return encoderSettings.Get(key);
                }
            }

            return null;
        }
    }
}