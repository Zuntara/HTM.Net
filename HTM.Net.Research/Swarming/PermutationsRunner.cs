using System;
using System.Collections.Generic;
using HTM.Net.Data;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    public static class PermutationsRunner
    {
        /// <summary>
        /// Starts a swarm, given an dictionary configuration.
        /// </summary>
        /// <param name="swarmConfig">{dict} A complete [swarm description](https://github.com/numenta/nupic/wiki/Running-Swarms#the-swarm-description) object.</param>
        /// <param name="options"> </param>
        /// <param name="outDir">Optional path to write swarm details (defaults to current working directory).</param>
        /// <param name="outputLabel">Optional label for output (defaults to "default").</param>
        /// <param name="permWorkDir">Optional location of working directory (defaults to current working directory).</param>
        /// <param name="verbosity">Optional (1,2,3) increasing verbosity of output.</param>
        /// <returns> Model parameters</returns>
        public static object RunWithConfig(SwarmDefinition swarmConfig, object options, string outDir = null, string outputLabel = "default",
            string permWorkDir = null, int verbosity = 1)
        {
            IDescription exp = _generateExpFilesFromSwarmDescription(swarmConfig, outDir);

            return _runAction(exp);
        }

        private static object _runAction(IDescription exp)
        {
            throw new System.NotImplementedException();
        }

        private static IDescription _generateExpFilesFromSwarmDescription(SwarmDefinition swarmConfig, string outDir)
        {
            return new ExpGenerator(swarmConfig).Generate();
        }
    }

    public class ExpGenerator
    {
        public SwarmDefinition Options { get; set; }

        public ExpGenerator(SwarmDefinition definition)
        {
            Options = definition;
        }

        public IDescription Generate()
        {
            // If the user specified nonTemporalClassification, make sure prediction steps is 0
            int[] predictionSteps = Options.inferenceArgs.predictionSteps;
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (predictionSteps != null && predictionSteps[0] != 0)
                {
                    throw new InvalidOperationException(
                        "When NontemporalClassification is used, prediction steps must be [0]");
                }
            }
            // If the user asked for 0 steps of prediction, then make this a spatial classification experiment
            if (predictionSteps != null && predictionSteps[0] == 0
                &&
                new List<InferenceType>
                {
                    InferenceType.NontemporalMultiStep,
                    InferenceType.TemporalMultiStep,
                    InferenceType.MultiStep
                }.Contains(Options.inferenceType))
            {
                Options.inferenceType = InferenceType.NontemporalClassification;
            }
            // If NontemporalClassification was chosen as the inferenceType, then the
            // predicted field can NOT be used as an input
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (Options.inferenceArgs.inputPredictedField.HasValue &&
                    (Options.inferenceArgs.inputPredictedField.Value == SwarmDefinition.InputPredictedField.yes ||
                     Options.inferenceArgs.inputPredictedField.Value == SwarmDefinition.InputPredictedField.auto))
                {
                    throw new InvalidOperationException(
                        "When the inference type is NontemporalClassification  inputPredictedField must be set to 'no'");
                }
                Options.inferenceArgs.inputPredictedField = SwarmDefinition.InputPredictedField.no;
            }

            // Process the swarmSize setting, if provided
            var swarmSize = Options.swarmSize;

            if (swarmSize == null)
            {
                if (Options.inferenceArgs.inputPredictedField == null)
                {
                    Options.inferenceArgs.inputPredictedField = SwarmDefinition.InputPredictedField.auto;
                }
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Small)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 3;
                if (Options.iterationCount == null)
                    Options.iterationCount = 100;
                if (Options.maxModels == null)
                    Options.maxModels = 1;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = SwarmDefinition.InputPredictedField.yes;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Medium)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 5;
                if (Options.iterationCount == null)
                    Options.iterationCount = 4000;
                if (Options.maxModels == null)
                    Options.maxModels = 200;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = SwarmDefinition.InputPredictedField.auto;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Large)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 15;
                Options.tryAll3FieldCombinationsWTimestamps = true;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = SwarmDefinition.InputPredictedField.auto;
            }

            // Get token replacements
            object tokenReplacements = new object();

            // Generate the encoder related substitution strings

            var includedFields = Options.includedFields;

            var encoders = _generateEncoderStringsV2(includedFields, Options);

            // Generate the string containing the sensor auto-reset dict.
            /*
              if options['resetPeriod'] is not None:
                sensorAutoResetStr = pprint.pformat(options['resetPeriod'],
                                                     indent=2*_INDENT_STEP)
              else:
                sensorAutoResetStr = 'None'
            */

            // Generate the string containing the aggregation settings.

            var aggregationPeriod = new AggregationSettings();

            // Honor any overrides provided in the stream definition
            if (Options.streamDef.aggregation != null)
            {
                aggregationPeriod = Options.streamDef.aggregation;

                aggregationPeriod.fields = Options.streamDef.aggregation.fields;
            }
            // Do we have any aggregation at all?
            bool hasAggregation = aggregationPeriod.AboveZero();


            throw new NotImplementedException(
                "https://github.com/numenta/nupic/blob/master/src/nupic/swarming/exp_generator/ExpGenerator.py line 1261 continue");

            ExperimentDescription descr = new ExperimentDescription();



            return descr;
        }

        private List<Map<string, object>> _generateEncoderStringsV2(
            List<SwarmDefinition.SwarmDefIncludedField> includedFields, SwarmDefinition options)
        {
            int width = 21;
            List<Map<string, object>> encoderDictList = new List<Map<string, object>>();

            string classifierOnlyField = null;

            // If this is a NontemporalClassification experiment, then the
            // the "predicted" field (the classification value) should be marked to ONLY 
            // go to the classifier
            if (new List<InferenceType>
            {
                InferenceType.NontemporalMultiStep,
                InferenceType.TemporalMultiStep,
                InferenceType.MultiStep
            }.Contains(options.inferenceType))
            {
                classifierOnlyField = options.inferenceArgs.predictedField;
            }

            // ==========================================================================
            // For each field, generate the default encoding dict and PermuteEncoder
            // constructor arguments
            foreach (var fieldInfo in includedFields)
            {
                var encoderDict = new Map<string, object>();
                string fieldName = fieldInfo.fieldName;
                string fieldType = fieldInfo.fieldType;

                // scalar?
                if (fieldType == "float" || fieldType == "int")
                {
                    // n=100 is reasonably hardcoded value for n when used by description.py
                    // The swarming will use PermuteEncoder below, where n is variable and
                    // depends on w
                    bool runDelta = fieldInfo.runDelta.GetValueOrDefault();
                    if (runDelta || !string.IsNullOrWhiteSpace(fieldInfo.space))
                    {
                        encoderDict = new Map<string, object>
                        {
                            {"type", "ScalarSpaceEncoder"},
                            {"name", fieldName},
                            {"fieldName", fieldName},
                            {"n", 100},
                            {"w", width},
                            {"clipInput", true},
                        };
                        if (runDelta)
                        {
                            encoderDict["runDelta"] = true;
                        }
                    }
                    else
                    {
                        encoderDict = new Map<string, object>
                        {
                            {"type", "AdaptiveScalarEncoder"},
                            {"name", fieldName},
                            {"fieldName", fieldName},
                            {"n", 100},
                            {"w", width},
                            {"clipInput", true},
                        };
                    }

                    if (fieldInfo.minValue.HasValue)
                    {
                        encoderDict["minval"] = fieldInfo.minValue.Value;
                    }
                    if (fieldInfo.maxValue.HasValue)
                    {
                        encoderDict["maxval"] = fieldInfo.maxValue.Value;
                    }
                    // If both min and max were specified, use a non-adaptive encoder
                    if (fieldInfo.minValue.HasValue && fieldInfo.maxValue.HasValue
                        && (string) encoderDict["type"] == "AdaptiveScalarEncoder")
                    {
                        encoderDict["type"] = "ScalarEncoder";
                    }
                    // Defaults may have been over-ridden by specifying an encoder type
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict["type"] = fieldInfo.encoderType;
                    }

                    if (!string.IsNullOrWhiteSpace(fieldInfo.space))
                    {
                        encoderDict["space"] = fieldInfo.space;
                    }
                    encoderDictList.Add(encoderDict);
                }

                // String?
                else if (fieldType == "string")
                {
                    encoderDict = new Map<string, object>
                    {
                        {"type", "SDRCategoryEncoder"},
                        {"name", fieldName},
                        {"fieldName", fieldName},
                        {"n", 100 + width},
                        {"w", width}
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict["type"] = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);
                }

                // DateTime?
                else if (fieldType == "datetime")
                {
                    // First, the time of day representation
                    encoderDict = new Map<string, object>
                    {
                        {"type", "DateEncoder"},
                        {"name", $"{fieldName}_timeOfDay"},
                        {"fieldName", fieldName},
                        {"timeOfDay", new Tuple(width, 1)}
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict["type"] = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);

                    // Now, the day of week representation
                    encoderDict = new Map<string, object>
                    {
                        {"type", "DateEncoder"},
                        {"name", $"{fieldName}_dayOfWeek"},
                        {"fieldName", fieldName},
                        {"dayOfWeek", new Tuple(width, 1)}
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict["type"] = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);

                    // Now, the weekend representation
                    encoderDict = new Map<string, object>
                    {
                        {"type", "DateEncoder"},
                        {"name", $"{fieldName}_weekend"},
                        {"fieldName", fieldName},
                        {"dayOfWeek", new Tuple(width)}
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict["type"] = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported field type " + fieldType);
                }

                // -----------------------------------------------------------------------
                // If this was the predicted field, insert another encoder that sends it
                // to the classifier only
                if (fieldName == classifierOnlyField)
                {
                    var clEncoderDict = new Map<string, object>(encoderDict);
                    clEncoderDict["classifierOnly"] = true;
                    clEncoderDict["name"] = "_classifierInput";
                    encoderDictList.Add(clEncoderDict);

                    // If the predicted field needs to be excluded, take it out of the encoder lists
                    if (options.inferenceArgs.inputPredictedField == SwarmDefinition.InputPredictedField.no)
                    {
                        encoderDictList.Remove(encoderDict);
                    }
                }
            }

            // Remove any encoders not in fixedFields
            //if (options.fixedFields != null)
            //{
            //    // TODO
            //}

            // ==========================================================================
            // Now generate the encoderSpecsStr and permEncoderChoicesStr strings from 
            // encoderDictsList and constructorStringList

            object encoderSpecsList;
            object permEncoderChoicesList;

            foreach (var encoderDict in encoderDictList)
            {
                if (((string) encoderDict["name"]).Contains("\\"))
                {
                    throw new InvalidOperationException("Illegal character in field: '\\'");
                }

                // Check for bad characters (?)

                //string constructorStr = _generatePermEncoderStr(options, encoderDict);
                //string encoderKey = encoderDict["name"] as string;
                //encoderSpecsList.Add($"{encoderKey}: {encoderDict}");
            }

            return encoderDictList;
        }
    }

    public class ExperimentDescription : DescriptionBase
        {
            public ExperimentDescription()
            {

            }

            #region Overrides of DescriptionBase

            public override IDescription Clone()
            {
                return Json.Deserialize<ExperimentDescription>(Json.Serialize(this));
            }

            public override Network.Network BuildNetwork()
            {
                throw new System.NotImplementedException();
            }

            public override Parameters GetParameters()
            {
                throw new System.NotImplementedException();
            }

            #endregion
        }

        /// <summary>
        /// ExpGenerator-experiment-description
        /// </summary>
        public class SwarmDefinition
        {
            /// <summary>
            /// JSON description of the stream to use. The schema for this can be found at https://github.com/numenta/nupic/blob/master/src/nupic/frameworks/opf/jsonschema/stream_def.json
            /// </summary>
            public StreamDef streamDef;
            /// <summary>
            /// Which fields to include in the hypersearch and their types. 
            /// The encoders used for each field will be based on the type designated here.
            /// </summary>
            public List<SwarmDefIncludedField> includedFields;
            /// <summary>
            /// The type of inference to conduct
            /// </summary>
            public InferenceType inferenceType;
            /// <summary>
            /// inferenceArgs -- arguments for the type of inference you want to use
            /// </summary>
            public SwarmDefInferenceArgs inferenceArgs;
            /// <summary>
            /// Maximum number of models to evaluate. This replaces the older location of this specification from the job params.
            /// </summary>
            public int? maxModels;
            /// <summary>
            /// The swarm size. This is a meta parameter which, when present, 
            /// sets the minParticlesPerSwarm, killUselessSwarms, minFieldContribution and other settings as appropriate for the requested swarm size.
            /// </summary>
            public SwarmSize? swarmSize;
            /// <summary>
            /// Maximum number of iterations to run. This is used primarily for unit test purposes. A value of -1 means run through the entire dataset.
            /// </summary>
            public int? iterationCount;
            /// <summary>
            /// The number of particles to run per swarm
            /// </summary>
            public int? minParticlesPerSwarm;

            public bool? tryAll3FieldCombinationsWTimestamps;

            public SwarmDefinition()
            {

            }

            public SwarmDefinition Clone()
            {
                return Json.Deserialize<SwarmDefinition>(Json.Serialize(this));
            }

            public class SwarmDefInferenceArgs
            {
                /// <summary>
                /// A list of integers that specifies which steps size(s) to learn/infer on
                /// </summary>
                public int[] predictionSteps { get; set; } = new[] { 1 };
                /// <summary>
                /// Name of the field being optimized for during prediction
                /// </summary>
                public string predictedField { get; set; }
                /// <summary>
                /// Whether or not to use the predicted field as an input. When set to 'auto', 
                /// swarming will use it only if it provides better performance. 
                /// When the inferenceType is NontemporalClassification, this value is forced to 'no'
                /// </summary>
                public InputPredictedField? inputPredictedField { get; set; }

            }

            public class SwarmDefIncludedField
            {
                /// <summary>
                /// A way to customize which spaces (absolute, delta) are evaluted when runDelta is True.
                /// </summary>
                public string space { get; set; }
                /// <summary>
                /// Maximum value. Only applicable for 'int' and 'float' fields
                /// </summary>
                public double? maxValue { get; set; }
                /// <summary>
                /// Minimum value. Only applicable for 'int' and 'float' fields
                /// </summary>
                public double? minValue { get; set; }
                /// <summary>
                /// If true, use a delta encoder.
                /// </summary>
                public bool? runDelta { get; set; }
                /// <summary>
                /// Name of field to be encoded
                /// </summary>
                public string fieldName { get; set; }
                /// <summary>
                /// Field type. Can be one of 'string', 'int', 'float'or 'datetime'
                /// </summary>
                public string fieldType { get; set; }
                /// <summary>
                /// Encoder type, for example 'ScalarEncoder, AdaptiveScalarEncoder, etc.
                /// </summary>
                public string encoderType { get; set; }

            }

            public enum InputPredictedField
            {
                auto, yes, no
            }

            public enum SwarmSize
            {
                Small, Medium, Large
            }
        }

        /// <summary>
        /// Stream Definition
        /// </summary>
        public class StreamDef
        {
            /// <summary>
            /// Version number to resolve hash collisions
            /// </summary>
            public int? version { get; set; }
            /// <summary>
            /// Any text information about the stream that might be needed
            /// </summary>
            public string info { get; set; }
            /// <summary>
            /// A list of input sources with their properties. ***Currently, we only support a list with 1 input***
            /// </summary>
            public StreamItem[] streams { get; set; }

            public string timeField { get; set; }
            public string sequenceIdField { get; set; }
            public string resetField { get; set; }
            /// <summary>
            /// Aggregation for the stream - global for all sources. 
            /// NOTE: years/months are mutually-exclusive with the other units. 
            /// If this parameter is omitted or all of the specified units are 0, then aggregation will be disabled in that permutation.
            /// </summary>
            public AggregationSettings aggregation { get; set; }
            /// <summary>
            /// List of various filters to apply to the records
            /// </summary>
            //public string filter { get; set; }

            public class StreamItem
            {
                /// <summary>
                /// Source URL
                /// </summary>
                public string source { get; set; }
                /// <summary>
                /// Any text information about the source that might be needed
                /// </summary>
                public string info { get; set; }
                /// <summary>
                /// A list of columns to use from the source / Column name, '*' means all columns
                /// </summary>
                public string[] columns { get; set; }
                /// <summary>
                /// A list of types to use from the source. If column names are set in 'columns', then 'types' must have the same number of elements
                /// </summary>
                public string[] types { get; set; }
                /// <summary>
                /// Index of the first record to use from the source - 0-based. Records before this one will be ignored. Omitting first_record is equivalent to beginning of stream.
                /// </summary>
                public int? first_record { get; set; }
                /// <summary>
                /// Record index limit - 0-based. Records starting with this index will be ignored. 
                /// If last_record is omitted or set to null, then the limit is the end of stream. 
                /// E.g., first_record=0 together with last_record=1 addresses a single record at the beginning of the stream.
                /// ["integer", "null"]
                /// </summary>
                public int?[] last_record { get; set; }
            }
        }
    }