using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;

namespace HTM.Net.Network
{
    /**
     * <p>
     * Ascribes to the {@link Supplier} interface to provide a {@link Publisher} upon request.
     * This supplier is expressed as a lambda which acts as a "lazy factory" to create Publishers
     * and set references on the Network when {@link PublisherSupplier#get()} is called.
     * </p><p>
     * The old way of creating Publishers has now changed when the {@link PAPI} is used.
     * Instead, a {@link PublisherSupplier} is used, which ascribes to the {@link Supplier} 
     * interface which acts like a "lazy factory" and kicks off other needed settings when a new
     * Publisher is created... 
     * </p><p>
     * The basic usage is:
     * </p><p>
     * <pre>
     *  Supplier<Publisher> supplier = PublisherSupplier.builder()
     *      .addHeader("dayOfWeek, timestamp")
     *      .addHeader("number, date")
     *      .addHeader("B, T")
     *      .build();
     *  
     *  // Since Suppliers are always added to Sensors we do...
     *  
     *  Sensor<ObservableSensor<String[]>> sensor = Sensor.create(
     *      ObservableSensor::create, SensorParams.create(
     *          Keys::obs, new Object[] {"name", supplier})); // <-- supplier created above
     *          
     *  <b>--- OR (all inline in "Fluent" fashion) ---</b>
     *  
     *  Sensor<ObservableSensor<String[]>> sensor = Sensor.create(
     *      ObservableSensor::create, SensorParams.create(Keys::obs, new Object[] {"name", 
     *          PublisherSupplier.builder()                                                // <-- supplier created fluently
     *              .addHeader("dayOfWeek, timestamp")
     *              .addHeader("number")
     *              .addHeader("B, T").build() }));
     * </pre>
     * @author cogmission
     *
     */
    [Serializable]
    public class PublisherSupplier : Persistable//, Supplier<Publisher>
    {
        private static readonly long serialVersionUID = 1L;

        /// <summary>
        /// The parent network this supplier services
        /// </summary>
        private Network network;

        /// <summary>
        /// 3 Header lines used during csv parsing of input and type determination
        /// </summary>
        private List<string> headers = new List<string>();

        /// <summary>
        /// last created Publisher instance
        /// </summary>
        [NonSerialized]
        private volatile Publisher suppliedInstance;

        /**
         * Package private constructor for use by the {@link Network} class only.
         * @param network   the network for which a Publisher is to be supplied.
         */
        private PublisherSupplier(Network network)
        {
            this.network = network;
        }

        /**
         * <p>
         * Implementation of the {@link Supplier} interface that returns a newly created 
         * {@link Publisher}. 
         * </p><p>
         * The {@link Publisher.Builder} is passed a {@link Consumer} in its constructor which 
         * basically triggers a call to {@link Network#setPublisher(Publisher)} with the newly
         * created {@link Publisher} - which must be available so that users can get a reference 
         * to the new Publisher that is created when {@link Network#load()} is called.
         * 
         * @return a new Publisher
         */
        public Publisher Get()
        {
            if (suppliedInstance == null)
            {

                Publisher.Builder<Subject<string>> builder =
                    Publisher.GetBuilder(p => network?.SetPublisher(p));

                headers.ForEach(line => builder.AddHeader(line));

                suppliedInstance = builder.Build();
                suppliedInstance.SetNetwork(network);
            }

            return suppliedInstance;
        }

        public void ClearSuppliedInstance()
        {
            this.suppliedInstance = null;
        }

        /**
         * Sets the {@link Network} for which this supplier supplies a publisher.
         * @param n the Network acting as consumer.
         */
        public void SetNetwork(Network n)
        {
            this.network = n;
            this.suppliedInstance.SetNetwork(n);
        }

        /**
         * <p>
         * Returns a {@link PublisherSupplier.Builder} which is used to build up 
         * a {@link Header} and then create a supplier. An example is:
         * </p>
         * <pre>
         *  Supplier<Publisher> supplier = PublisherSupplier.builder()
         *      .addHeader("dayOfWeek, timestamp")
         *      .addHeader("number, date")
         *      .addHeader("B, T")
         *      .build();
         *  
         *  // Since Suppliers are always added to Sensors we do...
         *  
         *  Sensor<ObservableSensor<String[]>> sensor = Sensor.create(
         *      ObservableSensor::create, SensorParams.create(
         *          Keys::obs, new Object[] {"name", supplier})); // <-- supplier created above
         *          
         *  <b>--- OR (all inline in "Fluent" fashion) ---</b>
         *  
         *  Sensor<ObservableSensor<String[]>> sensor = Sensor.create(
         *      ObservableSensor::create, SensorParams.create(Keys::obs, new Object[] {"name", 
         *          PublisherSupplier.builder()                                                // <-- supplier created fluently
         *              .addHeader("dayOfWeek, timestamp")
         *              .addHeader("number")
         *              .addHeader("B, T").build() }));
         * </pre>
         * 
         * @return  a builder with which to create a {@link PublisherSupplier}
         */
        public static Builder GetBuilder()
        {
            return new Builder();
        }


        /**
         * Follows "builder pattern" for building new {@link PublisherSupplier}s.
         * 
         * @see Supplier
         * @see PublisherSupplier
         * @see Publisher
         * @see Publisher.Builder
         */
        public class Builder
        {
            private Network network;

            private List<string> headers = new List<string>();


            /**
             * Constructs a new {@code PublisherSupplier.Builder}
             */
            internal Builder()
            {

            }

            /**
             * Adds a header line which in the case of a multi column input 
             * is a comma separated string.
             * 
             * @param s     string representing one line of a header
             * @return  this Builder
             */
            public Builder AddHeader(String headerLine)
            {
                headers.Add(headerLine);
                return this;
            }

            /**
             * Signals the builder to instantiate and return the new
             * {@code PublisherSupplier}
             * 
             * @return  a new PublisherSupplier
             */
            public PublisherSupplier Build()
            {
                PublisherSupplier retVal = new PublisherSupplier(network);
                retVal.headers = new List<string>(this.headers);
                return retVal;
            }
        }

    }
}
