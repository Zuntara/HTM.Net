using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Util;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    public static class PermutationsRunner
    {
        private static HyperSearchRunner _currentSearch;

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
        public static uint /*ConfigModelDescription*/ RunWithConfig(SwarmDefinition swarmConfig, Map<string, object> options,
            string outDir = null, string outputLabel = "default",
            string permWorkDir = null, int verbosity = 1)
        {
            if (options == null) options = new Map<string, object>();
            options["searchMethod"] = "v2";

            var exp = _generateExpFilesFromSwarmDescription(swarmConfig, outDir);

            return _runAction(options, exp);
        }

        private static uint _runAction(Map<string, object> options, (ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) exp)
        {
            var returnValue = _runHyperSearch(options, exp);
            return returnValue;
        }

        private static uint _runHyperSearch(Map<string, object> runOptions, (ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) exp)
        {
            var search = new HyperSearchRunner(runOptions);
            // Save in global for the signal handler.
            _currentSearch = search;

            // no options => just run
            search.runNewSearch(exp);

            //var modelParams = HyperSearchRunner.generateReport(
            //    options: runOptions,
            //    replaceReport: false,
            //    hyperSearchJob: search.peekSearchJob(),
            //    metricsKeys: search.getDiscoveredMetricKeys());

            return search.peekSearchJob().getJobId().GetValueOrDefault();
        }

        private static (ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) _generateExpFilesFromSwarmDescription(SwarmDefinition swarmConfig, string outDir)
        {
            return new ExpGenerator(swarmConfig).GenerateParams();
        }
    }

    /// <summary>
    /// Manages one instance of HyperSearch
    /// </summary>
    internal class HyperSearchRunner
    {
        private BaseClientJobDao __cjDAO;
        private Map<string, object> _options;
        private HyperSearchJob __searchJob;
        private HashSet<string> __foundMetrcsKeySet;
        private Task[] _workers;

        public HyperSearchRunner(Map<string, object> options)
        {
            __cjDAO = BaseClientJobDao.Create();
            _options = options;
            __searchJob = null;
            __foundMetrcsKeySet = new HashSet<string>();
            // If we are instead relying on the engine to launch workers for us, this
            // will stay as None, otherwise it becomes an array of subprocess Popen
            // instances.
            _workers = null;
        }

        /// <summary>
        /// Start a new hypersearch job and monitor it to completion
        /// </summary>
        /// <param name="exp"></param>
        public void runNewSearch((ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) exp)
        {
            __searchJob = this.__startSearch(exp);
            monitorSearchJob();
        }

        /// <summary>
        /// Starts HyperSearch as a worker or runs it inline for the "dryRun" action
        /// </summary>
        /// <returns></returns>
        private HyperSearchJob __startSearch((ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) exp)
        {
            // TODO: only dryrun supported, maybe support the queing for workers also
            var @params = ClientJobUtils.MakeSearchJobParamsDict(_options, exp);

            uint? jobId = __cjDAO.jobInsert(client: "test", cmdLine: "<started manually>",
                        @params: Json.Serialize(@params),
                        alreadyRunning: true, minimumWorkers: 1, maximumWorkers: 1,
                        jobType: BaseClientJobDao.JOB_TYPE_HS);

            var args = new[] { $"--jobID={jobId}" };
            jobId = HyperSearchWorker.Main(args);

            // Save search ID to file (used for report generation)
            var searchJob = new HyperSearchJob(jobId);

            return searchJob;
        }

        private void monitorSearchJob()
        {
            //Debug.Assert(__searchJob != null);
            //var jobId = __searchJob.getJobId();
            //DateTime startTime = DateTime.Now;
            //DateTime lastUpdateTime = startTime;

            //// Monitor HyperSearch and report progress
            //int expectedNumModels = __searchJob.getExpectedNumModels(searchMethod: _options["searchMethod"] as string);
            //int lastNumFinished = 0;
            //HashSet<int> finishedModelIDs = new HashSet<int>();
            //var finishedModelStats = _ModelStats();

            //// Keep track of the worker state, results, and milestones from the job record.
            //var lastWorkerState = null;
            //var lastJobResults = null;
            //var lastModelMilestones = null;
            //var lastEngStatus = null;
            //bool hyperSearchFinished = false;

            //while (!hyperSearchFinished)
            //{
            //    JobStatus jobInfo = __searchJob.getJobStatus(_workers);

            //    // Check for job completion BEFORE processing models; NOTE: this permits us
            //    // to process any models that we may not have accounted for in the
            //    // previous iteration.
            //    hyperSearchFinished = jobInfo.isFinished();

            //    // Look for newly completed models, and process them
            //    var modelIDs = __searchJob.queryModelIDs();
            //    Debug.WriteLine($"Current number of models is {modelIDs.Count} ({finishedModelIDs.Count} of them completed)");

            //    if (modelIDs.Count > 0)
            //    {

            //    }
            //}
        }


        /// <summary>
        /// Prints all available results in the given HyperSearch job and emits
        /// model information to the permutations report csv.
        /// </summary>
        /// <param name="options">NupicRunPermutations options dict</param>
        /// <param name="replaceReport"></param>
        /// <param name="hyperSearchJob"></param>
        /// <param name="metricKeys"></param>
        /// <returns></returns>
        public static ExperimentParameters generateReport(Map<string, object> options, bool replaceReport, HyperSearchJob hyperSearchJob,
            HashSet<string> metricsKeys)
        {
            if (hyperSearchJob == null)
            {
                throw new NotImplementedException("No support for loading last jobs yet.");
            }

            var modelIDs = hyperSearchJob.queryModelIDs();
            int? bestModel = null;

            // If metricsKeys was not provided, pre-scan modelInfos to create the list;
            // this is needed by _ReportCSVWriter
            // Also scan the parameters to generate a list of encoders and search
            // parameters
            HashSet<string> metricstmp = new HashSet<string>();
            HashSet<string> searchVar = new HashSet<string>();
            foreach (_NupicModelInfo modelInfo in _iterModels(modelIDs))
            {
                if (modelInfo.isFinished())
                {
                    var vars = modelInfo.getParamLabels().Keys;
                    searchVar.UnionWith(vars);
                    var metrics = modelInfo.getReportMetrics();
                    metricstmp.UnionWith(metrics.Keys);
                }
            }
            if (metricsKeys == null)
            {
                metricsKeys = metricstmp;
            }
            // Create a csv report writer
            //var reportWriter = new _ReportCSVWriter(hyperSearchJob: hyperSearchJob,
            //    metricsKeys: metricsKeys,
            //    searchVar: searchVar,
            //    outputDirAbsPath: options.Get("permWorkDir") as string,
            //    outputLabel: options.Get("outputLabel") as string,
            //    replaceReport: replaceReport);

            // Tallies of experiment dispositions
            var modelStats = new ModelStats();

            Console.WriteLine("\nResults from all experiments:");
            Console.WriteLine("----------------------------------------------------------------");

            // Get common optimization metric info from permutations script
            //var searchParams = hyperSearchJob.getParams();
            int i = 0;
            ExperimentParameters lastModel = null;
            foreach (_NupicModelInfo modelInfo in _iterModels(modelIDs))
            {
                Console.WriteLine("modelInfo:\n" + modelInfo);

                var genDescrFile = modelInfo.getGeneratedDescriptionFile();
                Console.WriteLine("genDescrFile:\n" + genDescrFile);
                lastModel = Json.Deserialize<ExperimentParameters>(genDescrFile);
                i++;
            }
            return lastModel;
        }

        private static IEnumerable<_NupicModelInfo> _iterModels(List<ulong> modelIDs)
        {
            var modelInfos = BaseClientJobDao.Create().modelsInfo(modelIDs);
            foreach (var modelID in modelIDs)
            {
                ModelTable rawInfo = modelInfos.Single(mi => mi.model_id == modelID);
                yield return new _NupicModelInfo(rawInfo);
            }
        }

        public HyperSearchJob peekSearchJob()
        {
            Debug.Assert(__searchJob != null);
            return __searchJob;
        }

        public HashSet<string> getDiscoveredMetricKeys()
        {
            return __foundMetrcsKeySet;
        }
    }

    internal class _ReportCSVWriter
    {
        private HyperSearchJob __searchJob;
        private uint? __searchJobID;
        private List<string> __sortedMetricsKeys;
        private string __outputDirAbsPath;
        private string __outputLabel;
        private bool __replaceReport;
        private HashSet<string> __sortedVariableNames;

        public _ReportCSVWriter(HyperSearchJob hyperSearchJob, HashSet<string> metricsKeys, HashSet<string> searchVar, string outputDirAbsPath, string outputLabel, bool replaceReport)
        {
            __searchJob = hyperSearchJob;
            __searchJobID = hyperSearchJob.getJobId();
            __sortedMetricsKeys = metricsKeys.OrderBy(k => k).ToList();
            __outputDirAbsPath = Path.GetFullPath(outputDirAbsPath);
            __outputLabel = outputLabel;
            __replaceReport = replaceReport;
            __sortedVariableNames = searchVar;
            // These are set up by __openAndInitCSVFile
            //__csvFileObj = null;
            //__reportCSVPath = null;
            //__backupCSVPath = null;
        }
    }

    internal class ClientJobUtils
    {
        public static Map<string, object> MakeSearchJobParamsDict(object options, (ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) exp)
        {
            string hsVersion = "v2";
            //int maxModels = 1;

            var @params = new Map<string, object>
            {
                {"hsVersion", hsVersion },
                //{"maxModels", maxModels },
            };
            @params["persistentJobGUID"] = Guid.NewGuid().ToString().Replace("-", "");

            @params["descriptionPyContents"] = exp.Item1;
            @params["permutationsPyContents"] = exp.Item2;

            return @params;
        }
    }


    /// <summary>
    /// Our Nupic Job abstraction
    /// </summary>
    internal abstract class NuPicJob
    {
        protected uint? __nupicJobID;
        private Map<string, object> __params;

        public NuPicJob(uint? nupicJobID)
        {
            __nupicJobID = nupicJobID;
            var jobInfo = BaseClientJobDao.Create().jobInfo(nupicJobID);

            __params = Json.Deserialize<Map<string, object>>(jobInfo.GetAsString("params"));
        }
    }

    internal class HyperSearchJob : NuPicJob
    {
        public HyperSearchJob(uint? jobId)
            : base(jobId)
        {

        }

        public uint? getJobId()
        {
            return base.__nupicJobID;
        }

        public int getExpectedNumModels(string searchMethod)
        {
            throw new NotImplementedException();
        }

        public JobStatus getJobStatus(Task[] workers)
        {
            var jobInfo = new JobStatus(__nupicJobID, workers);
            return jobInfo;
        }

        public List<ulong> queryModelIDs()
        {
            uint? jobId = getJobId();
            List<Tuple<ulong, uint>> modelCOunterPairs = BaseClientJobDao.Create().modelsGetUpdateCounters(jobId);
            var modelIDs = modelCOunterPairs.Select(x => x.Item1).ToList();
            return modelIDs;
        }
    }

    internal class JobStatus
    {
        private NamedTuple __jobInfo;

        public JobStatus(uint? nupicJobID, Task[] workers)
        {
            NamedTuple jobInfo = BaseClientJobDao.Create().jobInfo(nupicJobID);

            if (workers != null)
            {
                int runningCount = 0;
                foreach (var worker in workers)
                {
                    var state = worker.Status;
                    if (state == TaskStatus.Running)
                        runningCount += 1;
                }
                string status;
                if (runningCount > 0)
                {
                    status = BaseClientJobDao.STATUS_RUNNING;
                }
                else
                {
                    status = BaseClientJobDao.STATUS_COMPLETED;
                }
                jobInfo["status"] = status;
            }
            __jobInfo = jobInfo;
        }

        public string statusAsString()
        {
            return __jobInfo["status"] as string;
        }

        public bool isFinished()
        {
            bool done = (string)__jobInfo["status"] == BaseClientJobDao.STATUS_NOTSTARTED;
            return done;
        }
    }

    /// <summary>
    /// This class represents information obtained from ClientJobManager about a model
    /// </summary>
    internal class _NupicModelInfo
    {
        private ModelTable __rawInfo;
        private ModelParams __cachedParams;
        private Tuple __cachedResults;

        public _NupicModelInfo(ModelTable rawInfo)
        {
            __rawInfo = rawInfo;
            // Cached model metrics (see __unwrapResults())
            __cachedResults = null;
            Debug.Assert(__rawInfo.@params != null);
            // Cached model params (see __unwrapParams())
            __cachedParams = null;
        }

        public ulong getModelID()
        {
            return __rawInfo.model_id;
        }

        public string getModelDescription()
        {
            Map<string, object> paramSettings = getParamLabels();
            // Form a csv friendly string representation of this model
            List<string> items = new List<string>();
            foreach (var paramSetting in paramSettings)
            {
                items.Add($"{paramSetting.Key}_{paramSetting.Value}");
            }
            return string.Join(".", items);
        }

        public string getGeneratedDescriptionFile()
        {
            return __rawInfo.gen_description;
        }

        public Map<string, object> getParamLabels()
        {
            var @params = __unwrapParams();
            // Hypersearch v2 stores the flattened parameter settings in "particleState"
            if (@params.particleState != null)
            {
                var retVal = new Map<string, object>();

                var queue = @params.particleState.varStates
                    .Select(vs => new { pair = vs, retval = new Map<string, object>() })
                    .ToList();
                while (queue.Count > 0)
                {
                    var varState = queue.First();
                    queue.RemoveAt(0);
                    var pair = varState.pair;
                    var output = varState.retval;
                    var k = pair.Key;
                    var v = pair.Value;
                    if (v.position != null && v.bestPosition != null && v.velocity.HasValue)
                    {
                        output[k] = v.position;
                    }
                    else
                    {
                        if (!output.ContainsKey(k))
                        {
                            output[k] = new Map<string, object>();
                        }
                        queue.Add(new { pair = pair, retval = (Map<string, object>)output[k] });
                    }
                }
                return retVal;
            }
            return null;
        }

        public int getNumRecords()
        {
            return (int)__rawInfo.num_records;
        }

        public Map<string, double?> getReportMetrics()
        {
            return (Map<string, double?>)__unwrapResults().Item1;
        }

        public Map<string, double?> getOptimizationMetrics()
        {
            return (Map<string, double?>)__unwrapResults().Item2;
        }

        private ModelParams __unwrapParams()
        {
            if (__cachedParams == null)
            {
                __cachedParams = Json.Deserialize<ModelParams>(__rawInfo.@params);
                Debug.Assert(__cachedParams != null, $"{__rawInfo.@params} resulted in null");
            }
            return __cachedParams;
        }

        private Tuple __unwrapResults()
        {
            if (__cachedResults == null)
            {
                if (__rawInfo.results != null)
                {
                    var resultList = Json.Deserialize<Tuple>(__rawInfo.results);
                    __cachedResults = resultList;
                }
                else
                {
                    __cachedResults = new Tuple(new Map<string, double?>(), new Map<string, double?>());
                }
            }
            return __cachedResults;
        }

        public bool isFinished()
        {
            bool finished = __rawInfo.status == BaseClientJobDao.STATUS_COMPLETED;
            return finished;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"_NupicModelInfo(jobID={__rawInfo.job_id}, modelID={__rawInfo.model_id}, status={__rawInfo.status}, completionReason={__rawInfo.completion_reason}, updateCounter={__rawInfo.update_counter}, numRecords={__rawInfo.num_records})";
        }

        #endregion


    }

    internal class ModelStats
    {

    }


    public static class TokenReplacer
    {
        private static List<string> _replacedTokens = new List<string>();

        public static void ReplaceIn(object target, Map<string, object> tokenReplacements, int level = 0)
        {
            if (target == null) return;
            if (level == 0)
            {
                _replacedTokens = new List<string>();
            }
            Debug.WriteLine($"{Indent(level)}Evaluating {target.GetType().Name}");

            // Get all properties in the target type
            var props = target.GetType().GetProperties();

            var propsToReplaceIn = props.Where(p => p.GetCustomAttribute<TokenReplaceAttribute>() != null)
                .Select(p => new { Token = p.GetCustomAttribute<TokenReplaceAttribute>().TokenName, Prop = p });
            foreach (var prop in propsToReplaceIn)
            {
                try
                {
                    if (!tokenReplacements.ContainsKey(prop.Token))
                    {
                        Debug.WriteLine($"{Indent(level)}> Skipping (not found) {prop.Token} in {target.GetType().Name} (prop: {prop.Prop.Name})");
                        continue;
                    }
                    Debug.WriteLine($"{Indent(level)}> Setting {prop.Token} in {target.GetType().Name} (prop: {prop.Prop.Name})");
                    prop.Prop.SetValue(target, tokenReplacements[prop.Token]);
                    _replacedTokens.Add(prop.Token);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"{Indent(level)}Failed to set property {prop.Prop.Name} with token {prop.Token} : {e.Message}");
                }
            }

            foreach (PropertyInfo prop in props)
            {
                if (IsOurType(prop))
                {
                    Debug.WriteLine($"{Indent(level)}Drilling down on {prop.Name}");
                    ReplaceIn(prop.GetValue(target), tokenReplacements, level + 1);
                }
            }

            if (level == 0)
            {
                var notSetKeys = tokenReplacements.Keys.Except(_replacedTokens).ToList();
                Debug.WriteLine($"{Indent(level)}Unset tokens:");
                foreach (var key in notSetKeys)
                {
                    Debug.WriteLine($"{Indent(level)}-> {key}");
                }
                Debug.WriteLine("");
                Debug.WriteLine("");
            }
        }

        private static bool IsOurType(PropertyInfo prop)
        {
            if (prop.PropertyType.Namespace?.StartsWith("HTM.") == true)
            {
                if (prop.PropertyType.Name.Equals("Map`2")) return false;
                if (prop.Name.Equals("Item")) return false;
                return true;
            }
            return false;
        }

        private static string Indent(int level)
        {
            const char indentChar = '\t';
            string indent = string.Empty;
            for (int i = 0; i < level; i++)
            {
                indent += indentChar;
            }
            return indent;
        }
    }

    #region Permutation Objects

    public class TokenReplaceAttribute : Attribute
    {
        public string TokenName { get; set; }

        public TokenReplaceAttribute(string tokenName)
        {
            TokenName = tokenName;
        }
    }

    

    

    

    #endregion

    /// <summary>
    /// ExpGenerator-experiment-description
    /// </summary>
    [Serializable]
    public class SwarmDefinition
    {
        public SwarmDefinition()
        {
            includedFields = new List<SwarmDefIncludedField>();
        }

        public Map<string, object> customErrorMetric;

        /// <summary>
        /// The metric window size. If not specified, then a default value as specified by 
        /// the nupic configuration parameter nupic.opf.metricWindow will be used.
        /// </summary>
        public double? metricWindow { get; set; }
        public string[] loggedMetrics { get; set; }

        /// <summary>
        /// JSON description of the stream to use. The schema for this can be found at https://github.com/numenta/nupic/blob/master/src/nupic/frameworks/opf/jsonschema/stream_def.json
        /// </summary>
        public StreamDef streamDef { get; set; }
        /// <summary>
        /// Which fields to include in the hypersearch and their types. 
        /// The encoders used for each field will be based on the type designated here.
        /// </summary>
        public List<SwarmDefIncludedField> includedFields { get; set; }
        /// <summary>
        /// The type of inference to conduct
        /// </summary>
        public InferenceType inferenceType { get; set; }
        /// <summary>
        /// inferenceArgs -- arguments for the type of inference you want to use
        /// </summary>
        public InferenceArgsDescription inferenceArgs { get; set; }
        /// <summary>
        /// Maximum number of models to evaluate. This replaces the older location of this specification from the job params.
        /// </summary>
        public int? maxModels { get; set; }
        /// <summary>
        /// If true, permute over the sp permanence decrement value
        /// </summary>
        public bool spPermuteDecrement { get; set; }
        /// <summary>
        /// The swarm size. This is a meta parameter which, when present, 
        /// sets the minParticlesPerSwarm, killUselessSwarms, minFieldContribution and other settings as appropriate for the requested swarm size.
        /// </summary>
        public SwarmSize? swarmSize { get; set; }
        /// <summary>
        /// Maximum number of iterations to run. This is used primarily for unit test purposes. A value of -1 means run through the entire dataset.
        /// </summary>
        public int? iterationCount { get; set; }
        /// <summary>
        /// The number of particles to run per swarm
        /// </summary>
        public int? minParticlesPerSwarm { get; set; }


        /// <summary>
        /// How often predictions are computed. When this parameter is included, then different aggregations will be attempted during swarming. 
        /// This parameter sets the UPPER BOUND on the interval between generated predictions, 
        /// they may in fact be generated more often than this, depending on what the actual aggregation interval is for that particular model.  
        /// computeInterval MUST be an integer multiple of the minimum aggregation period, which is the aggregation setting of in the stream. 
        /// The max aggregation period attempted will always be an integer factor of computeInterval. 
        /// When this parameter is specified, inferenceArgs:predictionSteps must contain only 1 item, the number of prediction steps
        /// to use at the minimum aggregation setting. For other aggregation intervals that are evaluated during swarming, 
        /// predictionSteps will be adjusted accordingly
        /// </summary>
        public SwarmDefComputeInterval computeInterval { get; set; }

        /// <summary>
        /// Additional parameters relevant to an anomaly model
        /// </summary>
        public AnomalyParamsDescription anomalyParams { get; set; }

        /// <summary>
        /// sensor auto-reset period to enforce. NOTE: years/months are presently not supported for sensor auto-reset. 
        /// If this parameter is omitted or all of the specified units are 0, then sensor auto-reset will be disabled in that permutation.
        /// </summary>
        public AggregationSettings resetPeriod { get; set; }
        /// <summary>
        /// Bound permutations by the given number of fields, including meta-fields (e.g., 'timeOfDay', 'dayOfWeek'), 
        /// in any given experiment during hypersearch; the value must be 0 or positive. 
        /// If the value is 0 or this argument is omitted, 
        /// ExpGenerator will use the total count of fields and meta-fields in the dataset as the maximum.
        /// </summary>
        public int fieldPermutationLimit { get; set; }
        /// <summary>
        /// What percent of the columns's receptive field is available
        /// </summary>
        public double spCoincInputPoolPct { get; set; }

        /// <summary>
        /// What is the default connected threshold
        /// </summary>
        public double spSynPermConnected { get; set; } = 0.10000000000000001;

        /// <summary>
        /// If specified, the swarm will try only this field combination, but still search for the best encoder settings and other parameters. 
        /// This is way to speed up swarming significantly if you already know which fields should be included in the final model.
        /// </summary>
        public string[] fixedFields { get; set; }

        /// <summary>
        /// This is the JSON encoded params from another model that will be used to seed a fast swarm. 
        /// When included, the swarming will take a number of shortcuts based on this seed model, like turning off the normal field search logic 
        /// and instead using the same fields used by the seed model. 
        /// Normally, fixedFields should NOT be specified along with this option because the fixedFields will be extracted from these model params.
        /// </summary>
        public ExperimentPermutationParameters fastSwarmModelParams { get; set; }

        /// <summary>
        /// Additional metrics to be generated, along with thedefault ones for the given inferenceType. 
        /// Note: The specified metric should be compatible with the given inference
        /// </summary>
        public MetricSpec[] metrics { get; set; }

        public double? minFieldContribution { get; set; }
        public bool? killUselessSwarms { get; set; }
        public int? maxFieldBranching { get; set; }

        public bool? tryAll3FieldCombinations { get; set; }
        public bool? tryAll3FieldCombinationsWTimestamps { get; set; }
        public bool dynamicPredictionSteps { get; set; }
        /// <summary>
        /// Flag to run baseline error metrics with the job for comparison
        /// </summary>
        public bool runBaseLines { get; set; }




        public SwarmDefinition Clone()
        {
            return Json.Deserialize<SwarmDefinition>(Json.Serialize(this));
        }



        public class SwarmDefIncludedField
        {
            /// <summary>
            /// A way to customize which spaces (absolute, delta) are evaluated when runDelta is True.
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
            public FieldMetaType fieldType { get; set; }
            /// <summary>
            /// Encoder type, for example 'ScalarEncoder, AdaptiveScalarEncoder, etc.
            /// </summary>
            public EncoderTypes encoderType { get; set; }
            /// <summary>
            /// Type of the field for the network (learn, blank, reset, sequence, ...)
            /// Default Blank
            /// </summary>
            public SensorFlags specialType { get; set; } = SensorFlags.Blank;
        }

        public class SwarmDefComputeInterval
        {
            public uint? hours { get; set; }
            public uint? microseconds { get; set; }
            public uint? seconds { get; set; }
            public uint? weeks { get; set; }
            public uint? months { get; set; }
            public uint? minutes { get; set; }
            public uint? days { get; set; }
            public uint? milliseconds { get; set; }
            public uint? years { get; set; }
        }



        public enum SwarmSize
        {
            Small, Medium, Large
        }
    }

   

    [Serializable]
    public enum InputPredictedField
    {
        Auto,
        Yes,
        No
    }

    /// <summary>
    /// Stream Definition
    /// </summary>
    [Serializable]
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

        [Serializable]
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

        public StreamDef Clone()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, this);
            ms.Position = 0;
            StreamDef obj = (StreamDef)formatter.Deserialize(ms);
            return obj;
        }
    }
}