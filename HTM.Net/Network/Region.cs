using System;
using System.Collections.Generic;
using System.Reactive;
using HTM.Net.Model;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Network
{
    /**
     * <p>
     * Regions are collections of {@link Layer}s, which are in turn collections
     * of algorithmic components. Regions can be connected to each other to establish
     * a hierarchy of processing. To connect one Region to another, typically one 
     * would do the following:
     * </p><p>
     * <pre>
     *      Parameters p = Parameters.getDefaultParameters(); // May be altered as needed
     *      Network n = Network.create("Test Network", p);
     *      Region region1 = n.createRegion("r1"); // would typically add Layers to the Region after this
     *      Region region2 = n.createRegion("r2"); 
     *      region1.connect(region2);
     * </pre>
     * <b>--OR--</b>
     * <pre>
     *      n.connect(region1, region2);
     * </pre>
     * <b>--OR--</b>
     * <pre>
     *      Network.lookup("r1").connect(Network.lookup("r2"));
     * </pre>    
     * 
     * @author cogmission
     *
     */
     [Serializable]
    public class Region : Persistable<Region>
    {
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(Region));

        private Network network;
        private Region upstreamRegion;
        private Region downstreamRegion;
        private Map<string, Layer<IInference>> layers = new Map<string, Layer<IInference>>();
        private IObservable<IInference> regionObservable;
        private ILayer tail;
        private ILayer head;

        /** Marker flag to indicate that assembly is finished and Region initialized */
        private bool assemblyClosed;

        /** stores tlearn setting */
        private bool isLearn = true;

        /** Temporary variables used to determine endpoints of observable chain */
        private HashSet<Layer<IInference>> sources;
        private HashSet<Layer<IInference>> sinks;

        /** Stores the overlap of algorithms state for <see cref="IInference"/> sharing determination */
        internal LayerMask flagAccumulator = 0;
        /** 
         * Indicates whether algorithms are repeated, if true then no, if false then yes
         * (for <see cref="IInference"/> sharing determination) see {@link Region#connect(Layer, Layer)} 
         * and {@link Layer#getMask()}
         */
        internal bool layersDistinct = true;

        private object input;

        private string name;

        /**
         * Constructs a new {@code Region}
         * 
         * @param name          A unique identifier for this Region (uniqueness is enforced)
         * @param network       The containing <see cref="Network"/> 
         */
        public Region(string name, Network network)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Name may not be null or empty. " +
                                                    "...not that anyone here advocates name calling!");
            }

            this.name = name;
            this.network = network;
        }

        /**
         * Sets the parent <see cref="Network"/> of this {@code Region}
         * @param network
         */
        public void SetNetwork(Network network)
        {
            this.network = network;
            foreach (Layer<IInference> l in layers.Values)
            {
                l.SetNetwork(network);
                // Set the sensor & encoder reference for global access.
                if (l.HasSensor() && network != null)
                {
                    network.SetSensor(l.GetSensor());
                    network.SetEncoder(l.GetSensor().GetEncoder());
                }
                else if (network != null && l.GetEncoder() != null)
                {
                    network.SetEncoder(l.GetEncoder());
                }
            }
        }

        /**
         * Returns a flag indicating whether this {@code Region} contain multiple
         * {@link Layer}s.
         * 
         * @return  true if so, false if not.
         */
        public bool IsMultiLayer()
        {
            return layers.Count > 1;
        }

        /**
         * Closes the Region and completes the finalization of its assembly.
         * After this call, any attempt to mutate the structure of a Region
         * will result in an {@link IllegalStateException} being thrown.
         * 
         * @return
         */
        public Region Close()
        {
            if (layers.Count < 1)
            {
                LOGGER.Warn("Closing region: " + name + " before adding contents.");
                return this;
            }

            CompleteAssembly();

            ILayer l = tail;
            do
            {
                l.Close();
            } while ((l = l.GetNext()) != null);

            return this;
        }

        /**
         * Returns a flag indicating whether this {@code Region} has had
         * its {@link #close} method called, or not.
         * 
         * @return
         */
        public bool IsClosed()
        {
            return assemblyClosed;
        }

        /**
         * Sets the learning mode.
         * @param isLearn
         */
        public void SetLearn(bool isLearn)
        {
            this.isLearn = isLearn;
            ILayer l = tail;
            while (l != null)
            {
                l.SetLearn(isLearn);
                l = l.GetNext();
            }
        }

        /**
         * Returns the learning mode setting.
         * @return
         */
        public bool IsLearn()
        {
            return isLearn;
        }

        /**
         * Used to manually input data into a <see cref="Region"/>, the other way 
         * being the call to {@link Region#start()} for a Region that contains
         * a {@link Layer} which in turn contains a {@link Sensor} <em>-OR-</em>
         * subscribing a receiving Region to this Region's output Observable.
         * 
         * @param input One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)
         */
        public void Compute<T>(T input)
        {
            if (!assemblyClosed)
            {
                Close();
            }
            this.input = input;
            tail.Compute(input);
        }

        /**
         * Returns the current input into the region. This value may change
         * after every call to {@link Region#compute(Object)}.
         * 
         * @return
         */
        public object GetInput()
        {
            return input;
        }

        /**
         * Adds the specified {@link Layer} to this {@code Region}. 
         * @param l
         * @return
         * @throws IllegalStateException if Region is already closed
         * @throws IllegalArgumentException if a Layer with the same name already exists.
         */
        public Region Add(ILayer l)
        {
            if (assemblyClosed)
            {
                throw new InvalidOperationException("Cannot add Layers when Region has already been closed.");
            }

            if (sources == null)
            {
                sources = new HashSet<Layer<IInference>>();
                sinks = new HashSet<Layer<IInference>>();
            }

            // Set the sensor reference for global access.
            if (l.HasSensor() && network != null)
            {
                network.SetSensor(l.GetSensor());
                network.SetEncoder(l.GetSensor().GetEncoder());
            }

            string layerName = name + ":" + l.GetName();
            if (layers.ContainsKey(layerName))
            {
                throw new InvalidOperationException("A Layer with the name: " + l.GetName() + " has already been added to this Region.");
            }

            l.SetName(layerName);
            layers.Add(l.GetName(), (Layer<IInference>)l);
            l.SetRegion(this);
            l.SetNetwork(network);

            return this;
        }

        /**
         * Returns the String identifier for this {@code Region}
         * @return
         */
        public string GetName()
        {
            return name;
        }

        /**
         * Returns an <see cref="IObservable{T}"/> which can be used to receive
         * <see cref="IInference"/> emissions from this {@code Region}
         * @return
         */
        public IObservable<IInference> Observe()
        {
            if (regionObservable == null && !assemblyClosed)
            {
                Close();
            }
            return regionObservable;
        }

        /**
         * Calls {@link Layer#start()} on this Region's input {@link Layer} if 
         * that layer contains a {@link Sensor}. If not, this method has no 
         * effect.
         * 
         * @return flag indicating that thread was started
         */
        public bool Start()
        {
            if (!assemblyClosed)
            {
                Close();
            }

            if (tail.HasSensor())
            {
                LOGGER.Info("Starting Region [" + GetName() + "] input Layer thread.");
                tail.Start();
                return true;
            }
            else
            {
                LOGGER.Warn("Start called on Region [" + GetName() + "] with no effect due to no Sensor present.");
            }

            return false;
        }

        /**
         * Calls {@link Layer#restart(boolean)} on this Region's input {@link Layer} if
         * that layer contains a {@link Sensor}. If not, this method has no effect. If
         * "startAtIndex" is true, the Network will start at the last saved index as 
         * obtained from the serialized "recordNum" field; if false then the Network
         * will restart from 0.
         * 
         * @param startAtIndex      flag indicating whether to start from the previous save
         *                          point or not. If true, this region's Network will start
         *                          at the previously stored index, if false then it will 
         *                          start with a recordNum of zero.
         * @return  flag indicating whether the call to restart had an effect or not.
         */
        public bool Restart(bool startAtIndex)
        {
            if (!assemblyClosed)
            {
                return Start();
            }

            if (tail.HasSensor())
            {
                LOGGER.Info("Re-Starting Region [" + GetName() + "] input Layer thread.");
                tail.Restart(startAtIndex);
                return true;
            }
            else
            {
                LOGGER.Warn("Re-Start called on Region [" + GetName() + "] with no effect due to no Sensor present.");
            }

            return false;
        }

        /**
         * Stops each {@link Layer} contained within this {@code Region}
         */
        public void Halt()
        {
            LOGGER.Debug("Stop called on Region [" + GetName() + "]");
            if (tail != null)
            {
                tail.Halt();
            }
            LOGGER.Debug("Region [" + GetName() + "] stopped.");
        }

        /**
         * Finds any {@link Layer} containing a {@link TemporalMemory} 
         * and resets them.
         */
        public void Reset()
        {
            foreach (var l in layers.Values)
            {
                if (l.HasTemporalMemory())
                {
                    l.Reset();
                }
            }
        }

        /**
         * Resets the recordNum in all {@link Layer}s.
         */
        public void ResetRecordNum()
        {
            foreach (var l in layers.Values)
            {
                l.ResetRecordNum();
            }
        }

        /**
         * Connects the output of the specified {@code Region} to the 
         * input of this Region
         * 
         * @param inputRegion   the Region who's emissions will be observed by 
         *                      this Region.
         * @return
         */
        public Region Connect(Region inputRegion)
        {
            ManualInput localInf = new ManualInput();
            inputRegion.Observe().Subscribe(output =>
            {
                localInf.SetSdr(output.GetSdr()).SetRecordNum(output.GetRecordNum()).SetClassifierInput(output.GetClassifierInput()).SetLayerInput(output.GetSdr());
                if (output.GetSdr().Length > 0)
                {
                    ((Layer<IInference>)tail).Compute(localInf);
                }
            }, Console.WriteLine, () =>
            {
                tail.NotifyComplete();
            });

            //inputRegion.Observe().Subscribe(new Observer<IInference>() {
            //ManualInput localInf = new ManualInput();

            //@Override public void onCompleted()
            //{
            //tail.notifyComplete();
            //}
            //@Override public void onError(Throwable e) { e.printStackTrace(); }
            //@SuppressWarnings("unchecked")
            //    @Override public void onNext(IInference i)
            //{
            //localInf.sdr(i.getSDR()).recordNum(i.getRecordNum()).classifierInput(i.getClassifierInput()).layerInput(i.getSDR());
            //if (i.getSDR().length > 0)
            //{
            //    ((Layer<IInference>)tail).compute(localInf);
            //}
            //}
            //});
            // Set the upstream region
            this.upstreamRegion = inputRegion;
            inputRegion.downstreamRegion = this;

            return this;
        }

        /**
         * Returns this {@code Region}'s upstream region,
         * if it exists.
         * 
         * @return
         */
        public Region GetUpstreamRegion()
        {
            return upstreamRegion;
        }

        /**
         * Returns the {@code Region} that receives this Region's
         * output.
         * 
         * @return
         */
        public Region GetDownstreamRegion()
        {
            return downstreamRegion;
        }

        /**
         * Returns the top-most (last in execution order from
         * bottom to top) {@link Layer} in this {@code Region}
         * 
         * @return
         */
        public ILayer GetHead()
        {
            return this.head;
        }

        /**
         * Returns the bottom-most (first in execution order from
         * bottom to top) {@link Layer} in this {@code Region}
         * 
         * @return
         */
        public ILayer GetTail()
        {
            return this.tail;
        }

        /**
         * Connects two layers to each other in a unidirectional fashion 
         * with "toLayerName" representing the receiver or "sink" and "fromLayerName"
         * representing the sender or "source".
         * 
         * This method also forwards shared constructs up the connection chain
         * such as any {@link Encoder} which may exist, and the <see cref="IInference"/> result
         * container which is shared among layers.
         * 
         * @param toLayerName       the name of the sink layer
         * @param fromLayerName     the name of the source layer
         * @return
         * @throws IllegalStateException if Region is already closed
         */
        public Region Connect(string toLayerName, string fromLayerName)
        {
            if (assemblyClosed)
            {
                throw new InvalidOperationException("Cannot connect Layers when Region has already been closed.");
            }

            Layer<IInference> @in = (Layer<IInference>)Lookup(toLayerName);
            Layer<IInference> @out = (Layer<IInference>)Lookup(fromLayerName);
            if (@in == null)
            {
                throw new InvalidOperationException("Could not lookup (to) Layer with name: " + toLayerName);
            }
            if (@out == null)
            {
                throw new InvalidOperationException("Could not lookup (from) Layer with name: " + fromLayerName);
            }

            // Set source's pointer to its next Layer --> (sink : going upward).
            @out.Next(@in);
            // Set the sink's pointer to its previous Layer --> (source : going downward)
            @in.Previous(@out);
            // Connect out to in
            Connect(@in, @out);

            return this;
        }

        /**
         * Does a straight associative lookup by first creating a composite
         * key containing this {@code Region}'s name concatenated with the specified
         * {@link Layer}'s name, and returning the result.
         * 
         * @param layerName
         * @return
         */
        public ILayer Lookup(string layerName)
        {
            if (layerName.IndexOf(":", StringComparison.Ordinal) != -1)
            {
                return layers[layerName];
            }
            string key = name + ":" + layerName;
            if (layers.ContainsKey(key))
            {
                return layers[key];
            }
            return null;
        }

        /**
         * Called by {@link #start()}, {@link #observe()} and {@link #connect(Region)}
         * to finalize the internal chain of {@link Layer}s contained by this {@code Region}.
         * This method assigns the head and tail Layers and composes the <see cref="IObservable{T}"/>
         * which offers this Region's emissions to any upstream <see cref="Region"/>s.
         */
        private void CompleteAssembly()
        {
            if (!assemblyClosed)
            {
                if (layers.Count == 0) return;

                if (layers.Count == 1)
                {
                    var enumerator = layers.Values.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        head = tail = enumerator.Current;
                    }
                    else
                    {
                        throw new InvalidOperationException("No layers to iterate through?");
                    }
                }

                if (tail == null)
                {
                    HashSet<Layer<IInference>> temp = new HashSet<Layer<IInference>>(sources);
                    temp.ExceptWith(sinks);
                    if (temp.Count != 1)
                    {
                        throw new InvalidOperationException("Detected misconfigured Region too many or too few sinks.");
                    }
                    var enumerator = temp.GetEnumerator();
                    enumerator.MoveNext();
                    tail = enumerator.Current;
                }

                if (head == null)
                {
                    HashSet<Layer<IInference>> temp = new HashSet<Layer<IInference>>(sinks);
                    temp.ExceptWith(sources);
                    if (temp.Count != 1)
                    {
                        throw new InvalidOperationException("Detected misconfigured Region too many or too few sources.");
                    }
                    var enumerator = temp.GetEnumerator();
                    enumerator.MoveNext();
                    head = enumerator.Current;
                }

                regionObservable = head.Observe();

                assemblyClosed = true;
            }
        }

        /**
         * Called internally to "connect" two {@link Layer} <see cref="IObservable{T}"/>s
         * taking care of other connection details such as passing the inference
         * up the chain and any possible encoder.
         * 
         * @param in         the sink end of the connection between two layers
         * @param out        the source end of the connection between two layers
         * @throws IllegalStateException if Region is already closed
         */
        private void Connect<I, O>(I @in, O @out) // <I extends Layer<IInference>, O extends Layer<IInference>> 
            where I : Layer<IInference>
            where O : Layer<IInference>
        {
            if (assemblyClosed)
            {
                throw new InvalidOperationException("Cannot add Layers when Region has already been closed.");
            }

            HashSet<Layer<IInference>> all = new HashSet<Layer<IInference>>(sources);
            foreach (var sink in sinks)
            {
                all.Add(sink);
            }
            //all.addAll(sinks);
            LayerMask inMask = @in.GetMask();
            LayerMask outMask = @out.GetMask();
            if (!all.Contains(@out))
            {
                layersDistinct = (int)(flagAccumulator & outMask) < 1;
                flagAccumulator |= outMask;
            }
            if (!all.Contains(@in))
            {
                layersDistinct = (int)(flagAccumulator & inMask) < 1;
                flagAccumulator |= inMask;
            }

            sources.Add(@out);
            sinks.Add(@in);

            ManualInput localInf = new ManualInput();

            @out.Subscribe(Observer.Create<IInference>(i =>
            {
                if (layersDistinct)
                {
                    @in.Compute(i);
                }
                else
                {
                    localInf.SetSdr(i.GetSdr()).SetRecordNum(i.GetRecordNum()).SetLayerInput(i.GetSdr());
                    @in.Compute(localInf);
                }
            }, Console.WriteLine, () =>
            {
                @in.NotifyComplete();
            }));

            //    @out.subscribe(new Subscriber<IInference>() {
            //        ManualInput localInf = new ManualInput();

            //        @Override public void onCompleted() { in.notifyComplete(); }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override public void onNext(IInference i)
            //    {
            //        if (layersDistinct)
            //        {
            //                in.compute(i);
            //        }
            //        else {
            //            localInf.sdr(i.getSDR()).recordNum(i.getRecordNum()).layerInput(i.getSDR());
            //                in.compute(localInf);
            //        }
            //    }
            //});
        }

    }
}