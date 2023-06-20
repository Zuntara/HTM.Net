using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    [Serializable]
    public abstract class BaseRxLayer : BaseLayer
    {
        [NonSerialized]
        private IDisposable _subscription; //Subscription 
        [NonSerialized]
        private IObservable<IInference> _userObservable;
        [NonSerialized]
        protected List<IObserver<IInference>> _observers = new List<IObserver<IInference>>();
        [NonSerialized]
        protected ConcurrentQueue<IObserver<IInference>> _subscribers = new ConcurrentQueue<IObserver<IInference>>();
        [NonSerialized]
        protected Subject<object> Publisher = null;
        [NonSerialized]
        protected Map<Type, IObservable<ManualInput>> ObservableDispatch = new Map<Type, IObservable<ManualInput>>();// Collections.synchronizedMap(

        [NonSerialized]
        private CheckPointOperator _checkPointOp;
        [NonSerialized]
        protected List<IObserver<byte[]>> _checkPointOpObservers = new List<IObserver<byte[]>>();

        protected BaseRxLayer(string name, Network n, Parameters p)
            : base(name, n, p)
        {
        }

        protected BaseRxLayer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm,
            bool? autoCreateClassifiers, Anomaly a)
            : base(@params, e, sp, tm, autoCreateClassifiers, a)
        {
        }

        /// <summary>
        /// We cannot create the <see cref="IObservable{T}"/> sequence all at once because the
        /// first step is to transform the input type to the type the rest of the
        /// sequence uses (<see cref="IObservable{IInference}"/>). This can only happen
        /// during the actual call to <see cref="Compute{T}(T)"/> which presents the
        /// input type - so we create a map of all types of expected inputs, and then
        /// connect the sequence at execution time; being careful to only incur the
        /// cost of sequence assembly on the first call to <see cref="Compute{T}(T)"/>.
        /// After the first call, we dispose of this map and its contents.
        /// </summary>
        /// <returns>the map of input types to <see cref="Transformer"/></returns>
        protected abstract Map<Type, IObservable<ManualInput>> CreateDispatchMap();

        /// <summary>
        /// Returns an <see cref="IObservable{IInference}"/> that can be subscribed to, or otherwise
        /// operated upon by another Observable or by an Observable chain.
        /// </summary>
        /// <returns>this <see cref="ILayer"/>'s output <see cref="IObservable{IInference}"/></returns>
        public override IObservable<IInference> Observe()
        {
            // This will be called again after the Network is halted so we have to prepare
            // for rebuild of the Observer chain
            if (IsHalted())
            {
                ClearSubscriberObserverLists();
            }

            if (_userObservable == null)
            {
                _userObservable = Observable.Create<IInference>(t1 =>
                {
                    if (_observers == null)
                    {
                        _observers = new List<IObserver<IInference>>();
                    }

                    _observers.Add(t1);
                    return () => { }; // why is this?
                });
                // userObservable = Observable.Create(new Observable.OnSubscribe<IInference>() {
                //    public void call(Subscriber<? super Inference> t1)
                //    {
                //        observers.add((Observer<Inference>)t1);
                //    }
                //});
            }

            return _userObservable;
        }

        /// <summary>
        /// Called by the <see cref="ILayer"/> client to receive output <see cref="IInference"/>s from the configured algorithms.
        /// </summary>
        /// <param name="subscriber">a <see cref="IObserver{IInference}"/> to be notified as data is published.</param>
        /// <returns>A Subscription disposable</returns>
        public override IDisposable Subscribe(IObserver<IInference> subscriber)
        {
            // This will be called again after the Network is halted so we have to prepare
            // for rebuild of the Observer chain
            if (IsHalted())
            {
                ClearSubscriberObserverLists();
            }

            if (subscriber == null)
            {
                throw new InvalidOperationException("Subscriber cannot be null.");
            }
            if (_subscribers == null)
            {
                _subscribers = new ConcurrentQueue<IObserver<IInference>>();
            }
            _subscribers.Enqueue(subscriber);

            return CreateSubscription(subscriber);
        }

        /// <summary>
        /// Notify all subscribers through the delegate that stream processing has been completed or halted.
        /// </summary>
        public override void NotifyComplete()
        {
            foreach (IObserver<IInference> o in _subscribers)
            {
                o.OnCompleted();
            }
            foreach (IObserver<IInference> o in _observers)
            {
                o.OnCompleted();
            }
            Publisher.OnCompleted();
        }

        /// <summary>
        /// Called internally to propagate the specified <see cref="Exception"/> up the network hierarchy
        /// </summary>
        /// <param name="e">the exception to notify users of</param>
        public void NotifyError(Exception e)
        {
            foreach (IObserver<IInference> o in _subscribers)
            {
                o.OnError(e);
            }
            foreach (IObserver<IInference> o in _observers)
            {
                o.OnError(e);
            }
            Publisher.OnError(e);
        }

        /// <summary>
        /// Called internally to create a subscription on behalf of the specified <see cref="IObserver{IInference}"/>
        /// </summary>
        /// <param name="sub">the LayerObserver (subscriber).</param>
        /// <returns></returns>
        private IDisposable CreateSubscription(IObserver<IInference> sub)
        {
            ISubject<IInference> subject = new Subject<IInference>();
            return subject.Subscribe(sub);

            //return new Subscription()
            //{

            //    private Observer<Inference> observer = sub;

            //    @Override
            //    public void unsubscribe()
            //    {
            //        subscribers.remove(observer);
            //        if (subscribers.isEmpty())
            //        {
            //            subscription.unsubscribe();
            //        }
            //    }

            //    @Override
            //    public boolean isUnsubscribed()
            //    {
            //        return subscribers.contains(observer);
            //    }
            //};
        }

        /// <summary>
        /// Returns the <see cref="IObserver{IInference}"/>'s subscriber which delegates to all
        /// the <see cref="Layer{T}"/> subsribers.
        /// </summary>
        /// <returns></returns>
        private IObserver<IInference> GetDelegateSubscriber()
        {
            //return new Observer<IInference>()
            return Observer.Create<IInference>
                (
                    i =>
                    {
                        // OnNext
                        CurrentInference = i;
                        foreach (var o in _subscribers)
                        {
                            o.OnNext(i);
                        }
                    },
                    e =>
                    {
                        // OnError
                        foreach (var o in _subscribers)
                        {
                            o.OnError(e);
                        }
                    },
                    () =>
                    {
                        // OnCompleted
                        foreach (var o in _subscribers)
                        {
                            o.OnCompleted();
                        }
                    }
                );
        }

        /// <summary>
        /// Returns the <see cref="IObserver{IInference}"/>'s subscriber which delegates to all
        /// the <see cref="Layer{T}"/> subsribers.
        /// </summary>
        /// <returns></returns>
        protected IObserver<IInference> GetDelegateObserver()
        {
            return Observer.Create<IInference>
                (
                    i =>
                    {
                        // Next
                        CurrentInference = i;
                        foreach (var o in _observers)
                        {
                            o.OnNext(i);
                        }
                    },
                    e =>
                    {
                        // Error
                        foreach (var o in _observers)
                        {
                            o.OnError(e);
                            Console.WriteLine(e);
                        }
                    },
                    () =>
                    {
                        // Completed
                        foreach (var o in _observers)
                        {
                            o.OnCompleted();
                        }
                    }
                );
        }

        /// <summary>
        /// Connects the first observable which does the transformation of input
        /// types, to the rest of the sequence - then clears the helper map and sets
        /// it to null.
        /// </summary>
        /// <param name="sequence">The Input Transformer for the specified input type</param>
        protected void CompleteSequenceDispatch(IObservable<ManualInput> sequence)
        {
            // All subscribers and observers are notified from a single delegate.
            if (_subscribers == null) _subscribers = new ConcurrentQueue<IObserver<IInference>>();
            _subscribers.Enqueue(GetDelegateObserver());
            _subscription = sequence.Subscribe(GetDelegateSubscriber());

            // The map of input types to transformers is no longer needed.
            ObservableDispatch.Clear();
            ObservableDispatch = null;
        }

        /// <summary>
        /// Returns a flag indicating whether we've connected the first observable in
        /// the sequence (which lazily does the input type of &lt;T&gt; to
        /// <see cref="IInference"/> transformation) to the Observables connecting the rest
        /// of the algorithm components.
        /// </summary>
        /// <returns>flag indicating all observables connected. True if so, false if not</returns>
        protected bool DispatchCompleted()
        {
            return ObservableDispatch == null;
        }

        /// <summary>
        /// This method is necessary to be able to retrieve the mapped <see cref="IObservable{ManualInput}"/> types 
        /// to input types or their subclasses if any.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="t">the input type. The "expected" types are:
        /// <ul>
        ///     <li><see cref="Map{K,V}"/></li>
        ///     <li><see cref="ManualInput"/></li>
        ///     <li>String[]</li>
        ///     <li>int[]</li>
        /// </ul>
        /// or their subclasses.
        /// </param>
        /// <returns></returns>
        protected IObservable<ManualInput> ResolveObservableSequence<TInput>(TInput t)
        {
            IObservable<ManualInput> sequenceStart = null;

            if (ObservableDispatch == null)
            {
                ObservableDispatch = CreateDispatchMap();
            }

            if (ObservableDispatch != null)
            {
                if (t is ManualInput)
                {
                    sequenceStart = ObservableDispatch[typeof(ManualInput)];
                }
                else if (t is IDictionary)
                {
                    sequenceStart = ObservableDispatch[typeof(IDictionary)];
                }
                else if (typeof(TInput).IsArray)
                {
                    if (typeof(TInput) == typeof(string[]))
                    {
                        sequenceStart = ObservableDispatch[typeof(string[])];
                    }
                    else if (typeof(TInput) == typeof(int[]))
                    {
                        sequenceStart = ObservableDispatch[typeof(int[])];
                    }
                }
            }

            // Insert skip observable operator if initializing with an advanced record number
            // (i.e. Serialized Network)
            if (_recordNum > 0 && _skip != -1)
            {
                sequenceStart = sequenceStart.Skip(_recordNum + 1);

                int? skipCount;
                if ((skipCount = (int?)Params.GetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY)) != null)
                {
                    // No need to "warm up" the SpatialPooler if we're deserializing an SP
                    // that has been running... However "skipCount - recordNum" is there so 
                    // we make sure the Network has run at least long enough to satisfy the 
                    // original requested "primer delay".
                    Params.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, Math.Max(0, skipCount.GetValueOrDefault() - _recordNum));
                }
            }

            sequenceStart = sequenceStart.Where(m=> {
                if (_checkPointOpObservers.Any() && ParentNetwork != null)
                {
                    // Execute check point logic
                    DoCheckPoint();
                }

                return true;
            });

            return sequenceStart;
        }

        /**
         * Executes the check point logic, handles the return of the serialized byte array
         * by delegating the call to {@link rx.Observer#onNext(byte[])} of all the currently queued
         * Observers; then clears the list of Observers.
         */
        private void DoCheckPoint()
        {
            byte[] bytes = ParentNetwork.InternalCheckPointOp();

            if (bytes != null)
            {
                LOGGER.Debug("Layer [" + GetName() + "] checkPointed file: " +
                    Persistence.Get().GetLastCheckPointFileName());
            }
            else
            {
                LOGGER.Debug("Layer [" + GetName() + "] checkPoint   F A I L E D   at: " + (new DateTime()));
            }

            foreach (IObserver<byte[]> o in _checkPointOpObservers)
            {
                o.OnNext(bytes);
                o.OnCompleted();
            }

            _checkPointOpObservers.Clear();
        }

        /**
         * Returns an {@link rx.Observable} operator that when subscribed to, invokes an operation
         * that stores the state of this {@code Network} while keeping the Network up and running.
         * The Network will be stored at the pre-configured location (in binary form only, not JSON).
         * 
         * @param network   the {@link Network} to check point.
         * @return  the {@link CheckPointOp} operator 
         */
        public override ICheckPointOp<byte[]> GetCheckPointOperator()
        {
            if (_checkPointOp == null)
            {
                _checkPointOp = new CheckPointOperator(this);
            }
            return (ICheckPointOp<byte[]>)_checkPointOp;
        }

        /**
        * Clears the subscriber and observer lists so they can be rebuilt
        * during restart or deserialization.
        */
        private void ClearSubscriberObserverLists()
        {
            if (_observers == null) _observers = new List<IObserver<IInference>>();
            /*if (_subscribers == null)*/ _subscribers = new ConcurrentQueue<IObserver<IInference>>();
           /* _subscribers.Clear();*/
            _userObservable = null;
        }

        //////////////////////////////////////////////////////////////
        //   Inner Class Definition for CheckPointer (Observable)   //
        //////////////////////////////////////////////////////////////

        /**
         * <p>
         * Implementation of the CheckPointOp interface which serves to checkpoint
         * and register a listener at the same time. The {@link rx.Observer} will be
         * notified with the byte array of the {@link Network} being serialized.
         * </p><p>
         * The layer thread automatically tests for the list of observers to 
         * contain > 0 elements, which indicates a check point operation should
         * be executed.
         * </p>
         * 
         * @param <T>       {@link rx.Observer}'s return type
         */
        internal class CheckPointOperator : ICheckPointOp<byte[]>
        {
            [NonSerialized]
            private IObservable<byte[]> _instance;

            internal CheckPointOperator(ILayer l)
            //: this()
            {
                _instance = Observable.Create<byte[]>(o =>
                {
                    if (l.GetLayerThread() != null)
                    {
                        ((BaseRxLayer) l)._checkPointOpObservers.Add(o);
                    }
                    else
                    {
                        ((BaseRxLayer)l).DoCheckPoint();
                    }
                    return Observable.Empty<byte[]>().Subscribe();
                });
                //        this(new Observable.OnSubscribe<T>() {
                //        @SuppressWarnings({ "unchecked" })
                //            @Override public void call(Subscriber<? super T> r)
                //    {
                //        if (l.LAYER_THREAD != null)
                //        {
                //            // The layer thread automatically tests for the list of observers to 
                //            // contain > 0 elements, which indicates a check point operation should
                //            // be executed.
                //            l.checkPointOpObservers.add((Observer<byte[]>)r);
                //        }
                //        else
                //        {
                //            l.doCheckPoint();
                //        }
                //    }
                //});
            }

            /**
             * Constructs this {@code CheckPointOperator}
             * @param f     a subscriber function
             */
            //protected CheckPointOperator(rx.Observable.OnSubscribe<T> f)
            //{
            //    super(f);
            //}

            /**
             * Queues the specified {@link rx.Observer} for notification upon
             * completion of a check point operation.
             */
            public IDisposable CheckPoint(IObserver<byte[]> t)
            {
                return _instance.Subscribe(t);
            }
        }
    }
}