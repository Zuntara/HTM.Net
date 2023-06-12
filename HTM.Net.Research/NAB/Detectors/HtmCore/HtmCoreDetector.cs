using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using System.Collections.Generic;
using System;
using System.Linq;
using HTM.Net.Model;
using System.IO;
using System.Text.RegularExpressions;
using MathNet.Numerics.Statistics;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;

namespace HTM.Net.Research.NAB.Detectors.HtmCore;

public class HtmcoreDetector : AnomalyDetector
{
    private const double SpatialTolerance = 0.05;
    private const bool PandaVisBakeData = false;

    private bool useLikelihood = true;
    private bool useSpatialAnomaly = true;
    private bool verbose = true;
    private bool useOptimization = Environment.GetEnvironmentVariable("HTMCORE_OPTIMIZE") != null;

    private DateEncoder encTimestamp;
    private RandomDistributedScalarEncoder encValue;
    private SpatialPooler sp;
    private TemporalMemory tm;
    private AnomalyLikelihoodCpp anomalyLikelihood;
    private Connections connections;
    public Metrics EncInfo { get; private set; }
    public Metrics SpInfo { get; private set; }
    public Metrics TmInfo { get; private set; }
    private List<double> inputs;
    private int iteration;
    private Parameters defaultParameters;
    private double? minVal, maxVal;

