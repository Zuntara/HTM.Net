using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Swarming.HyperSearch;
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

        private static uint _runAction(Map<string, object> options, Tuple<ClaExperimentParameters, ClaPermutations> exp)
        {
            var returnValue = _runHyperSearch(options, exp);
            return returnValue;
        }

        private static uint _runHyperSearch(Map<string, object> runOptions, Tuple<ClaExperimentParameters, ClaPermutations> exp)
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

        private static Tuple<ClaExperimentParameters, ClaPermutations> _generateExpFilesFromSwarmDescription(SwarmDefinition swarmConfig, string outDir)
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
        public void runNewSearch(Tuple<ClaExperimentParameters, ClaPermutations> exp)
        {
            __searchJob = this.__startSearch(exp);
            monitorSearchJob();
        }

        /// <summary>
        /// Starts HyperSearch as a worker or runs it inline for the "dryRun" action
        /// </summary>
        /// <returns></returns>
        private HyperSearchJob __startSearch(Tuple<ClaExperimentParameters, ClaPermutations> exp)
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
        public static PermutationModelParameters generateReport(Map<string, object> options, bool replaceReport, HyperSearchJob hyperSearchJob,
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
            PermutationModelParameters lastModel = null;
            foreach (_NupicModelInfo modelInfo in _iterModels(modelIDs))
            {
                Console.WriteLine("modelInfo:\n" + modelInfo);

                var genDescrFile = modelInfo.getGeneratedDescriptionFile();
                Console.WriteLine("genDescrFile:\n" + genDescrFile);
                lastModel = Json.Deserialize<PermutationModelParameters>(genDescrFile);
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
        public static Map<string, object> MakeSearchJobParamsDict(object options, Tuple<ClaExperimentParameters, ClaPermutations> exp)
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
                    if (v.position.HasValue && v.bestPosition.HasValue && v.velocity.HasValue)
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


    #region Description Objects

    public interface IDescription
    {
        ConfigModelDescription modelConfig { get; set; }
        ControlModelDescription control { get; set; }

        Parameters GetParameters();

        IDescription Clone();
    }

    [JsonConverter(typeof(TypedDescriptionBaseJsonConverter))]
    [Serializable]
    public abstract class BaseDescription : IDescription
    {
        protected BaseDescription()
        {
            Type = GetType().AssemblyQualifiedName;
        }

        public virtual Network.Network BuildNetwork()
        {
            return null;
        }

        public virtual Parameters GetParameters()
        {
            Parameters p = Parameters.Empty();

            p.Union(modelConfig.GetParameters());

            return p;
        }

        public IDescription Clone()
        {
            return Json.Deserialize<BaseDescription>(Json.Serialize(this));
        }

        /// <summary>
        /// Model Configuration Dictionary
        /// </summary>
        public ConfigModelDescription modelConfig { get; set; }

        public ControlModelDescription control { get; set; }

        /// <summary>
        /// Used for deserialisation of this description
        /// </summary>
        public string Type { get; set; }
    }

    [Serializable]
    public class ClaExperimentParameters : Parameters
    {
        private bool _groupedEncoders = false;

        protected ClaExperimentParameters()
        {
            Control = new ExperimentControl();
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            // Spatial defaults
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.GLOBAL_INHIBITION, true);
            SetParameterByKey(KEY.COLUMN_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
            SetParameterByKey(KEY.SEED_SP, 1956);
            SetParameterByKey(KEY.RANDOM_SP, new XorshiftRandom((int)paramMap[KEY.SEED_SP]));
            SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.05);
            SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.0005);
            SetParameterByKey(KEY.MAX_BOOST, 1.0);
            // Temporal defaults
            SetParameterByKey(KEY.TM_VERBOSITY, 0);
            SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
            SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.SEED_TM, 1960);
            SetParameterByKey(KEY.RANDOM_TM, new XorshiftRandom((int)paramMap[KEY.SEED_TM]));
            SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
            SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
            SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, 128);
            SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.21);
            SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
            SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
            //SetParameterByKey(KEY.GLOBAL_DECAY, 0);
            //SetParameterByKey(KEY.MAX_AGE, 0);
            SetParameterByKey(KEY.MIN_THRESHOLD, 12);
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 16);
            //SetParameterByKey(KEY.PAM_LENGTH, 1);

            // Classifier params
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.001);
            SetParameterByKey(KEY.AUTO_CLASSIFY, true);
            SetParameterByKey(KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier));
            SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1 });
        }

        public static ClaExperimentParameters Default()
        {
            return new ClaExperimentParameters();
        }

        public ExperimentControl Control { get; set; }

        /// <summary>
        /// Type of model that the rest of these parameters apply to.
        /// </summary>
        public string Model { get; set; } = "CLA";
        /// <summary>
        /// The type of inference that this model will perform
        /// </summary>
        public InferenceType InferenceType { get; set; }
        public bool EnableSpatialPooler { get; set; }
        /// <summary>
        /// Controls whether TP is enabled or disabled;
        /// TP is necessary for making temporal predictions, such as predicting
        /// the next inputs.  Without TP, the model is only capable of
        /// reconstructing missing sensor inputs (via SP).
        /// </summary>
        public bool EnableTemporalMemory { get; set; }
        public bool EnableClassification { get; set; }
        /// <summary>
        /// A dictionary specifying the period for automatically-generated
        /// resets from a RecordSensor;
        ///
        /// None = disable automatically-generated resets (also disabled if
        /// all of the specified values evaluate to 0).
        /// Valid keys is the desired combination of the following:
        ///   days, hours, minutes, seconds, milliseconds, microseconds, weeks
        ///
        /// Example for 1.5 days: sensorAutoReset = dict(days=1,hours=12),
        /// </summary>
        public AggregationSettings SensorAutoReset { get; set; }
        /// <summary>
        /// Intermediate variables used to compute fields in modelParams and also
        /// referenced from the control section.
        /// </summary>
        public AggregationSettings AggregationInfo { get; set; }

        public bool TrainSPNetOnlyIfRequested { get; set; }



        public EncoderSettingsList GetEncoderSettings()
        {
            if (_groupedEncoders)
            {
                return (EncoderSettingsList)GetParameterByKey(KEY.FIELD_ENCODING_MAP);
            }
            _groupedEncoders = true;

            // Lookup DateEncoders and group them if needed
            EncoderSettingsList list = (EncoderSettingsList)GetParameterByKey(KEY.FIELD_ENCODING_MAP);

            var selection = list.Where(e => e.Key.Contains("_") && e.Value.GetEncoderType() == "DateEncoder").ToList();
            var grouped = selection.GroupBy(k => k.Value.fieldName, e => e.Key);
            if (selection.Count > 1)
            {
                foreach (var grouping in grouped)
                {
                    string fieldName = grouping.Key;
                    EncoderSetting setting = new EncoderSetting();
                    setting.encoderType = "DateEncoder";
                    setting.fieldName = fieldName;

                    foreach (string name in grouping)
                    {
                        if (name.EndsWith("timeOfDay"))
                        {
                            setting.timeOfDay = selection.Single(s => s.Key == name).Value.timeOfDay;
                        }
                        else if (name.EndsWith("dayOfWeek"))
                        {
                            setting.dayOfWeek = selection.Single(s => s.Key == name).Value.dayOfWeek;
                        }
                        else if (name.EndsWith("weekend"))
                        {
                            setting.weekend = selection.Single(s => s.Key == name).Value.weekend;
                        }
                        else if (name.EndsWith("season"))
                        {
                            setting.season = selection.Single(s => s.Key == name).Value.season;
                        }
                        else if (name.EndsWith("holiday"))
                        {
                            setting.holiday = selection.Single(s => s.Key == name).Value.holiday;
                        }
                        list.Remove(name);
                    }
                    list.Add(fieldName, setting);
                }
            }
            return (EncoderSettingsList)GetParameterByKey(KEY.FIELD_ENCODING_MAP);
        }

        public ClaExperimentParameters Union(ClaExperimentParameters p)
        {
            foreach (KEY k in p.paramMap.Keys)
            {
                SetParameterByKey(k, p.GetParameterByKey(k));
            }
            return this;
        }

        public new ClaExperimentParameters Copy()
        {
            var p = new ClaExperimentParameters().Union(this);
            p.InferenceType = InferenceType;
            p.EnableSpatialPooler = EnableSpatialPooler;
            p.EnableTemporalMemory = EnableTemporalMemory;
            p.EnableClassification = EnableClassification;
            p.SensorAutoReset = SensorAutoReset?.Clone();
            p.AggregationInfo = AggregationInfo?.Clone();
            p.TrainSPNetOnlyIfRequested = TrainSPNetOnlyIfRequested;
            p.Control = Control.Clone();

            return p;
        }
    }

    [Serializable]
    public class ExperimentControl
    {
        public StreamDef DatasetSpec { get; set; }
        public FieldMetaInfo[] InputRecordSchema { get; set; }
        public InferenceArgsDescription InferenceArgs { get; set; }
        /// <summary>
        /// Logged Metrics: A sequence of regular expressions that specify which of
        /// the metrics from the Inference Specifications section MUST be logged for
        /// every prediction. The regex"s correspond to the automatically generated
        /// metric labels. This is similar to the way the optimization metric is
        /// specified in permutations.py.
        /// </summary>
        public string[] LoggedMetrics { get; set; }
        /// <summary>
        /// Metrics: A list of MetricSpecs that instantiate the metrics that are
        /// computed for this experiment
        /// </summary>
        public MetricSpec[] Metrics { get; set; }
        public int? IterationCount { get; set; }
        public int? IterationCountInferOnly { get; set; }

        public ExperimentControl Clone()
        {
            ExperimentControl c = new ExperimentControl();
            c.InputRecordSchema = (FieldMetaInfo[])InputRecordSchema.Clone();
            c.InferenceArgs = InferenceArgs.Clone();
            c.LoggedMetrics = (string[])LoggedMetrics.Clone();
            c.Metrics = (MetricSpec[])Metrics.Clone();
            c.DatasetSpec = DatasetSpec;
            c.IterationCount = IterationCount;
            c.IterationCountInferOnly = IterationCountInferOnly;
            return c;
        }
    }

    /// <summary>
    /// Template file used by the OPF Experiment Generator to generate the actual description
    /// </summary>
    [Serializable]
    public class ClaExperimentDescription : BaseDescription
    {
        public ClaExperimentDescription()
        {
            // Fill in the default values, enrich with token replaces
            var config = new ConfigModelDescription
            {
                model = "CLA",
                version = 1,
                modelParams = new ModelParamsDescription
                {
                    sensorParams = new SensorParamsDescription
                    {
                        verbosity = 0
                    },
                    spParams = new SpatialParamsDescription
                    {
                        spVerbosity = 0,
                        globalInhibition = true,
                        columnCount = new[] { 2048 },
                        inputWidth = new[] { 0 },
                        numActiveColumnsPerInhArea = 40,
                        seed = 1956,
                        synPermActiveInc = 0.05,
                        synPermInactiveDec = 0.0005,
                        maxBoost = 1.0
                    },
                    tpParams = new TemporalParamsDescription
                    {
                        verbosity = 0,
                        columnCount = new[] { 2048 },
                        cellsPerColumn = 32,
                        inputWidth = new[] { 2048 },
                        seed = 1960,
                        temporalImp = "cpp",
                        newSynapseCount = 20,
                        maxSynapsesPerSegment = 32,
                        maxSegmentsPerCell = 128,
                        initialPerm = 0.21,
                        permanenceInc = 0.1,
                        permanenceDec = 0.1,
                        globalDecay = 0.0,
                        maxAge = 0,
                        minThreshold = 12,
                        activationThreshold = 16,
                        outputType = "normal",
                        pamLength = 1
                    },
                    clEnable = true,
                    clParams = new ClassifierParamsDescription
                    {
                        regionName = typeof(CLAClassifier).AssemblyQualifiedName,
                        verbosity = 0,
                        alpha = 0.001
                    },
                    trainSPNetOnlyIfRequested = false
                }
            };

            control = new ControlModelDescription();

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
                config.modelParams.clParams.steps = new[] { predictionSteps };
            }

        }



        public void updateConfigFromSubConfig(ConfigModelDescription config)
        {

        }
    }

    public class TokenReplaceAttribute : Attribute
    {
        public string TokenName { get; set; }

        public TokenReplaceAttribute(string tokenName)
        {
            TokenName = tokenName;
        }
    }

    [Serializable]
    public class ConfigModelDescription
    {
        /// <summary>
        /// Type of model that the rest of these parameters apply to.
        /// </summary>
        public string model { get; set; }

        /// <summary>
        /// Version that specifies the format of the config.
        /// </summary>
        public int? version { get; set; }

        // Intermediate variables used to compute fields in modelParams and also
        // referenced from the control section.
        [TokenReplace("$AGGREGATION_INFO")]
        public AggregationSettings aggregationInfo { get; set; }
        [TokenReplace("$PREDICT_AHEAD_TIME")]
        public AggregationSettings predictAheadTime { get; set; }

        /// <summary>
        /// Model parameter dictionary.
        /// </summary>
        public ModelParamsDescription modelParams { get; set; }

        [TokenReplace("$INPUT_RECORD_SCHEMA")]
        public FieldMetaInfo[] inputRecordSchema { get; set; }

        public Map<string, object> GetDictionary()
        {
            return new Map<string, object>
            {
                {"model",model },
                {"version",version },
                {"aggregationInfo",aggregationInfo },
                {"predictAheadTime",predictAheadTime },
                {"modelParams",modelParams },
            };
        }

        public Parameters GetParameters()
        {
            var parameters = Parameters.Empty();
            parameters.Union(modelParams.GetParameters());
            return parameters;
        }

        public object this[string key]
        {
            get { return GetDictionary()[key]; }
        }

        public void SetDictValue(string key, object value)
        {
            switch (key)
            {
                case "model":
                    model = (string)value;
                    break;
                case "version":
                    version = (int)value;
                    break;
                case "aggregationInfo":
                    aggregationInfo = (AggregationSettings)value;
                    break;
                case "predictAheadTime":
                    predictAheadTime = (AggregationSettings)value;
                    break;
                case "modelParams":
                    modelParams = (ModelParamsDescription)value;
                    break;
            }
        }
    }

    [Serializable]
    public class ControlModelDescription
    {
        [TokenReplace("$ENVIRONMENT")]
        public string environment { get; set; }
        [TokenReplace("$DATASET_SPEC")]
        public StreamDef dataset { get; set; }
        [TokenReplace("$LOGGED_METRICS")]
        public string[] loggedMetrics { get; set; }
        [TokenReplace("$METRICS")]
        public MetricSpec[] metrics { get; set; }
        [TokenReplace("$INFERENCE_ARGS")]
        public InferenceArgsDescription inferenceArgs { get; set; }
        [TokenReplace("$ITERATION_COUNT")]
        public int? iterationCount { get; set; }
        public int? iterationCountInferOnly { get; set; }
    }

    [Serializable]
    public class SensorParamsDescription
    {
        [TokenReplace("$ENCODER_SPECS")]
        [ParameterMapping("fieldEncodings")]
        public EncoderSettingsList encoders { get; set; }
        public int verbosity { get; set; }
        [TokenReplace("$SENSOR_AUTO_RESET")]
        public IDictionary<string, object> sensorAutoReset { get; set; }
    }

    [Serializable]
    public class ModelParamsDescription
    {
        /// <summary>
        /// The type of inference that this model will perform
        /// </summary>
        [TokenReplace("$INFERENCE_TYPE")]
        public InferenceType inferenceType { get; set; }

        public SensorParamsDescription sensorParams { get; set; }

        [TokenReplace("$SP_ENABLE")]
        public bool spEnable { get; set; }
        public SpatialParamsDescription spParams { get; set; }

        [TokenReplace("$TP_ENABLE")]
        public bool tpEnable { get; set; }
        public TemporalParamsDescription tpParams { get; set; }

        [TokenReplace("$ANOMALY_PARAMS")]
        public AnomalyParamsDescription anomalyParams { get; set; }

        public bool clEnable;
        public ClassifierParamsDescription clParams { get; set; }

        public bool trainSPNetOnlyIfRequested { get; set; }


        public Parameters GetParameters()
        {
            Parameters p = Parameters.Empty();
            Parameters.ApplyParametersFromDescription(sensorParams, p);
            if (spEnable)
            {
                Parameters.ApplyParametersFromDescription(spParams, p);
            }
            if (tpEnable)
            {
                // NOTE: Temporary fix
                if (tpParams.minThreshold is PermuteVariable)
                {
                    tpParams.minThreshold = (int)((PermuteVariable)tpParams.minThreshold).GetPosition();
                }
                if (tpParams.activationThreshold is PermuteVariable)
                {
                    tpParams.activationThreshold = (int)((PermuteVariable)tpParams.activationThreshold).GetPosition();
                }

                Parameters.ApplyParametersFromDescription(tpParams, p);
            }
            if (clEnable)
            {
                Parameters.ApplyParametersFromDescription(clParams, p);
            }
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, false);
            if (!string.IsNullOrWhiteSpace(clParams.regionName))
            {
                p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY_TYPE, Type.GetType(clParams.regionName, true));
            }

            return p;
        }
    }

    [Serializable]
    public class SpatialParamsDescription
    {
        [ParameterMapping]
        public int spVerbosity { get; set; }
        [ParameterMapping]
        public bool globalInhibition { get; set; }
        [ParameterMapping("columnDimensions")]
        public int[] columnCount { get; set; }
        [ParameterMapping("inputDimensions")]
        public int[] inputWidth { get; set; }
        [ParameterMapping]
        public double numActiveColumnsPerInhArea { get; set; }
        [ParameterMapping("seedSpatial")]
        public int seed { get; set; }
        [ParameterMapping]
        [TokenReplace("$SP_POOL_PCT")]
        public double potentialPct { get; set; }
        [ParameterMapping]
        [TokenReplace("$SP_PERM_CONNECTED")]
        public double synPermConnected { get; set; }
        [ParameterMapping]
        public double synPermActiveInc { get; set; }
        [ParameterMapping]
        [TokenReplace("$PERM_SP_CHOICES_synPermInactiveDec")]
        public double synPermInactiveDec { get; set; }
        [ParameterMapping]
        public double maxBoost { get; set; }
    }

    [Serializable]
    public class TemporalParamsDescription
    {
        [ParameterMapping]
        [TokenReplace("$PERM_TP_CHOICES_minThreshold")]
        public object minThreshold { get; set; }
        [ParameterMapping]
        [TokenReplace("$PERM_TP_CHOICES_activationThreshold")]
        public object activationThreshold { get; set; }
        [ParameterMapping("tmVerbosity")]
        public int verbosity { get; set; }
        [ParameterMapping("columnDimensions")]
        public int[] columnCount { get; set; }
        [ParameterMapping]
        public int cellsPerColumn { get; set; }
        [ParameterMapping("inputDimensions")]
        public int[] inputWidth { get; set; }
        [ParameterMapping("seedTemporal")]
        public int seed { get; set; }
        //[ParameterMapping]
        public string temporalImp { get; set; }
        [ParameterMapping("maxNewSynapseCount")]
        public int newSynapseCount { get; set; }
        [ParameterMapping("maxSynapsesPerSegment")]
        public int maxSynapsesPerSegment { get; set; }
        [ParameterMapping("maxSegmentsPerCell")]
        public int maxSegmentsPerCell { get; set; }
        [ParameterMapping("initialPermanence")]
        public double initialPerm { get; set; }
        [ParameterMapping("permanenceIncrement")]
        public double permanenceInc { get; set; }
        [ParameterMapping("permanenceDecrement")]
        public double permanenceDec { get; set; }
        //[ParameterMapping]
        public double globalDecay { get; set; }
        //[ParameterMapping]
        public int maxAge { get; set; }
        //[ParameterMapping]
        public string outputType { get; set; }
        //[ParameterMapping]
        [TokenReplace("$PERM_TP_CHOICES_pamLength")]
        public object pamLength { get; set; }
    }

    [Serializable]
    public class ClassifierParamsDescription
    {
        public string regionName { get; set; }
        public int verbosity { get; set; }
        [ParameterMapping("classifierAlpha")]
        public double alpha { get; set; }
        [TokenReplace("$PREDICTION_STEPS")]
        [ParameterMapping("classifierSteps")]
        public int[] steps { get; set; }
    }

    #endregion

    #region Permutation Objects

    public interface IPermutionFilter
    {
        /// <summary>
        /// The name of the field being predicted.  Any allowed permutation MUST contain the prediction field.
        ///  (generated from PREDICTION_FIELD)
        /// </summary>
        string predictedField { get; set; }

        PermutationModelParameters permutations { get; set; }

        /// <summary>
        /// Fields selected for final hypersearch report;
        /// NOTE: These values are used as regular expressions by RunPermutations.py's report generator
        /// (fieldname values generated from PERM_PREDICTED_FIELD_NAME)
        /// </summary>
        string[] report { get; set; }

        /// <summary>
        /// Permutation optimization setting: either minimize or maximize metric
        /// used by RunPermutations.
        /// </summary>
        string minimize { get; set; }

        PermutationModelParameters fastSwarmModelParams { get; set; }
        List<string> fixedFields { get; set; }
        int? minParticlesPerSwarm { get; set; }
        bool? killUselessSwarms { get; set; }
        InputPredictedField? inputPredictedField { get; set; }
        bool? tryAll3FieldCombinations { get; set; }
        bool? tryAll3FieldCombinationsWTimestamps { get; set; }
        int? minFieldContribution { get; set; }
        int? maxFieldBranching { get; set; }
        string maximize { get; set; }
        int? maxModels { get; set; }

        IDictionary<string, object> dummyModelParams(PermutationModelParameters perm);
        bool permutationFilter(PermutationModelParameters perm);
    }

    [JsonConverter(typeof(TypedPermutionFilterJsonConverter))]
    [Serializable]
    public abstract class BasePermutations : IPermutionFilter
    {
        protected BasePermutations()
        {
            Type = GetType().AssemblyQualifiedName;
        }

        public abstract IDictionary<string, object> dummyModelParams(PermutationModelParameters perm);

        public abstract bool permutationFilter(PermutationModelParameters perm);

        /// <summary>
        /// Used for deserialisation
        /// </summary>
        public string Type { get; set; }

        [TokenReplace("$PREDICTED_FIELD")]
        public string predictedField { get; set; }
        public PermutationModelParameters permutations { get; set; }
        [TokenReplace("$PREDICTED_FIELD_report")] // [.*$PREDICTED_FIELD.*]
        public string[] report { get; set; }
        [TokenReplace("$PERM_OPTIMIZE_SETTING")]
        public string minimize { get; set; }
        public string maximize { get; set; }
        [TokenReplace("$PERM_FAST_SWARM_MODEL_PARAMS")]
        public PermutationModelParameters fastSwarmModelParams { get; set; }
        [TokenReplace("$PERM_FIXED_FIELDS")]
        public List<string> fixedFields { get; set; }
        public int? minParticlesPerSwarm { get; set; }
        [TokenReplace("$PERM_KILL_USELESS_SWARMS")]
        public bool? killUselessSwarms { get; set; }
        [TokenReplace("$PERM_ALWAYS_INCLUDE_PREDICTED_FIELD")]
        public InputPredictedField? inputPredictedField { get; set; }
        [TokenReplace("$PERM_TRY_ALL_3_FIELD_COMBINATIONS")]
        public bool? tryAll3FieldCombinations { get; set; }
        [TokenReplace("$PERM_TRY_ALL_3_FIELD_COMBINATIONS_W_TIMESTAMPS")]
        public bool? tryAll3FieldCombinationsWTimestamps { get; set; }
        [TokenReplace("$PERM_MIN_FIELD_CONTRIBUTION")]
        public int? minFieldContribution { get; set; }
        [TokenReplace("$PERM_MAX_FIELD_BRANCHING")]
        public int? maxFieldBranching { get; set; }
        [TokenReplace("$PERM_MAX_MODELS")]
        public int? maxModels { get; set; }
    }

    [Serializable]
    public class ClaPermutations : BasePermutations
    {
        public ClaPermutations()
        {
            permutations = new PermutationModelParameters
            {
                modelParams = new PermutationModelDescriptionParams
                {
                    sensorParams = new PermutationSensorParams
                    {
                        encoders = new Map<string, object>()
                    },
                    spParams = new PermutationSpatialPoolerParams(),
                    tpParams = new PermutationTemporalPoolerParams(),
                    clParams = new PermutationClassifierParams()
                }
            };

        }

        #region Overrides of BasePermutations

        public override IDictionary<string, object> dummyModelParams(PermutationModelParameters perm)
        {
            throw new NotImplementedException();
        }

        public override bool permutationFilter(PermutationModelParameters perm)
        {
            return true;
        }

        #endregion
    }

    [Serializable]
    public class PermutationModelParameters
    {
        [TokenReplace("$PERM_AGGREGATION_CHOICES")]
        public AggregationSettings aggregationInfo { get; set; }
        public PermutationModelDescriptionParams modelParams { get; set; }
    }

    [Serializable]
    public class PermutationModelDescriptionParams
    {
        [TokenReplace("$PERM_INFERENCE_TYPE_CHOICES_inferenceType")]
        public object inferenceType { get; set; }
        public PermutationSensorParams sensorParams { get; set; }
        public PermutationSpatialPoolerParams spParams { get; set; }
        public PermutationTemporalPoolerParams tpParams { get; set; }
        public PermutationClassifierParams clParams { get; set; }
    }

    [Serializable]
    public class PermutationSensorParams
    {
        [TokenReplace("$PERM_ENCODER_CHOICES")]
        public Map<string, object> encoders { get; set; }
    }

    [Serializable]
    public class PermutationSpatialPoolerParams
    {
        [TokenReplace("$PERM_SP_CHOICES_synPermInactiveDec")]
        public object synPermInactiveDec { get; set; }
    }

    [Serializable]
    public class PermutationTemporalPoolerParams
    {
        [TokenReplace("$PERM_TP_CHOICES_minThreshold")]
        public object minThreshold { get; set; }  // float, int or permutevar
        [TokenReplace("$PERM_TP_CHOICES_activationThreshold")]
        public object activationThreshold { get; set; } // float, int or permutevar
        [TokenReplace("$PERM_TP_CHOICES_pamLength")]
        public object pamLength { get; set; } // int or permutevar
    }

    [Serializable]
    public class PermutationClassifierParams
    {
        [TokenReplace("$PERM_CL_CHOICES_alpha")]
        public object alpha { get; set; } // float or PermuteVariable
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
        public PermutationModelParameters fastSwarmModelParams { get; set; }

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
            public string encoderType { get; set; }
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
    public class InferenceArgsDescription
    {
        public bool? useReconstruction;

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

        public InferenceArgsDescription Clone()
        {
            return new InferenceArgsDescription
            {
                predictionSteps = (int[])predictionSteps.Clone(),
                predictedField = predictedField,
                inputPredictedField = inputPredictedField
            };
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
    }
}