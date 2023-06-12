using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    [Serializable]
    public enum DateEncoderSelection
    {
        None,
        Season,
        DayOfWeek,
        TimeOfDay,
        Weekend,
        Holiday,
        CustomDays
    }

    [Serializable]
    public record BaseDateTuple(int BitsToUse);
    [Serializable]
    public record SeasonTuple(int BitsToUse, double Radius) : BaseDateTuple(BitsToUse);
    [Serializable]
    public record DayOfWeekTuple(int BitsToUse, double Radius) : BaseDateTuple(BitsToUse);
    [Serializable]
    public record TimeOfDayTuple(int BitsToUse, double Radius) : BaseDateTuple(BitsToUse);
    [Serializable]
    public record WeekendTuple(int BitsToUse, double Radius) : BaseDateTuple(BitsToUse);
    [Serializable]
    public record HolidayTuple(int BitsToUse, double Radius) : BaseDateTuple(BitsToUse);
    [Serializable]
    public record CustomDaysTuple(int BitsToUse, List<DayOfWeek> Days) : BaseDateTuple(BitsToUse);

    /// <summary>
    /// DOCUMENTATION TAKEN DIRECTLY FROM THE PYTHON VERSION:
    ///
    /// A date encoder encodes a date according to encoding parameters specified in its constructor.
    ///
    /// The input to a date encoder is a datetime.datetime object. The output is
    /// the concatenation of several sub-encodings, each of which encodes a different
    /// aspect of the date. Which sub-encodings are present, and details of those
    /// sub-encodings, are specified in the DateEncoder constructor.
    ///
    /// Each parameter describes one attribute to encode. By default, the attribute
    /// is not encoded.
    ///
    /// season (season of the year; units = day):
    /// (int) width of attribute; default radius = 91.5 days (1 season)
    /// (tuple)  season[0] = width; season[1] = radius
    ///
    /// dayOfWeek (monday = 0; units = day)
    /// (int) width of attribute; default radius = 1 day
    /// (tuple) dayOfWeek[0] = width; dayOfWeek[1] = radius
    ///
    /// weekend (boolean: 0, 1)
    /// (int) width of attribute
    ///
    /// holiday (boolean: 0, 1)
    /// (int) width of attribute
    ///
    /// timeOfday (midnight = 0; units = hour)
    /// (int) width of attribute: default radius = 4 hours
    /// (tuple) timeOfDay[0] = width; timeOfDay[1] = radius
    ///
    /// customDays TODO: what is it?
    ///
    /// forced (default True) : if True, skip checks for parameters' settings; see {@code ScalarEncoders} for details
    ///
    /// TODO Improve the document:
    ///
    /// - improve wording on unspecified attributes: "Each parameter describes one extra attribute(other than the datetime
    ///   object itself) to encode. By default, the unspecified attributes are not encoded."
    /// - refer to DateEncoder::Builder, which where these parameters are defined.
    /// - explain customDays here and at Python version
    /// </summary>
    [Serializable]
    public class DateEncoder : Encoder<DateTime>
    {

        protected int width;

        //See IBuilder for default values.

        protected SeasonTuple season;
        protected ScalarEncoder seasonEncoder;

        protected DayOfWeekTuple dayOfWeek;
        protected ScalarEncoder dayOfWeekEncoder;

        protected WeekendTuple weekend;
        protected ScalarEncoder weekendEncoder;

        protected CustomDaysTuple customDays;
        protected ScalarEncoder customDaysEncoder;

        protected HolidayTuple holiday;
        protected ScalarEncoder holidayEncoder;

        protected TimeOfDayTuple timeOfDay;
        protected ScalarEncoder timeOfDayEncoder;

        protected List<int> customDaysList = new List<int>();

        // Currently the only holiday we know about is December 25
        // holidays is a list of holidays that occur on a fixed date every year
        protected List<Tuple> holidaysList = new List<Tuple> { new Tuple(12, 25) };

        //////////////// Convenience DateTime Formats ////////////////////
        [NonSerialized]
        public static DateTimeFormatInfo FULL_DATE_TIME_ZONE = new DateTimeFormatInfo { FullDateTimePattern = "YYYY/MM/dd HH:mm:ss.SSSz" };//.forPattern("YYYY/MM/dd HH:mm:ss.SSSz");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_DATE_TIME = new DateTimeFormatInfo { FullDateTimePattern = "YYYY/MM/dd HH:mm:ss.SSS" };//.forPattern("YYYY/MM/dd HH:mm:ss.SSS");
        [NonSerialized]
        public static DateTimeFormatInfo RELAXED_DATE_TIME = new DateTimeFormatInfo { FullDateTimePattern = "YYYY/MM/dd HH:mm:ss" };//.forPattern("YYYY/MM/dd HH:mm:ss");
        [NonSerialized]
        public static DateTimeFormatInfo LOOSE_DATE_TIME = new DateTimeFormatInfo { FullDateTimePattern = "MM/dd/YY HH:mm" };//.forPattern("MM/dd/YY HH:mm");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_DATE = new DateTimeFormatInfo { FullDateTimePattern = "YYYY/MM/dd" };//.forPattern("YYYY/MM/dd");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_TIME_ZONE = new DateTimeFormatInfo { FullDateTimePattern = "HH:mm:ss.SSSz" };//.forPattern("HH:mm:ss.SSSz");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_TIME_MILLIS = new DateTimeFormatInfo { FullDateTimePattern = "HH:mm:ss.SSS" };//.forPattern("HH:mm:ss.SSS");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_TIME_SECS = new DateTimeFormatInfo { FullDateTimePattern = "HH:mm:ss" };//.forPattern("HH:mm:ss");
        [NonSerialized]
        public static DateTimeFormatInfo FULL_TIME_MINS = new DateTimeFormatInfo { FullDateTimePattern = "HH:mm" };//.forPattern("HH:mm");

        [NonSerialized]
        protected DateTimeFormatInfo customFormatter;

        /**
         * Constructs a new {@code DateEncoder}
         *
         * Package private to encourage construction using the Builder Pattern
         * but still allow inheritance.
         */
        private DateEncoder()
        {
        }

        /**
         * Returns a builder for building DateEncoder.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code IBuilder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        /**
         * Init the {@code DateEncoder} with parameters
         */
        public void Init()
        {

            width = 0;

            // Because most of the ScalarEncoder fields have less than 21 bits(recommended in
            // ScalarEncoder.checkReasonableSettings), so for now we set forced to be true to
            // override.
            // TODO figure out how to remove this
            SetForced(true);

            // Note: The order of adding encoders matters, must be in the following
            // season, dayOfWeek, weekend, customDays, holiday, timeOfDay

            if (IsValidEncoderPropertyTuple(season))
            {
                seasonEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                    .W(season.BitsToUse)
                    .Radius(season.Radius)
                    .MinVal(0)
                    .MaxVal(366)
                    .Periodic(true)
                    .Name("season")
                    .Forced(this.IsForced())
                    .Build();
                AddChildEncoder(seasonEncoder);
            }

            if (IsValidEncoderPropertyTuple(dayOfWeek))
            {
                dayOfWeekEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                        .W((int)dayOfWeek.BitsToUse)
                        .Radius((double)dayOfWeek.Radius)
                        .MinVal(0)
                        .MaxVal(7)
                        .Periodic(true)
                        .Name("day of week")
                        .Forced(this.IsForced())
                        .Build();
                AddChildEncoder(dayOfWeekEncoder);
            }

            if (IsValidEncoderPropertyTuple(weekend))
            {
                weekendEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                        .W((int)weekend.BitsToUse)
                        .Radius((double)weekend.Radius)
                        .MinVal(0)
                        .MaxVal(1)
                        .Periodic(false)
                        .Name("weekend")
                        .Forced(this.IsForced())
                        .Build();
                AddChildEncoder(weekendEncoder);
            }

            if (IsValidEncoderPropertyTuple(customDays))
            {
                List<DayOfWeek> days = (List<DayOfWeek>)customDays.Days;

                StringBuilder customDayEncoderName = new StringBuilder();

                if (days.Count == 1)
                {
                    customDayEncoderName.Append(days[0].ToString());
                }
                else {
                    foreach (DayOfWeek day in days)
                    {
                        customDayEncoderName.Append(day.ToString()).Append(" ");
                    }
                }

                customDaysEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                        .W((int)customDays.BitsToUse)
                        .Radius(1)
                        .MinVal(0)
                        .MaxVal(1)
                        .Periodic(false)
                        .Name(customDayEncoderName.ToString())
                        .Forced(this.IsForced())
                        .Build();
                //customDaysEncoder is special in naming
                AddEncoder("customdays", customDaysEncoder);
                AddCustomDays(days);
            }

            if (IsValidEncoderPropertyTuple(holiday))
            {
                holidayEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                        .W((int)holiday.BitsToUse)
                        .Radius((double)holiday.Radius)
                        .MinVal(0)
                        .MaxVal(1)
                        .Periodic(false)
                        .Name("holiday")
                        .Forced(this.IsForced())
                        .Build();
                AddChildEncoder(holidayEncoder);
            }

            if (IsValidEncoderPropertyTuple(timeOfDay))
            {
                timeOfDayEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                        .W((int)timeOfDay.BitsToUse)
                        .Radius((double)timeOfDay.Radius)
                        .MinVal(0)
                        .MaxVal(24)
                        .Periodic(true)
                        .Name("time of day")
                        .Forced(this.IsForced())
                        .Build();
                AddChildEncoder(timeOfDayEncoder);
            }

        }

        private bool IsValidEncoderPropertyTuple(BaseDateTuple encoderPropertyTuple)
        {
            return encoderPropertyTuple != null && encoderPropertyTuple.BitsToUse != 0;
        }

        // Adapted from MultiEncoder
        public void AddEncoder(string encoderName, IEncoder child)
        {
            AddEncoder(this, encoderName, child, width);

            foreach (Tuple d in child.GetDescription())
            {
                Tuple dT = d;
                description.Add(new Tuple(dT.Get(0), (int)dT.Get(1) + GetWidth()));
            }
            width += child.GetWidth();
        }

        protected void AddChildEncoder(ScalarEncoder encoder)
        {
            AddEncoder(encoder.GetName(), encoder);
        }

        protected void AddCustomDays(List<DayOfWeek> daysList)
        {
            foreach (DayOfWeek weekDay in daysList)
            {
                switch (weekDay)
                {
                    case DayOfWeek.Monday:
                        customDaysList.Add(0);
                        break;
                    case DayOfWeek.Tuesday:
                        customDaysList.Add(1);
                        break;
                    case DayOfWeek.Wednesday:
                        customDaysList.Add(2);
                        break;
                    case DayOfWeek.Thursday:
                        customDaysList.Add(3);
                        break;
                    case DayOfWeek.Friday:
                        customDaysList.Add(4);
                        break;
                    case DayOfWeek.Saturday:
                        customDaysList.Add(5);
                        break;
                    case DayOfWeek.Sunday:
                        customDaysList.Add(6);
                        break;
                    default:
                        throw new ArgumentException($"Unable to understand {weekDay} as a day of week");
                }
            }
        }

        public SeasonTuple GetSeason()
        {
            return season;
        }

        public void SetSeason(SeasonTuple season)
        {
            this.season = season;
        }

        public DayOfWeekTuple GetDayOfWeek()
        {
            return dayOfWeek;
        }

        public void SetDayOfWeek(DayOfWeekTuple dayOfWeek)
        {
            this.dayOfWeek = dayOfWeek;
        }

        public WeekendTuple GetWeekend()
        {
            return weekend;
        }

        public void SetWeekend(WeekendTuple weekend)
        {
            this.weekend = weekend;
        }

        public CustomDaysTuple GetCustomDays()
        {
            return customDays;
        }

        public void SetCustomDays(CustomDaysTuple customDays)
        {
            this.customDays = customDays;
        }

        public HolidayTuple GetHoliday()
        {
            return holiday;
        }

        public void SetHoliday(HolidayTuple holiday)
        {
            this.holiday = holiday;
        }

        public TimeOfDayTuple GetTimeOfDay()
        {
            return timeOfDay;
        }

        public void SetTimeOfDay(TimeOfDayTuple timeOfDay)
        {
            this.timeOfDay = timeOfDay;
        }

        /**
         * {@inheritDoc}
         */
        public override int GetWidth()
        {
            return width;
        }

        /**
         * {@inheritDoc}
         */
        public override int GetN()
        {
            return width;
        }

        /**
         * {@inheritDoc}
         */
        public override int GetW()
        {
            return width;
        }

        /**
         * {@inheritDoc}
         */
        public override bool IsDelta()
        {
            return false;
        }

        /**
         * Sets the custom formatter for the format field.
         * @param formatter
         */
        public void SetCustomFormat(DateTimeFormatInfo formatter)
        {
            this.customFormatter = formatter;
        }

        /**
         * Convenience method to employ the configured {@link DateTimeFormatter} 
         * to return a {@link DateTime}
         * 
         * This method assumes that a custom formatter has been configured
         * and set on this object. see {@link #setCustomFormat(DateTimeFormatter)}
         * 
         * @param dateTimeStr
         * @return
         */
        public DateTime Parse(string dateTimeStr)
        {
            DateTime date;
            if (customFormatter != null && DateTime.TryParseExact(dateTimeStr, customFormatter.FullDateTimePattern, customFormatter, DateTimeStyles.AssumeLocal, out date))
            {
                return date;
            }
            if (customFormatter != null && DateTime.TryParse(dateTimeStr, customFormatter, DateTimeStyles.None, out date))
            {
                return date;
            }
            if (DateTime.TryParse(dateTimeStr, out date))
            {
                return date;
            }
            return DateTime.Parse(dateTimeStr, DateTimeFormatInfo.InvariantInfo);
        }

        /**
         * Convenience method to parse a date string into a date 
         * before delegating to {@link #encodeIntoArray(DateTime, int[])}
         * 
         * This method assumes that a custom formatter has been configured
         * and set on this object. see {@link #setCustomFormat(DateTimeFormatter)}
         * 
         * @param dateStr       the date string to parse
         * @return  the binary encoded date 
         * @throws NullPointerException if the custom formatter is not previously 
         * configured.
         */
        public int[] ParseEncode(string dateStr)
        {
            int[] output = new int[GetN()];
            EncodeIntoArray(DateTime.Parse(dateStr, customFormatter), output);
            return output;
        }

        /**
         * {@inheritDoc}
         */
        // Adapted from MultiEncoder
        public override void EncodeIntoArray(DateTime inputData, int[] output)
        {
            if (inputData == null)
            {
                throw new ArgumentException("DateEncoder requires a valid Date object but got null");
            }

            // Get the scalar values for each sub-field
            List<double> scalars = GetScalars(inputData);

            int fieldCounter = 0;
            foreach (EncoderTuple t in GetEncoders(this))
            {
                IEncoder<double> encoder = t.GetEncoder<IEncoder<double>>();
                int offset = t.GetOffset();

                int[] tempArray = new int[encoder.GetWidth()];
                encoder.EncodeIntoArray(scalars[fieldCounter], tempArray);

                Array.Copy(tempArray, 0, output, offset, tempArray.Length);

                ++fieldCounter;
            }
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            if (o is string)
            {
                DateTime parsed = DateTime.Parse((string)o, DateTimeFormatInfo.InvariantInfo);
                EncodeIntoArray(parsed, tempArray);
            }
            else
            {
                EncodeIntoArray((DateTime)o, tempArray);
            }
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
         * @param inputData 	The input data in the format it is received from the data source
         *
         * @return A list of values, in the same format and in the same order as they
         * are returned by topDownCompute.
         *
         * @return	list of encoded values in String form
         */
        public List<string> GetEncodedValues(DateTime inputData)
        {
            List<string> values = new List<string>();

            List<string> encodedValues = GetEncodedValues(inputData);

            foreach (string v in encodedValues)
            {
                values.Add(v);
            }

            return values;
        }

        /**
         * Returns an {@link TDoubleList} containing the sub-field scalar value(s) for
         * each sub-field of the inputData. To get the associated field names for each of
         * the scalar values, call getScalarNames().
         *
         * @param inputData	the input value, in this case a date object
         * @return	a list of one input double
         */
        public List<double> GetScalars(DateTime inputData)
        {
            if (inputData == null)
            {
                throw new ArgumentException("DateEncoder requires a valid Date object but got null");
            }

            List<double> values = new List<double>();

            //Get the scalar values for each sub-field

            double timeOfDay = inputData.Hour + inputData.Minute / 60.0
                    + inputData.Second / 3600.0;

            // The day of week was 1 based, so convert to 0 based
            double dayOfWeekOffset = ((int)inputData.DayOfWeek + 6) % 7;
            double dayOfWeekPlusTimeOfDay = dayOfWeekOffset + (timeOfDay / 24.0);

            if (seasonEncoder != null)
            {
                // The day of year was 1 based, so convert to 0 based
                double dayOfYear = inputData.DayOfYear - 1;
                values.Add(dayOfYear);
            }

            if (dayOfWeekEncoder != null)
            {
                values.Add(dayOfWeekPlusTimeOfDay);
            }

            if (weekendEncoder != null)
            {
                //saturday, sunday or friday evening
                bool isWeekend = (dayOfWeekPlusTimeOfDay >= 4.75);

                int weekend = isWeekend ? 1 : 0;

                values.Add(weekend);
            }

            if (customDaysEncoder != null)
            {
                int ordinalDay = (int)dayOfWeekPlusTimeOfDay;
                bool isCustomDays = customDaysList.Contains(ordinalDay);

                int customDay = isCustomDays ? 1 : 0;

                values.Add(customDay);
            }

            if (holidayEncoder != null)
            {
                // A "continuous" binary value. = 1 on the holiday itself and smooth ramp
                //  0->1 on the day before the holiday and 1->0 on the day after the holiday.

                double holidayness = 0;

                foreach (Tuple h in holidaysList)
                {
                    //hdate is midnight on the holiday
                    DateTime hdate = new DateTime(inputData.Year, (int)h.Get(0), (int)h.Get(1), 0, 0, 0);

                    if (inputData > hdate)
                    {
                        TimeSpan diff = (inputData - hdate);// new Interval(hdate, inputData).toDuration();
                        long days = diff.Days;
                        if (days == 0)
                        {
                            //return 1 on the holiday itself
                            holidayness = 1;
                            break;
                        }
                        else if (days == 1)
                        {
                            //ramp smoothly from 1 -> 0 on the next day
                            holidayness = 1.0 - ((diff.TotalSeconds - 86400.0 * days) / 86400.0);
                            break;
                        }

                    }
                    else {
                        //TODO This is not the same as when date.isAfter(hdate), why?
                        TimeSpan diff = (inputData - hdate);// new Interval(inputData, hdate).toDuration();
                        long days = diff.Days;
                        if (days == 0)
                        {
                            //ramp smoothly from 0 -> 1 on the previous day
                            holidayness = 1.0 - ((-diff.TotalSeconds - 86400.0 * days) / 86400.0);
                            //TODO Why no break?
                        }
                    }
                }

                values.Add(holidayness);
            }

            if (timeOfDayEncoder != null)
            {
                values.Add(timeOfDay);
            }

            return values;
        }

        /**
         * {@inheritDoc}
         */
        // TODO Why can getBucketValues return null for some encoders, e.g. MultiEncoder
        public override List<S> GetBucketValues<S>(Type returnType)
        {
            return null;
        }

        /**
         * Returns an array containing the sub-field bucket indices for
         * each sub-field of the inputData. To get the associated field names for each of
         * the buckets, call getScalarNames().
         * @param  	input 	The data from the source. This is typically a object with members.
         *
         * @return 	array of bucket indices
         */
        public int[] GetBucketIndices(DateTime input)
        {
            var scalars = GetScalars(input);

            List<int> l = new List<int>();
            List<EncoderTuple> subEncoders = GetEncoders(this);
            if (encoders != null && encoders.Any())
            {
                int i = 0;
                foreach (EncoderTuple t in subEncoders)
                {
                    l.AddRange(t.GetEncoder().GetBucketIndices(scalars[i]));
                    ++i;
                }
            }
            else {
                throw new InvalidOperationException("Should be implemented in base classes that are not " +
                        "containers for other encoders");
            }
            return l.ToArray();
        }

        /**
         * {@inheritDoc}
         */
        public override void SetLearning(bool learningEnabled)
        {
            foreach (EncoderTuple t in GetEncoders(this))
            {
                IEncoder encoder = t.GetEncoder();
                encoder.SetLearningEnabled(learningEnabled);
            }
        }

        /**
         * Returns a {@link Encoder.Builder} for constructing {@link DateEncoder}s
         *
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses.
         * Hopefully! :-)
         *
         * @see ScalarEncoder.Builder#setStuff(int)
         */
        public class Builder : BuilderBase
        {

            //    Ignore leap year differences -- assume 366 days in a year
            //    Radius = 91.5 days = length of season
            //    Value is number of days since beginning of year (0 - 355)
            protected SeasonTuple season = new SeasonTuple(0, 91.5);

            // Value is day of week (floating point)
            // Radius is 1 day
            protected DayOfWeekTuple dayOfWeek = new DayOfWeekTuple(0, 1.0);

            // Binary value.
            protected WeekendTuple weekend = new WeekendTuple(0, 1.0);

            // Custom days encoder, first argument in tuple is width
            // second is either a single day of the week or a list of the days
            // you want encoded as ones.
            protected CustomDaysTuple customDays = new CustomDaysTuple(0, new List<DayOfWeek>());

            // A "continuous" binary value. = 1 on the holiday itself and smooth ramp
            //  0->1 on the day before the holiday and 1->0 on the day after the holiday.
            protected HolidayTuple holiday = new HolidayTuple(0, 1.0);

            // Value is time of day in hours
            // Radius = 4 hours, e.g. morning, afternoon, evening, early night,
            //  late night, etc.
            protected TimeOfDayTuple timeOfDay = new TimeOfDayTuple(0, 4.0);

            protected DateTimeFormatInfo customFormatter;

            public Builder() { }


            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new DateEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////
                DateEncoder e = ((DateEncoder)encoder);

                e.SetSeason(this.season);
                e.SetDayOfWeek(this.dayOfWeek);
                e.SetWeekend(this.weekend);
                e.SetHoliday(this.holiday);
                e.SetTimeOfDay(this.timeOfDay);
                e.SetCustomDays(this.customDays);
                e.SetCustomFormat(this.customFormatter);

                ((DateEncoder)encoder).Init();

                return (DateEncoder)encoder;
            }

            /**
             * Set how many bits are used to encode season
             */
            public Builder Season(int bitsToUse, double radius)
            {
                this.season = new SeasonTuple(bitsToUse, radius);
                return this;
            }

            /**
             * Set how many bits are used to encode season
             */
            public Builder Season(int bitsToUse)
            {
                return this.Season(bitsToUse, (double)this.season.Radius);
            }

            /**
             * Set how many bits are used to encode dayOfWeek
             */
            public Builder DayOfWeek(int bitsToUse, double radius)
            {
                this.dayOfWeek = new DayOfWeekTuple(bitsToUse, radius);
                return this;
            }

            /**
             * Set how many bits are used to encode dayOfWeek
             */
            public Builder DayOfWeek(int bitsToUse)
            {
                return this.DayOfWeek(bitsToUse, (double)this.dayOfWeek.Radius);
            }

            /**
             * Set how many bits are used to encode weekend
             */
            public Builder Weekend(int bitsToUse, double radius)
            {
                this.weekend = new WeekendTuple(bitsToUse, radius);
                return this;
            }

            /**
             * Set how many bits are used to encode weekend
             */
            public Builder Weekend(int bitsToUse)
            {
                return this.Weekend(bitsToUse, (double)this.weekend.Radius);
            }

            /**
             * Set how many bits are used to encode customDays
             */
            public Builder CustomDays(int bitsToUse, List<DayOfWeek> customDaysList)
            {
                this.customDays = new CustomDaysTuple(bitsToUse, customDaysList);
                return this;
            }

            /**
             * Set how many bits are used to encode customDays
             */
            public Builder CustomDays(int bitsToUse)
            {
                return this.CustomDays(bitsToUse, (List<DayOfWeek>)this.customDays.Days);
            }

            /**
             * Set how many bits are used to encode holiday
             */
            public Builder Holiday(int bitsToUse, double radius)
            {
                this.holiday = new HolidayTuple(bitsToUse, radius);
                return this;
            }

            /**
             * Set how many bits are used to encode holiday
             */
            public Builder Holiday(int bitsToUse)
            {
                return this.Holiday(bitsToUse, (double)this.holiday.Radius);
            }

            /**
             * Set how many bits are used to encode timeOfDay
             */
            public Builder TimeOfDay(int bitsToUse, double radius)
            {
                this.timeOfDay = new TimeOfDayTuple(bitsToUse, radius);
                return this;
            }

            /**
             * Set how many bits are used to encode timeOfDay
             */
            public Builder TimeOfDay(int bitsToUse)
            {
                return this.TimeOfDay(bitsToUse, (double)this.timeOfDay.Radius);
            }

            /**
             * Set the name of the encoder
             */
            public override IBuilder Name(string name)
            {
                this.name = name;
                return this;
            }

            /**
             * Creates the pattern used to parse the date field.
             * @param pattern
             * @return
             */
            public Builder FormatPattern(string pattern)
            {
                customFormatter = new DateTimeFormatInfo { FullDateTimePattern = pattern };
                return this;
            }

            /**
             * Sets the {@link DateTimeFormatte} on this builder.
             * @param formatter
             * @return
             */
            public Builder Formatter(DateTimeFormatInfo formatter)
            {
                customFormatter = formatter;
                return this;
            }
        }
    }
}