    public HtmcoreDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        defaultParameters = Parameters.Empty();
        defaultParameters.SetParameterByKey(Parameters.KEY.ENCODER, null);
        defaultParameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
        defaultParameters.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
        defaultParameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 13);
        defaultParameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.21);
        defaultParameters.SetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE, 0.2);
        defaultParameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 10);
        defaultParameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 20);
        defaultParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.1);
        defaultParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.1);
        defaultParameters.SetParameterByKey(Parameters.KEY.MAX_SEGMENTS_PER_CELL, 128);
        defaultParameters.SetParameterByKey(Parameters.KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
        defaultParameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 0.0);
        defaultParameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, 40.0 / 1048.0);
        defaultParameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.4);
        defaultParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.003);
        defaultParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.2);
        defaultParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.0005);
        defaultParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, 0.1);

        defaultParameters.SetParameterByKey(Parameters.KEY.RESOLUTION, 0.001);
        defaultParameters.SetParameterByKey(Parameters.KEY.SPARSE_THRESHOLD, 0.1);
        defaultParameters.SetParameterByKey(Parameters.KEY.DATEFIELD_TOFD, new TimeOfDayTuple(21, 9.49));
        defaultParameters.SetParameterByKey(Parameters.KEY.DATEFIELD_WKEND, new WeekendTuple(0, 0));


        /*defaultParameters = new Dictionary<string, object>
        {
            ["enc"] = new Dictionary<string, object>
            {
                ["value"] = new Dictionary<string, object>
                {
                    ["resolution"] = 0.001,
                    ["size"] = 4000,
                    ["sparsity"] = 0.10
                },
                ["time"] = new Dictionary<string, object>
                {
                    ["timeOfDay"] = new Tuple<int, double>(21, 9.49),
                    ["weekend"] = 0
                }
            },
            ["predictor"] = new Dictionary<string, object>
            {
                ["sdrc_alpha"] = 0.1
            }
        };*/

        useLikelihood = true;
        useSpatialAnomaly = true;
        verbose = true;
        useOptimization = false;

        encTimestamp = null;
        encValue = null;
        sp = null;
        tm = null;
        anomalyLikelihood = null;
        EncInfo = null;
        SpInfo = null;
        TmInfo = null;
        inputs = new List<double>();
        iteration = 0;
    }

    protected override string[] GetAdditionalHeaders()
    {
        return new[] { "raw_score" };
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        var timestamp = DateTime.Parse((string)inputData["timestamp"]);
        var value = double.Parse((string)inputData["value"], NumberFormatInfo.InvariantInfo);

        var result = ModelRun(timestamp, value);

        return new List<object> { result.Item1, result.Item2 };
    }

    public static Parameters ReadParams(string filename)
    {
        string directoryName = Path.GetDirectoryName(typeof(HtmcoreDetector).Assembly.Location);
        string filePath = Path.Combine(directoryName, filename);

        using (StreamReader file = File.OpenText(filePath))
        {
            string contents = file.ReadToEnd();
            Parameters parameters = JsonConvert.DeserializeObject<Parameters>(contents);
            return parameters;
        }
    }

    public override void Initialize()
    {
        // toggle parameters here
        Parameters parameters;
        if (useOptimization)
        {
            parameters = ReadParams("params.json");
        }
        else
        {
            parameters = defaultParameters;
        }

        // setup spatial anomaly
        if (useSpatialAnomaly)
        {
            // Keep track of value range for spatial anomaly detection
            minVal = null;
            maxVal = null;
        }

        // setup Enc, SP, TM, Likelihood
        // Make the Encoders. These will convert input data into binary representations.
        var tofd = (TimeOfDayTuple)parameters.GetParameterByKey(Parameters.KEY.DATEFIELD_TOFD);
        var wkend = (WeekendTuple)parameters.GetParameterByKey(Parameters.KEY.DATEFIELD_WKEND);
        encTimestamp = (DateEncoder)((DateEncoder.Builder)DateEncoder.GetBuilder())
            .TimeOfDay(tofd.BitsToUse, tofd.Radius)
            .Weekend(wkend.BitsToUse)
            .Build();

        encValue = (RandomDistributedScalarEncoder)((RandomDistributedScalarEncoder.Builder)RandomDistributedScalarEncoder.GetBuilder())
            .Resolution((double)parameters.GetParameterByKey(Parameters.KEY.RESOLUTION))
            .N(4000)
            .W((int)(4000 * 0.1)+1)
            .Name("ScalarEncoder")
            .Build();

        /* RDSE_Parameters scalarEncoderParams = new RDSE_Parameters();
         scalarEncoderParams.size = parameters["enc"]["value"]["size"]; // n
         scalarEncoderParams.sparsity = parameters["enc"]["value"]["sparsity"];
         scalarEncoderParams.resolution = parameters["enc"]["value"]["resolution"];

         encValue = new RDSE(scalarEncoderParams);*/
        int encodingWidth = encTimestamp.GetN() + encValue.GetN();
        EncInfo = new Metrics(new int[] { encodingWidth }, 999999999);

        // Make the HTM. SpatialPooler & TemporalMemory & associated tools.
        // SpatialPooler
        var spParams = parameters.Copy();
        spParams.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { encodingWidth });
        spParams.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, encodingWidth);
        spParams.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
        spParams.SetParameterByKey(Parameters.KEY.WRAP_AROUND, true);
        spParams.SetParameterByKey(Parameters.KEY.SEED, 0);

        connections = new Connections();
        spParams.Apply(connections);

        sp = new SpatialPooler();
        sp.Init(connections);

        SpInfo = new Metrics(connections.GetColumnDimensions(), 999999999);

        // TemporalMemory
        var tmParams = parameters.Copy();
        tmParams.SetParameterByKey(Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.0);
        tmParams.SetParameterByKey(Parameters.KEY.SEED, 0);
        tmParams.SetParameterByKey(Parameters.KEY.TM_ANOMALY_MODE, AnomalyMode.Raw);

        tm = new TemporalMemory();
        tmParams.Apply(connections);
        TemporalMemory.Init(connections);

        /*tm = new TemporalMemory(
            columnDimensions: new int[] { spParams["columnCount"] },
            cellsPerColumn: tmParams["cellsPerColumn"],
            activationThreshold: tmParams["activationThreshold"],
            initialPermanence: tmParams["initialPerm"],
            connectedPermanence: spParams["synPermConnected"],
            minThreshold: tmParams["minThreshold"],
            maxNewSynapseCount: tmParams["newSynapseCount"],
            permanenceIncrement: tmParams["permanenceInc"],
            permanenceDecrement: tmParams["permanenceDec"],
            predictedSegmentDecrement: 0.0,
            maxSegmentsPerCell: tmParams["maxSegmentsPerCell"],
            maxSynapsesPerSegment: tmParams["maxSynapsesPerSegment"],
            externalPredictiveInputs: encTimestamp.size,
            seed: 0
        );*/
        TmInfo = new Metrics(new int[] { connections.GetCells().Length }, 999999999);

        // setup likelihood, these settings are used in NAB
        if (useLikelihood)
        {
            int learningPeriod = (int)Math.Floor(ProbationaryPeriod / 2.0);
            anomalyLikelihood = new AnomalyLikelihoodCpp(learningPeriod);
        }

        // initialize pandaBaker
        //if (PANDA_VIS_BAKE_DATA)
        //{
        //    BuildPandaSystem(sp, tm, parameters["enc"]["value"]["size"], encTimestamp.size);
        //}
    }

    private int[] _activeColumnsCache = null;

    public (double, double) ModelRun(DateTime ts, double val)
    {
        // Run a single pass through HTM model
        // @params ts - Timestamp
        // @params val - float input value
        // @return rawAnomalyScore computed for the `val` in this step

        // run data through our model pipeline: enc -> SP -> TM -> Anomaly
        inputs.Add(val);
        iteration++;

        // 1. Encoding
        // Call the encoders to create bit representations for each value. These are SDR objects.
        var dateBits = encTimestamp.Encode(ts);
        var valueBits = encValue.Encode((float)val);
        // Concatenate all these encodings into one large encoding for Spatial Pooling.

        var encoding = new SparseDistributedRepresentation(encTimestamp.GetN() + encValue.GetN())
            .Concatenate(valueBits, dateBits);

        EncInfo.AddData(encoding);

        // 2. Spatial Pooler
        // Create an SDR to represent active columns. This will be populated by the compute method below.
        // It must have the same dimensions as the Spatial Pooler.
        _activeColumnsCache = _activeColumnsCache ?? new int[connections.GetNumColumns()];
        // Execute Spatial Pooling algorithm over input space.
        sp.Compute(connections, encoding.GetDense(), _activeColumnsCache, true);
        SpInfo.AddData(new SparseDistributedRepresentation(_activeColumnsCache.Length).SetSparse(_activeColumnsCache));

        // 3. Temporal Memory
        // Execute Temporal Memory algorithm over active mini-columns.

        // to get predictive cells we need to call activateDendrites & activateCells separately
        /*if (PANDA_VIS_BAKE_DATA)
        {
            // activateDendrites calculates active segments
            tm.ActivateDendrites(learn: true);
            // predictive cells are calculated directly from active segments
            var predictiveCells = tm.GetPredictiveCells();
            // activates cells in columns by TM algorithm (winners, bursting...)
            tm.ActivateCells(activeColumns, learn: true);
        }
        else
        {*/
        tm.Compute(connections, _activeColumnsCache, learn: true);
        //}

        TmInfo.AddData(connections.GetActiveCells().Select(c => c.GetIndex()).ToSdr(new int[] { connections.GetCells().Length }));

        // 4.1 (optional) Predictor #TODO optional
        //TODO optional: also return an error metric on predictions (RMSE, R2,...)

        // 4.2 Anomaly
        // handle spatial, contextual (raw, likelihood) anomalies
        // -Spatial
        double spatialAnomaly = 0.0; //TODO optional: make this computed in SP (and later improve)
        if (useSpatialAnomaly)
        {
            // Update min/max values and check if there is a spatial anomaly
            if (minVal != null && maxVal != null)
            {
                double tolerance = (maxVal.Value - minVal.Value) * SpatialTolerance;
                double maxExpected = maxVal.Value + tolerance;
                double minExpected = minVal.Value - tolerance;
                if (val > maxExpected || val < minExpected)
                {
                    spatialAnomaly = 1.0;
                }
            }
            if (maxVal == null || val > maxVal)
            {
                maxVal = val;
            }
            if (minVal == null || val < minVal)
            {
                minVal = val;
            }
        }

        double temporalAnomaly = connections.GetTmAnomalyScore();
        double raw = temporalAnomaly;

        if (useLikelihood)
        {
            temporalAnomaly = anomalyLikelihood.AnomalyProbability(temporalAnomaly);
        }

        double anomalyScore = Math.Max(spatialAnomaly, temporalAnomaly); // this is the "main" anomaly, compared in NAB

        // 5. print stats
        if (verbose && iteration % 1000 == 0)
        {
            // print(enc_info);
            // print(sp_info);
            // print(tm_info);
        }

        // 6. panda vis
        //if (PANDA_VIS_BAKE_DATA)
        //{
        //    // ------------------HTMpandaVis----------------------
        //    // see more about this structure at https://github.com/htm-community/HTMpandaVis/blob/master/pandaBaker/README.md
        //    // fill up values
        //    pandaBaker.inputs["Value"].stringValue = "value: " + val.ToString("F2");
        //    pandaBaker.inputs["Value"].bits = valueBits.Sparse;

        //    pandaBaker.inputs["TimeOfDay"].stringValue = ts.ToString();
        //    pandaBaker.inputs["TimeOfDay"].bits = dateBits.Sparse;

        //    pandaBaker.layers["Layer1"].activeColumns = activeColumns.Sparse;
        //    pandaBaker.layers["Layer1"].winnerCells = tm.GetWinnerCells().Sparse;
        //    pandaBaker.layers["Layer1"].predictiveCells = predictiveCells.Sparse;
        //    pandaBaker.layers["Layer1"].activeCells = tm.GetActiveCells().Sparse;

        //    // customizable datastreams to be show on the DASH PLOTS
        //    pandaBaker.dataStreams["rawAnomaly"].value = temporalAnomaly;
        //    pandaBaker.dataStreams["value"].value = val;
        //    pandaBaker.dataStreams["numberOfWinnerCells"].value = tm.GetWinnerCells().Sparse.Length;
        //    pandaBaker.dataStreams["numberOfPredictiveCells"].value = predictiveCells.Sparse.Length;
        //    pandaBaker.dataStreams["valueInput_sparsity"].value = valueBits.GetSparsity();
        //    pandaBaker.dataStreams["dateInput_sparsity"].value = dateBits.GetSparsity();

        //    pandaBaker.dataStreams["Layer1_SP_overlap_metric"].value = sp_info.Overlap.Overlap;
        //    pandaBaker.dataStreams["Layer1_TM_overlap_metric"].value = sp_info.Overlap.Overlap;
        //    pandaBaker.dataStreams["Layer1_SP_activation_frequency"].value = sp_info.ActivationFrequency.Mean();
        //    pandaBaker.dataStreams["Layer1_TM_activation_frequency"].value = tm_info.ActivationFrequency.Mean();
        //    pandaBaker.dataStreams["Layer1_SP_entropy"].value = sp_info.ActivationFrequency.Mean();
        //    pandaBaker.dataStreams["Layer1_TM_entropy"].value = tm_info.ActivationFrequency.Mean();

        //    pandaBaker.StoreIteration(iteration_ - 1);
        //    Console.WriteLine("ITERATION: " + (iteration_ - 1));

        //    // ------------------HTMpandaVis----------------------
        //}

        return (anomalyScore, raw);
    }
}

