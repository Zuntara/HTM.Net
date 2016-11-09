using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Monitor
{
    public abstract class MonitorMixinBase
    {
        public abstract Connections getConnections();
        public abstract IDictionary getDataMap();
        public abstract Map<string, ITrace> getTraceMap();
        public abstract string mmGetName();
        public abstract void mmClearHistory();
        public abstract List<ITrace> mmGetDefaultTraces(int verbosity);
        //public abstract List<Metric> mmGetDefaultMetrics(int verbosity);

        public virtual string mmPrettyPrintTraces(List<ITrace> traces, BoolsTrace breakOnResets)
        {
            throw new NotImplementedException();
        }

        public virtual string mmPrettyPrintMetrics(List<Metric<int>> metrics, int sigFigs)
        {
            string hashes = "";
            for (int y = 0; y < Math.Max(2, sigFigs); y++)
            {
                hashes += "#";
            }

            //DecimalFormat df = new DecimalFormat("0." + hashes);

            string[] header = new string[] { "Metric", "mean", "standard deviation", "min", "max", "sum" };

            string[][] data = new string[metrics.Count][];
            int i = 0;
            foreach (var metric in metrics)
            {
                data[i] = new string[header.Length];

                for (int j = 0; j < header.Length; j++)
                {
                    if (j == 0)
                    {
                        data[i][j] = metric.prettyPrintTitle();
                    }
                    else {
                        double[] stats = metric.getStats(sigFigs);
                        data[i][j] = ((int) stats[j - 1]) == stats[j - 1]
                            ? stats[j - 1].ToString("N")
                            : stats[j - 1].ToString("N");
                        //data[i][j] = ((int)stats[j - 1]) == stats[j - 1] ? 
                        //    df.format(stats[j - 1]) + ".0" : df.format(stats[j - 1]);
                    }
                }
                i++;
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(string.Join("\t",header));

            foreach (var dataLine in data)
            {
                sb.AppendLine(string.Join("\t", dataLine));
            }

            //String retVal = AsciiTableInstance.get().getTable(header, data, AsciiTable.ALIGN_CENTER);
            //return retVal;
            return sb.ToString();
        }
    }

    public abstract class TemporalMemoryMonitorMixin : MonitorMixinBase
    {
        /// <summary>
        /// Returns the ComputeDecorator mixin target
        /// </summary>
        /// <returns></returns>
        public abstract IComputeDecorator getMonitor();
        /// <summary>
        /// Returns the resetActive flag
        /// </summary>
        /// <returns></returns>
        public abstract bool resetActive();
        /// <summary>
        /// Sets the resetActive flag
        /// </summary>
        /// <param name="b"></param>
        public abstract void setResetActive(bool b);
        /// <summary>
        /// Returns the flag indicating whether the current traces are stale and need to be recomputed, or not.
        /// </summary>
        /// <returns></returns>
        public abstract bool transitionTracesStale();
        /// <summary>
        ///  Sets the flag indicating whether the current traces are stale and need to be recomputed, or not.
        /// </summary>
        /// <param name="b"></param>
        public abstract void setTransitionTracesStale(bool b);
        /// <summary>
        ///  Returns Trace of the active <see cref="Column"/> indexes.
        /// </summary>
        /// <returns></returns>
        public virtual IndicesTrace mmGetTraceActiveColumns()
        {
            return (IndicesTrace)getTraceMap().Get("activeColumns");
        }
        public virtual IndicesTrace mmGetTracePredictiveCells()
        {
            return (IndicesTrace)getTraceMap().Get("predictiveCells");
        }
        public virtual CountsTrace mmGetTraceNumSegments()
        {
            return (CountsTrace)getTraceMap().Get("numSegments");
        }
        public virtual CountsTrace mmGetTraceNumSynapses()
        {
            return (CountsTrace)getTraceMap().Get("numSynapses");
        }
        public virtual StringsTrace mmGetTraceSequenceLabels()
        {
            return (StringsTrace)getTraceMap().Get("sequenceLabels");
        }
        public virtual BoolsTrace mmGetTraceResets()
        {
            return (BoolsTrace)getTraceMap().Get("resets");
        }
        public virtual IndicesTrace mmGetTracePredictedActiveCells()
        {
            mmComputeTransitionTraces();
            return (IndicesTrace)getTraceMap().Get("predictedActiveCells");
        }
        public virtual IndicesTrace mmGetTracePredictedInactiveCells()
        {
            mmComputeTransitionTraces();
            return (IndicesTrace)getTraceMap().Get("predictedInactiveCells");
        }
        public virtual IndicesTrace mmGetTracePredictedActiveColumns()
        {
            mmComputeTransitionTraces();
            return (IndicesTrace)getTraceMap().Get("predictedActiveColumns");
        }

        /**
         * Trace of predicted => inactive columns
         * @return
         */
        public virtual IndicesTrace mmGetTracePredictedInactiveColumns()
        {
            mmComputeTransitionTraces();
            return (IndicesTrace)getTraceMap().Get("predictedInactiveColumns");
        }

        /**
         * Trace of unpredicted => active columns
         * @return
         */
        public virtual IndicesTrace mmGetTraceUnpredictedActiveColumns()
        {
            mmComputeTransitionTraces();
            return (IndicesTrace)getTraceMap().Get("unpredictedActiveColumns");
        }

        /**
         * Convenience method to compute a metric over an counts trace, excluding
         * resets.
         * 
         * @param trace     Trace of indices
         * @return
         */
        public virtual Metric<double> mmGetMetricFromTrace(Trace<double> trace)
        {
            return Metric<double>.createFromTrace(trace, mmGetTraceResets());
        }
        /**
         * Convenience method to compute a metric over an indices trace, excluding
         * resets.
         * 
         * @param trace     Trace of indices
         * @return
         */
        public virtual Metric<int> mmGetMetricFromTrace(IndicesTrace trace)
        {
            List<HashSet<int>> data = null;
            BoolsTrace excludeResets = mmGetTraceResets();
            if (excludeResets != null)
            {
                int[] i = { 0 };
                data = trace.items.Where(t => !excludeResets.items[i[0]++]).ToList();
            }

            trace.items = data;
            CountsTrace iTrace = trace.makeCountsTrace();
            return Metric<int>.createFromTrace(iTrace, mmGetTraceResets());
        }

        /**
         * Metric for number of predicted => active cells per column for each sequence
         * @return
         */
        public virtual Metric<int> mmGetMetricSequencesPredictedActiveCellsPerColumn()
        {
            mmComputeTransitionTraces();

            List<int> numCellsPerColumn = new List<int>();

            var map = ((Map<string, HashSet<int>>) getDataMap().Get("predictedActiveCellsForSequence"));
            foreach (var m in map)
            {
                numCellsPerColumn.Add(m.Value.Count);
            }
            //for (Map.Entry<String, Set<Integer>> m : 
            //((Map<String, Set<Integer>>)getDataMap().get("predictedActiveCellsForSequence")).entrySet())
            //{

            //    numCellsPerColumn.add(m.getValue().size());
            //}

            return new Metric<int>(this, "# predicted => active cells per column for each sequence", numCellsPerColumn);
        }

        /**
         * Metric for number of sequences each predicted => active cell appears in
         *
         * Note: This metric is flawed when it comes to high-order sequences.
         * @return
         */
        public virtual Metric<int> mmGetMetricSequencesPredictedActiveCellsShared()
        {
            mmComputeTransitionTraces();

            Map<int, int> numSequencesForCell = new Map<int, int>();

            foreach (KeyValuePair<string, HashSet<int>> m in ((Map<string, HashSet<int>>)getDataMap().Get("predictedActiveCellsForSequence")))
            {
                foreach (int cell in m.Value)
                {
                    if (numSequencesForCell.Get(cell) == null)
                    {
                        numSequencesForCell.Add(cell, 0);
                        continue;
                    }
                    numSequencesForCell.Add(cell, numSequencesForCell.Get(cell) + 1);
                }
            }

            return new Metric<int>(this, "# sequences each predicted => active cells appears in", new List<int>(numSequencesForCell.Values));
        }

        public virtual void mmComputeTransitionTraces()
        {
            if (!transitionTracesStale())
            {
                return;
            }

            Map<string, HashSet<int>> predActCells = (Map<string, HashSet<int>>)getDataMap().Get("predictedActiveCellsForSequence");
            if (predActCells == null)
            {
                getDataMap().Add("predictedActiveCellsForSequence", predActCells = new Map<string, HashSet<int>>());
            }

            getTraceMap().Add("predictedActiveCells", new IndicesTrace(this, "predicted => active cells (correct)"));
            getTraceMap().Add("predictedInactiveCells", new IndicesTrace(this, "predicted => inactive cells (extra)"));
            getTraceMap().Add("predictedActiveColumns", new IndicesTrace(this, "predicted => active columns (correct)"));
            getTraceMap().Add("predictedInactiveColumns", new IndicesTrace(this, "predicted => inactive columns (extra)"));
            getTraceMap().Add("unpredictedActiveColumns", new IndicesTrace(this, "unpredicted => active columns (bursting)"));

            IndicesTrace predictedCellsTrace = (IndicesTrace)getTraceMap().Get("predictedCells");

            int i = 0;
            HashSet<int> predictedActiveColumns = null;
            foreach (HashSet<int> activeColumns in mmGetTraceActiveColumns().items)
            {
                HashSet<int> predictedActiveCells = new HashSet<int>();
                HashSet<int> predictedInactiveCells = new HashSet<int>();
                predictedActiveColumns = new HashSet<int>();
                HashSet<int> predictedInactiveColumns = new HashSet<int>();

                foreach (int predictedCell in predictedCellsTrace.items[i])
                {
                    int predictedColumn = getConnections().GetCell(predictedCell).GetColumn().GetIndex();

                    if (activeColumns.Contains(predictedColumn))
                    {
                        predictedActiveCells.Add(predictedCell);
                        predictedActiveColumns.Add(predictedColumn);

                        string sequenceLabel = (string)mmGetTraceSequenceLabels().items[i];
                        if (sequenceLabel != null && !string.IsNullOrWhiteSpace(sequenceLabel))
                        {
                            HashSet<int> sequencePredictedCells = null;
                            if ((sequencePredictedCells = (HashSet<int>)predActCells.Get(sequenceLabel)) == null)
                            {

                                ((Map<string, HashSet<int>>)predActCells).Add(
                                    sequenceLabel, sequencePredictedCells = new HashSet<int>());
                            }

                            sequencePredictedCells.Add(predictedCell);
                        }
                    }
                    else {
                        predictedInactiveCells.Add(predictedCell);
                        predictedInactiveColumns.Add(predictedColumn);
                    }
                }

                HashSet<int> unpredictedActiveColumns = new HashSet<int>(activeColumns);
                unpredictedActiveColumns.ExceptWith(predictedActiveColumns);

                ((IndicesTrace)getTraceMap().Get("predictedActiveCells")).items.Add(predictedActiveCells);
                ((IndicesTrace)getTraceMap().Get("predictedInactiveCells")).items.Add(predictedInactiveCells);
                ((IndicesTrace)getTraceMap().Get("predictedActiveColumns")).items.Add(predictedActiveColumns);
                ((IndicesTrace)getTraceMap().Get("predictedInactiveColumns")).items.Add(predictedInactiveColumns);
                ((IndicesTrace)getTraceMap().Get("unpredictedActiveColumns")).items.Add(unpredictedActiveColumns);

                i++;
            }

            setTransitionTracesStale(false);
        }

        public virtual ComputeCycle Compute(Connections cnx, int[] activeColumns, string sequenceLabel, bool learn)
        {
            // Append last cycle's predictiveCells to *predicTEDCells* trace
            ((IndicesTrace)getTraceMap().Get("predictedCells")).items.Add(
                new HashSet<int>(Connections.AsCellIndexes(cnx.GetPredictiveCells())));

            ComputeCycle cycle = getMonitor().Compute(cnx, activeColumns, learn);

            // Append this cycle's predictiveCells to *predicTIVECells* trace
            ((IndicesTrace)getTraceMap().Get("predictiveCells")).items.Add(
                new HashSet<int>(Connections.AsCellIndexes(cnx.GetPredictiveCells())));

            ((IndicesTrace)getTraceMap().Get("activeCells")).items.Add(
                new HashSet<int>(Connections.AsCellIndexes(cnx.GetActiveCells())));
            ((IndicesTrace)getTraceMap().Get("activeColumns")).items.Add(new HashSet<int>(activeColumns));
            //Arrays.stream(activeColumns).boxed().collect(Collectors.toCollection(HashSet::new)));
            ((CountsTrace)getTraceMap().Get("numSegments")).items.Add(cnx.GetNumSegments());
            ((CountsTrace)getTraceMap().Get("numSynapses")).items.Add((int)(cnx.GetNumSynapses() ^ (cnx.GetNumSynapses() >> 32)));
            ((StringsTrace)getTraceMap().Get("sequenceLabels")).items.Add(sequenceLabel);
            ((BoolsTrace)getTraceMap().Get("resets")).items.Add(resetActive());

            setResetActive(false);

            setTransitionTracesStale(true);

            return cycle;
        }

        /**
         * Called to delegate a {@link TemporalMemory#reset(Connections)} call and
         * then set a flag locally which controls remaking of test {@link Trace}s.
         * 
         * @param c
         */
        public virtual void ResetSequences(Connections c)
        {
            getMonitor().Reset(c);

            setResetActive(true);
        }

        public virtual List<Metric<int>> mmGetDefaultMetrics(int verbosity)
        {
            BoolsTrace resetsTrace = mmGetTraceResets();
            List<Metric<int>> metrics = new List<Metric<int>>();

            List <ITrace> utilTraces = mmGetDefaultTraces(verbosity);
            for (int i = 0; i < utilTraces.Count - 3; i++)
            {
                metrics.Add(Metric<int>.createFromTrace((Trace<int>)utilTraces[i], resetsTrace));
            }
            for (int i = utilTraces.Count - 3; i < utilTraces.Count - 1; i++)
            {
                metrics.Add(Metric<int>.createFromTrace((Trace<int>)utilTraces[i], null));
            }
            metrics.Add(mmGetMetricSequencesPredictedActiveCellsPerColumn());
            metrics.Add(mmGetMetricSequencesPredictedActiveCellsShared());

            return metrics;
        }

        public override void mmClearHistory()
        {
            getTraceMap().Clear();
            getDataMap().Clear();

            getTraceMap().Add("predictedCells", new IndicesTrace(this, "predicted cells"));
            getTraceMap().Add("activeColumns", new IndicesTrace(this, "active columns"));
            getTraceMap().Add("activeCells", new IndicesTrace(this, "active cells"));
            getTraceMap().Add("predictiveCells", new IndicesTrace(this, "predictive cells"));
            getTraceMap().Add("numSegments", new CountsTrace(this, "# segments"));
            getTraceMap().Add("numSynapses", new CountsTrace(this, "# synapses"));
            getTraceMap().Add("sequenceLabels", new StringsTrace(this, "sequence labels"));
            getTraceMap().Add("resets", new BoolsTrace(this, "resets"));

            setTransitionTracesStale(true);
        }

        public override List<ITrace> mmGetDefaultTraces(int verbosity)
        {
            List<ITrace> traces = new List<ITrace>();
            traces.Add((ITrace)mmGetTraceActiveColumns());
            traces.Add((ITrace)mmGetTracePredictedActiveColumns());
            traces.Add((ITrace)mmGetTracePredictedInactiveColumns());
            traces.Add((ITrace)mmGetTraceUnpredictedActiveColumns());
            traces.Add((ITrace)mmGetTracePredictedActiveCells());
            traces.Add((ITrace)mmGetTracePredictedInactiveCells());

            List<ITrace> tracesToAdd = new List<ITrace>();
            if (verbosity == 1)
            {
                foreach (ITrace t in traces)
                {
                    tracesToAdd.Add((ITrace)((IndicesTrace)t).makeCountsTrace());
                }
                traces.Clear();
                traces.AddRange(tracesToAdd);
            }

            traces.Add((ITrace)mmGetTraceNumSegments());
            traces.Add((ITrace)mmGetTraceNumSynapses());
            traces.Add((ITrace)mmGetTraceSequenceLabels());

            return traces;
        }
    }

    public class MonitoredTemporalMemory : TemporalMemoryMonitorMixin, IComputeDecorator
    {
        private IComputeDecorator decorator;

        private Connections connections;

        private Map<string, ITrace> mmTraces = new Map<string, ITrace>();
        private Map<string, Map<string, HashSet<int>>> mmData = new Map<string, Map<string, HashSet<int>>>();

        private string mmName;

        private bool mmResetActive;
        private bool _transitionTracesStale = true;

        /**
        * Constructs a new {@code MonitoredTemporalMemory}
        * 
        * @param decorator     The decorator class
        * @param cnx           the {@link Connections} object.
        */
        public MonitoredTemporalMemory(IComputeDecorator decorator, Connections cnx)
        {
            this.decorator = decorator;
            this.mmResetActive = true;
            this.connections = cnx;

            mmClearHistory();
        }



        public ComputeCycle Compute(Connections connections, int[] activeColumns, bool learn)
        {
            throw new NotImplementedException();
            return Compute(connections, activeColumns, learn);
        }

        public void Reset(Connections connections)
        {
            decorator.Reset(connections);
        }

        public override Connections getConnections()
        {
            return connections;
        }

        public override IDictionary getDataMap()
        {
            return mmData;
        }

        public override Map<string, ITrace> getTraceMap()
        {
            return mmTraces;
        }

        public override string mmGetName()
        {
            return mmName;
        }

        public override IComputeDecorator getMonitor()
        {
            return decorator;
        }

        public override bool resetActive()
        {
            return mmResetActive;
        }

        public override void setResetActive(bool b)
        {
            this.mmResetActive = b;
        }

        public override bool transitionTracesStale()
        {
            return _transitionTracesStale;
        }

        public override void setTransitionTracesStale(bool b)
        {
            this._transitionTracesStale = b;
        }
    }

    public interface ITrace
    {

    }

    public abstract class Trace<T> : ITrace
    {
        internal MonitorMixinBase monitor;
        internal string title;

        public List<T> items;

        /**
         * Constructs a new {@code Trace}
         * @param monitor
         * @param title
         */
        public Trace(MonitorMixinBase monitor, string title)
        {
            this.monitor = monitor;
            this.title = title;

            items = new List<T>();
        }

        /**
     * Returns the implementing mixin name if not null 
     * plus the configured title.
     * 
     * @return
     */
        public string prettyPrintTitle()
        {
            return monitor.mmGetName() != null ?
                string.Format("[{0}] {1}", monitor.mmGetName(), title) :
                    string.Format("{0}", title);
        }

        /**
         * Simply returns the {@link Object#toString()} of the specified 
         * Object. Should be overridden to enhance output if desired.
         * 
         * @param datum     Object to pretty print
         * @return
         */
        public string prettyPrintDatum(object datum)
        {
            return datum.ToString();
        }
    }

    public class IndicesTrace : Trace<HashSet<int>>
    {
        public IndicesTrace(MonitorMixinBase monitor, string title)
            : base(monitor, title)
        {

        }

        /**
     * A new Trace made up of counts of this trace's indices.
     * @return
     */
        public CountsTrace makeCountsTrace()
        {
            CountsTrace trace = new CountsTrace(monitor, string.Format("# {0}", title));
            trace.items = items.Select(l => l.Count).ToList();
            return trace;
        }

        /**
         * Trace made up of cumulative counts of trace indices.
         * @return
         */
        public CountsTrace makeCumCountsTrace()
        {
            CountsTrace trace = new CountsTrace(monitor, string.Format("# (cumulative) {0}", title));
            Trace<int> countsTrace = makeCountsTrace();
            int[] accum = { 0 };
            trace.items = countsTrace.items.Select(i => accum[0] += ((int)i)).ToList();
            return trace;
        }

        /**
         * Prints the specified datum
         * @param c
         * @return
         */
        public string prettyPrintDatum(IEnumerable<int> collection)
        {
            return Arrays.ToString(collection.OrderBy(c => c)).Replace("[", "").Replace("]", "").Trim();
        }
    }

    public class CountsTrace : Trace<int>
    {
        public CountsTrace(MonitorMixinBase monitor, string title)
            : base(monitor, title)
        {

        }
    }

    public class BoolsTrace : Trace<bool>
    {
        public BoolsTrace(MonitorMixinBase monitor, string title)
            : base(monitor, title)
        {

        }
    }

    public class StringsTrace : Trace<string>
    {
        public StringsTrace(MonitorMixinBase monitor, string title)
            : base(monitor, title)
        {

        }
    }

    public class Metric<TNumber>
        where TNumber : struct
    {
        public MonitorMixinBase monitor;
        public string title;

        public double min;
        public double max;
        public double sum;
        public double mean = double.NaN;
        public double variance;
        public double standardDeviation;

        public Metric(MonitorMixinBase monitor, string title, List<TNumber> l)
        {
            this.monitor = monitor;
            this.title = title;

            computeStats(l);
        }

        /**
         * Returns a {@code Metric} object created from the specified {@link Trace}
         * @param trace
         * @param excludeResets
         * @return
         */
        public static Metric<TNumber> createFromTrace<T>(T trace, BoolsTrace excludeResets)
            where T : Trace<TNumber>, ITrace
        {
            List<TNumber> data = (List<TNumber>)trace.items;
            if (excludeResets != null)
            {
                data = new List<TNumber>();
                for (int k = 0; k < trace.items.Count; k++)
                {
                    if (!excludeResets.items[k])
                    {
                        TNumber n = trace.items[k];
                        data.Add(n);
                    }
                }
            }
            return new Metric<TNumber>(trace.monitor, trace.title, data);
        }

        /**
         * Returns a copy of this {@code Metric}
         * @return
         */
        public Metric<TNumber> copy()
        {
            Metric<TNumber> metric = new Metric<TNumber>(monitor, title, new List<TNumber>());

            metric.min = min;
            metric.max = max;
            metric.sum = sum;
            metric.mean = mean;
            metric.variance = variance;
            metric.standardDeviation = standardDeviation;

            return metric;
        }

        public string prettyPrintTitle()
        {
            return string.Format(monitor.mmGetName() == null ? "{0}" : "[{0}] {1}",
                monitor.mmGetName() == null ? new object[] { title } : new object[] { monitor.mmGetName(), title });
        }

        /**
         * Populates the inner fields of this {@code Metric} with the computed stats.
         * @param l
         */
        public void computeStats(List<TNumber> l)
        {
            if (l.Count < 1)
            {
                return;
            }

            double[] doubs = null;
            if (typeof(int).IsAssignableFrom(l[0].GetType()))
            {
                doubs = ArrayUtils.ToDoubleArray(l.Cast<int>().ToArray());
            }
            else if (typeof(double).IsAssignableFrom(l[0].GetType()))
            {
                doubs = l.Cast<double>().ToArray();
            }

            min = ArrayUtils.Min(doubs);
            max = ArrayUtils.Max(doubs);
            sum = ArrayUtils.Sum(doubs);

            double d = ArrayUtils.Average(doubs);
            mean = d;
            double v = ArrayUtils.Variance(doubs, d);
            variance = v;
            double s = v > 0 ? Math.Sqrt(v) : 0.0;
            standardDeviation = s;
        }

        /**
         * Returns an array of this {@link Metric}'s stats.
         * @param sigFigs   the number of significant figures to limit the output numbers to.
         * @return
         */
        public double[] getStats(int sigFigs)
        {
            if (double.IsNaN(mean))
            {
                return new double[] { 0, 0, 0, 0, 0 };
            }
            return new double[]
            {
                Math.Round(mean, sigFigs, MidpointRounding.AwayFromZero),
                Math.Round(standardDeviation, sigFigs, MidpointRounding.AwayFromZero),
                Math.Round(min, sigFigs, MidpointRounding.AwayFromZero),
                Math.Round(max, sigFigs, MidpointRounding.AwayFromZero),
                Math.Round(sum, sigFigs, MidpointRounding.AwayFromZero)
                //BigDecimal.valueOf(mean).setScale(sigFigs, BigDecimal.ROUND_HALF_UP).doubleValue(),
                //BigDecimal.valueOf(standardDeviation).setScale(sigFigs, BigDecimal.ROUND_HALF_UP).doubleValue(),
                //BigDecimal.valueOf(min).setScale(sigFigs, BigDecimal.ROUND_HALF_UP).doubleValue(),
                //BigDecimal.valueOf(max).setScale(sigFigs, BigDecimal.ROUND_HALF_UP).doubleValue(),
                //BigDecimal.valueOf(sum).setScale(sigFigs, BigDecimal.ROUND_HALF_UP).doubleValue()
            };
        }
    }

    public class MetricsTrace : Trace<double>
    {
        public MetricsTrace(MonitorMixinBase monitor, string title)
            : base(monitor, title)
        {

        }

        public string prettyPrintDatum(Metric<double> datum)
        {
            return string.Format("min: {0:0.00}, max: {1:0.00}, sum: {2:0.00}, mean: {3:0.00}, std dev: {4:0.00}",
                datum.min, datum.max, datum.sum, datum.mean, datum.standardDeviation);
        }
    }
}