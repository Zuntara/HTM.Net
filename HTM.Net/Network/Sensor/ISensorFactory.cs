namespace HTM.Net.Network.Sensor
{
    /**
     * <p>
     * Allows a level of indirection which makes calling {@link Sensor#create(SensorFactory, SensorParams)}
     * a lot more concise (and usable in "fluent" style): Use...
     * </p>
     * <p>
     * <pre>
     * Sensor.create(FileSensor::create, SensorParams); //Can be URISensor::create, or ObservableSensor::create
     * </pre>
     * <p>
     * 
     * @see Sensor
     * @param <T>   The resource type (i.e. {@link File}, {@link URI}, <see cref="IObservable{T}"/>)
     */
    public interface ISensorFactory<T>
    {
        /**
        * Returns the implemented type of {@link Sensor} configured
        * using the specified {@link SensorParams}
        * 
        * @param params    the {@link SensorParams} to use for configuration.
        * @return
        */
        Sensor<T> Create(SensorParams @params);
    }
}