public class Metrics
{
    private int[] dimensions_;
    private Sparsity sparsity_;
    private ActivationFrequency activationFrequency_;
    private Overlap overlap_;

    public Metrics(int[] dimensions, int period)
    {
        dimensions_ = dimensions;
        sparsity_ = new Sparsity(dimensions, period);
        activationFrequency_ = new ActivationFrequency(dimensions, period);
        overlap_ = new Overlap(dimensions, period);
    }

    public Metrics(SparseDistributedRepresentation dataSource, int period)
    {
        dimensions_ = dataSource.dimensions_;
        sparsity_ = new Sparsity(dataSource, period);
        activationFrequency_ = new ActivationFrequency(dataSource, period);
        overlap_ = new Overlap(dataSource, period);
    }

    public void Reset()
    {
        overlap_.Reset();
    }

    public virtual void AddData(SparseDistributedRepresentation data)
    {
        sparsity_.AddData(data);
        activationFrequency_.AddData(data);
        overlap_.AddData(data);
    }

    public override string ToString()
    {
        // Introduction line: "SDR ( dimensions )"
        string dimensionsString = string.Join(" ", dimensions_);
        string introductionLine = $"SDR( {dimensionsString} )\n";

        // Print data to temporary area for formatting.
        StringWriter dataStream = new StringWriter();
        dataStream.WriteLine(sparsity_);
        dataStream.WriteLine(activationFrequency_);
        dataStream.WriteLine(overlap_);
        string data = dataStream.ToString();

        // Indent all of the data text (4 spaces). Append indent to newlines.
        data = Regex.Replace(data, "\n", "\n    ");
        // Strip trailing whitespace
        data = Regex.Replace(data, "\\s+$", "");
        // Insert first indent, append trailing newline.
        return $"    {data}\n";
    }
}

