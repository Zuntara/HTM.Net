using System;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ObservableImageSensor : Sensor<IObservable<ImageDefinition>>
    {
        private const int HEADER_SIZE = 3;
        private const int BATCH_SIZE = 20;
        private const bool DEFAULT_PARALLEL_MODE = false;

        private RawImageStream stream;
        private SensorParams @params;

        private ObservableImageSensor(SensorParams @params)
        {
            if (!@params.HasKey("ONSUB"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"ONSUB\"");
            }
            if (!@params.HasKey("DIMENSIONS"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"DIMENSIONS\"");
            }
            this.@params = @params;

            IObservable<ImageDefinition> obs = null;
            obs = (IObservable<ImageDefinition>)@params.Get("ONSUB");
            //this.stream = RawImageStream.Batch(
            //    new Stream<ImageDefinition>(obs), BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);

        }

        public static ObservableImageSensor Create(SensorParams p)
        {
            ObservableImageSensor sensor = new ObservableImageSensor(p);

            return sensor as ObservableImageSensor;
        }

        /**
         * Returns the {@link SensorParams} object used to configure this
         * {@code ObservableSensor}
         * 
         * @return the SensorParams
         */
        public override SensorParams GetParams()
        {
            return @params;
        }

        /**
         * Returns the configured {@link MetaStream}.
         * 
         * @return  the MetaStream
         */
        public override IMetaStream GetInputStream()
        {
            return (IMetaStream)stream;
        }

        public override MultiEncoder GetEncoder()
        {
            throw new NotImplementedException();
        }

        public override bool EndOfStream()
        {
            throw new NotImplementedException();
        }

        /**
         * Returns the values specifying meta information about the 
         * underlying stream.
         */
        public override IValueList GetMetaInfo()
        {
            return stream.GetMeta();
        }

        public override void InitEncoder(Parameters p)
        {
            throw new NotImplementedException();
        }
    }
}