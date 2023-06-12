using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    // https://github.com/numenta/nupic/blob/master/src/nupic/swarming/HypersearchWorker.py

    /// <summary>
    /// The HypersearchWorker is responsible for evaluating one or more models
    ///   within a specific Hypersearch job.
    ///   One or more instances of this object are launched by the engine, each in a
    ///   separate process.When running within Hadoop, each instance is run within a
    ///   separate Hadoop Map Task.Each instance gets passed the parameters of the
    /// 
    ///   hypersearch via a reference to a search job request record in a "jobs" table
    ///   within a database.
    /// 
    ///   From there, each instance will try different models, based on the search
    /// 
    ///   parameters and share it's results via a "models" table within the same
    /// 
    ///   database.
    ///   The general flow for each worker is this:
    ///       while more models to evaluate:
    /// 
    ///   pick a model based on information about the job and the other models that
    /// 
    ///       have already been evaluated.
    /// 
    ///   mark the model as "in progress" in the "models" table.
    ///   evaluate the model, storing metrics on performance periodically back to
    ///       the model's entry in the "models" table.
    ///       mark the model as "completed" in the "models" table
    ///       """
    /// </summary>
    public class HyperSearchWorker
    {
        private readonly ILog logger = LogManager.GetLogger(typeof(HyperSearchWorker));
        private HypersearchV2 _hs;
        private Dictionary<ulong, uint> _modelIDCtrDict;
        private List<Tuple<ulong, uint>> _modelIDCtrList;
        private HashSet<ulong> _modelIDSet;
        private string _workerID;
        private HyperSearchWorkerOptions _options;
        private MersenneTwister random;

        /// <summary>
        /// Instantiate the Hypersearch worker
        /// </summary>
        /// <param name="options">The command line options. See the main() method for a description of these options</param>
        /// <param name="cmdLineArgs">Copy of the command line arguments, so we can place them in the log</param>
        public HyperSearchWorker(HyperSearchWorkerOptions options, string[] cmdLineArgs)
        {
            // Save options
            this._options = options;

            // Instantiate our logger
            //this.logger = logging.getLogger(".".join(['com.numenta.nupic.swarming', this.__class__.__name__]));

            this.logger.Info(string.Format("Launched with command line arguments: {0}", Arrays.ToArrayString(cmdLineArgs)));

            StringBuilder sb = new StringBuilder();
            var envDict = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry entry in envDict)
            {
                sb.AppendFormat("{{{0}:{1}}},", entry.Key, entry.Value);
            }
            sb.Length = sb.Length - 1;
            this.logger.Debug("Env variables: " + sb);
            // this.logger.debug("Value of nupic.hypersearch.modelOrphanIntervalSecs: %s" \
            //          % Configuration.get('nupic.hypersearch.modelOrphanIntervalSecs'))

            // Init random seed
            random = new MersenneTwister(42);

            // This will hold an instance of a Hypersearch class which handles
            //  the logic of which models to create/evaluate.
            this._hs = null;


            // -------------------------------------------------------------------------
            // These elements form a cache of the update counters we last received for
            // the all models in the database. It is used to determine which models we
            // have to notify the Hypersearch object that the results have changed.

            // This is a dict of modelID -> updateCounter
            this._modelIDCtrDict = new Dictionary<ulong, uint>();

            // This is the above is a list of tuples: (modelID, updateCounter)
            this._modelIDCtrList = new List<Tuple<ulong, uint>>();

            // This is just the set of modelIDs (keys)
            this._modelIDSet = new HashSet<ulong>();

            // This will be filled in by run()
            this._workerID = null;
        }

        /// <summary>
        /// For all models that modified their results since last time this method
        /// was called, send their latest results to the Hypersearch implementation.
        /// </summary>
        /// <param name="cjDAO"></param>
        private void _processUpdatedModels(BaseClientJobDao cjDAO)
        {
            // Get the latest update counters. This returns a list of tuples:
            //  (modelID, updateCounter)
            List<Tuple<ulong, uint>> curModelIDCtrList = cjDAO.modelsGetUpdateCounters(this._options.jobID);
            if (curModelIDCtrList.Count == 0)
            {
                return;
            }

            this.logger.Debug("current modelID/updateCounters: " + Arrays.ToString(curModelIDCtrList));
            this.logger.Debug("last modelID/updateCounters: " + Arrays.ToString(this._modelIDCtrList));

            // --------------------------------------------------------------------
            // Find out which ones have changed update counters. Since these are models
            // that the Hypersearch implementation already knows about, we don't need to
            // send params or paramsHash
            curModelIDCtrList.Sort();
            int numItems = curModelIDCtrList.Count;

            // Each item in the list we are filtering contains:
            //  (idxIntoModelIDCtrList, (modelID, curCtr), (modelID, oldCtr))
            // We only want to keep the ones where the oldCtr != curCtr
            //changedEntries = filter(lambda x: x[1][1] != x[2][1],
            //                  itertools.izip(xrange(numItems), curModelIDCtrList,this._modelIDCtrList));
            var changedEntries = ArrayUtils.Zip(ArrayUtils.XRange(0, numItems, 1), curModelIDCtrList, this._modelIDCtrList)
                .Where(x => ((Tuple<ulong,uint>)x.Item2).Item2 != ((Tuple<ulong, uint>)x.Item3).Item2)
                .ToList();

            if (changedEntries.Count > 0)
            {
                // Update values in our cache
                this.logger.Debug("changedEntries: " + changedEntries);
                foreach (Tuple entry in changedEntries)
                {
                    //(idx, (modelID, curCtr), (_, oldCtr)) = entry;
                    int idx = (int)entry.Item1;
                    ulong modelID = (ulong)((Tuple)entry.Item2).Item1;
                    uint curCtr = (uint)((Tuple)entry.Item2).Item2;
                    int oldCtr = (int)((Tuple)entry.Item3).Item2;

                    this._modelIDCtrDict[modelID] = curCtr;
                    Debug.Assert((ulong)this._modelIDCtrList[idx].Item1 == modelID);
                    Debug.Assert(curCtr != oldCtr);
                    //this._modelIDCtrList[idx].Item2 = curCtr;
                    this._modelIDCtrList[idx]=new Tuple<ulong, uint>(_modelIDCtrList[idx].Item1, curCtr);
                }


                // Tell Hypersearch implementation of the updated results for each model
                //changedModelIDs = [x[1][0] for x in changedEntries];
                List<ulong> changedModelIDs = changedEntries.Select(x => (ulong)((Tuple)x.Item2).Item1).ToList();
                List<ResultAndStatusModel> modelResults = cjDAO.modelsGetResultAndStatus(changedModelIDs);
                foreach (var mResult in modelResults)
                {
                    Tuple results = mResult.results;
                    if (mResult.resultsSerialized != null)
                    {
                        results = JsonConvert.DeserializeObject<Tuple>(mResult.resultsSerialized);
                    }
                    this._hs.recordModelProgress(modelID: mResult.modelId,
                                 modelParams: null,
                                 modelParamsHash: mResult.engParamsHash,
                                 results: results,
                                 completed: (mResult.status == BaseClientJobDao.STATUS_COMPLETED),
                                 completionReason: mResult.completionReason,
                                 matured: mResult.engMatured,
                                 numRecords: mResult.numRecords);
                }
            }

            // --------------------------------------------------------------------
            // Figure out which ones are newly arrived and add them to our
            //   cache
            //curModelIDSet = set([x[0] for x in curModelIDCtrList]);
            var curModelIDSet = new HashSet<ulong>(curModelIDCtrList.Select(x => (ulong)x.Item1));
            var newModelIDs = curModelIDSet.Except(this._modelIDSet).ToList();
            if (newModelIDs.Count > 0)
            {

                // Add new modelID and counters to our cache
                this._modelIDSet.UnionWith(newModelIDs);
                var curModelIDCtrDict = curModelIDCtrList.ToDictionary(k => (ulong)k.Item1, v => (uint)v.Item2);  //  dict(curModelIDCtrList);

                // Get the results for each of these models and send them to the
                //  Hypersearch implementation.
                var modelInfos = cjDAO.modelsGetResultAndStatus(newModelIDs);
                modelInfos.Sort();
                var modelParamsAndHashs = cjDAO.modelsGetParams(newModelIDs);
                modelParamsAndHashs.Sort();

                //for (mResult, mParamsAndHash) in itertools.izip(modelInfos,modelParamsAndHashs)
                foreach (var resultAndParamsAndHash in ArrayUtils.Zip(modelInfos, modelParamsAndHashs))
                {
                    var mResult = (ResultAndStatusModel)resultAndParamsAndHash.Item1;
                    var mParamsAndHash = (NamedTuple)resultAndParamsAndHash.Item2;
                    ulong modelID = mResult.modelId;
                    Debug.Assert(modelID == (ulong)mParamsAndHash["modelId"]);

                    // Update our cache of IDs and update counters
                    this._modelIDCtrDict[modelID] = curModelIDCtrDict[modelID];
                    this._modelIDCtrList.Add(new Tuple<ulong,uint>(modelID, curModelIDCtrDict[modelID]));//[modelID, curModelIDCtrDict[modelID]]);

                    // Tell the Hypersearch implementation of the new model
                    Tuple results = mResult.results;
                    if (results == null && mResult.resultsSerialized != null)
                    {
                        results = mResult.results = JsonConvert.DeserializeObject<Tuple>(mResult.resultsSerialized);
                    }

                    this._hs.recordModelProgress(modelID: modelID,
                        modelParams: JsonConvert.DeserializeObject<ModelParams>(mParamsAndHash["params"] as string),
                        modelParamsHash: mParamsAndHash["engParamsHash"] as string,
                        results: results,
                        completed: (mResult.status == BaseClientJobDao.STATUS_COMPLETED),
                        completionReason: (mResult.completionReason),
                        matured: mResult.engMatured,
                        numRecords: mResult.numRecords);
                }
                // Keep our list sorted
                this._modelIDCtrList.Sort();
            }
        }

        /// <summary>
        /// Run this worker.
        /// </summary>
        /// <returns>
        /// jobID of the job we ran. 
        /// This is used by unit test code when calling this working using the --params command
        /// line option(which tells this worker to insert the job itself).</returns>
        public uint? Run()
        {
            // Easier access to options
            var options = this._options;

            // ---------------------------------------------------------------------
            // Connect to the jobs database
            this.logger.Info("Connecting to the jobs database");
            var cjDAO = BaseClientJobDao.Create();

            // Get our worker ID
            this._workerID = cjDAO.getConnectionID();

            if (options.clearModels)
            {
                cjDAO.modelsClearAll();
            }

            // -------------------------------------------------------------------------
            // if params were specified on the command line, insert a new job using them.
            if (options.@params != null)
            {
                options.jobID = cjDAO.jobInsert(client: "hwTest", cmdLine: "echo 'test mode'",
                            @params: options.@params, alreadyRunning: true,
                            minimumWorkers: 1, maximumWorkers: 1,
                            jobType: BaseClientJobDao.JOB_TYPE_HS);
            }

            string wID;
            if (options.workerID != null)
            {
                wID = options.workerID;
            }
            else
            {
                wID = this._workerID;
            }

            //int buildID = Assembly.GetExecutingAssembly().GetName().Version.Build;  //Configuration.get('nupic.software.buildNumber', 'N/A');
            //string logPrefix = string.Format("<BUILDID={0}, WORKER=HW, WRKID={1}, JOBID={2}> ", buildID, wID, options.jobID);
            //ExtendedLogger.setLogPrefix(logPrefix);

            // ---------------------------------------------------------------------
            // Get the search parameters
            // If asked to reset the job status, do that now
            if (options.resetJobStatus)
            {
                cjDAO.jobSetFields(options.jobID,
                    fields: new Dictionary<string, object>
                    {
                        {"workerCompletionReason", BaseClientJobDao.CMPL_REASON_SUCCESS},
                        {"cancel", false},
                        //'engWorkerState': None
                    },
                    useConnectionID: false,
                    ignoreUnchanged: true);
            }
            NamedTuple jobInfo = cjDAO.jobInfo(options.jobID);
            this.logger.Info($"Job info retrieved: {Utils.ClippedObj(jobInfo)}");


            // ---------------------------------------------------------------------
            // Instantiate the Hypersearch object, which will handle the logic of
            //  which models to create when we need more to evaluate.

            var jobParamsMap = JsonConvert.DeserializeObject<Map<string, object>>(jobInfo["params"] as string);

            HyperSearchSearchParams jobParams = new HyperSearchSearchParams();
            jobParams.Populate(jobParamsMap);

            // Validate job params
            //var jsonSchemaPath = os.Path.join(os.Path.dirname(__file__), "jsonschema", "jobParamsSchema.json");
            //Utils.validate(jobParams, new Dictionary<string, object> { { "schemaPath", jsonSchemaPath } });

            string hsVersion = jobParams.hsVersion ?? null;
            if (hsVersion == "v2")
            {
                this._hs = new HypersearchV2(searchParams: jobParams, workerID: this._workerID,
                        cjDAO: cjDAO, jobID: options.jobID);
            }
            else
            {
                throw new InvalidOperationException(string.Format("Invalid Hypersearch implementation ({0}) specified", hsVersion));
            }


            // =====================================================================
            // The main loop.
            ulong? modelIDToRun;
            int numModelsTotal = 0;
            bool exit = false;
            try
            {
                Console.WriteLine("reporter:status:Evaluating first model...");
                while (!exit)
                {
                    // ------------------------------------------------------------------
                    // Choose a model to evaluate
                    int batchSize = 10;     // How many to try at a time.
                    modelIDToRun = null;

                    bool ours = false;
                    List<Tuple<ModelParams, string, string>> newModels;

                    bool success = true;
                    ModelParams modelParams = null;
                    string modelParamsHash = null;
                    string particleHash = null;

                    while (modelIDToRun == null)
                    {
                        #region While Loop Contents

                        if (options.modelID == null)
                        {
                            // -----------------------------------------------------------------
                            // Get the latest results on all running models and send them to the Hypersearch implementation
                            // This calls cjDAO.modelsGetUpdateCounters(), compares the
                            // updateCounters with what we have cached, fetches the results for the
                            // changed and new models, and sends those to the Hypersearch
                            // implementation's this._hs.recordModelProgress() method.
                            this._processUpdatedModels(cjDAO);

                            // --------------------------------------------------------------------
                            // Create a new batch of models
                            //(exit, newModels)
                            var createPair = this._hs.createModels(numModels: batchSize);
                            exit = createPair.Item1;
                            newModels = createPair.Item2;
                            if (exit)
                            {
                                break;
                            }

                            // No more models left to create, just loop. The _hs is waiting for
                            //   all remaining running models to complete, and may pick up on an
                            //  orphan if it detects one.
                            if (newModels.Count == 0)
                            {
                                continue;
                            }

                            // Try and insert one that we will run
                            //for (modelParams, modelParamsHash, particleHash) in newModels
                            foreach (var tuple in newModels)
                            {
                                modelParams = tuple.Item1;
                                modelParamsHash = tuple.Item2;
                                particleHash = tuple.Item3;
                                string jsonModelParams = JsonConvert.SerializeObject(modelParams);

                                Debug.WriteLine("Evaluating model particle: " + particleHash);

                                var insertTuple = cjDAO.modelInsertAndStart(options.jobID,
                                    jsonModelParams, modelParamsHash, particleHash);
                                ulong modelID = insertTuple.Item1;
                                ours = insertTuple.Item2;

                                // Some other worker is already running it, tell the Hypersearch object
                                //  so that it doesn't try and insert it again
                                if (!ours)
                                {
                                    var mParamsAndHash = cjDAO.modelsGetParams(new List<ulong> { modelID })[0];
                                    var mResult = cjDAO.modelsGetResultAndStatus(new List<ulong> { modelID })[0];
                                    var results = mResult.results;
                                    if (mResult.resultsSerialized != null)
                                    {
                                        results = JsonConvert.DeserializeObject<Tuple>(mResult.resultsSerialized);
                                    }

                                    modelParams = JsonConvert.DeserializeObject<ModelParams>(mParamsAndHash["params"] as string);
                                    particleHash = cjDAO.modelsGetFields(modelID, new[] { "engParticleHash" })._eng_particle_hash;
                                    string particleInst = string.Format("{0}.{1}", modelParams.particleState.id,
                                        modelParams.particleState.genIdx);

                                    this.logger.Info(string.Format("Adding model {0} to our internal DB " +
                                                                   "because modelInsertAndStart() failed to insert it: " +
                                                                   "paramsHash={1}, particleHash={2}, particleId='{3}'",
                                        modelID,
                                        mParamsAndHash["engParamsHash"], particleHash, particleInst));

                                    this._hs.recordModelProgress(modelID: modelID,
                                        modelParams: modelParams,
                                        modelParamsHash: mParamsAndHash["engParamsHash"] as string,
                                        results: results,
                                        completed: (mResult.status == BaseClientJobDao.STATUS_COMPLETED),
                                        completionReason: mResult.completionReason,
                                        matured: mResult.engMatured,
                                        numRecords: mResult.numRecords);
                                }
                                else
                                {
                                    modelIDToRun = modelID;
                                    break;
                                }
                            }
                        }

                        else
                        {
                            // A specific modelID was passed on the command line
                            modelIDToRun = ulong.Parse(options.modelID);
                            var mParamsAndHash = cjDAO.modelsGetParams(new List<ulong> { modelIDToRun.Value })[0];
                            modelParams = JsonConvert.DeserializeObject<ModelParams>(mParamsAndHash["params"] as string);
                            modelParamsHash = mParamsAndHash["engParamsHash"] as string;

                            // Make us the worker
                            cjDAO.modelSetFields(modelIDToRun,
                                new Dictionary<string, object> { { "engWorkerConnId", this._workerID } });
                            //dict(engWorkerConnId = this._workerID)
                            if (false)
                            {
                                // Change the hash and params of the old entry so that we can
                                //  create a new model with the same params
                                //foreach (var attempt in ArrayUtils.Range(0, 1000))
                                //{
                                //    paramsHash = hashlib.md5("OrphanParams.%d.%d" % (modelIDToRun, attempt)).digest();
                                //    particleHash = hashlib.md5("OrphanParticle.%d.%d" % (modelIDToRun, attempt)).digest();
                                //    try
                                //    {
                                //        cjDAO.modelSetFields(modelIDToRun, dict(engParamsHash = paramsHash,
                                //                engParticleHash = particleHash));
                                //        success = true;
                                //    }
                                //    catch
                                //    {
                                //        success = false;
                                //    }
                                //    if (success)
                                //    {
                                //        break;
                                //    }
                                //}
                                //if (!success)
                                //{
                                //    throw new InvalidOperationException(
                                //        "Unexpected failure to change paramsHash and particleHash of orphaned model");
                                //}

                                //// (modelIDToRun, ours)
                                //var insertPair = cjDAO.modelInsertAndStart(options.jobID,
                                //    mParamsAndHash["params"] as string, modelParamsHash);
                                //modelIDToRun = insertPair.Item1;
                                //ours = insertPair.Item2;
                            }

                            // ^^^ end while modelIDToRun ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                        }

                        #endregion
                    }

                    // ---------------------------------------------------------------
                    // We have a model, evaluate it now
                    // All done?
                    if (exit)
                    {
                        break;
                    }

                    // Run the model now
                    this.logger.Info(string.Format("RUNNING MODEL GID={0}, paramsHash={1}, params={2}",
                        modelIDToRun, modelParamsHash, modelParams));

                    // ---------------------------------------------------------------------
                    // Construct model checkpoint GUID for this model:
                    // jobParams['persistentJobGUID'] contains the client's (e.g., API Server)
                    // persistent, globally-unique model identifier, which is what we need;
                    var persistentJobGUID = jobParams.persistentJobGUID;
                    Debug.Assert(persistentJobGUID != null, "persistentJobGUID: " + persistentJobGUID);

                    string modelCheckpointGUID = jobInfo["client"] + "_" + persistentJobGUID + ('_' + modelIDToRun);


                    this._hs.runModel(modelID: modelIDToRun.GetValueOrDefault(), jobID: options.jobID,
                        modelParams: modelParams, modelParamsHash: modelParamsHash,
                        jobsDAO: cjDAO, modelCheckpointGUID: ref modelCheckpointGUID);

                    // TODO: don't increment for orphaned models
                    numModelsTotal += 1;

                    this.logger.Info(string.Format("COMPLETED MODEL GID={0}; EVALUATED {1} MODELs", modelIDToRun, numModelsTotal));
                    Console.WriteLine("reporter:status:Evaluated {0} models...", numModelsTotal);
                    Console.WriteLine("reporter:counter:HypersearchWorker,numModels,1");

                    if (options.modelID != null)
                    {
                        exit = true;
                    }
                    // ^^^ end while not exit
                }
            }
            catch (Exception e)
            {
                if(Debugger.IsAttached) Debugger.Break();
                throw;
            }
            finally
            {
                // Provide Hypersearch instance an opportunity to clean up temporary files
                this._hs.close();
            }

            this.logger.Info(string.Format("FINISHED. Evaluated {0} models.", numModelsTotal));
            Console.WriteLine("reporter:status:Finished, evaluated {0} models", numModelsTotal);
            return options.jobID;
        }

        /// <summary>
        /// The main function of the HypersearchWorker script. This parses the command
        /// line arguments, instantiates a HypersearchWorker instance, and then
        /// runs it.
        /// Parameters:
        /// ----------------------------------------------------------------------
        /// retval:     jobID of the job we ran. This is used by unit test code
        ///             when calling this working using the --params command
        ///             line option(which tells this worker to insert the job
        ///             itself).
        /// </summary>
        /// <returns></returns>
        public static uint? Main(string[] argv)
        {
            HyperSearchWorkerOptions options = ParseArguments(argv);

            if (options.jobID.HasValue && !string.IsNullOrWhiteSpace(options.@params))
            {
                throw new InvalidOperationException("--jobID and --params can not be used at the same time");
            }

            if (!options.jobID.HasValue && string.IsNullOrWhiteSpace(options.@params))
            {
                throw new InvalidOperationException("Either --jobID or --params must be specified.");
            }

            //initLogging(verbose = True);

            // Instantiate the HypersearchWorker and run it
            var hst = new HyperSearchWorker(options, argv);

            // Normal use. This is one of among a number of workers. If we encounter
            //  an exception at the outer loop here, we fail the entire job.
            uint? jobID;
            if (options.@params == null)
            {
                try
                {
                    jobID = hst.Run();
                }

                catch (Exception e)
                {
                    jobID = options.jobID;
                    //msg = StringIO.StringIO();
                    //print >> msg, "%s: Exception occurred in Hypersearch Worker: %r" % \ (ErrorCodes.hypersearchLogicErr, e);
                    //traceback.print_exc(None, msg);
                    string msg = string.Format("hypersearchLogicErr: Exception occurred in Hypersearch Worker: {0}", e);
                    string completionReason = BaseClientJobDao.CMPL_REASON_ERROR;
                    string completionMsg = msg;
                    hst.logger.Error(completionMsg);

                    // If no other worker already marked the job as failed, do so now.
                    var jobsDAO = BaseClientJobDao.Create();
                    string workerCmpReason = jobsDAO.jobGetFields(options.jobID, new[] { "workerCompletionReason" })[0] as string;
                    if (workerCmpReason == BaseClientJobDao.CMPL_REASON_SUCCESS)
                    {
                        jobsDAO.jobSetFields(options.jobID, fields: new Dictionary<string, object>
                            {
                                {"cancel", true},
                                {"workerCompletionReason", BaseClientJobDao.CMPL_REASON_ERROR},
                                {"workerCompletionMsg", completionMsg}
                            },
                            useConnectionID: false,
                            ignoreUnchanged: true);
                    }
                    throw;
                }
            }


            // Run just 1 worker for the entire job. Used for unit tests that run in
            // 1 process
            else
            {
                jobID = null;
                string completionReason = BaseClientJobDao.CMPL_REASON_SUCCESS;
                string completionMsg = "Success";

                try
                {
                    jobID = hst.Run();
                }
                catch (Exception e)
                {
                    jobID = hst._options.jobID;
                    completionReason = BaseClientJobDao.CMPL_REASON_ERROR;
                    completionMsg = string.Format("ERROR: {0}", e);
                    throw;
                }
                finally
                {
                    if (jobID.HasValue)
                    {
                        var cjDAO = BaseClientJobDao.Create();
                        cjDAO.jobSetCompleted(jobID: jobID,
                                              completionReason: completionReason,
                                              completionMsg: completionMsg);
                    }
                }
            }

            return jobID;
        }

        private static HyperSearchWorkerOptions ParseArguments(string[] argv)
        {
            var list = argv.Where(a => a.StartsWith("--")).Select(a => a.TrimStart('-')).ToList();

            HyperSearchWorkerOptions options = new HyperSearchWorkerOptions();

            foreach (string arg in list)
            {
                if (arg.StartsWith("jobID="))
                {
                    options.jobID = uint.Parse(GetArgValue(arg));
                }
                else if (arg == "clearModels")
                {
                    options.clearModels = true;
                }
            }

            return options;
        }

        private static string GetArgValue(string arg)
        {
            return arg.Split('=').Skip(1).FirstOrDefault();
        }
    }


    public class HyperSearchWorkerOptions
    {
        public uint? jobID { get; set; }
        public string modelID { get; set; }
        public string workerID { get; set; }
        public string @params { get; set; } //json
        public bool clearModels { get; set; }
        public bool resetJobStatus { get; set; }

        /*
          parser.add_option("--jobID", action="store", type="int", default=None,
                help="jobID of the job within the dbTable [default: %default].");

          parser.add_option("--modelID", action="store", type="str", default=None,
                help=("Tell worker to re-run this model ID. When specified, jobID "
                 "must also be specified [default: %default]."));

          parser.add_option("--workerID", action="store", type="str", default=None,
                help=("workerID of the scheduler's SlotAgent (GenericWorker) that "
                  "hosts this SpecializedWorker [default: %default]."));

          parser.add_option("--params", action="store", default=None,
                help="Create and execute a new hypersearch request using this JSON " \
                "format params string. This is helpful for unit tests and debugging. " \
                "When specified jobID must NOT be specified. [default: %default].");

          parser.add_option("--clearModels", action="store_true", default=False,
                help="clear out the models table before starting [default: %default].");

          parser.add_option("--resetJobStatus", action="store_true", default=False,
                help="Reset the job status before starting  [default: %default].");

          parser.add_option("--logLevel", action="store", type="int", default=None,
                help="override default log level. Pass in an integer value that "
                "represents the desired logging level (10=logging.DEBUG, "
                "20=logging.INFO, etc.) [default: %default].");
        */
    }

    /*





helpString = \
"""%prog [options]
This script runs as a Hypersearch worker process. It loops, looking for and
evaluating prospective models from a Hypersearch database.
""";



def main(argv)
{
  """
  The main function of the HypersearchWorker script. This parses the command
  line arguments, instantiates a HypersearchWorker instance, and then
  runs it.
  Parameters:
  ----------------------------------------------------------------------
  retval:     jobID of the job we ran. This is used by unit test code
                when calling this working using the --params command
                line option (which tells this worker to insert the job
                itself).
  """;

  parser = OptionParser(helpString);

  parser.add_option("--jobID", action="store", type="int", default=None,
        help="jobID of the job within the dbTable [default: %default].");

  parser.add_option("--modelID", action="store", type="str", default=None,
        help=("Tell worker to re-run this model ID. When specified, jobID "
         "must also be specified [default: %default]."));

  parser.add_option("--workerID", action="store", type="str", default=None,
        help=("workerID of the scheduler's SlotAgent (GenericWorker) that "
          "hosts this SpecializedWorker [default: %default]."));

  parser.add_option("--params", action="store", default=None,
        help="Create and execute a new hypersearch request using this JSON " \
        "format params string. This is helpful for unit tests and debugging. " \
        "When specified jobID must NOT be specified. [default: %default].");

  parser.add_option("--clearModels", action="store_true", default=False,
        help="clear out the models table before starting [default: %default].");

  parser.add_option("--resetJobStatus", action="store_true", default=False,
        help="Reset the job status before starting  [default: %default].");

  parser.add_option("--logLevel", action="store", type="int", default=None,
        help="override default log level. Pass in an integer value that "
        "represents the desired logging level (10=logging.DEBUG, "
        "20=logging.INFO, etc.) [default: %default].");

  // Evaluate command line arguments
  (options, args) = parser.parse_args(argv[1:]);
  if( len(args) != 0)
  {
    raise RuntimeError("Expected no command line arguments, but got: %s" % \
                        (args));
  }

  if (options.jobID and options.params)
  {
    raise RuntimeError("--jobID and --params can not be used at the same time");
  }

  if (options.jobID is None and options.params is None)
  {
    raise RuntimeError("Either --jobID or --params must be specified.");
  }

  initLogging(verbose=True);

  // Instantiate the HypersearchWorker and run it
  hst = HypersearchWorker(options, argv[1:]);

  // Normal use. This is one of among a number of workers. If we encounter
  //  an exception at the outer loop here, we fail the entire job.
  if( options.params is None)
  {
    try
    {
      jobID = hst.run();
    }

    catch( Exception, e)
    {
      jobID = options.jobID;
      msg = StringIO.StringIO();
      print >>msg, "%s: Exception occurred in Hypersearch Worker: %r" % \
         (ErrorCodes.hypersearchLogicErr, e);
      traceback.print_exc(None, msg);

      completionReason = ClientJobsDAO.CMPL_REASON_ERROR;
      completionMsg = msg.getvalue();
      hst.logger.error(completionMsg);

      // If no other worker already marked the job as failed, do so now.
      jobsDAO = ClientJobsDAO.get();
      workerCmpReason = jobsDAO.jobGetFields(options.jobID,
          ['workerCompletionReason'])[0];
      if( workerCmpReason == ClientJobsDAO.CMPL_REASON_SUCCESS)
      {
        jobsDAO.jobSetFields(options.jobID, fields=dict(
            cancel=True,
            workerCompletionReason = ClientJobsDAO.CMPL_REASON_ERROR,
            workerCompletionMsg = completionMsg),
            useConnectionID=False,
            ignoreUnchanged=True);
      }
    }
  }


  // Run just 1 worker for the entire job. Used for unit tests that run in
  // 1 process
  else
  {
    jobID = None;
    completionReason = ClientJobsDAO.CMPL_REASON_SUCCESS;
    completionMsg = "Success";

    try
    {
      jobID = hst.run();
    }
    catch( Exception, e)
    {
      jobID = hst._options.jobID;
      completionReason = ClientJobsDAO.CMPL_REASON_ERROR;
      completionMsg = "ERROR: %s" % (e,);
      raise;
    }
    finally
    {
      if( jobID is not None)
      {
        cjDAO = ClientJobsDAO.get();
        cjDAO.jobSetCompleted(jobID=jobID,
                              completionReason=completionReason,
                              completionMsg=completionMsg);
      }
    }
  }

  return jobID;
}



if( __name__ == "__main__")
{
  logging.setLoggerClass(ExtendedLogger);
  buildID = Configuration.get('nupic.software.buildNumber', 'N/A');
  logPrefix = '<BUILDID=%s, WORKER=HS, WRKID=N/A, JOBID=N/A> ' % buildID;
  ExtendedLogger.setLogPrefix(logPrefix);

  try
  {
    main(sys.argv);
  }
  catch()
  {
    logging.exception("HypersearchWorker is exiting with unhandled exception; "
                      "argv=%r", sys.argv);
    raise;
  }
}


    class HypersearchWorker(object)
{
  def __init__(self, options, cmdLineArgs)
  {

    // Save options
    this._options = options;

    // Instantiate our logger
    this.logger = logging.getLogger(".".join(
        ['com.numenta.nupic.swarming', this.__class__.__name__]));


    // Override log level?
    if( options.logLevel is not None)
    {
      this.logger.setLevel(options.logLevel);
    }


    this.logger.info("Launched with command line arguments: %s" %
                      str(cmdLineArgs));

    this.logger.debug("Env variables: %s" % (pprint.pformat(os.environ)));
    #this.logger.debug("Value of nupic.hypersearch.modelOrphanIntervalSecs: %s" \
    //          % Configuration.get('nupic.hypersearch.modelOrphanIntervalSecs'))

    // Init random seed
    random.seed(42);

    // This will hold an instance of a Hypersearch class which handles
    //  the logic of which models to create/evaluate.
    this._hs = None;


    // -------------------------------------------------------------------------
    // These elements form a cache of the update counters we last received for
    // the all models in the database. It is used to determine which models we
    // have to notify the Hypersearch object that the results have changed.

    // This is a dict of modelID -> updateCounter
    this._modelIDCtrDict = dict();

    // This is the above is a list of tuples: (modelID, updateCounter)
    this._modelIDCtrList = [];

    // This is just the set of modelIDs (keys)
    this._modelIDSet = set();

    // This will be filled in by run()
    this._workerID = None;
  }
    def _processUpdatedModels(self, cjDAO)
  {
    """ For all models that modified their results since last time this method
    was called, send their latest results to the Hypersearch implementation.
    """;


    // Get the latest update counters. This returns a list of tuples:
    //  (modelID, updateCounter)
    curModelIDCtrList = cjDAO.modelsGetUpdateCounters(this._options.jobID);
    if( len(curModelIDCtrList) == 0)
    {
      return;
    }

    this.logger.debug("current modelID/updateCounters: %s" \
                      % (str(curModelIDCtrList)));
    this.logger.debug("last modelID/updateCounters: %s" \
                      % (str(this._modelIDCtrList)));

    // --------------------------------------------------------------------
    // Find out which ones have changed update counters. Since these are models
    // that the Hypersearch implementation already knows about, we don't need to
    // send params or paramsHash
    curModelIDCtrList = sorted(curModelIDCtrList);
    numItems = len(curModelIDCtrList);

    // Each item in the list we are filtering contains:
    //  (idxIntoModelIDCtrList, (modelID, curCtr), (modelID, oldCtr))
    // We only want to keep the ones where the oldCtr != curCtr
    changedEntries = filter(lambda x:x[1][1] != x[2][1],
                      itertools.izip(xrange(numItems), curModelIDCtrList,
                                     this._modelIDCtrList));

    if( len(changedEntries) > 0)
    {
      // Update values in our cache
      this.logger.debug("changedEntries: %s", str(changedEntries));
      for( entry in changedEntries)
      {
        (idx, (modelID, curCtr), (_, oldCtr)) = entry;
        this._modelIDCtrDict[modelID] = curCtr;
        assert (this._modelIDCtrList[idx][0] == modelID);
        assert (curCtr != oldCtr);
        this._modelIDCtrList[idx][1] = curCtr;
      }


      // Tell Hypersearch implementation of the updated results for each model
      changedModelIDs = [x[1][0] for x in changedEntries];
      modelResults = cjDAO.modelsGetResultAndStatus(changedModelIDs);
      for( mResult in modelResults)
      {
        results = mResult.results;
        if( results is not None)
        {
          results = json.loads(results);
        }
        this._hs.recordModelProgress(modelID=mResult.modelId,
                     modelParams = None,
                     modelParamsHash = mResult.engParamsHash,
                     results = results,
                     completed = (mResult.status == cjDAO.STATUS_COMPLETED),
                     completionReason = mResult.completionReason,
                     matured = mResult.engMatured,
                     numRecords = mResult.numRecords);
      }
    }

    // --------------------------------------------------------------------
    // Figure out which ones are newly arrived and add them to our
    //   cache
    curModelIDSet = set([x[0] for x in curModelIDCtrList]);
    newModelIDs = curModelIDSet.difference(this._modelIDSet);
    if( len(newModelIDs) > 0)
    {

      // Add new modelID and counters to our cache
      this._modelIDSet.update(newModelIDs);
      curModelIDCtrDict = dict(curModelIDCtrList);

      // Get the results for each of these models and send them to the
      //  Hypersearch implementation.
      modelInfos = cjDAO.modelsGetResultAndStatus(newModelIDs);
      modelInfos.sort();
      modelParamsAndHashs = cjDAO.modelsGetParams(newModelIDs);
      modelParamsAndHashs.sort();

      for (mResult, mParamsAndHash) in itertools.izip(modelInfos,
                                                  modelParamsAndHashs)
      {

        modelID = mResult.modelId;
        assert (modelID == mParamsAndHash.modelId);

        // Update our cache of IDs and update counters
        this._modelIDCtrDict[modelID] = curModelIDCtrDict[modelID];
        this._modelIDCtrList.append([modelID, curModelIDCtrDict[modelID]]);

        // Tell the Hypersearch implementation of the new model
        results = mResult.results;
        if( results is not None)
        {
          results = json.loads(mResult.results);
        }

        this._hs.recordModelProgress(modelID = modelID,
            modelParams = json.loads(mParamsAndHash.params),
            modelParamsHash = mParamsAndHash.engParamsHash,
            results = results,
            completed = (mResult.status == cjDAO.STATUS_COMPLETED),
            completionReason = (mResult.completionReason),
            matured = mResult.engMatured,
            numRecords = mResult.numRecords);
      }




      // Keep our list sorted
      this._modelIDCtrList.sort();
    }
  }





  def run(self)
  {

    // Easier access to options
    options = this._options;

    // ---------------------------------------------------------------------
    // Connect to the jobs database
    this.logger.info("Connecting to the jobs database");
    cjDAO = ClientJobsDAO.get();

    // Get our worker ID
    this._workerID = cjDAO.getConnectionID();

    if( options.clearModels)
    {
      cjDAO.modelsClearAll();
    }

    // -------------------------------------------------------------------------
    // if params were specified on the command line, insert a new job using
    //  them.
    if( options.params is not None)
    {
      options.jobID = cjDAO.jobInsert(client='hwTest', cmdLine="echo 'test mode'",
                  params=options.params, alreadyRunning=True,
                  minimumWorkers=1, maximumWorkers=1,
                  jobType = cjDAO.JOB_TYPE_HS);
    }
    if( options.workerID is not None)
    {
      wID = options.workerID;
    }
    else
    {
      wID = this._workerID;
    }

    buildID = Configuration.get('nupic.software.buildNumber', 'N/A');
    logPrefix = '<BUILDID=%s, WORKER=HW, WRKID=%s, JOBID=%s> ' % \
                (buildID, wID, options.jobID);
    ExtendedLogger.setLogPrefix(logPrefix);

    // ---------------------------------------------------------------------
    // Get the search parameters
    // If asked to reset the job status, do that now
    if( options.resetJobStatus)
    {
      cjDAO.jobSetFields(options.jobID,
           fields={'workerCompletionReason': ClientJobsDAO.CMPL_REASON_SUCCESS,
                   'cancel': False,
                   #'engWorkerState': None
                   },
           useConnectionID=False,
           ignoreUnchanged=True);
    }
    jobInfo = cjDAO.jobInfo(options.jobID);
    this.logger.info("Job info retrieved: %s" % (str(clippedObj(jobInfo))));


    // ---------------------------------------------------------------------
    // Instantiate the Hypersearch object, which will handle the logic of
    //  which models to create when we need more to evaluate.
    jobParams = json.loads(jobInfo.params);

    // Validate job params
    jsonSchemaPath = os.Path.join(os.Path.dirname(__file__),
                                  "jsonschema",
                                  "jobParamsSchema.json");
    validate(jobParams, schemaPath=jsonSchemaPath);


    hsVersion = jobParams.get('hsVersion', None);
    if( hsVersion == 'v2')
    {
      this._hs = HypersearchV2(searchParams=jobParams, workerID=this._workerID,
              cjDAO=cjDAO, jobID=options.jobID, logLevel=options.logLevel);
    }
    else
    {
      raise RuntimeError("Invalid Hypersearch implementation (%s) specified" \
                          % (hsVersion));
    }


    // =====================================================================
    // The main loop.
    try
    {
      exit = False;
      numModelsTotal = 0;
      print >>sys.stderr, "reporter:status:Evaluating first model...";
      while( not exit)
      {

        // ------------------------------------------------------------------
        // Choose a model to evaluate
        batchSize = 10;              // How many to try at a time.
        modelIDToRun = None;
        while( modelIDToRun is None)
        {

          if( options.modelID is None)
          {
            // -----------------------------------------------------------------
            // Get the latest results on all running models and send them to
            //  the Hypersearch implementation
            // This calls cjDAO.modelsGetUpdateCounters(), compares the
            // updateCounters with what we have cached, fetches the results for the
            // changed and new models, and sends those to the Hypersearch
            // implementation's this._hs.recordModelProgress() method.
            this._processUpdatedModels(cjDAO);

            // --------------------------------------------------------------------
            // Create a new batch of models
            (exit, newModels) = this._hs.createModels(numModels = batchSize);
            if( exit)
            {
              break;
            }

            // No more models left to create, just loop. The _hs is waiting for
            //   all remaining running models to complete, and may pick up on an
            //  orphan if it detects one.
            if( len(newModels) == 0)
            {
              continue;
            }

            // Try and insert one that we will run
            for (modelParams, modelParamsHash, particleHash) in newModels
            {
              jsonModelParams = json.dumps(modelParams);
              (modelID, ours) = cjDAO.modelInsertAndStart(options.jobID,
                                  jsonModelParams, modelParamsHash, particleHash);

              // Some other worker is already running it, tell the Hypersearch object
              //  so that it doesn't try and insert it again
              if( not ours)
              {
                mParamsAndHash = cjDAO.modelsGetParams([modelID])[0];
                mResult = cjDAO.modelsGetResultAndStatus([modelID])[0];
                results = mResult.results;
                if( results is not None)
                {
                  results = json.loads(results);
                }

                modelParams = json.loads(mParamsAndHash.params);
                particleHash = cjDAO.modelsGetFields(modelID,
                                  ['engParticleHash'])[0];
                particleInst = "%s.%s" % (
                          modelParams['particleState']['id'],
                          modelParams['particleState']['genIdx']);
                this.logger.info("Adding model %d to our internal DB " \
                      "because modelInsertAndStart() failed to insert it: " \
                      "paramsHash=%s, particleHash=%s, particleId='%s'", modelID,
                      mParamsAndHash.engParamsHash.encode('hex'),
                      particleHash.encode('hex'), particleInst);
                this._hs.recordModelProgress(modelID = modelID,
                      modelParams = modelParams,
                      modelParamsHash = mParamsAndHash.engParamsHash,
                      results = results,
                      completed = (mResult.status == cjDAO.STATUS_COMPLETED),
                      completionReason = mResult.completionReason,
                      matured = mResult.engMatured,
                      numRecords = mResult.numRecords);
              }
              else
              {
                modelIDToRun = modelID;
                break;
              }
            }
          }

          else
          {
            // A specific modelID was passed on the command line
            modelIDToRun = int(options.modelID);
            mParamsAndHash = cjDAO.modelsGetParams([modelIDToRun])[0];
            modelParams = json.loads(mParamsAndHash.params);
            modelParamsHash = mParamsAndHash.engParamsHash;

            // Make us the worker
            cjDAO.modelSetFields(modelIDToRun,
                                     dict(engWorkerConnId=this._workerID));
            if( False)
            {
              // Change the hash and params of the old entry so that we can
              //  create a new model with the same params
              for( attempt in range(1000))
              {
                paramsHash = hashlib.md5("OrphanParams.%d.%d" % (modelIDToRun,
                                                                 attempt)).digest();
                particleHash = hashlib.md5("OrphanParticle.%d.%d" % (modelIDToRun,
                                                                  attempt)).digest();
                try
                {
                  cjDAO.modelSetFields(modelIDToRun,
                                           dict(engParamsHash=paramsHash,
                                                engParticleHash=particleHash));
                  success = True;
                }
                catch()
                {
                  success = False;
                }
                if( success)
                {
                  break;
                }
              }
              if( not success)
              {
                raise RuntimeError("Unexpected failure to change paramsHash and "
                                   "particleHash of orphaned model");
              }

              (modelIDToRun, ours) = cjDAO.modelInsertAndStart(options.jobID,
                                  mParamsAndHash.params, modelParamsHash);
            }



            // ^^^ end while modelIDToRun ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
          }
        }

        // ---------------------------------------------------------------
        // We have a model, evaluate it now
        // All done?
        if( exit)
        {
          break;
        }

        // Run the model now
        this.logger.info("RUNNING MODEL GID=%d, paramsHash=%s, params=%s",
              modelIDToRun, modelParamsHash.encode('hex'), modelParams);

        // ---------------------------------------------------------------------
        // Construct model checkpoint GUID for this model:
        // jobParams['persistentJobGUID'] contains the client's (e.g., API Server)
        // persistent, globally-unique model identifier, which is what we need;
        persistentJobGUID = jobParams['persistentJobGUID'];
        assert persistentJobGUID, "persistentJobGUID: %r" % (persistentJobGUID,);

        modelCheckpointGUID = jobInfo.client + "_" + persistentJobGUID + (
          '_' + str(modelIDToRun));


        this._hs.runModel(modelID=modelIDToRun, jobID = options.jobID,
                          modelParams=modelParams, modelParamsHash=modelParamsHash,
                          jobsDAO=cjDAO, modelCheckpointGUID=modelCheckpointGUID);

        // TODO: don't increment for orphaned models
        numModelsTotal += 1;

        this.logger.info("COMPLETED MODEL GID=%d; EVALUATED %d MODELs",
          modelIDToRun, numModelsTotal);
        print >>sys.stderr, "reporter:status:Evaluated %d models..." % \
                                    (numModelsTotal);
        print >>sys.stderr, "reporter:counter:HypersearchWorker,numModels,1";

        if( options.modelID is not None)
        {
          exit = True;
        }
        // ^^^ end while not exit
      }
    }

    finally
    {
      // Provide Hypersearch instance an opportunity to clean up temporary files
      this._hs.close();
    }

    this.logger.info("FINISHED. Evaluated %d models." % (numModelsTotal));
    print >>sys.stderr, "reporter:status:Finished, evaluated %d models" % (numModelsTotal);
    return options.jobID;
  }
}
    */
}