public class Sparsity : MetricsHelper
{
    public double SparsityValue => sparsity;
    public double Min => min;
    public double Max => max;
    public double Mean => mean;
    public double Std => Math.Sqrt(variance);

    private double sparsity;
    private double min;
    private double max;
    private double mean;
    private double variance;

    public Sparsity(SparseDistributedRepresentation dataSource, int period) : base(dataSource, period)
    {
        Initialize();
    }

    public Sparsity(int[] dimensions, int period) : base(dimensions, period)
    {
        Initialize();
    }

    private void Initialize()
    {
        sparsity = double.NaN;
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;
        mean = 1234.567;
        variance = 1234.567;
    }

    protected override void Callback(SparseDistributedRepresentation dataSource, double alpha)
    {
        sparsity = dataSource.GetSparsity();
        min = Math.Min(min, sparsity);
        max = Math.Max(max, sparsity);
        var diff = sparsity - mean;
        var incr = alpha * diff;
        mean += incr;
        variance = (1.0 - alpha) * (variance + diff * incr);
    }

    public override string ToString()
    {
        return $"Sparsity Min/Mean/Std/Max {Min} / {Mean} / {Std} / {Max}";
    }
}

public class ActivationFrequency : MetricsHelper
{
    public List<double> ActivationFrequencyValue => activationFrequency;
    public double Min => activationFrequency.Min();
    public double Max => activationFrequency.Max();
    public double Mean => activationFrequency.Average();
    public double Std => Math.Sqrt(activationFrequency.Variance());
    public double Entropy => BinaryEntropy(activationFrequency) / BinaryEntropy(new List<double> { Mean });

    private List<double> activationFrequency;
    private bool alwaysExponential;

    public ActivationFrequency(SparseDistributedRepresentation dataSource, int period, double initialValue = -1)
        : base(dataSource, period)
    {
        Initialize(dataSource.size_, initialValue);
    }

    public ActivationFrequency(int[] dimensions, int period, double initialValue = -1)
        : base(dimensions, period)
    {
        Initialize(dimensions.Aggregate((size, dim) => size * dim), initialValue);
    }

    private void Initialize(int size, double initialValue)
    {
        if (initialValue == -1)
        {
            activationFrequency = Enumerable.Repeat(1234.567, (int)size).ToList();
            alwaysExponential = false;
        }
        else
        {
            activationFrequency = Enumerable.Repeat(initialValue, (int)size).ToList();
            alwaysExponential = true;
        }
    }

    protected override void Callback(SparseDistributedRepresentation dataSource, double alpha)
    {
        if (alwaysExponential)
        {
            alpha = 1.0 / Period;
        }

        var decay = 1.0 - alpha;
        for (int i = 0; i < activationFrequency.Count; i++)
        {
            activationFrequency[i] *= decay;
        }

        var sparse = dataSource.GetSparse();
        foreach (var idx in sparse)
        {
            activationFrequency[idx] += alpha;
        }
    }

    private double BinaryEntropy(List<double> frequencies)
    {
        double accumulator = 0.0;
        foreach (var p in frequencies)
        {
            var p_ = 1.0 - p;
            var e = -p * Math.Log(p, 2) - p_ * Math.Log(p_, 2);
            accumulator += e;
        }
        return accumulator;
    }

    public override string ToString()
    {
        return $"ActivationFrequency Min/Mean/Std/Max/Entropy {Min} / {Mean} / {Std} / {Max} / {Entropy}";
    }
}

public class Overlap : MetricsHelper
{

    private double overlap;
    private double min;
    private double max;
    private double mean;
    private double variance;
    private bool PreviousValid { get; set; }

    private SparseDistributedRepresentation Previous { get; set; }

    public Overlap(SparseDistributedRepresentation dataSource, int period) 
        : base(dataSource, period)
    {
        Previous = new SparseDistributedRepresentation(dataSource.dimensions_);
        Initialize();
    }

    public Overlap(int[] dimensions, int period) : base(dimensions, period)
    {
        Previous = new SparseDistributedRepresentation(dimensions);
        Initialize();
    }

