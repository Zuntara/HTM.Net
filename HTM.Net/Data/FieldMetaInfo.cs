using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Data
{
    public class FieldMetaInfo
    {
        public FieldMetaInfo(string name, FieldMetaType type, SensorFlags special)
        {
            this.name = name;
            this.type = type;
            this.special = special;
        }

        public string name { get;  }

        public FieldMetaType type { get; }
        public SensorFlags special { get;  }
    }

    public class AggregationSettings
    {
        public double years { get; set; }
        public double months { get; set; }
        public double weeks { get; set; }
        public double days { get; set; }
        public double hours { get; set; }
        public double minutes { get; set; }
        public double seconds { get; set; }
        public double milliseconds { get; set; }
        public double microseconds { get; set; }
        public Map<string, object> fields { get; set; }

        public bool AboveZero()
        {
            if (years > 0) return true;
            if (months > 0) return true;
            if (weeks > 0) return true;
            if (days > 0) return true;
            if (hours > 0) return true;
            if (minutes > 0) return true;
            if (seconds > 0) return true;
            if (milliseconds > 0) return true;
            if (microseconds > 0) return true;

            return false;
        }

        public AggregationSettings Clone()
        {
            return new AggregationSettings
            {
                years = this.years,
                months = this.months,
                weeks = this.weeks,
                days = this.days,
                hours = this.hours,
                minutes = this.minutes,
                seconds = this.seconds,
                milliseconds = this.milliseconds,
                microseconds = this.microseconds,
                fields = new Map<string, object>(this.fields ?? new Map<string, object>())
            };
        }

        public void MultiplyAllFieldsWith(int numSteps)
        {
            years *= numSteps;
            months *= numSteps;
            weeks *= numSteps;
            days *= numSteps;
            hours *= numSteps;
            minutes *= numSteps;
            seconds *= numSteps;
            milliseconds *= numSteps;
            microseconds *= numSteps;
        }
    }
}