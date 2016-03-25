using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    public abstract class BaseRxLayer : BaseLayer
    {
        private IDisposable _subscription; //Subscription 
        private IObservable<IInference> _userObservable;
        private readonly List<IObserver<IInference>> _observers = new List<IObserver<IInference>>();
        private readonly ConcurrentQueue<IObserver<IInference>> _subscribers = new ConcurrentQueue<IObserver<IInference>>();
        protected Subject<object> Publisher = null;
        protected Map<Type, IObservable<ManualInput>> ObservableDispatch = new Map<Type, IObservable<ManualInput>>();// Collections.synchronizedMap(

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
            if (_userObservable == null)
            {
                _userObservable = Observable.Create<IInference>(t1 =>
                {
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
        public IDisposable Subscribe(IObserver<IInference> subscriber)
        {
            if (subscriber == null)
            {
                throw new InvalidOperationException("Subscriber cannot be null.");
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
        private IObserver<IInference> GetDelegateObserver()
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

            return sequenceStart;
        }
    }
}