    private void Initialize()
    {
        overlap = double.NaN;
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;
        mean = 1234.567;
        variance = 1234.567;
        Reset();
    }

    public void Reset()
    {
        PreviousValid = false;
    }

    protected override void Callback(SparseDistributedRepresentation dataSource, double alpha)
    {
        if (!PreviousValid)
        {
            Previous.SetSDR(dataSource);
            PreviousValid = true;
            // It takes two data samples to compute overlap so decrement the
            // samples counter & return & wait for the next sample.
            Samples -= 1;
            overlap = float.NaN;
            return;
        }

        var nbits = Math.Max(Previous.GetSum(), dataSource.GetSum());
        var rawOverlap = Previous.GetOverlap(dataSource);
        overlap = (nbits == 0u) ? 1.0f : (float)rawOverlap / nbits;
        min = Math.Min(min, overlap);
        max = Math.Max(max, overlap);

        // http://people.ds.cam.ac.uk/fanf2/hermes/doc/antiforgery/stats.pdf
        // See section 9.
        var diff = overlap - mean;
        var incr = alpha * diff;
        mean += incr;
        variance = (1.0f - alpha) * (variance + diff * incr);

        Previous.SetSDR(dataSource);
    }

    public double OverlapValue => overlap;
    public double Min => min;
    public double Max => max;
    public double Mean => mean;
    public double Std => Math.Sqrt(variance);

    public override string ToString()
    {
        return $"Overlap Min/Mean/Std/Max {Min} / {Mean} / {Std} / {Max}";
    }
}

public abstract class MetricsHelper
{
    protected int[] Dimensions { get; }
    protected int Period { get; }
    protected int Samples { get; set; }
    protected SparseDistributedRepresentation DataSource { get; set; }
    protected Action callback_handle { get; }
    protected Action destroyCallback_handle { get; }

    protected MetricsHelper(SparseDistributedRepresentation dataSource, int period)
        : this(dataSource.dimensions_, period)
    {
        DataSource = dataSource;

        this.callback_handle = dataSource.AddCallback(() =>
        {
            Callback(dataSource, 1.0f / Math.Min(Period, ++Samples));
        });
        this.destroyCallback_handle = dataSource.AddDestroyCallback(() =>
        {
            Deconstruct();
        });
    }

    protected MetricsHelper(int[] dimensions, int period)
    {
        if (period <= 0)
            throw new ArgumentException("Period must be greater than 0.");
        if (dimensions.Length == 0)
            throw new ArgumentException("Dimensions array must not be empty.");

        this.Dimensions = dimensions;
        this.Period = period;
        this.Samples = 0;
        this.DataSource = null;
        this.callback_handle = null;
        this.destroyCallback_handle = null;
    }

    ~MetricsHelper()
    {
        Deconstruct();
    }

    public void AddData(SparseDistributedRepresentation data)
    {
        if (data == null)
        {
            throw new Exception("Method addData can only be called if this metric was NOT initialize with an SDR!");
        }

        if (DataSource != null)
            throw new InvalidOperationException("Method AddData can only be called if this metric was not initialized with an SDR.");
        if (!Enumerable.SequenceEqual(Dimensions, data.dimensions_))
            throw new ArgumentException("SDR dimensions do not match.");

        Callback(data, 1.0f / Math.Min(Period, ++Samples));
    }

    protected abstract void Callback(SparseDistributedRepresentation dataSource, double alpha);

    protected virtual void Deconstruct()
    {
        if (DataSource != null)
        {
            DataSource.RemoveCallback(callback_handle);
            DataSource.RemoveDestroyCallback(destroyCallback_handle);
            DataSource = null;
        }
    }
}

