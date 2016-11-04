using System;
using System.Reactive.Subjects;
using HTM.Net.Model;

namespace HTM.Net.Network.Sensor
{
    /**
 * Provides a clean way to create an {@link rx.Observable} with which one can input CSV
 * Strings. It ensures that the underlying Observable's stream is set up properly with
 * a header with exactly 3 lines. These header lines describe the input in such a way 
 * as to specify input names and types together with control data for automatic Stream
 * consumption as designed by Numenta's input file format.
 * 
 * <b>NOTE:</b> The {@link Publisher.Builder#addHeader(String)} method must be called
 * before adding the publisher to the {@link Layer} (i.e. {@link Sensor}).
 * 
 * Typical usage is as follows:
 * <pre>
 * <b>In the case of manual input</b>
 * Publisher manualPublisher = Publisher.builder()
 *     .addHeader("timestamp,consumption")
 *     .addHeader("datetime,float")
 *     .addHeader("B")
 *     .build();
 * 
 * ...then add the object to a {@link SensorParams}, and {@link Sensor}
 * 
 * Sensor&lt;ObservableSensor&lt;String[]&gt;&gt; sensor = Sensor.create(
 *     ObservableSensor::create, 
 *         SensorParams.create(
 *             Keys::obs, new Object[] { "your name", manualPublisher }));
 *             
 * ...you can then add the "sensor" to a {@link Layer}
 * 
 * Layer&lt;int[]&gt; l = new Layer&lt;&gt;(n)
 *     .addSensor(sensor);
 *     
 * ...then manually input comma separated strings as such:
 * 
 * String[] entries = { 
 *     "7/2/10 0:00,21.2",
 *     "7/2/10 1:00,34.0",
 *     "7/2/10 2:00,40.4",
 *     "7/2/10 3:00,123.4",
 * };
 * 
 * manual.onNext(entries[0]);
 * manual.onNext(entries[1]);
 * manual.onNext(entries[2]);
 * manual.onNext(entries[3]);
 * </pre>
 *
 */
 [Serializable]
    public class Publisher : Persistable
 {
        private static int HEADER_SIZE = 3;

        [NonSerialized]
        private ReplaySubject<string> subject;

        private Network parentNetwork;

        public class Builder<T>
        {
            private ReplaySubject<string> _subject;

            string[] _lines = new string[3];
            int _cursor = 0;
            /**
             * Adds a header line which in the case of a multi column input 
             * is a comma separated string.
             * 
             * @param s
             * @return
             */
            public Builder<T> AddHeader(string s)
            {
                _lines[_cursor] = s;
                ++_cursor;
                return this;
            }

            /**
             * Builds and validates the structure of the expected header then
             * returns an <see cref="IObservable{T}"/> that can be used to submit info to the
             * <see cref="Network"/>
             * @return
             */
            public Publisher Build()
            {
                _subject = new ReplaySubject<string>(3);  //Subject.CreateWithSize(3);
                for (int i = 0; i < HEADER_SIZE; i++)
                {
                    if (_lines[i] == null)
                    {
                        throw new InvalidOperationException("Header not properly formed (must contain 3 lines) see Header.cs");
                    }
                    _subject.OnNext(_lines[i]);
                }

                Publisher p = new Publisher();
                p.subject = _subject;

                return p;
            }
        }

        /**
         * Returns a builder that is capable of returning a configured {@link PublishSubject} 
         * (publish-able) <see cref="IObservable{T}"/>
         * 
         * @return
         */
        public static Builder<Subject<string>> GetBuilder()
        {
            return new Builder<Subject<string>>();
        }

        /**
            * Sets the parent {@link Network} on this {@code Publisher} for use as a convenience. 
            * @param n     the Network to which the {@code Publisher} is connected.
            */
        public void SetNetwork(Network n)
        {
            this.parentNetwork = n;
        }

        /**
         * Returns the parent {@link Network} connected to this {@code Publisher} for use as a convenience. 
         * @return  this {@code Publisher}'s parent {@link Network}
         */
        public Network GetNetwork()
        {
            return parentNetwork;
        }

        /**
         * Provides the Observer with a new item to observe.
         * <p>
         * The <see cref="IObservable{T}"/> may call this method 0 or more times.
         * <p>
         * The {@code Observable} will not call this method again after it calls either {@link #onComplete} or
         * {@link #onError}.
         * 
         * @param input the item emitted by the Observable
         */
        public void OnNext(string input)
        {
            subject.OnNext(input);
        }

        /**
         * Notifies the Observer that the <see cref="IObservable{T}"/> has finished sending push-based notifications.
         * <p>
         * The <see cref="IObservable{T}"/> will not call this method if it calls {@link #onError}.
         */
        public void OnComplete()
        {
            subject.OnCompleted();
        }

        /**
         * Notifies the Observer that the <see cref="IObservable{T}"/> has experienced an error condition.
         * <p>
         * If the <see cref="IObservable{T}"/> calls this method, it will not thereafter call {@link #onNext} or
         * {@link #onComplete}.
         * 
         * @param e     the exception encountered by the Observable
         */
        public void OnError(Exception e)
        {
            subject.OnError(e);
        }

        /**
         * Subscribes to an Observable and provides an Observer that implements functions to handle the items the
         * Observable emits and any error or completion notification it issues.
         * <dl>
         *  <dt><b>Scheduler:</b></dt>
         *  <dd>{@code subscribe} does not operate by default on a particular {@link Scheduler}.</dd>
         * </dl>
         *
         * @param observer
         *             the Observer that will handle emissions and notifications from the Observable
         * @return a {@link Subscription} reference with which the {@link Observer} can stop receiving items before
         *         the Observable has completed
         * @see <a href="http://reactivex.io/documentation/operators/subscribe.html">ReactiveX operators documentation: Subscribe</a>
         */
        public IDisposable Subscribe(IObserver<string> observer)
        {
            return subject.Subscribe(observer);
        }

        /**
         * Called within package to access this {@link Publisher}'s wrapped <see cref="IObservable{T}"/>
         * @return
         */
        public IObservable<string> Observable()
        {
            return subject;
        }
    }
}