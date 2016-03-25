using System;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;

namespace HTM.Net.Network
{
    /**
 * Extension to Prediction-Assisted CLA
 * 
 * PALayer is a Layer which can contain a PASpatialPooler, in which case (via Connections) 
 * it prepopulates the paSP's overlap vector with a depolarisation value derived from its 
 * TM's predictive cells. PASpatialPooler adds this vector to the overlaps calculated from the 
 * feedforward input before doing inhibition. This change pre-biases the paSP to favour columns 
 * with predictive cells. 
 * 
 * Full details at http://arxiv.org/abs/1509.08255
 *
 * @author David Ray
 * @author Fergal Byrne
 */
    public class PALayer<T> : Layer<T>
    {

        /** Set to 0.0 to default to parent behavior */
        double paDepolarize = 1.0;

        int verbosity = 0;

        /**
         * Constructs a new {@code PALayer} which resides in the specified
         * {@link Network}
         *
         * @param n     the parent {@link Network}
         */
        public PALayer(Network n)
                : base(n)
        {

        }

        /**
         * Constructs a new {@code PALayer} which resides in the specified
         * {@link Network} and uses the specified {@link Parameters}
         *
         * @param n     the parent {@link Network}
         * @param p     the parameters object from which to obtain settings
         */
        public PALayer(Network n, Parameters p)
            : base(n, p)
        {
            
        }

        /**
         * Constructs a new {@code PALayer} which resides in the specified
         * {@link Network} and uses the specified {@link Parameters}, with
         * the specified name.
         *
         * @param name  the name specified
         * @param n     the parent {@link Network}
         * @param p     the parameters object from which to obtain settings
         */
        public PALayer(string name, Network n, Parameters p)
            : base(name, n, p)
        {
        }

        /**
         * Manual method of creating a {@code Layer} and specifying its content.
         *
         * @param params                    the parameters object from which to obtain settings
         * @param e                         an (optional) encoder providing input
         * @param sp                        an (optional) SpatialPooler
         * @param tm                        an (optional) {@link TemporalMemory}
         * @param autoCreateClassifiers     flag indicating whether to create {@link CLAClassifier}s
         * @param a                         an (optional) {@link Anomaly} computer.
         */
        public PALayer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm, bool? autoCreateClassifiers, Anomaly a)
            :base(@params, e, sp, tm, autoCreateClassifiers, a)
        {
            
        }

        /**
         * Returns paDepolarize (predictive assist per cell) for this {@link PALayer}
         *
         * @return
         */
        public double GetPADepolarize()
        {
            return paDepolarize;
        }

        /**
         * Sets paDepolarize {@code PALayer}
         *
         * @param pa
         */
        public void SetPADepolarize(double pa)
        {
            paDepolarize = pa;
        }

        /**
         * Returns verbosity level
         *
         * @return
         */
        public int GetVerbosity()
        {
            return verbosity;
        }

        /**
         * Sets verbosity level (0 for silent)
         *
         * @param verbosity
         */
        public void SetVerbosity(int verbosity)
        {
            this.verbosity = verbosity;
        }
        /**
         * Returns network
         *
         * @return
         */
        public Network GetParentNetwork()
        {
            return ParentNetwork;
        }

        /**
         * Called internally to invoke the {@link SpatialPooler}
         *
         * @param input
         * @return
         */
        internal override int[] SpatialInput(int[] input)
        {
            if (input == null)
            {
                Logger.Info("Layer "+GetName()+" received null input");
            }
            else if (input.Length < 1)
            {
                Logger.Info("Layer "+GetName() + " received zero length bit vector");
                return input;
            }
            else if (input.Length > Connections.GetNumInputs())
            {
                if (verbosity > 0)
                {
                    Console.WriteLine(input);
                }
                throw new ArgumentException(string.Format("Input size {0} > SP's NumInputs {1}", input.Length, Connections.GetNumInputs()));
            }
            SpatialPooler.Compute(Connections, input, FeedForwardActiveColumns, Sensor == null || Sensor.GetMetaInfo().IsLearn(), IsLearn);

            return FeedForwardActiveColumns;
        }

        /**
         * Called internally to invoke the {@link TemporalMemory}
         *
         * @param input
         *            the current input vector
         * @param mi
         *            the current input inference container
         * @return
         */
        internal override int[] TemporalInput(int[] input, ManualInput mi)
        {
            int[] sdr = base.TemporalInput(input, mi);
            ComputeCycle cc = mi.GetComputeCycle();
            if (SpatialPooler != null && SpatialPooler is PASpatialPooler) {
                int boosted = 0;
                double[] polarization = new double[Connections.GetNumColumns()];
                foreach (Cell cell in cc.PredictiveCells())
                {
                    Column column = cell.GetColumn();
                    if (polarization[column.GetIndex()] == 0.0)
                    {
                        boosted++;
                    }
                    polarization[column.GetIndex()] += paDepolarize;

                    if (verbosity >= 2)
                    {
                        Console.WriteLine(string.Format("[{0}] = {1}", column.GetIndex(), (int)paDepolarize));
                    }
                }
                if (verbosity >= 1)
                {
                    Console.WriteLine(string.Format("boosted {0}/{1} columns", boosted, Connections.GetNumColumns()));
                }
                Connections.SetPAOverlaps(polarization);
            }
            return sdr;
        }
    }
}