/**
 * SparseDistributedRepresentation class
 * Also known as "SDR" class
 *
 * Description:
 * This class manages the specification and momentary value of a Sparse
 * Distributed Representation (SDR). An SDR is a group of boolean values which
 * represent the state of a group of neurons or their associated processes.
 *
 * This class automatically converts between the commonly used SDR data formats,
 * which are dense, sparse, and coordinates. Converted values are cached by
 * this class, so getting a value in one format many times incurs no extra
 * performance cost. Assigning to the SDR via a setter method will clear these
 * cached values and cause them to be recomputed as needed.
 *
 * Dense Format: A contiguous array of boolean values, representing all of
 * the bits in the SDR. This format allows random-access queries of the SDR's
 * values.
 *
 * Sparse Index Format: Contains the indices of only the true values in
 * the SDR. This is a list of the indices, indexed into the flattened SDR.
 * This format allows for quickly accessing all of the true bits in the SDR.
 *
 * Coordinate Format: Contains the indices of only the true values in the
 * SDR. This is a list of lists: the outer list contains an entry for each
 * dimension in the SDR. The inner lists contain the coordinates of each true
 * bit in that dimension. The inner lists run in parallel. This format is
 * useful because it contains the location of each true bit inside of the
 * SDR's dimensional space.
 *
 * Array Memory Layout: This class uses C-order throughout, meaning that when
 * iterating through the SDR, the last/right-most index changes fastest.
 *
 * Example usage:
 *
 * // Make an SDR with 9 values, arranged in a (3 x 3) grid.
 * // "SDR" is an alias/typedef for SparseDistributedRepresentation.
 * SDR X = new SDR(new List<uint> { 3, 3 });
 *
 * // These three statements are equivalent.
 * X.SetDense(new ElemDense[] { 0, 1, 0, 0, 1, 0, 0, 0, 1 });
 * X.SetSparse(new ElemSparse[] { 1, 4, 8 });
 * X.SetCoordinates(new List<List<uint>> { new List<uint> { 0, 1, 2 }, new List<uint> { 1, 1, 2 } });
 *
 * // Access data in any format, SDR will automatically convert data formats.
 * X.GetDense();       // [0, 1, 0, 0, 1, 0, 0, 0, 1]
 * X.GetCoordinates(); // [[0, 1, 2], [1, 1, 2]]
 * X.GetSparse();      // [1, 4, 8]
 *
 * // Data format conversions are cached, and when an SDR value changes, the
 * // cache is cleared.
 * X.SetSparse(new ElemSparse[] { });  // Assign new data to the SDR, clearing the cache.
 * X.GetDense();       // [0, 0, 0, 0, 0, 0, 0, 0, 0]
 * X.GetCoordinates(); // []
 * X.GetSparse();      // []
 *
 * // The SDR can be passed by reference, so that it can be changed directly.
 * // The cache will be cleared.
 * X.GetSparse(out var sparse);  // sparse = []
 * sparse.Add(5);
 * X.GetSparse();                // [5]
 *
 * // Callbacks can be added, so that they will be called when the value of the
 * // SDR changes.
 * void Callback1() { Console.WriteLine("Callback 1"); }
 * void Callback2() { Console.WriteLine("Callback 2"); }
 * X.AddCallback(Callback1);
 * X.AddCallback(Callback2);
 * X.SetSparse(new ElemSparse[] { 0 });
 * // Output:
 * // Callback 1
 * // Callback 2
 */
public class SparseDistributedRepresentation
{
    public int[] dimensions_; // The dimensions of the SDR.
    public int size_; // The total number of elements in the SDR.

    private int[] dense_; // The dense format representation of the SDR.
    private List<int> sparse_; // The sparse index format representation of the SDR.
    private List<List<int>> coordinates_; // The coordinate format representation of the SDR.

    private bool dense_valid; // Flag indicating if the dense format representation is valid.
    private bool sparse_valid; // Flag indicating if the sparse index format representation is valid.
    private bool coordinates_valid; // Flag indicating if the coordinate format representation is valid.

    private List<Action> callbacks; // List of callbacks to be called when the SDR value changes.
    /**
     * These hooks are called when the SDR is destroyed.  These can be NULL
     * pointers!  See methods addDestroyCallback & removeDestroyCallback for API
     * details.
     */
    private List<Action> destroyCallbacks;

    /**
     * Constructor for SparseDistributedRepresentation class.
     * Initializes the dimensions, size, and flags.
     *
     * @param dimensions The dimensions of the SDR.
     */
    public SparseDistributedRepresentation(int[] dimensions)
    {
        dimensions_ = dimensions;
        size_ = CalculateSize(dimensions_);
        dense_ = new int[size_];
        sparse_ = new List<int>();
        coordinates_ = new List<List<int>>();
        dense_valid = false;
        sparse_valid = false;
        coordinates_valid = false;
        callbacks = new List<Action>();
        destroyCallbacks = new List<Action>();
    }

    public SparseDistributedRepresentation(int dimensions)
    : this(new [] { dimensions })
    {
    }

    ~SparseDistributedRepresentation()
    {
        Deconstruct();
    }

    /**
     * Calculates the total number of elements in the SDR based on the dimensions.
     *
     * @param dimensions The dimensions of the SDR.
     * @return The total number of elements in the SDR.
     */
    private int CalculateSize(IList<int> dimensions)
    {
        int size = 1;
        foreach (int dimension in dimensions)
        {
            size *= dimension;
        }
        return size;
    }

    /**
     * Converts the dense format representation of the SDR to the sparse index format.
     */
    private void ConvertDenseToSparse()
    {
        sparse_ = new List<int>();
        for (int i = 0; i < size_; i++)
        {
            if (dense_[(int)i] != 0)
            {
                sparse_.Add(i);
            }
        }
    }

    /**
     * Converts the dense format representation of the SDR to the coordinate format.
     */
    private void ConvertDenseToCoordinates()
    {
        coordinates_ = new List<List<int>>();
        List<int> coordinate = new List<int>(dimensions_.Length);
        for (int i = 0; i < size_; i++)
        {
            if (dense_[(int)i] != 0)
            {
                int index = i;
                for (int j = (int)dimensions_.Length - 1; j >= 0; j--)
                {
                    int dimension = dimensions_[j];
                    int coord = index % dimension;
                    coordinate.Insert(0, coord);
                    index /= dimension;
                }
                coordinates_.Add(new List<int>(coordinate));
                coordinate.Clear();
            }
        }
    }

    /**
     * Converts the sparse index format representation of the SDR to the dense format.
     */
    private void ConvertSparseToDense()
    {
        dense_ = new int[size_];
        for (int i = 0; i < size_; i++)
        {
            dense_[i] = 0;
        }
        foreach (int index in sparse_)
        {
            dense_[index] = 1;
        }
    }

