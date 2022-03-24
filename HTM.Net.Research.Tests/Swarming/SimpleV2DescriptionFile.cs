using System;
using System.Collections.Generic;
using System.Diagnostics;
using HTM.Net.Algorithms;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Tests.Swarming
{
    public class SimpleV2DescriptionFile : DescriptionBase
    {
        public SimpleV2DescriptionFile()
        {
            var config = new DescriptionConfigModel
            {
                // Type of model that the rest of these parameters apply to.
                model = "CLA",

                // Version that specifies the format of the config.
                version = 1,

                // Intermediate variables used to compute fields in modelParams and also
                // referenced from the control section.
                aggregationInfo = new Map<string, object>
                {
                    {"days", 0},
                    {
                        "fields", new Map<string, object>
                        {
                            {"timestamp", "first"},
                            {"gym", "first"},
                            {"consumption", "mean"},
                            {"address", "first"}
                        }
                    },
                    {"hours", 0},
                    {"microseconds", 0},
                    {"milliseconds", 0},
                    {"minutes", 0},
                    {"months", 0},
                    {"seconds", 0},
                    {"weeks", 0},
                    {"years", 0}
                },

                predictAheadTime = null,

                // Model parameter dictionary.
                modelParams = new ModelDescriptionParamsDescrModel
                {
                    // The type of inference that this model will perform
                    inferenceType = InferenceType.TemporalNextStep,

                    sensorParams = new SensorParamsDescrModel
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
                        encoders = new Map<string, Map<string, object>>
                        {
                            {
                                "address", new Map<string, object>
                                {
                                    {"fieldname", "address"},
                                    {"n", 300},
                                    {"name", "address"},
                                    {"type", "SDRCategoryEncoder"},
                                    {"w", 21},
                                    {"categoryList", new List<string>() }
                                }
                            },
                            {
                                "consumption", new Map<string, object>
                                {
                                    {"clipInput", true},
                                    {"fieldname", "consumption"},
                                    {"maxval", 200},
                                    {"minval", 0},
                                    {"n", 1500},
                                    {"name", "consumption"},
                                    {"type", "ScalarEncoder"},
                                    {"w", 21}
                                }
                            },
                            {
                                "gym", new Map<string, object>
                                {
                                    {"fieldname", "gym"},
                                    {"n", 300},
                                    {"name", "gym"},
                                    {"type", "SDRCategoryEncoder"},
                                    {"w", 21},
                                    {"categoryList", new List<string>() }
                                }
                            },
                            {
                                "timestamp_dayOfWeek", new Map<string, object>
                                {
                                    {"dayOfWeek", new Tuple(7, 3)},
                                    {"fieldname", "timestamp"},
                                    {"name", "timestamp_dayOfWeek"},
                                    {"type", "DateEncoder"}
                                }
                            },
                            {
                                "timestamp_timeOfDay", new Map<string, object>
                                {
                                    {"fieldname", "timestamp"},
                                    {"name", "timestamp_timeOfDay"},
                                    {"timeOfDay", new Tuple(7, 8)},
                                    {"type", "DateEncoder"}
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

                    spParams = new SpatialParamsDescr
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
                        potentialPct = 0.5,

                        // The default connected threshold. Any synapse whose
                        // permanence value is above the connected threshold is
                        // a "connected synapse", meaning it can contribute to the
                        // cell"s firing. Typical value is 0.10. Cells whose activity
                        // level before inhibition falls below minDutyCycleBeforeInh
                        // will have their own internal synPermConnectedCell
                        // threshold set below this default value.
                        // (This concept applies to both SP and TP and so "cells"
                        // is correct here as opposed to "columns")
                        synPermConnected = 0.1,

                        synPermActiveInc = 0.1,

                        synPermInactiveDec = 0.01,
                    },

                    // Controls whether TP is enabled or disabled;
                    // TP is necessary for making temporal predictions, such as predicting
                    // the next inputs.  Without TP, the model is only capable of
                    // reconstructing missing sensor inputs (via SP).
                    tpEnable = true,

                    tpParams = new TemporalParamsDescr
                    {
                        // TP diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        // (see verbosity in nupic/trunk/py/nupic/research/TP.py and TP10X*.py)
                        //verbosity = 0,

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
                        newSynapseCount = 15,

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
                        minThreshold = 12,

                        // Segment activation threshold.
                        // A segment is active if it has >= tpSegmentActivationThreshold
                        // connected synapses that are active due to infActiveState
                        // None=use default
                        // Replaces: tpActivationThreshold
                        activationThreshold = 16,

                        outputType = "normal",

                        // "Pay Attention Mode" length. This tells the TP how many new
                        // elements to append to the end of a learned sequence at a time.
                        // Smaller values are better for datasets with short sequences,
                        // higher values are better for datasets with long sequences.
                        pamLength = 1,
                    },

                    clEnable = true,

                    clParams = new ClassifierParamsDescr
                    {
                        regionName = typeof(CLAClassifier).AssemblyQualifiedName,// "CLAClassifierRegion",

                        // Classifier diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        clVerbosity = 0,

                        // This controls how fast the classifier learns/forgets. Higher values
                        // make it adapt faster and forget older patterns faster.
                        alpha = 0.001,

                        // This is set after the call to updateConfigFromSubConfig and is
                        // computed from the aggregationInfo and predictAheadTime.
                        steps = 1,
                    },

                    trainSPNetOnlyIfRequested = false,
                }
            };
            // end of config dictionary

            // Adjust base config dictionary for any modifications if imported from a
            // sub-experiment
            updateConfigFromSubConfig(config);
            modelConfig = config;

            // Compute predictionSteps based on the predictAheadTime and the aggregation
            // period, which may be permuted over.
            if (config.predictAheadTime != null)
            {
                int predictionSteps = (int)Math.Round(Utils.aggregationDivide(config.predictAheadTime, config.aggregationInfo));
                Debug.Assert(predictionSteps >= 1);
                config.modelParams.clParams.steps = predictionSteps;
            }

            // Adjust config by applying ValueGetterBase-derived
            // futures. NOTE: this MUST be called after updateConfigFromSubConfig() in order
            // to support value-getter-based substitutions from the sub-experiment (if any)
            //applyValueGettersToContainer(config);

            control = new DescriptionControlModel
            {
                // The environment that the current model is being run in
                environment = "nupic",

                // Input stream specification per py/nupicengine/cluster/database/StreamDef.json.
                //
                dataset = new Map<string, object>
                {
                    { "info", "test_NoProviders"},
                    {"streams", new Map<string, object>
                        {
                            { "columns", new[] {"*"}},
                            {"info", "test data"},
                            { "source", "rec-center-hourly.csv"}
                        }
                    },
                    {"version", 1}
                },


                // Iteration count: maximum number of iterations.  Each iteration corresponds
                // to one record from the (possibly aggregated) dataset.  The task is
                // terminated when either number of iterations reaches iterationCount or
                // all records in the (possibly aggregated) database have been processed,
                // whichever occurs first.
                //
                // iterationCount of -1 = iterate over the entire dataset
                //"iterationCount" : ITERATION_COUNT,

                // Metrics: A list of MetricSpecs that instantiate the metrics that are
                // computed for this experiment
                metrics = new[] { new MetricSpec(field: "consumption", inferenceElement: InferenceElement.Prediction, metric: "rmse") },

                // Logged Metrics: A sequence of regular expressions that specify which of
                // the metrics from the Inference Specifications section MUST be logged for
                // every prediction. The regex"s correspond to the automatically generated
                // metric labels. This is similar to the way the optimization metric is
                // specified in permutations.py.
                loggedMetrics = new[] { ".*nupicScore.*" }
            };
        }

        public void updateConfigFromSubConfig(DescriptionConfigModel config)
        {

        }

        public override Network.Network BuildNetwork()
        {
            Parameters p = GetParameters();

            return null;
        }

        public override Parameters GetParameters()
        {
            Parameters p = Parameters.GetAllDefaultParameters();

            // Spatial pooling parameters
            SpatialParamsDescr spParams = this.modelConfig.modelParams.spParams;
            TemporalParamsDescr tpParams = this.modelConfig.modelParams.tpParams;

            Parameters.ApplyParametersFromDescription(spParams, p);
            Parameters.ApplyParametersFromDescription(tpParams, p);

            return p;
        }

        public override IDescription Clone()
        {
            return new SimpleV2DescriptionFile();
        }
    }
}