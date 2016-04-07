using System;
using System.IO;
using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    /// <summary>
    /// Parent type for all <see cref="Sensor{T}"/>s. This type describes strategies
    /// used to connect to a data source on behalf of an HTM network
    /// (see <see cref="Network"/>). Subtypes of this type use <see cref="SensorParams"/>
    /// to configure the connection and location details.In this way, Sensors
    /// may be used to connect <see cref="IStream{T}"/>s of data in multiple ways; either
    /// from a file or URL, or in a functional/reactive way via an <see cref="IObservable{T}"/>
    /// or <see cref="Publisher"/> from this library.
    /// </summary>
    /// <typeparam name="T">the resource type to retrieve (i.e. <see cref="FileInfo"/>, <see cref="Uri"/>, <see cref="IObservable{T}"/></typeparam>
    public abstract class Sensor<T> : ISensor
    {
        /// <summary>
        /// <p>Creates and returns the <see cref="Sensor"/> subtype indicated by the method reference passed in for the SensorFactory <see cref="Func{SensorParams, Sensor{T}}"/>
        /// argument. <br/><br/><b>Typical usage is as follows:</b></p>
        /// <p><pre>Sensor.create(FileSensor.Create, SensorParams); //Can be URISensor, or ObservableSensor</pre></p>
        /// </summary>
        /// <param name="factoryMethod">the <see cref="Func{SensorParams, Sensor{T}}"/> or method reference. SensorFactory is a {@link FunctionalInterface}</param>
        /// <param name="t">the <see cref="SensorParams"/> which hold the configuration and data source details.</param>
        /// <returns></returns>
        public static Sensor<T> Create(Func<SensorParams, ISensor> factoryMethod, SensorParams t)
        {
            if (factoryMethod == null)
            {
                throw new InvalidOperationException("Factory cannot be null");
            }
            if (t == null)
            {
                throw new InvalidOperationException("Properties (i.e. \"SensorParams\") cannot be null");
            }

            return new HTMSensor<T>(factoryMethod(t));
        }

        /**
         * Returns an instance of {@link SensorParams} used 
         * to initialize the different types of Sensors with
         * their resource location or source object.
         * 
         * @return a {@link SensorParams} object.
         */
        public abstract SensorParams GetParams();

        /**
         * Returns the configured {@link Stream} if this is of
         * Type Stream, otherwise it throws an {@link UnsupportedOperationException}
         * 
         * @return the constructed Stream
         */
        public abstract IMetaStream GetInputStream();

        /**
         * Returns the inner Stream's meta information.
         * @return
         */
        public abstract IValueList GetMetaInfo();

        public abstract void InitEncoder(Parameters p);

        public abstract MultiEncoder GetEncoder();
        public abstract bool EndOfStream();
    }

    public interface ISensor
    {
        SensorParams GetParams();
        IMetaStream GetInputStream();
        IValueList GetMetaInfo();
        void InitEncoder(Parameters parameters);
        MultiEncoder GetEncoder();
        bool EndOfStream();
    }

    
}