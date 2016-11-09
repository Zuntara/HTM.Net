using System;
using System.Collections;
using System.Collections.Generic;
using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    /**
     * Wraps an {@link rx.Observable} or {@link Publisher} event emitter which can be used to input CSV
     * strings into a given {@link Layer} of a <see cref="Region"/> and <see cref="Network"/> by
     * either manually calling {@link Publisher#onNext(String)} or by connecting an Observable
     * to an existing chain of Observables (operations/transformations) which eventually yield an appropriate CSV
     * String input.
     * @param <T>
     */
    [Serializable]
    public class ObservableSensor<T> : Sensor<IObservable<T>>
    {
        private const int HEADER_SIZE = 3;
        private const int BATCH_SIZE = 20;
        private const bool DEFAULT_PARALLEL_MODE = false;

        [NonSerialized]
        private BatchedCsvStream<string[]> stream;
        private SensorParams @params;


        /**
         * Creates a new {@code ObservableSensor} using the specified 
         * {@link SensorParams}
         * 
         * @param params
         */
        internal ObservableSensor(SensorParams @params)
        {
            if (!@params.HasKey("ONSUB"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"ONSUB\"");
            }

            this.@params = @params;

            IObservable<string> obs = null;
            object publisher = @params.Get("ONSUB");
            if (publisher is Publisher)
            {
                obs = ((Publisher)publisher).Observable();
            }
            else if (publisher is PublisherSupplier) {
                obs = ((PublisherSupplier)publisher).Get().Observable();
            }
            else
            {
                obs = (IObservable<string>)@params.Get("ONSUB");
            }
            //IEnumerator<string> observerator = obs.GetEnumerator();

            //IEnumerator<string> iterator = new CustomIterator<string>(
            //    () =>
            //    {
            //        bool moved = observerator.MoveNext();
            //        return new System.Tuple<bool, string>(moved, observerator.Current);
            //    });

            //Iterator<String> iterator = new Iterator<String>() {
            //    public boolean hasNext() { return observerator.hasNext(); }
            //    public String next()
            //    {
            //        return observerator.next();
            //    }
            //};

            //int characteristics = Spliterator.SORTED | Spliterator.ORDERED;
            //Spliterator<string> spliterator = Spliterators.spliteratorUnknownSize(iterator, characteristics);

            this.stream = BatchedCsvStream<string>.Batch(
                new Stream<string>(obs), BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);
        }

        public static ObservableSensor<T> Create(SensorParams p)
        {
            ObservableSensor<string[]> sensor = new ObservableSensor<string[]>(p);

            return sensor as ObservableSensor<T>;
        }

        /**
         * Returns the {@link SensorParams} object used to configure this
         * {@code ObservableSensor}
         * 
         * @return the SensorParams
         */
        public override SensorParams GetSensorParams()
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

    public class CustomIterator<T> : IEnumerator<T>
    {
        private Func<System.Tuple<bool, string>> _moveNext;
        public CustomIterator(Func<System.Tuple<bool, string>> moveNext)
        {
            _moveNext = moveNext;
        }

        #region Implementation of IDisposable

        public void Dispose()
        {

        }

        #endregion

        #region Implementation of IEnumerator

        public bool MoveNext()
        {
            var tuple = _moveNext();
            //Current = tuple.Item2;
            return tuple.Item1;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public T Current { get; internal set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        #endregion
    }
}