    /**
     * Converts the sparse index format representation of the SDR to the coordinate format.
     */
    private void ConvertSparseToCoordinates()
    {
        coordinates_ = new List<List<int>>();
        List<int> coordinate = new List<int>(dimensions_.Length);
        foreach (int index in sparse_)
        {
            int remaining = index;
            for (int j = (int)dimensions_.Length - 1; j >= 0; j--)
            {
                int dimension = dimensions_[j];
                int coord = remaining % dimension;
                coordinate.Insert(0, coord);
                remaining /= dimension;
            }
            coordinates_.Add(new List<int>(coordinate));
            coordinate.Clear();
        }
    }

    /**
     * Converts the coordinate format representation of the SDR to the dense format.
     */
    private void ConvertCoordinatesToDense()
    {
        dense_ = new int[size_];
        for (uint i = 0; i < size_; i++)
        {
            dense_[i] = 0;
        }
        foreach (List<int> coordinate in coordinates_)
        {
            int index = 0;
            for (int j = 0; j < dimensions_.Length; j++)
            {
                int dimension = dimensions_[j];
                int coord = coordinate[j];
                index = index * dimension + coord;
            }
            dense_[index] = 1;
        }
    }

    /**
     * Converts the coordinate format representation of the SDR to the sparse index format.
     */
    private void ConvertCoordinatesToSparse()
    {
        sparse_ = new List<int>();
        foreach (List<int> coordinate in coordinates_)
        {
            int index = 0;
            for (int j = 0; j < dimensions_.Length; j++)
            {
                int dimension = dimensions_[j];
                int coord = coordinate[j];
                index = index * dimension + coord;
            }
            sparse_.Add(index);
        }
    }

    /**
     * Clears the cached values and invalidates the flags.
     */
    private void ClearCache()
    {
        dense_valid = false;
        sparse_valid = false;
        coordinates_valid = false;
    }

    /**
     * Sets the value of the SDR in the dense format.
     *
     * @param dense The dense format representation of the SDR.
     */
    public void SetDense(int[] dense)
    {
        dense_ = new List<int>(dense).ToArray();
        ClearCache();
        dense_valid = true;
        InvokeCallbacks();
    }

    /**
     * Sets the value of the SDR in the sparse index format.
     *
     * @param sparse The sparse index format representation of the SDR.
     */
    public SparseDistributedRepresentation SetSparse(int[] sparse)
    {
        sparse_ = new List<int>(sparse);
        ClearCache();
        sparse_valid = true;
        InvokeCallbacks();
        return this;
    }

    /**
     * Sets the value of the SDR in the coordinate format.
     *
     * @param coordinates The coordinate format representation of the SDR.
     */
    public void SetCoordinates(List<List<int>> coordinates)
    {
        coordinates_ = new List<List<int>>(coordinates);
        coordinates_valid = true;
        ClearCache();
        InvokeCallbacks();
    }

    /**
     * Retrieves the value of the SDR in the dense format.
     *
     * @return The dense format representation of the SDR.
     */
    public int[] GetDense()
    {
        if (!dense_valid)
        {
            if (sparse_valid)
            {
                ConvertSparseToDense();
            }
            else if (coordinates_valid)
            {
                ConvertCoordinatesToDense();
            }
            dense_valid = true;
        }
        return dense_;
    }

    /**
     * Retrieves the value of the SDR in the sparse index format.
     *
     * @param sparse The sparse index format representation of the SDR.
     */
    public int[] GetSparse()
    {
        if (!sparse_valid)
        {
            if (dense_valid)
            {
                ConvertDenseToSparse();
            }
            else if (coordinates_valid)
            {
                ConvertCoordinatesToSparse();
            }
            sparse_valid = true;
        }
        return sparse_.ToArray();
    }

    /**
     * Retrieves the value of the SDR in the coordinate format.
     *
     * @return The coordinate format representation of the SDR.
     */
    public List<List<int>> GetCoordinates()
    {
        if (!coordinates_valid)
        {
            if (dense_valid)
            {
                ConvertDenseToCoordinates();
            }
            else if (sparse_valid)
            {
                ConvertSparseToCoordinates();
            }
            coordinates_valid = true;
        }
        return new List<List<int>>(coordinates_);
    }

    /**
     * Adds a callback function to be called when the value of the SDR changes.
     *
     * @param callback The callback function to be added.
     */
    public Action AddCallback(Action callback)
    {
        callbacks.Add(callback);
        return callback;
    }

    public void RemoveCallback(Action callback)
    {
        callbacks.Remove(callback);
    }

    /**
     * Adds a callback function to be called when the value of the SDR changes.
     *
     * @param callback The callback function to be added.
     */
    public Action AddDestroyCallback(Action callback)
    {
        destroyCallbacks.Add(callback);
        return callback;
    }

    public void RemoveDestroyCallback(Action callback)
    {
        destroyCallbacks.Remove(callback);
    }

    /**
     * Invokes all the registered callbacks.
     */
    private void InvokeCallbacks()
    {
        foreach (Action callback in callbacks)
        {
            callback.Invoke();
        }
    }

    public void Deconstruct()
    {
        size_ = 0;
        dimensions_ = new int[dimensions_.Length];
        foreach (var destroyCallback in destroyCallbacks)
        {
            if (destroyCallback != null)
            {
                destroyCallback();
            }
        }
        callbacks.Clear();
        destroyCallbacks.Clear();
    }

