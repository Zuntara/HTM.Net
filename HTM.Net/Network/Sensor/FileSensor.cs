using System;
using System.IO;
using System.Text;
using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    /**
 * Default implementation of a {@link Sensor} for inputting data from
 * a file.
 * 
 * All {@link Sensor}s represent the bottom-most level of any given <see cref="Network"/>. 
 * Sensors are used to connect to a data source and feed data into the Network, therefore
 * there are no nodes beneath them or which precede them within the Network hierarchy, in
 * terms of data flow. In fact, a Sensor will throw an {@link Exception} if an attempt to 
 * connect another node to the input of a node containing a Sensor is made.
 *  
 * @author David Ray
 * @see SensorFactory
 * @see Sensor#create(SensorFactory, SensorParams)
 */
    public class FileSensor : Sensor<FileInfo>
    {
        protected const int HEADER_SIZE = 3;
        protected const int BATCH_SIZE = 20;
        // This is OFF until Encoders are made concurrency safe
        protected const bool DEFAULT_PARALLEL_MODE = false;

        protected BatchedCsvStream<string[]> stream;
        protected readonly SensorParams @params;

        /**
         * Protected constructor. Instances of this class should be obtained 
         * through the {@link #create(SensorParams)} factory method.
         * 
         * @param params
         */
        protected FileSensor(SensorParams @params)
        {
            this.@params = @params;

            if (!@params.HasKey("PATH"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"PATH\"");
            }

            string pathStr = (string)@params.Get("PATH");

            if (pathStr.IndexOf("!") != -1)
            {
                pathStr = pathStr.Substring("file:".Length);

                IStream<string> stream = GetZipEntryStream(pathStr);
                this.stream = BatchedCsvStream<string>.Batch(stream, BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);
            }
            else {
                FileInfo f = new FileInfo(pathStr);
                if (!f.Exists)
                {
                    throw new ArgumentException("Passed improperly formed Tuple: invalid PATH: " + @params.Get("PATH"));
                }

                try
                {
                    IStream<string> fileStream = new Stream<string>(YieldingFileReader.ReadAllLines(f.FullName, Encoding.UTF8));
                    this.stream = BatchedCsvStream<string>.Batch(fileStream, BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                }
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
        public static Sensor<FileInfo> Create(SensorParams p)
        {
            Sensor<FileInfo> fs = new FileSensor(p);
            return fs;
        }

        public override SensorParams GetSensorParams()
        {
            return @params;
        }

        /**
         * Returns the configured {@link MetaStream} if this is of
         * Type Stream, otherwise it throws an {@link UnsupportedOperationException}
         * 
         * @return  the MetaStream
         */
        public override IMetaStream GetInputStream()
        {
            return stream;
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

        /**
         * Returns a {@link Stream} from a Jar entry
         * @param path
         * @return
         */
        public static IStream<string> GetZipEntryStream(string path)
        {
            //Stream<string> retVal = null;
            //string[] parts = path.Split("\\!");
            //try
            //{

            //    ZipFile jar = new JarFile(parts[0]);
            //    InputStream inStream = jar.getInputStream(jar.getEntry(parts[1].substring(1)));
            //    BufferedReader br = new BufferedReader(new InputStreamReader(inStream));
            //    //retVal = br.lines().onClose(()-> {
            //    //    try
            //    //    {
            //    //        br.close();
            //    //        jar.close();
            //    //    }
            //    //    catch (Exception e)
            //    //    {
            //    //        Console.WriteLine(e);
            //    //    }
            //    //});
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}
            return null;
            //return retVal;
        }
        //public static void main(string[] args)
        //{
        //    string path = "/Users/metaware/git/htm.java/NetworkAPIDemo_1.0.jar!/org/numenta/nupic/datagen/rec-center-hourly.csv";
        //    Stream<string> stream = getJarEntryStream(path);
        //    stream.forEach(l->System.out.println(l));
        //}
    }
}