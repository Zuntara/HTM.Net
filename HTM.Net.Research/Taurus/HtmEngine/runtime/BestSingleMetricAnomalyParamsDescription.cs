using System;
using System.Diagnostics;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Taurus.HtmEngine.runtime
{
    public class BestSingleMetricAnomalyParamsDescription : BaseDescription
    {

        public BestSingleMetricAnomalyParamsDescription()
        {
            control = new ControlModelDescription
            {
                inferenceArgs = new InferenceArgsDescription
                {
                    predictionSteps = new[] { 1 },
                    predictedField = "c1",
                    inputPredictedField = InputPredictedField.Auto
                }
            };

            var config = new ConfigModelDescription
            {
                // Type of model that the rest of these parameters apply to.
                model = "CLA",

                // Version that specifies the format of the config.
                version = 1,

                // Intermediate variables used to compute fields in modelParams and also
                // referenced from the control section.
                aggregationInfo = new AggregationSettings
                {
                    days = 0,
                    fields = new Map<string, object>(),
                    hours = 0,
                    microseconds = 0,
                    milliseconds = 0,
                    minutes = 0,
                    months = 0,
                    seconds = 0,
                    weeks = 0,
                    years = 0
                },

                predictAheadTime = null,

                // Model parameter dictionary.
                modelParams = new ModelParamsDescription
                {
                    // The type of inference that this model will perform
                    inferenceType = InferenceType.TemporalAnomaly,

                    sensorParams = new SensorParamsDescription
                    {
                        // Sensor diagnostic output verbosity control;
                        // if > 0: sensor region will print out on screen what it"s sensing
                        // at each step 0: silent; >=1: some info; >=2: more info;
                        // >=3: even more info (see compute() in py/regions/RecordSensor.py)
                        verbosity = 0,

                        // Example:
                        //     dsEncoderSchema = [
                        //       DeferredDictLookup("__field_name_encoder"),
                        //     ],
                        //
                        // (value generated from DS_ENCODER_SCHEMA)
                        encoders = new EncoderSettingsList
                        {
                            {
                                "c0_timeOfDay", new EncoderSetting
                                {
                                    dayOfWeek= new Tuple(21, 9.49),
                                    fieldName= "c0",
                                    name= "c0",
                                    type= "DateEncoder"
                                }
                            },
                            {
                                "c0_dayOfWeek", null
                            },
                            {
                                "c0_weekend", null
                            },
                            {
                                "c1", new EncoderSetting
                                {
                                    fieldName= "c1",
                                    name= "c1",
                                    type= "RandomDistributedScalarEncoder",
                                    numBuckets= 130.0
                                }
                            }
                        },

                        // A dictionary specifying the period for automatically-generated
                        // resets from a RecordSensor;
                        //
                        // None = disable automatically-generated resets (also disabled if
                        // all of the specified values evaluate to 0).
                        // Valid keys is the desired combination of the following:
                        //   days, hours, minutes, seconds, milliseconds, microseconds, weeks
                        //
                        // Example for 1.5 days: sensorAutoReset = dict(days=1,hours=12),
                        //
                        // (value generated from SENSOR_AUTO_RESET)
                        sensorAutoReset = null,
                    },

                    spEnable = true,

                    spParams = new SpatialParamsDescription
                    {
                        // SP diagnostic output verbosity control;
                        // 0: silent; >=1: some info; >=2: more info;
                        spVerbosity = 0,

                        globalInhibition = true,

                        // Number of cell columns in the cortical region (same number for
                        // SP and TP)
                        // (see also tpNCellsPerCol)
                        columnCount = new int[] { 2048 },

                        inputWidth = new int[] { 0 },

                        // SP inhibition control (absolute value);
                        // Maximum number of active columns in the SP region"s output (when
                        // there are more, the weaker ones are suppressed)
                        numActiveColumnsPerInhArea = 40.0,

                        seed = 1956,

                        // potentialPct
                        // What percent of the columns"s receptive field is available
                        // for potential synapses. At initialization time, we will
                        // choose potentialPct * (2*potentialRadius+1)^2
                        potentialPct = 0.8,

                        // The default connected threshold. Any synapse whose
                        // permanence value is above the connected threshold is
                        // a "connected synapse", meaning it can contribute to the
                        // cell"s firing. Typical value is 0.10. Cells whose activity
                        // level before inhibition falls below minDutyCycleBeforeInh
                        // will have their own internal synPermConnectedCell
                        // threshold set below this default value.
                        // (This concept applies to both SP and TP and so "cells"
                        // is correct here as opposed to "columns")
                        synPermConnected = 0.2,

                        synPermActiveInc = 0.003,

                        synPermInactiveDec = 0.0005,

                        maxBoost = 1.0
                    },

                    // Controls whether TP is enabled or disabled;
                    // TP is necessary for making temporal predictions, such as predicting
                    // the next inputs.  Without TP, the model is only capable of
                    // reconstructing missing sensor inputs (via SP).
                    tpEnable = true,

                    tpParams = new TemporalParamsDescription
                    {
                        // TP diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        // (see verbosity in nupic/trunk/py/nupic/research/TP.py and TP10X*.py)
                        verbosity = 0,

                        // Number of cell columns in the cortical region (same number for
                        // SP and TP)
                        // (see also tpNCellsPerCol)
                        columnCount = new[] { 2048 },

                        // The number of cells (i.e., states), allocated per column.
                        cellsPerColumn = 32,

                        inputWidth = new[] { 2048 },

                        seed = 1960,

                        // Temporal Pooler implementation selector (see _getTPClass in
                        // CLARegion.py).
                        temporalImp = "cpp",

                        // New Synapse formation count
                        // NOTE: If None, use spNumActivePerInhArea
                        //
                        // TODO: need better explanation
                        newSynapseCount = 20,

                        // Maximum number of synapses per segment
                        //  > 0 for fixed-size CLA
                        // -1 for non-fixed-size CLA
                        //
                        // TODO: for Ron: once the appropriate value is placed in TP
                        // constructor, see if we should eliminate this parameter from
                        // description.py.
                        maxSynapsesPerSegment = 32,

                        // Maximum number of segments per cell
                        //  > 0 for fixed-size CLA
                        // -1 for non-fixed-size CLA
                        //
                        // TODO: for Ron: once the appropriate value is placed in TP
                        // constructor, see if we should eliminate this parameter from
                        // description.py.
                        maxSegmentsPerCell = 128,

                        // Initial Permanence
                        // TODO: need better explanation
                        initialPerm = 0.21,

                        // Permanence Increment
                        permanenceInc = 0.1,

                        // Permanence Decrement
                        // If set to None, will automatically default to tpPermanenceInc
                        // value.
                        permanenceDec = 0.1,

                        globalDecay = 0.0,

                        maxAge = 0,

                        // Minimum number of active synapses for a segment to be considered
                        // during search for the best-matching segments.
                        // None=use default
                        // Replaces: tpMinThreshold
                        minThreshold = 10,

                        // Segment activation threshold.
                        // A segment is active if it has >= tpSegmentActivationThreshold
                        // connected synapses that are active due to infActiveState
                        // None=use default
                        // Replaces: tpActivationThreshold
                        activationThreshold = 13,

                        outputType = "normal",

                        // "Pay Attention Mode" length. This tells the TP how many new
                        // elements to append to the end of a learned sequence at a time.
                        // Smaller values are better for datasets with short sequences,
                        // higher values are better for datasets with long sequences.
                        pamLength = 3,
                    },

                    clEnable = false,

                    clParams = new ClassifierParamsDescription
                    {
                        regionName = typeof(CLAClassifier).AssemblyQualifiedName,// "CLAClassifierRegion",

                        // Classifier diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        verbosity = 0,

                        // This controls how fast the classifier learns/forgets. Higher values
                        // make it adapt faster and forget older patterns faster.
                        alpha = 0.035828933612157998,

                        // This is set after the call to updateConfigFromSubConfig and is
                        // computed from the aggregationInfo and predictAheadTime.
                        steps = new[] { 1 },
                    },
                    anomalyParams = new AnomalyParamsDescription
                    {
                        anomalyCacheRecords = null,
                        autoDetectThreshold = null,
                        autoDetectWaitRecords = 5030
                    },
                    trainSPNetOnlyIfRequested = false,
                }
            };
            // end of config dictionary

            // Adjust base config dictionary for any modifications if imported from a
            // sub-experiment
            UpdateConfigFromSubConfig(config);
            modelConfig = config;

            // Compute predictionSteps based on the predictAheadTime and the aggregation
            // period, which may be permuted over.
            if (config.predictAheadTime != null)
            {
                int predictionSteps = (int)Math.Round(Utils.aggregationDivide(config.predictAheadTime, config.aggregationInfo));
                Debug.Assert(predictionSteps >= 1);
                config.modelParams.clParams.steps = new[] { predictionSteps };
            }


        }

        public void UpdateConfigFromSubConfig(ConfigModelDescription config)
        {

        }

        public override Network.Network BuildNetwork()
        {
            throw new NotImplementedException();
        }

        public override Parameters GetParameters()
        {
            Parameters p = Parameters.GetAllDefaultParameters();

            // Spatial pooling parameters
            SpatialParamsDescription spParams = modelConfig.modelParams.spParams;
            TemporalParamsDescription tpParams = modelConfig.modelParams.tpParams;

            Parameters.ApplyParametersFromDescription(spParams, p);
            Parameters.ApplyParametersFromDescription(tpParams, p);

            return p;
        }

        public static BestSingleMetricAnomalyParamsDescription BestSingleMetricAnomalyParams
        {
            get { return new BestSingleMetricAnomalyParamsDescription(); }
        }
    }
}