    public SparseDistributedRepresentation Concatenate(List<SparseDistributedRepresentation> inputs, int axis = 0)
    {
        // Check inputs.
        if (inputs.Count < 2)
            throw new Exception($"Not enough inputs to SDR::Concatenate, need at least 2 SDRs, got {inputs.Count}!");

        if (axis >= dimensions_.Length)
            throw new Exception("Invalid axis value.");

        int concatAxisSize = 0;
        foreach (var sdr in inputs)
        {
            if (sdr == null)
                throw new Exception("Input SDR is null.");

            if (sdr.dimensions_.Length != dimensions_.Length)
                throw new Exception("All inputs to SDR::Concatenate must have the same number of dimensions as the output SDR!");

            for (int dim = 0; dim < dimensions_.Length; dim++)
            {
                if (dim == axis)
                    concatAxisSize += sdr.dimensions_[axis];
                else if (sdr.dimensions_[dim] != dimensions_[dim])
                    throw new Exception("All dimensions except the axis must be the same!");
            }
        }

        if (concatAxisSize != dimensions_[axis])
            throw new Exception($"Axis of concatenation dimensions do not match, inputs sum to {concatAxisSize}, output expects {dimensions_[axis]}!");

        // Setup for copying the data as rows & strides.
        List<int[]> buffers = new List<int[]>();
        List<int> rowLengths = new List<int>();
        foreach (var sdr in inputs)
        {
            buffers.Add(sdr.GetDense().ToArray());

            int row = 1;
            for (int d = axis; d < dimensions_.Length; ++d)
                row *= sdr.dimensions_[d];
            rowLengths.Add(row);
        }

        // Get the output buffer.
        dense_ = new int[size_];
        var denseData = dense_;
        var dataEnd = denseData.Length;
        var nInputs = inputs.Count;
        int denseIndex = 0;

        while (denseIndex < dataEnd)
        {
            // Copy one row from each input SDR.
            for (int i = 0; i < nInputs; ++i)
            {
                var buf = buffers[i];
                var row = rowLengths[i];

                Array.Copy(buf, 0, denseData, denseIndex, row);

                // Increment the index.
                denseIndex += row;
            }
        }

        SetDenseInplace();

        return this;
    }

    public int GetSum()
    {
        return GetSparse().Length;
    }

    public double GetSparsity()
    {
        return GetSum() / (double)size_;
    }

    public void SetSDR(SparseDistributedRepresentation value)
    {
        Reshape(value.dimensions_);
        // Cast the data to CONST, which forces the SDR to copy the vector
        // instead of swapping it with its current data vector. This protects
        // the input SDR from being changed.
        var copyDontSwap = value.GetSparse();
        SetSparse(copyDontSwap);
    }

    public uint GetOverlap(SparseDistributedRepresentation sdr)
    {
        Debug.Assert(dimensions_.SequenceEqual(sdr.dimensions_));

        uint overlap = 0u;
        int[] aSparse = GetSparse();
        int[] bSparse = sdr.GetSparse();
        long aIdx = 0l;
        long bIdx = 0l;
        while (aIdx < aSparse.Length && bIdx < bSparse.Length)
        {
            int a = aSparse[(int)aIdx];
            int b = bSparse[(int)bIdx];
            if (a == b)
            {
                overlap += 1u;
                aIdx += 1u;
                bIdx += 1u;
            }
            else if (a > b)
            {
                bIdx += 1u;
            }
            else
            {
                aIdx += 1u;
            }
        }
        return overlap;
    }

    public void Reshape(int[] dimensions)
    {
        // Make sure we have the data in a format which does not care about the
        // dimensions, IE: dense or sparse but not coordinates
        if (!dense_valid && !sparse_valid)
            GetSparse();
        coordinates_valid = false;
        coordinates_.Clear();
        for (int i = 0; i < dimensions.Length; i++)
        {
            coordinates_.Add(new List<int>());
        }
        dimensions_ = dimensions;
        // Re-Calculate the SDR's size and check that it did not change.
        int newSize = dimensions.Aggregate(1, (current, dimension) => current * dimension);
        if (newSize != size_)
            throw new Exception("SDR.Reshape changed the size of the SDR!");
    }

    public void SetDenseInplace()
    {
        // Check data is valid.
        if (dense_.Length != size_)
            throw new Exception("Invalid dense data size.");

        // Set the valid flags.
        ClearCache();
        dense_valid = true;
        InvokeCallbacks();
    }

    public SparseDistributedRepresentation Concatenate(params int[][] values)
    {
        List< SparseDistributedRepresentation > sdrs = new List<SparseDistributedRepresentation>();
        foreach (int[] value in values)
        {
            SparseDistributedRepresentation sdr = new SparseDistributedRepresentation(new []{value.Length});
            sdr.SetDense(value);
            sdrs.Add(sdr);
        }

        return Concatenate(sdrs);
    }
}

public static class SparseDistributedRepresentationExtensions
{
    public static SparseDistributedRepresentation ToSdr(this IEnumerable<int> values, int[] dimensions)
    {
        int[] array = values.ToArray();
        var sdr = new SparseDistributedRepresentation(dimensions);
        sdr.SetSparse(array);
        return sdr;
    }
}