using System;
using System.IO;
using System.Text;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    public class CustomFileSensor : FileSensor
    {
        /**
         * Protected constructor. Instances of this class should be obtained 
         * through the {@link #create(SensorParams)} factory method.
         * 
         * @param params
         */
        protected CustomFileSensor(SensorParams @params)
            : base(@params)
        {
            string pathStr = (string)@params.Get("PATH");

            FileInfo f = new FileInfo(pathStr);
            if (!f.Exists)
            {
                throw new ArgumentException("Passed improperly formed Tuple: invalid PATH: " + @params.Get("PATH"));
            }

            try
            {
                IStream<string> fileStream = new Stream<string>(YieldingFileReader.ReadAllLines(f.FullName, Encoding.UTF8));
                this.stream = BatchedCsvStream<string>.Batch(fileStream, BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE,
                    s =>
                    {
                        return s.Split('\t');
                    });
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Factory method to allow creation through the {@link SensorFactory} in
         * the {@link Sensor#create(SensorFactory, SensorParams)} method of the 
         * parent {@link Sensor} class. This indirection allows the decoration of 
         * the returned {@code Sensor} type by wrapping it in an {@link HTMSensor}
         * (which is the current implementation but could be any wrapper).
         * 
         * @param p     the {@link SensorParams} which describe connection or source
         *              data details.
         * @return      the Sensor.
         */
        public new static Sensor<FileInfo> Create(SensorParams p)
        {
            Sensor<FileInfo> fs = new CustomFileSensor(p);
            return fs;
        }
    }
}