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
        public static void Assemble(MultiEncoder encoder, EncoderSettingsList encoderSettings)
        {
            if (encoderSettings == null || !encoderSettings.Any())
            {
                throw new ArgumentException("Cannot initialize this Sensor's MultiEncoder with a null settings");
            }

            // Sort the encoders so that they end up in a controlled order
            List<string> sortedFields = new List<string>(encoderSettings.Keys);
            sortedFields.Sort();

            foreach (string field in sortedFields)
            {
                EncoderSetting @params = encoderSettings[field];
                if (@params == null) continue;
                if (!@params.HasFieldName())
                {
                    throw new ArgumentException("Missing fieldname for encoder " + field);
                }
                string fieldName = @params.fieldName;

                if (!@params.HasEncoderType() && !@params.HasType())
                {
                    throw new ArgumentException("Missing type for encoder " + field);
                }

                string encoderType = (string)(@params.encoderType ?? @params.type);
                IBuilder builder = encoder.GetBuilder(encoderType);

                if (encoderType.EqualsIgnoreCase("SDRCategoryEncoder"))
                {
                    // Add mappings for category list
                    ConfigureCategoryBuilder(encoder, @params, builder);
                }
                else if (encoderType.EqualsIgnoreCase("DateEncoder"))
                {
                    // Extract date specific mappings out of the map so that we can
                    // pre-configure the DateEncoder with its needed directives.
                    ConfigureDateBuilder(encoder, field, encoderSettings, (DateEncoder.Builder)builder);
                }
                else if (encoderType.EqualsIgnoreCase("GeospatialCoordinateEncoder"))
                {
                    // Extract Geo specific mappings out of the map so that we can
                    // pre-configure the GeospatialCoordinateEncoder with its needed directives.
                    ConfigureGeoBuilder(encoder, field, encoderSettings, (GeospatialCoordinateEncoder.Builder)builder);
                }
                else
                {
                    foreach (string param in @params.Keys)
                    {
                        if (param.EqualsIgnoreCase("kwArgs")) // for permutation file in swarming
                        {
                            continue;   // ignore , already added
                        }
                        if (!param.EqualsIgnoreCase("fieldName") && !param.EqualsIgnoreCase("encoderType") && !param.EqualsIgnoreCase("type") &&
                            !param.EqualsIgnoreCase("fieldType") && !param.EqualsIgnoreCase("fieldEncodings"))
                        {
                            encoder.SetValue(builder, param, @params[param]);
                        }
                    }
                }

                encoder.AddEncoder(field, fieldName, builder.Build());
            }

        }

        private static void ConfigureCategoryBuilder(MultiEncoder multiEncoder,
            EncoderSetting encoderSettings, IBuilder builder)
        {
            if (encoderSettings.HasName())
                multiEncoder.SetValue(builder, "name", encoderSettings.name);
            multiEncoder.SetValue(builder, "n", encoderSettings.n.GetValueOrDefault());
            multiEncoder.SetValue(builder, "w", encoderSettings.w.GetValueOrDefault());
            multiEncoder.SetValue(builder, "forced", encoderSettings.forced.GetValueOrDefault(true));
            multiEncoder.SetValue(builder, "categoryList", encoderSettings.categoryList);
        }

        /**
         * Do special configuration for DateEncoder
         * @param encoderSettings
         */
        private static void ConfigureDateBuilder(MultiEncoder multiEncoder, string fieldName, EncoderSettingsList encoderSettings, DateEncoder.Builder b)
        {
            EncoderSetting dateEncoderSettings = GetEncoderMap(fieldName, encoderSettings, "DateEncoder");
            if (dateEncoderSettings == null)
            {
                throw new InvalidOperationException("Input requires missing DateEncoder settings mapping.");
            }

            foreach (string key in dateEncoderSettings.Keys)
            {
                if (!key.EqualsIgnoreCase("fieldName") && !key.EqualsIgnoreCase("encoderType") && !key.EqualsIgnoreCase("type") &&
                    !key.EqualsIgnoreCase("fieldType") && !key.EqualsIgnoreCase("fieldEncodings"))
                {

                    if (!key.EqualsIgnoreCase("season") && !key.EqualsIgnoreCase("dayOfWeek") &&
                        !key.EqualsIgnoreCase("weekend") && !key.EqualsIgnoreCase("holiday") &&
                        !key.EqualsIgnoreCase("timeOfDay") && !key.EqualsIgnoreCase("customDays") &&
                        !key.EqualsIgnoreCase("formatPattern") && !key.EqualsIgnoreCase("dateFormatter"))
                    {
                        multiEncoder.SetValue(b, key, dateEncoderSettings[key]);
                    }
                    else
                    {
                        if (key.EqualsIgnoreCase("formatPattern"))
                        {
                            b.FormatPattern((string)dateEncoderSettings[key]);
                        }
                        else if (key.EqualsIgnoreCase("dateFormatter"))
                        {
                            b.Formatter((DateTimeFormatInfo)dateEncoderSettings[key]);
                        }
                        else
                        {
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
        private static void SetDateFieldBits(DateEncoder.Builder b, EncoderSetting m, string key)
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
                        else
                        {
                            b.Season((int)t.Get(0));
                        }
                        break;
                    }
                case "dayOfWeek":
                case "dayofweek":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1)) > 0.0))
                        {
                            b.DayOfWeek(TypeConverter.Convert<int>(t.Get(0)), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else
                        {
                            b.DayOfWeek(TypeConverter.Convert<int>(t.Get(0)));
                        }
                        break;
                    }
                case "weekend":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.Weekend(TypeConverter.Convert<int>(t.Get(0)), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else
                        {
                            b.Weekend(TypeConverter.Convert<int>(t.Get(0)));
                        }
                        break;
                    }
                case "holiday":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.Holiday(TypeConverter.Convert<int>(t.Get(0)), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else
                        {
                            b.Holiday(TypeConverter.Convert<int>(t.Get(0)));
                        }
                        break;
                    }
                case "timeOfDay":
                case "timeofday":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.TimeOfDay(TypeConverter.Convert<int>(t.Get(0)), TypeConverter.Convert<double>(t.Get(1)));
                        }
                        else
                        {
                            b.TimeOfDay(TypeConverter.Convert<int>(t.Get(0)));
                        }
                        break;
                    }
                case "customDays":
                case "customdays":
                    {
                        if (t.Count > 1 && (TypeConverter.Convert<double>(t.Get(1))) > 0.0)
                        {
                            b.CustomDays(TypeConverter.Convert<int>(t.Get(0)), (List<string>)t.Get(1));
                        }
                        else
                        {
                            b.CustomDays(TypeConverter.Convert<int>(t.Get(0)));
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
        private static void ConfigureGeoBuilder(MultiEncoder multiEncoder, string fieldName, EncoderSettingsList encoderSettings, GeospatialCoordinateEncoder.Builder builder)
        {
            EncoderSetting geoEncoderSettings = GetEncoderMap(fieldName, encoderSettings, "GeospatialCoordinateEncoder");
            if (geoEncoderSettings == null)
            {
                throw new InvalidOperationException("Input requires missing GeospatialCoordinateEncoder settings mapping.");
            }

            foreach (string key in geoEncoderSettings.Keys)
            {
                if (!key.EqualsIgnoreCase("fieldName") && !key.EqualsIgnoreCase("encoderType") &&
                        !key.EqualsIgnoreCase("fieldType") && !key.EqualsIgnoreCase("fieldEncodings"))
                {

                    if (!key.EqualsIgnoreCase("scale") && !key.EqualsIgnoreCase("timestep"))
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
        private static void SetGeoFieldBits(GeospatialCoordinateEncoder.Builder b, EncoderSetting m, string key)
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

        /// <summary>
        /// Extract the encoder settings out of the main map so that we can do special initialization on it
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="encoderSettings"></param>
        /// <param name="encoderType"></param>
        /// <returns>Extracted settings for encoder</returns>
        private static EncoderSetting GetEncoderMap(string fieldName, EncoderSettingsList encoderSettings, string encoderType)
        {
            foreach (string key in encoderSettings.Keys.Where(k => k.Equals(fieldName, StringComparison.InvariantCultureIgnoreCase)))
            {
                string keyType = null;
                if (encoderSettings[key].HasEncoderType())
                {
                    if (encoderSettings[key].encoderType.EqualsIgnoreCase(encoderType))
                    {
                        // Remove the key from the specified map (extraction)
                        return encoderSettings[key];
                    }
                }
                if (encoderSettings[key].HasType())
                {
                    if (encoderSettings[key].type.EqualsIgnoreCase(encoderType))
                    {
                        // Remove the key from the specified map (extraction)
                        return encoderSettings[key];
                    }
                }
            }
            return null;
        }
    }
}