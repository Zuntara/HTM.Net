using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HTM.Net.Research.Tests.Swarming
{
    public class MyTestEnvironment
    {
        public string testSrcExpDir;
        public string testSrcDataDir;

        public MyTestEnvironment()
        {
            string testDir = Environment.CurrentDirectory;
            this.testSrcExpDir = Path.Combine(testDir, "experiments");
            this.testSrcDataDir = Path.Combine(testDir, "data");
        }
    }

    public abstract class ExperimentTestBaseClass
    {
        protected MyTestEnvironment g_myEnv;

        protected virtual void SetUp()
        {

        }

        protected virtual void TearDown()
        {
            // Reset our log items
            //this.resetExtraLogItems(); // TODO > testcasebase
        }

        /// <summary>
        /// Put the path to our datasets int the NTA_DATA_PATH variable which will 
        /// be used to set the environment for each of the workers
        /// </summary>
        public void _setDataPath(/*env*/)
        {
            //assert env is not None;
            string newPath;
            // If already have a path, concatenate to it
            if (Environment.GetEnvironmentVariable("NTA_DATA_PATH") != null)
            {
                newPath = string.Format("{0}:{1}", Environment.GetEnvironmentVariable("NTA_DATA_PATH"), g_myEnv.testSrcDataDir);
            }
            else
            {
                newPath = g_myEnv.testSrcDataDir;
            }

            Environment.SetEnvironmentVariable("NTA_DATA_PATH", newPath);
        }

        /// <summary>
        /// Launch worker processes to execute the given command line
        /// </summary>
        /// <param name="cmdLine">The command line for each worker</param>
        /// <param name="numWorkers">number of workers to launch</param>
        /// <returns>list of workers</returns>
        private List<object> _launchWorkers(string[] cmdLine, int numWorkers)
        {
            var workers = new List<object>();
            foreach (var i in ArrayUtils.Range(0, numWorkers))
            {
                List<string> lArgs = new List<string> { "bash", "-c" };
                lArgs.AddRange(cmdLine);
                string[] args = lArgs.ToArray();// ["bash", "-c", cmdLine];
                //stdout = tempfile.TemporaryFile();
                //stderr = tempfile.TemporaryFile();
                //p = subprocess.Popen(args, bufsize = 1, env = os.environ, shell = False,
                //                     stdin = None, stdout = stdout, stderr = stderr);
                //workers.Add(p);
            }

            return workers;
        }

        /// <summary>
        /// Return the job info for a job
        /// </summary>
        /// <param name="cjDAO">client jobs database instance</param>
        /// <param name="workers">list of workers for this job</param>
        /// <param name="jobID">which job ID</param>
        /// <returns>job info</returns>
        private NamedTuple _getJobInfo(BaseClientJobDao cjDAO, List<Process> workers, uint? jobID)
        {
            // Get the job info
            var jobInfo = cjDAO.jobInfo(jobID);

            // Since we"re running outside of the Nupic engine, we launched the workers
            //  ourself, so see how many are still running and jam the correct status
            //  into the job info. When using the Nupic engine, it would do this
            //  for us.
            int runningCount = 0;
            foreach (var worker in workers)
            {
                int? retCode = worker.ExitCode == 0 ? (int?)null : worker.ExitCode;//poll();
                if (retCode == null)
                {
                    runningCount += 1;
                }
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
            if (status == BaseClientJobDao.STATUS_COMPLETED)
            {
                jobInfo["completionReason"] = BaseClientJobDao.CMPL_REASON_SUCCESS;
            }
            return jobInfo;
        }

        /// <summary>
        /// This method generates a canned Hypersearch Job Params structure based on some high level options
        /// </summary>
        /// <param name="expDirectory"></param>
        /// <param name="hsImp"></param>
        /// <param name="maxModels"></param>
        /// <param name="predictionCacheMaxRecords">If specified, determine the maximum number of records in the prediction cache.</param>
        /// <param name="dataPath">
        /// When expDirectory is not specified, this is the data file
        /// to be used for the operation.If this value is not specified,
        /// it will use the / extra / qa / hotgym / qa_hotgym.csv.
        /// </param>
        /// <param name="maxRecords"></param>
        /// <returns></returns>
        protected Map<string, object> _generateHSJobParams(string expDirectory = null, string hsImp = "v2", int? maxModels = 2,
                           int? predictionCacheMaxRecords = null, string dataPath = null, int? maxRecords = 10)
        {
            Map<string, object> jobParams = null;
            if (expDirectory != null)
            {
                string descriptionPyPath = Path.Combine(expDirectory, "description.py");
                string permutationsPyPath = Path.Combine(expDirectory, "permutations.py");

                string permutationsPyContents = "";//File.ReadAllText(permutationsPyPath);
                string descriptionPyContents = "";//File.ReadAllText(descriptionPyPath);

                jobParams = new Map<string, object>
                {
                    {"persistentJobGUID", Utils.generatePersistentJobGUID()},
                    {"permutationsPyContents", new SimpleV2PermutationsFile()},
                    {"descriptionPyContents", new SimpleV2DescriptionFile()},
                    {"maxModels", maxModels},
                    {"hsVersion", hsImp}
                };

                if (predictionCacheMaxRecords.HasValue)
                {
                    jobParams["predictionCacheMaxRecords"] = predictionCacheMaxRecords;
                }
            }

            else
            {


                // Form the stream definition
                if (dataPath == null)
                {
                    //dataPath = resource_filename("nupic.data", os.path.join("extra", "qa", "hotgym", "qa_hotgym.csv"));
                }

                var streamDef = new Map<string, object>
                {
                    {"version", 1},
                    {"info", "TestHypersearch"},
                    {
                        "streams", new List<Map<string, object>>
                        {
                            new Map<string, object>
                            {
                                {"source", "file://" + (dataPath)},
                                {"info", dataPath},
                                {"columns", new[] {"*"}},
                                {"first_record", 0},
                                {"last_record", maxRecords}
                            }
                        }
                    }
                };


                // Generate the experiment description
                var expDesc = new Map<string, object>
                {
                    {"predictionField", "consumption"},
                    {"streamDef", streamDef},
                    {
                        "includedFields",
                        new List<Map<string, object>>
                        {
                            new Map<string, object>
                            {
                                {"fieldName", "gym"},
                                {"fieldType", "string"}
                            },
                            new Map<string, object>
                            {
                                {"fieldName", "consumption"},
                                {"fieldType", "float"},
                                {"minValue", 0},
                                {"maxValue", 200},
                            },
                        }
                    },
                    {"iterationCount", maxRecords},
                    {"resetPeriod",
                        new Map<string, object>
                        {
                            {"weeks", 0},
                            {"days", 0},
                            {"hours", 8},
                            {"minutes", 0},
                            {"seconds", 0},
                            {"milliseconds", 0},
                            {"microseconds", 0}
                        }
                    }
                };

                jobParams = new Map<string, object>
                {
                    {"persistentJobGUID", Utils.generatePersistentJobGUID()},
                    {"description", expDesc},
                    {"maxModels", maxModels},
                    {"hsVersion", hsImp},
                };


                if (predictionCacheMaxRecords.HasValue)
                {
                    jobParams["predictionCacheMaxRecords"] = predictionCacheMaxRecords;
                }
            }

            return jobParams;
        }

        /// <summary>
        /// This runs permutations on the given experiment using just 1 worker in the current process
        /// </summary>
        /// <param name="jobParams">filled in job params for a hypersearch</param>
        /// <param name="waitForCompletion">If True, wait for job to complete before returning
        /// If False, then return resultsInfoForAllModels and metricResults will be None</param>
        /// <param name="continueJobId">If not None, then this is the JobId of a job we want to continue working on with another worker.</param>
        /// <param name="ignoreErrModels">If true, ignore erred models</param>
        /// <returns>(jobId, jobInfo, resultsInfoForAllModels, metricResults)</returns>
        private PermutationsLocalResult _runPermutationsLocal(Map<string, object> jobParams,
                            bool waitForCompletion = true,
                            int? continueJobId = null, bool ignoreErrModels = false)
        {
            Debug.WriteLine("");
            Debug.WriteLine("==================================================================");
            Debug.WriteLine("Running Hypersearch job using 1 worker in current process");
            Debug.WriteLine("==================================================================");

            // Plug in modified environment variables
            //if (env is not None)
            //{
            //    saveEnvState = copy.deepcopy(os.environ);
            //    os.environ.update(env);
            //}

            // Insert the job entry into the database in the pre-running state
            var cjDAO = BaseClientJobDao.Create();
            int jobID = -1;
            if (!continueJobId.HasValue)
            {
                jobID = (int)cjDAO.jobInsert(client: "test", cmdLine: "<started manually>",
                        @params: JsonConvert.SerializeObject(jobParams),
                        alreadyRunning: true, minimumWorkers: 1, maximumWorkers: 1,
                        jobType: BaseClientJobDao.JOB_TYPE_HS);
            }
            else
            {
                jobID = continueJobId.GetValueOrDefault();
            }

            // Command line args.
            List<string> args = new List<string> { "ignoreThis", string.Format("--jobID={0}", jobID) };
            //args = ["ignoreThis", string.Format("--jobID={0}", jobID)];
            if (!continueJobId.HasValue)
            {
                args.Add("--clearModels");
            }

            // Run it in the current process
            try
            {
                HyperSearchWorker.main(args.ToArray());
            }

            // The dummy model runner will call sys.exit(0) when
            //  NTA_TEST_sysExitAfterNIterations is set
            //catch (SystemExit)
            //{
            //    pass;
            //}
            catch (Exception)
            {
                throw;
            }

            // Restore environment
            //if (env is not None)
            //{
            //    os.environ = saveEnvState;
            //}

            // ----------------------------------------------------------------------
            // Make sure all models completed successfully
            var models = cjDAO.modelsGetUpdateCounters((uint?) jobID);
            //modelIDs =  [model.modelId for model in models];
            var modelIDs = models.Select(m => (ulong)m.Item1).ToList();
            List<ResultAndStatusModel> results;
            if (modelIDs.Count > 0)
            {
                results = cjDAO.modelsGetResultAndStatus(modelIDs);
            }
            else
            {
                results = new List<ResultAndStatusModel>();
            }

            var metricResults = new List<double?>();
            foreach (var result in results)
            {
                if (result.results != null)
                {
                    metricResults.Add(result.results.Item2.Values.First());
                }
                else
                {
                    metricResults.Add(null);
                }
                if (!ignoreErrModels)
                {
                    Assert.AreNotEqual(BaseClientJobDao.CMPL_REASON_ERROR, result.completionReason,
                        string.Format("Model did not complete successfully:\n{0}", result.completionMsg));
                }
            }

            // Print worker completion message
            var jobInfo = cjDAO.jobInfo((uint?) jobID);

            return new PermutationsLocalResult
            {
                jobID = jobID,
                jobInfo = jobInfo,
                results = results,
                metricResults = metricResults
            };
            //return (jobID, jobInfo, results, metricResults);
        }

        /// <summary>
        /// This runs permutations on the given experiment using just 1 worker
        /// </summary>
        /// <param name="expDirectory">directory containing the description.py and permutations.py</param>
        /// <param name="hsImp">which implementation of Hypersearch to use</param>
        /// <param name="maxModels">max // of models to generate</param>
        /// <param name="maxNumWorkers">max // of workers to use, N/A if onCluster is False</param>
        /// <param name="onCluster">if True, run on the Hadoop cluster</param>
        /// <param name="waitForCompletion">If True, wait for job to complete before returning
        /// If False, then return resultsInfoForAllModels and
        /// metricResults will be None</param>
        /// <param name="continueJobId">If not None, then this is the JobId of a job we want
        /// to continue working on with another worker.</param>
        /// <param name="dataPath">This value is passed to the function, _generateHSJobParams(),
        /// which points to the data file for the operation.</param>
        /// <param name="maxRecords"></param>
        /// <param name="timeoutSec"></param>
        /// <param name="ignoreErrModels"></param>
        /// <param name="predictionCacheMaxRecords">If specified, determine the maximum number of records in
        /// the prediction cache.</param>
        /// <param name="kwargs"></param>
        /// <returns>(jobID, jobInfo, resultsInfoForAllModels, metricResults, minErrScore)</returns>
        public PermutationsLocalResult runPermutations(string expDirectory, string hsImp = "v2", int? maxModels = 2,
                      int maxNumWorkers = 4, bool onCluster = false, bool waitForCompletion = true,
                      int? continueJobId = null, string dataPath = null, int? maxRecords = null,
                      int? timeoutSec = null, bool ignoreErrModels = false,
                      int? predictionCacheMaxRecords = null, KWArgsModel kwargs = null)
        {

            // Put in the path to our datasets
            //if (env is None)
            //{
            //    env = dict();
            //}
            this._setDataPath(/*env*/);

            // ----------------------------------------------------------------
            // Prepare the jobParams
            var jobParams = this._generateHSJobParams(expDirectory: expDirectory,
                                                  hsImp: hsImp, maxModels: maxModels,
                                                  maxRecords: maxRecords,
                                                  dataPath: dataPath,
                                                  predictionCacheMaxRecords: predictionCacheMaxRecords);

            jobParams.Update(kwargs);

            PermutationsLocalResult permutationResult = null;
            if (onCluster)
            {
                //(jobID, jobInfo, resultInfos, metricResults) \
                //= this._runPermutationsCluster(jobParams = jobParams,
                //                                loggingLevel = loggingLevel,
                //                                maxNumWorkers = maxNumWorkers,
                //                                env = env,
                //                                waitForCompletion = waitForCompletion,
                //                                ignoreErrModels = ignoreErrModels,
                //                                timeoutSec = timeoutSec);
                throw new NotSupportedException("Not yet implemented");
            }

            else
            {
                permutationResult = this._runPermutationsLocal(jobParams: jobParams,
                                     /* loggingLevel: loggingLevel,
                                      env: env,*/
                                     waitForCompletion: waitForCompletion,
                                     continueJobId: continueJobId,
                                     ignoreErrModels: ignoreErrModels);
                //(jobID, jobInfo, resultInfos, metricResults) \

            }

            if (!waitForCompletion)
            {
                //return (jobID, jobInfo, resultInfos, metricResults, None);
                permutationResult.minErrScore = null;
                return permutationResult;
            }

            var jobID = permutationResult.jobID;
            var jobInfo = permutationResult.jobInfo;
            var metricResults = permutationResult.metricResults;
            var resultInfos = permutationResult.results;

            // Print job status
            Console.WriteLine("\n------------------------------------------------------------------");
            Console.WriteLine("Hadoop completion reason: {0}", jobInfo["completionReason"]);
            Console.WriteLine("Worker completion reason: {0}", jobInfo["workerCompletionReason"]);
            Console.WriteLine("Worker completion msg: {0}", jobInfo["workerCompletionMsg"]);

            if (jobInfo["engWorkerState"] != null)
            {
                Console.WriteLine("\nEngine worker state:");
                Console.WriteLine("---------------------------------------------------------------");
                Console.WriteLine(jobInfo["engWorkerState"]);
                //pprint.pprint(json.loads(jobInfo.engWorkerState));

            }


            // Print out best results
            double? minErrScore = null;
            List<double> metricAmts = new List<double>();
            foreach (var result in metricResults)
            {
                if (result == null)
                {
                    metricAmts.Add(double.PositiveInfinity);
                }
                else
                {
                    metricAmts.Add(result.Value);
                }
            }

            metricAmts = metricAmts.ToList();
            if (metricAmts.Count > 0)
            {
                minErrScore = metricAmts.Min();
                ulong minModelID = resultInfos[ArrayUtils.Argmin(metricAmts.ToArray())].modelID;

                // Get model info
                var cjDAO = BaseClientJobDao.Create();
                var modelParams = cjDAO.modelsGetParams(new List<ulong> { minModelID })[0]["params"];
                //Console.WriteLine("Model params for best model: \n{0}", pprint.pformat(json.loads(modelParams)));
                Console.WriteLine("Best model result: {0}", minErrScore);
            }

            else
            {
                Console.WriteLine("No models finished");
            }

            return new PermutationsLocalResult
            {
                jobID = jobID,
                jobInfo = jobInfo,
                results = resultInfos,
                metricResults = metricResults,
                minErrScore = minErrScore
            };
            //return (jobID, jobInfo, resultInfos, metricResults, minErrScore);
        }

        public class PermutationsLocalResult
        {
            public int? jobID { get; set; }
            public NamedTuple jobInfo { get; set; }
            public List<ResultAndStatusModel> results { get; set; }
            public List<double?> metricResults { get; set; }

            public double? minErrScore { get; set; }
        }


    }

    [TestClass]
    public class OneNodeTests : ExperimentTestBaseClass
    {
        [TestInitialize]
        public void SetUpTests()
        {
            base.SetUp();
            g_myEnv = new MyTestEnvironment();
        }

        /// <summary>
        /// Try running simple permutations
        /// </summary>
        /// <param name="onCluster"></param>
        /// <param name="kwargs"></param>
        public void TestSimpleV2Internal(bool onCluster = false, KWArgsModel kwargs = null)
        {
            //this._printTestHeader();
            string expDir = Path.Combine(g_myEnv.testSrcExpDir, "simpleV2");

            // Test it out
            //if (env is None)
            //{
            //    env = dict();
            //}
            //env["NTA_TEST_numIterations"] = "99";
            //env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = "%d" % (g_repeatableSwarmMaturityWindow);

            //(jobID, jobInfo, resultInfos, metricResults, minErrScore) 
            var permutationResult = this.runPermutations(expDirectory: expDir,
                                   hsImp: "v2",
                                   //loggingLevel = g_myEnv.options.logLevel,
                                   onCluster: onCluster,
                                   //env = env,
                                   maxModels: null,
                                   kwargs: kwargs);


            Assert.AreEqual(20, permutationResult.minErrScore);
            Assert.IsTrue(permutationResult.results.Count < 350);
            //this.assertLess(len(resultInfos), 350);
        }

        //[TestMethod]
        public void TestSimpleV2()
        {
            TestSimpleV2Internal();
        }

        [TestMethod]
        public void TestDescriptionSerialization()
        {
            SimpleV2DescriptionFile file = new SimpleV2DescriptionFile();
            
            string json = JsonConvert.SerializeObject(file);
            Debug.WriteLine(json);
            var deserialized = JsonConvert.DeserializeObject(json, typeof(DescriptionBase));
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(SimpleV2DescriptionFile));
        }

        [TestMethod]
        public void TestDescriptionNetworkCreation()
        {
            SimpleV2DescriptionFile file = new SimpleV2DescriptionFile();

            var network = file.BuildNetwork();
            Assert.IsNull(network); // for now
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.csv")]
        //public void TestClaModelNetworkCreation()
        //{
        //    SimpleV2DescriptionFile file = new SimpleV2DescriptionFile();

        //    CLAModel model = new CLAModel(file);
            
        //    Assert.IsNotNull(model);
        //}

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.csv")]
        //public void TestClaModelNetworkRun()
        //{
        //    SimpleV2DescriptionFile file = new SimpleV2DescriptionFile();

        //    CLAModel model = new CLAModel(file);

        //    Assert.IsNotNull(model);

        //    model.enableInference(new Map<string, object> { {"predictedField", "consumption" } });

        //    model.run(new Map<string, object>
        //    {
        //        {"address", "home" },
        //        {"consumption", 23.0},
        //        {"gym", "gym1" },
        //        {"timestamp", DateTime.Parse("01/01/2016 13:00:00") },
        //    });
        //}

        [TestMethod]
        public void TestPermutationSerialization()
        {
            SimpleV2PermutationsFile file = new SimpleV2PermutationsFile();

            string json = JsonConvert.SerializeObject(file);
            var deserialized = JsonConvert.DeserializeObject(json, typeof(PermutionFilterBase));
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(SimpleV2PermutationsFile));

            SimpleV2PermutationsFile des = (SimpleV2PermutationsFile) deserialized;
            Assert.AreEqual(7, file.permutations.modelParams.sensorParams.encoders["consumption"].kwArgs["w"]);
            Assert.IsInstanceOfType(file.permutations.modelParams.sensorParams.encoders["consumption"].kwArgs["n"], typeof(PermuteInt));

            Assert.AreEqual(7, des.permutations.modelParams.sensorParams.encoders["consumption"].kwArgs["w"]);
            Assert.IsInstanceOfType(des.permutations.modelParams.sensorParams.encoders["consumption"].kwArgs["n"], typeof(PermuteInt));
        }

        [TestMethod]
        public void TestParamsDeserialisation()
        {
            string expDir = Path.Combine(g_myEnv.testSrcExpDir, "simpleV2");
            var jobParamsDict = this._generateHSJobParams(expDirectory: expDir,hsImp: "v2");
            jobParamsDict.Update(null);

            string jobParamsJson = JsonConvert.SerializeObject(jobParamsDict);

            var jobParams = JsonConvert.DeserializeObject<HyperSearchSearchParams>(jobParamsJson);
        }
    }

    /*
    

DEFAULT_JOB_TIMEOUT_SEC = 60 * 2;

// Filters _debugOut messages
g_debug = True;

// Our setUpModule entry block sets this to an instance of MyTestEnvironment()
g_myEnv = None;

// These are the args after using the optparse

// This value for the swarm maturity window gives more repeatable results for
//  unit tests that use multiple workers
g_repeatableSwarmMaturityWindow = 5;



class OneNodeTests(ExperimentTestBaseClass)
{
  """
  """;
  // AWS tests attribute required for tagging via automatic test discovery via
  // nosetests
  engineAWSClusterTest=True;


  def setUp(self)
  {
    super(OneNodeTests, self).setUp();
    if( not g_myEnv.options.runInProc)
    {
      this.skipTest("Skipping One Node test since runInProc is not specified");
    }
  }


  def testSimpleV2(self, onCluster=False, env=None, **kwargs)
  {
    """
    Try running simple permutations
    """;
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);


    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 350);

    return;
  }


  def testDeltaV2(self, onCluster=False, env=None, **kwargs)
  {
    """ Try running a simple permutations with delta encoder
    Test which tests the delta encoder. Runs a swarm of the sawtooth dataset
    With a functioning delta encoder this should give a perfect result
    DEBUG: disabled temporarily because this test takes too long!!!
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "delta");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);
    env["NTA_TEST_exitAfterNModels"] = str(20);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);


    this.assertLess(minErrScore, 0.002);

    return;
  }


  def testSimpleV2NoSpeculation(self, onCluster=False, env=None, **kwargs)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  speculativeParticles=False,
                                  **kwargs);


    this.assertEqual(minErrScore, 20);
    this.assertGreater(len(resultInfos), 1);
    this.assertLess(len(resultInfos), 350);
    return;
  }


  def testCLAModelV2(self, onCluster=False, env=None, maxModels=2,
                      **kwargs)
  {
    """ Try running a simple permutations using an actual CLA model, not
    a dummy
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "dummyV2");

    // Test it out
    if( env is None)
    {
      env = dict();
    }

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=maxModels,
                                  **kwargs);


    this.assertEqual(len(resultInfos), maxModels);
    return;
  }


  def testCLAMultistepModel(self, onCluster=False, env=None, maxModels=2,
                      **kwargs)
  {
    """ Try running a simple permutations using an actual CLA model, not
    a dummy
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simple_cla_multistep");

    // Test it out
    if( env is None)
    {
      env = dict();
    }

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=maxModels,
                                  **kwargs);


    this.assertEqual(len(resultInfos), maxModels);
    return;
  }


  def testLegacyCLAMultistepModel(self, onCluster=False, env=None, maxModels=2,
                      **kwargs)
  {
    """ Try running a simple permutations using an actual CLA model, not
    a dummy. This is a legacy CLA multi-step model that doesn"t declare a
    separate "classifierOnly" encoder for the predicted field.
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "legacy_cla_multistep");

    // Test it out
    if( env is None)
    {
      env = dict();
    }

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=maxModels,
                                  **kwargs);


    this.assertEqual(len(resultInfos), maxModels);
    return;
  }


  def testFilterV2(self, onCluster=False)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");



    // Don"t allow the consumption encoder maxval to get to it"s optimum
    //   value (which is 250). This increases our errScore by +25.
    env = dict();
    env["NTA_TEST_maxvalFilter"] = "225";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = "6";
    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None);


    this.assertEqual(minErrScore, 45);
    this.assertLess(len(resultInfos), 400);
    return;
  }


  def testLateWorker(self, onCluster=False)
  {
    """ Try running a simple permutations where a worker comes in late,
    after the some models have already been evaluated
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    env = dict();
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
               "%d" % (g_repeatableSwarmMaturityWindow);
    env["NTA_TEST_exitAfterNModels"] =  "100";

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=None,
                                onCluster=onCluster,
                                env=env,
                                waitForCompletion=True,
                                );
    this.assertEqual(len(resultInfos), 100);

    // Run another worker the rest of the way
    env.pop("NTA_TEST_exitAfterNModels");
    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=None,
                                onCluster=onCluster,
                                env=env,
                                waitForCompletion=True,
                                continueJobId = jobID,
                                );

    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 350);
    return;
  }


  def testOrphanedModel(self, onCluster=False, modelRange=(0,1))
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // NTA_TEST_numIterations is watched by the dummyModelParams() method of
    //  the permutations file.
    // NTA_TEST_sysExitModelRange  is watched by the dummyModelParams() method of
    //  the permutations file. It tells it to do a sys.exit() after so many
    //  iterations.
    // We increase the swarm maturity window to make our unit tests more
    //   repeatable. There is an element of randomness as to which model
    //   parameter combinations get evaluated first when running with
    //   multiple workers, so this insures that we can find the "best" model
    //   that we expect to see in our unit tests.
    env = dict();
    env["NTA_TEST_numIterations"] = "2";
    env["NTA_TEST_sysExitModelRange"] = "%d,%d" % (modelRange[0], modelRange[1]);
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] \
            =  "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=300,
                                onCluster=onCluster,
                                env=env,
                                waitForCompletion=False,
                                );
    // At this point, we should have 1 model, still running
    (beg, end) = modelRange;
    this.assertEqual(len(resultInfos), end);
    numRunning = 0;
    for( res in resultInfos)
    {
      if( res.status == ClientJobsDAO.STATUS_RUNNING)
      {
        numRunning += 1;
      }
    }
    this.assertEqual(numRunning, 1);


    // Run another worker the rest of the way, after delaying enough time to
    //  generate an orphaned model
    env["NTA_CONF_PROP_nupic_hypersearch_modelOrphanIntervalSecs"] = "1";
    time.sleep(2);

    // Here we launch another worker to finish up the job. We set the maxModels
    //  to 300 (200 something should be enough) in case the orphan detection is
    //  not working, it will make sure we don"t loop for excessively long.
    // With orphan detection working, we should detect that the first model
    //  would never complete, orphan it, and create a new one in the 1st sprint.
    // Without orphan detection working, we will wait forever for the 1st sprint
    //  to finish, and will create a bunch of gen 1, then gen2, then gen 3, etc.
    //  and gen 0 will never finish, so the swarm will never mature.
    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=300,
                                onCluster=onCluster,
                                env=env,
                                waitForCompletion=True,
                                continueJobId = jobID,
                                );

    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 350);
    return;
  }


  def testOrphanedModelGen1(self)
  {
    """ Run a worker on a model for a while, then have it exit before a
    model finishes in gen index 2. Then, run another worker, which should detect
    the orphaned model.
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testOrphanedModel(modelRange=(10,11));
  }


  def testErredModel(self, onCluster=False, modelRange=(6,7))
  {
    """ Run with 1 or more models generating errors
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // We increase the swarm maturity window to make our unit tests more
    //   repeatable. There is an element of randomness as to which model
    //   parameter combinations get evaluated first when running with
    //   multiple workers, so this insures that we can find the "best" model
    //   that we expect to see in our unit tests.
    env = dict();
    env["NTA_TEST_errModelRange"] = "%d,%d" % (modelRange[0], modelRange[1]);
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] \
            =  "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                onCluster=onCluster,
                                env=env,
                                maxModels=None,
                                ignoreErrModels=True
                                );

    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 350);
    return;
  }


  def testJobFailModel(self, onCluster=False, modelRange=(6,7))
  {
    """ Run with 1 or more models generating jobFail exception
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // We increase the swarm maturity window to make our unit tests more
    //   repeatable. There is an element of randomness as to which model
    //   parameter combinations get evaluated first when running with
    //   multiple workers, so this insures that we can find the "best" model
    //   that we expect to see in our unit tests.
    env = dict();
    env["NTA_TEST_jobFailErr"] = "True";

    maxNumWorkers = 4;
    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                onCluster=onCluster,
                                env=env,
                                maxModels=None,
                                maxNumWorkers=maxNumWorkers,
                                ignoreErrModels=True
                                );

    // Make sure workerCompletionReason was error
    this.assertEqual (jobInfo.workerCompletionReason,
                      ClientJobsDAO.CMPL_REASON_ERROR);
    this.assertLess (len(resultInfos), maxNumWorkers+1);
    return;
  }


  def testTooManyErredModels(self, onCluster=False, modelRange=(5,10))
  {
    """ Run with too many models generating errors
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir,  "simpleV2");

    // We increase the swarm maturity window to make our unit tests more
    //   repeatable. There is an element of randomness as to which model
    //   parameter combinations get evaluated first when running with
    //   multiple workers, so this insures that we can find the "best" model
    //   that we expect to see in our unit tests.
    env = dict();
    env["NTA_TEST_errModelRange"] = "%d,%d" % (modelRange[0], modelRange[1]);
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] \
            =  "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                onCluster=onCluster,
                                env=env,
                                maxModels=None,
                                ignoreErrModels=True
                                );

    this.assertEqual (jobInfo.workerCompletionReason,
                      ClientJobsDAO.CMPL_REASON_ERROR);
    return;
  }


  def testFieldThreshold(self, onCluster=False, env=None, **kwargs)
  {
    """ Test minimum field contribution threshold for a field to be included in further sprints
    """;


    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "field_threshold_temporal");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (0);
    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (2);
    env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                         "%f" % (100);


    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance",
            "visitor_winloss"]);

    this.assertEqual(params["particleState"]["swarmId"],
                     expectedSwarmId,
                     "Actual swarm id = %s\nExpcted swarm id = %s" \
                     % (params["particleState"]["swarmId"],
                        expectedSwarmId));
    this.assertEqual( bestModel.optimizedMetric, 75);


    #==========================================================================
    env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                         "%f" % (20);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance",
            "home_winloss",
            "visitor_winloss"]);
    this.assertEqual(params["particleState"]["swarmId"],
                     expectedSwarmId,
                     "Actual swarm id = %s\nExpcted swarm id = %s" \
                     % (params["particleState"]["swarmId"],
                        expectedSwarmId));
    assert bestModel.optimizedMetric == 55, bestModel.optimizedMetric;



    #==========================================================================
    // Find best combo possible
    env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                         "%f" % (0.0);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance",
            "home_winloss",
            "precip",
            "timestamp_dayOfWeek",
            "timestamp_timeOfDay",
            "visitor_winloss"]);
    this.assertEqual(params["particleState"]["swarmId"],
                     expectedSwarmId,
                     "Actual swarm id = %s\nExpcted swarm id = %s" \
                     % (params["particleState"]["swarmId"],
                        expectedSwarmId));

    assert bestModel.optimizedMetric == 25, bestModel.optimizedMetric;
  }


  def testSpatialClassification(self, onCluster=False, env=None, **kwargs)
  {
    """
    Try running a spatial classification swarm
    """;
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "spatial_classification");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);


    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 350);

    // Check the expected field contributions
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    actualFieldContributions = jobResults["fieldContributions"];
    print "Actual field contributions:", \
                              pprint.pformat(actualFieldContributions);
    expectedFieldContributions = {
                      "address": 100 * (90.0-30)/90.0,
                      "gym": 100 * (90.0-40)/90.0,
                      "timestamp_dayOfWeek": 100 * (90.0-80.0)/90.0,
                      "timestamp_timeOfDay": 100 * (90.0-90.0)/90.0,
                      };

    for( key, value in expectedFieldContributions.items())
    {
      this.assertEqual(actualFieldContributions[key], value,
                       "actual field contribution from field "%s" does not "
                       "match the expected value of %f" % (key, value));
    }


    // Check the expected best encoder combination
    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "address",
            "gym"]);

    this.assertEqual(params["particleState"]["swarmId"],
                     expectedSwarmId,
                     "Actual swarm id = %s\nExpcted swarm id = %s" \
                     % (params["particleState"]["swarmId"],
                        expectedSwarmId));


    return;
  }


  def testAlwaysInputPredictedField(self, onCluster=False, env=None,
                                      **kwargs)
  {
    """
    Run a swarm where "inputPredictedField" is set in the permutations
    file. The dummy model for this swarm is designed to give the lowest
    error when the predicted field is INCLUDED, so make sure we don"t get
    this low error
    """;
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "input_predicted_field");

    // Test it out not requiring the predicted field. This should yield a
    //  low error score
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_inputPredictedField"] = "auto";
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (2);
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);


    this.assertEqual(minErrScore, -50);
    this.assertLess(len(resultInfos), 350);


    // Now, require the predicted field. This should yield a high error score
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_inputPredictedField"] = "yes";
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (2);
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);


    this.assertEqual(minErrScore, -40);
    this.assertLess(len(resultInfos), 350);

    return;
  }


  def testFieldThresholdNoPredField(self, onCluster=False, env=None, **kwargs)
  {
    """ Test minimum field contribution threshold for a field to be included
    in further sprints when doing a temporal search that does not require
    the predicted field.
    """;


    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "input_predicted_field");

    // Test it out without any max field branching in effect
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_TEST_inputPredictedField"] = "auto";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (0);
    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (2);
    env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                         "%f" % (0);


    if( True)
    {
      (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
             = this.runPermutations(expDir,
                                    hsImp="v2",
                                    loggingLevel=g_myEnv.options.logLevel,
                                    onCluster=onCluster,
                                    env=env,
                                    maxModels=None,
                                    dummyModel={"iterations":200},
                                    **kwargs);

      // Verify the best model and check the field contributions.
      cjDAO = ClientJobsDAO.get();
      jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
      jobResults = json.loads(jobResultsStr);
      bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
      params = json.loads(bestModel.params);

      prefix = "modelParams|sensorParams|encoders|";
      expectedSwarmId = prefix + ("." + prefix).join([
              "address",
              "gym",
              "timestamp_dayOfWeek",
              "timestamp_timeOfDay"]);

      this.assertEqual(params["particleState"]["swarmId"],
                       expectedSwarmId,
                       "Actual swarm id = %s\nExpcted swarm id = %s" \
                       % (params["particleState"]["swarmId"],
                          expectedSwarmId));
      this.assertEqual( bestModel.optimizedMetric, -50);


      // Check the field contributions
      actualFieldContributions = jobResults["fieldContributions"];
      print "Actual field contributions:", \
                                pprint.pformat(actualFieldContributions);

      expectedFieldContributions = {
                        "consumption": 0.0,
                        "address": 100 * (60.0-40.0)/60.0,
                        "timestamp_timeOfDay": 100 * (60.0-20.0)/60.0,
                        "timestamp_dayOfWeek": 100 * (60.0-10.0)/60.0,
                        "gym": 100 * (60.0-30.0)/60.0};


      for( key, value in expectedFieldContributions.items())
      {
        this.assertEqual(actualFieldContributions[key], value,
                         "actual field contribution from field "%s" does not "
                         "match the expected value of %f" % (key, value));
      }
    }


    if( True)
    {
      #==========================================================================
      // Now test ignoring all fields that contribute less than 55% to the
      //   error score. This means we can only use the timestamp_timeOfDay and
      //   timestamp_dayOfWeek fields.
      // This should bring our best error score up to 50-30-40 = -20
      env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                           "%f" % (55);
      env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (5);

      (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
             = this.runPermutations(expDir,
                                    hsImp="v2",
                                    loggingLevel=g_myEnv.options.logLevel,
                                    onCluster=onCluster,
                                    env=env,
                                    maxModels=None,
                                    dummyModel={"iterations":200},
                                    **kwargs);

      // Get the best model
      cjDAO = ClientJobsDAO.get();
      jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
      jobResults = json.loads(jobResultsStr);
      bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
      params = json.loads(bestModel.params);

      prefix = "modelParams|sensorParams|encoders|";
      expectedSwarmId = prefix + ("." + prefix).join([
              "timestamp_dayOfWeek",
              "timestamp_timeOfDay"]);
      this.assertEqual(params["particleState"]["swarmId"],
                       expectedSwarmId,
                       "Actual swarm id = %s\nExpcted swarm id = %s" \
                       % (params["particleState"]["swarmId"],
                          expectedSwarmId));
      this.assertEqual( bestModel.optimizedMetric, -20);

      // Check field contributions returned
      actualFieldContributions = jobResults["fieldContributions"];
      print "Actual field contributions:", \
                                pprint.pformat(actualFieldContributions);

      expectedFieldContributions = {
                        "consumption": 0.0,
                        "address": 100 * (60.0-40.0)/60.0,
                        "timestamp_timeOfDay": 100 * (60.0-20.0)/60.0,
                        "timestamp_dayOfWeek": 100 * (60.0-10.0)/60.0,
                        "gym": 100 * (60.0-30.0)/60.0};

      for( key, value in expectedFieldContributions.items())
      {
        this.assertEqual(actualFieldContributions[key], value,
                         "actual field contribution from field "%s" does not "
                         "match the expected value of %f" % (key, value));
      }
    }

    if( True)
    {
      #==========================================================================
      // Now, test using maxFieldBranching to limit the max number of fields to
      //  3. This means we can only use the timestamp_timeOfDay, timestamp_dayOfWeek,
      // gym fields.
      // This should bring our error score to 50-30-40-20 = -40
      env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                           "%f" % (0);
      env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                           "%d" % (3);

      (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
             = this.runPermutations(expDir,
                                    hsImp="v2",
                                    loggingLevel=g_myEnv.options.logLevel,
                                    onCluster=onCluster,
                                    env=env,
                                    maxModels=None,
                                    dummyModel={"iterations":200},
                                    **kwargs);

      // Get the best model
      cjDAO = ClientJobsDAO.get();
      jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
      jobResults = json.loads(jobResultsStr);
      bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
      params = json.loads(bestModel.params);

      prefix = "modelParams|sensorParams|encoders|";
      expectedSwarmId = prefix + ("." + prefix).join([
              "gym",
              "timestamp_dayOfWeek",
              "timestamp_timeOfDay"]);
      this.assertEqual(params["particleState"]["swarmId"],
                       expectedSwarmId,
                       "Actual swarm id = %s\nExpcted swarm id = %s" \
                       % (params["particleState"]["swarmId"],
                          expectedSwarmId));
      this.assertEqual( bestModel.optimizedMetric, -40);
    }


    if( True)
    {
      #==========================================================================
      // Now, test setting max models so that no swarm can finish completely.
      // Make sure we get the expected field contributions
      env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                           "%d" % (g_repeatableSwarmMaturityWindow);

      env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                           "%d" % (0);
      env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                           "%d" % (5);
      env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                           "%f" % (0);

      (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
             = this.runPermutations(expDir,
                                    hsImp="v2",
                                    loggingLevel=g_myEnv.options.logLevel,
                                    onCluster=onCluster,
                                    env=env,
                                    maxModels=10,
                                    dummyModel={"iterations":200},
                                    **kwargs);

      // Get the best model
      cjDAO = ClientJobsDAO.get();
      jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
      jobResults = json.loads(jobResultsStr);
      bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
      params = json.loads(bestModel.params);

      prefix = "modelParams|sensorParams|encoders|";
      expectedSwarmId = prefix + ("." + prefix).join([
              "timestamp_dayOfWeek"]);
      this.assertEqual(params["particleState"]["swarmId"],
                       expectedSwarmId,
                       "Actual swarm id = %s\nExpcted swarm id = %s" \
                       % (params["particleState"]["swarmId"],
                          expectedSwarmId));
      this.assertEqual( bestModel.optimizedMetric, 10);

      // Check field contributions returned
      actualFieldContributions = jobResults["fieldContributions"];
      print "Actual field contributions:", \
                                pprint.pformat(actualFieldContributions);

      expectedFieldContributions = {
                        "consumption": 0.0,
                        "address": 100 * (60.0-40.0)/60.0,
                        "timestamp_timeOfDay": 100 * (60.0-20.0)/60.0,
                        "timestamp_dayOfWeek": 100 * (60.0-10.0)/60.0,
                        "gym": 100 * (60.0-30.0)/60.0};
    }
  }
}



class MultiNodeTests(ExperimentTestBaseClass)
{
  """
  Test hypersearch on multiple nodes
  """;
  // AWS tests attribute required for tagging via automatic test discovery via
  // nosetests
  engineAWSClusterTest=True;


  def testSimpleV2(self)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testSimpleV2(onCluster=True); #, maxNumWorkers=7)
  }


  def testDeltaV2(self)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testDeltaV2(onCluster=True); #, maxNumWorkers=7)
  }


  def testSmartSpeculation(self, onCluster=True, env=None, **kwargs)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "smart_speculation_temporal");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (1);


    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobInfoStr = cjDAO.jobGetFields(jobID, ["results","engWorkerState"]);
    jobResultsStr = jobInfoStr[0];
    engState = jobInfoStr[1];
    engState = json.loads(engState);
    swarms = engState["swarms"];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    // Make sure that the only nonkilled models are the ones that would have been
    // run without speculation
    prefix = "modelParams|sensorParams|encoders|";
    correctOrder = ["A","B","C","D","E","F","G","Pred"];
    correctOrder = [prefix + x for x in correctOrder];
    for( swarm in swarms)
    {
      if( swarms[swarm]["status"] == "killed")
      {
        swarmId = swarm.split(".");
        if(len(swarmId)>1)
        {
          // Make sure that something before the last two encoders is in the
          // wrong sprint progression, hence why it was killed
          // The last encoder is the predicted field and the second to last is
          // the current new addition
          wrong=0;
          for( i in range(len(swarmId)-2))
          {
            if( correctOrder[i] != swarmId[i])
            {
              wrong=1;
            }
          }
          assert wrong==1, "Some of the killed swarms should not have been " \
                            + "killed as they are a legal combination.";
        }
      }

      if( swarms[swarm]["status"] == "completed")
      {
          swarmId = swarm.split(".");
          if(len(swarmId)>3)
          {
            // Make sure that the completed swarms are all swarms that should
            // have been run.
            // The last encoder is the predicted field and the second to last is
            // the current new addition
            for( i in range(len(swarmId)-3))
            {
              if( correctOrder[i] != swarmId[i])
              {
                assert False ,  "Some of the completed swarms should not have " \
                          "finished as they are illegal combinations";
              }
            }
          }
      }
      if( swarms[swarm]["status"] == "active")
      {
        assert False ,  "Some swarms are still active at the end of hypersearch";
      }
    }

    pass;
  }


  def testSmartSpeculationSpatialClassification(self, onCluster=True,
                                                env=None, **kwargs)
  {
    """ Test that smart speculation does the right thing with spatial
    classification models. This also applies to temporal models where the
    predicted field is optional (or excluded) since Hypersearch treats them
    the same.
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir,
                          "smart_speculation_spatial_classification");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (1);


    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  maxNumWorkers=5,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the worker state
    cjDAO = ClientJobsDAO.get();
    jobInfoStr = cjDAO.jobGetFields(jobID, ["results","engWorkerState"]);
    jobResultsStr = jobInfoStr[0];
    engState = jobInfoStr[1];
    engState = json.loads(engState);
    swarms = engState["swarms"];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);


    // Make sure that the only non-killed models are the ones that would have been
    // run without speculation
    prefix = "modelParams|sensorParams|encoders|";
    correctOrder = ["A","B","C"];
    correctOrder = [prefix + x for x in correctOrder];
    for( swarm in swarms)
    {
      if( swarms[swarm]["status"] == "killed")
      {
        swarmId = swarm.split(".");
        if(len(swarmId) > 1)
        {
          // Make sure that the best encoder is not in this swarm
          if( correctOrder[0] in swarmId)
          {
            raise RuntimeError("Some of the killed swarms should not have been "
                            "killed as they are a legal combination.");
          }
        }
      }

      else if( swarms[swarm]["status"] == "completed")
      {
        swarmId = swarm.split(".");
        if(len(swarmId) >= 2)
        {
          // Make sure that the completed swarms are all swarms that should
          // have been run.
          for( i in range(len(swarmId)-1))
          {
            if( correctOrder[i] != swarmId[i])
            {
              raise RuntimeError("Some of the completed swarms should not have "
                        "finished as they are illegal combinations");
            }
          }
        }
      }

      else if( swarms[swarm]["status"] == "active")
      {
        raise RuntimeError("Some swarms are still active at the end of "
                           "hypersearch");
      }
    }
  }


  def testFieldBranching(self, onCluster=True, env=None, **kwargs)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "max_branching_temporal");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (4);
    env["NTA_CONF_PROP_nupic_hypersearch_min_field_contribution"] = \
                         "%f" % (-20.0);
    env["NTA_CONF_PROP_nupic_hypersearch_minParticlesPerSwarm"] = \
                         "%d" % (2);


    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance", "home_winloss", "timestamp_dayOfWeek",
            "timestamp_timeOfDay", "visitor_winloss"]);
    assert params["particleState"]["swarmId"] == expectedSwarmId, \
                  params["particleState"]["swarmId"];
    assert bestModel.optimizedMetric == 432, bestModel.optimizedMetric;

    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (3);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance", "home_winloss", "timestamp_timeOfDay",
            "visitor_winloss"]);
    assert params["particleState"]["swarmId"] == expectedSwarmId, \
                  params["particleState"]["swarmId"];

    assert bestModel.optimizedMetric == 465, bestModel.optimizedMetric;

    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (5);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance", "home_winloss", "precip", "timestamp_dayOfWeek",
            "timestamp_timeOfDay", "visitor_winloss"]);
    assert params["particleState"]["swarmId"] == expectedSwarmId, \
                  params["particleState"]["swarmId"];

    assert bestModel.optimizedMetric == 390, bestModel.optimizedMetric;

    #Find best combo with 3 fields
    env["NTA_CONF_PROP_nupic_hypersearch_max_field_branching"] = \
                         "%d" % (0);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=100,
                                  dummyModel={"iterations":200},
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    bestModel = cjDAO.modelsInfo([jobResults["bestModel"]])[0];
    params = json.loads(bestModel.params);

    prefix = "modelParams|sensorParams|encoders|";
    expectedSwarmId = prefix + ("." + prefix).join([
            "attendance", "daynight", "visitor_winloss"]);
    assert params["particleState"]["swarmId"] == expectedSwarmId, \
                  params["particleState"]["swarmId"];

    assert bestModel.optimizedMetric == 406, bestModel.optimizedMetric;



    return;
  }


  def testFieldThreshold(self, onCluster=True, env=None, **kwargs)
  {
    """ Test minimum field contribution threshold for a field to be included in further sprints
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testFieldThreshold(onCluster=True);
  }


  def testFieldContributions(self, onCluster=True, env=None, **kwargs)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "field_contrib_temporal");

    // Test it out
    if( env is None)
    {
      env = dict();
    }
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = \
                         "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
           = this.runPermutations(expDir,
                                  hsImp="v2",
                                  loggingLevel=g_myEnv.options.logLevel,
                                  onCluster=onCluster,
                                  env=env,
                                  maxModels=None,
                                  **kwargs);

    // Get the field contributions from the hypersearch results dict
    cjDAO = ClientJobsDAO.get();
    jobResultsStr = cjDAO.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);

    actualFieldContributions = jobResults["fieldContributions"];
    print "Actual field contributions:", actualFieldContributions;

    expectedFieldContributions = {"consumption": 0.0,
                                  "address": 0.0,
                                  "timestamp_timeOfDay": 20.0,
                                  "timestamp_dayOfWeek": 50.0,
                                  "gym": 10.0};


    for( key, value in expectedFieldContributions.items())
    {
      this.assertEqual(actualFieldContributions[key], value,
                       "actual field contribution from field "%s" does not "
                       "match the expected value of %f" % (key, value));
    }
    return;
  }


  def testCLAModelV2(self)
  {
    """ Try running a simple permutations through a real CLA model
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testCLAModelV2(onCluster=True, maxModels=4);
  }


  def testCLAMultistepModel(self)
  {
    """ Try running a simple permutations through a real CLA model that
    uses multistep
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testCLAMultistepModel(onCluster=True, maxModels=4);
  }


  def testLegacyCLAMultistepModel(self)
  {
    """ Try running a simple permutations through a real CLA model that
    uses multistep
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testLegacyCLAMultistepModel(onCluster=True, maxModels=4);
  }


  def testSimpleV2VariableWaits(self)
  {
    """ Try running a simple permutations where certain field combinations
    take longer to complete, this lets us test that we successfully kill
    models in bad swarms that are still running.
    """;

    this._printTestHeader();

    // NTA_TEST_variableWaits and NTA_TEST_numIterations are watched by the
    //  dummyModelParams() method of the permutations.py file
    // NTA_TEST_numIterations
    env = dict();
    env["NTA_TEST_variableWaits"] ="True";
    env["NTA_TEST_numIterations"] = "100";

    inst = OneNodeTests("testSimpleV2");
    return inst.testSimpleV2(onCluster=True, env=env);
  }


  def testOrphanedModel(self, modelRange=(0,2))
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "simpleV2");

    // NTA_TEST_numIterations is watched by the dummyModelParams() method of
    //  the permutations file.
    // NTA_TEST_sysExitModelRange  is watched by the dummyModelParams() method of
    //  the permutations file. It tells it to do a sys.exit() after so many
    //  iterations.
    env = dict();
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_TEST_sysExitModelRange"] = "%d,%d" % (modelRange[0], modelRange[1]);
    env["NTA_CONF_PROP_nupic_hypersearch_modelOrphanIntervalSecs"] = "1";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] \
            =  "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=500,
                                onCluster=True,
                                env=env,
                                waitForCompletion=True,
                                maxNumWorkers=4,
                                );

    this.assertEqual(minErrScore, 20);
    this.assertLess(len(resultInfos), 500);
    return;
  }


  def testTwoOrphanedModels(self, modelRange=(0,2))
  {
    """ Test behavior when a worker marks 2 models orphaned at the same time.
    """;

    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "oneField");

    // NTA_TEST_numIterations is watched by the dummyModelParams() method of
    //  the permutations file.
    // NTA_TEST_sysExitModelRange  is watched by the dummyModelParams() method of
    //  the permutations file. It tells it to do a sys.exit() after so many
    //  iterations.
    env = dict();
    env["NTA_TEST_numIterations"] = "99";
    env["NTA_TEST_delayModelRange"] = "%d,%d" % (modelRange[0], modelRange[1]);
    env["NTA_CONF_PROP_nupic_hypersearch_modelOrphanIntervalSecs"] = "1";
    env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] \
            =  "%d" % (g_repeatableSwarmMaturityWindow);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=100,
                                onCluster=True,
                                env=env,
                                waitForCompletion=True,
                                maxNumWorkers=4,
                                );

    this.assertEqual(minErrScore, 50);
    this.assertLess(len(resultInfos), 100);
    return;
  }


  def testOrphanedModelGen1(self)
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;

    this._printTestHeader();
    inst = MultiNodeTests(this._testMethodName);
    return inst.testOrphanedModel(modelRange=(10,11));
  }


  def testOrphanedModelMaxModels(self)
  {
    """ Test to make sure that the maxModels parameter doesn"t include
    orphaned models. Run a test with maxModels set to 2, where one becomes
    orphaned. At the end, there should be 3 models in the models table, one
    of which will be the new model that adopted the orphaned model
    """;
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "dummyV2");

    numModels = 5;

    env = dict();
    env["NTA_CONF_PROP_nupic_hypersearch_modelOrphanIntervalSecs"] = "3";
    env["NTA_TEST_max_num_models"]=str(numModels);

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
    = this.runPermutations(expDir,
                          hsImp="v2",
                          loggingLevel=g_myEnv.options.logLevel,
                          maxModels=numModels,
                          env=env,
                          onCluster=True,
                          waitForCompletion=True,
                          dummyModel={"metricValue":  ["25","50"],
                                      "sysExitModelRange": "0, 1",
                                      "iterations": 20,
                                      }
                          );

    cjDB = ClientJobsDAO.get();

    this.assertGreaterEqual(len(resultInfos), numModels+1);
    completionReasons = [x.completionReason for x in resultInfos];
    this.assertGreaterEqual(completionReasons.count(cjDB.CMPL_REASON_EOF), numModels);
    this.assertGreaterEqual(completionReasons.count(cjDB.CMPL_REASON_ORPHAN), 1);
  }


  def testOrphanedModelConnection(self)
  {
    """Test for the correct behavior when a model uses a different connection id
    than what is stored in the db. The correct behavior is for the worker to log
    this as a warning and move on to a new model""";

    this._printTestHeader();

    // -----------------------------------------------------------------------
    // Trigger "Using connection from another worker" exception inside
    // ModelRunner
    // -----------------------------------------------------------------------
    expDir = os.path.join(g_myEnv.testSrcExpDir, "dummy_multi_v2");

    numModels = 2;

    env = dict();
    env["NTA_CONF_PROP_nupic_hypersearch_modelOrphanIntervalSecs"] = "1";

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
    = this.runPermutations(expDir,
                          hsImp="v2",
                          loggingLevel=g_myEnv.options.logLevel,
                          maxModels=numModels,
                          env=env,
                          onCluster=True,
                          waitForCompletion=True,
                          dummyModel={"metricValue":  ["25","50"],
                                      "sleepModelRange": "0, 1:5",
                                      "iterations": 20,
                                      }
                          );

    cjDB = ClientJobsDAO.get();

    this.assertGreaterEqual(len(resultInfos), numModels,
                     "%d were run. Expecting %s"%(len(resultInfos), numModels+1));
    completionReasons = [x.completionReason for x in resultInfos];
    this.assertGreaterEqual(completionReasons.count(cjDB.CMPL_REASON_EOF), numModels);
    this.assertGreaterEqual(completionReasons.count(cjDB.CMPL_REASON_ORPHAN), 1);
  }


  def testErredModel(self, modelRange=(6,7))
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testErredModel(onCluster=True);
  }


  def testJobFailModel(self)
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testJobFailModel(onCluster=True);
  }


  def testTooManyErredModels(self, modelRange=(5,10))
  {
    """ Run a worker on a model for a while, then have it exit before the
    model finishes. Then, run another worker, which should detect the orphaned
    model.
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testTooManyErredModels(onCluster=True);
  }


  def testSpatialClassification(self)
  {
    """ Try running a simple permutations
    """;

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testSpatialClassification(onCluster=True); #, maxNumWorkers=7)
  }


  def testAlwaysInputPredictedField(self)
  {

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testAlwaysInputPredictedField(onCluster=True);
  }


  def testFieldThresholdNoPredField(self)
  {

    this._printTestHeader();
    inst = OneNodeTests(this._testMethodName);
    return inst.testFieldThresholdNoPredField(onCluster=True);
  }
}



class ModelMaturityTests(ExperimentTestBaseClass)
{
  """
  """;
  // AWS tests attribute required for tagging via automatic test discovery via
  // nosetests
  engineAWSClusterTest=True;


  def setUp(self)
  {
    // Ignore the global hypersearch version setting. Always test hypersearch v2
    hsVersion = 2;
    this.expDir = os.path.join(g_myEnv.testSrcExpDir, "dummyV%d" %hsVersion);
    this.hsImp = "v%d" % hsVersion;

    this.env = {"NTA_CONF_PROP_nupic_hypersearch_enableModelTermination":"0",
                "NTA_CONF_PROP_nupic_hypersearch_enableModelMaturity":"1",
                "NTA_CONF_PROP_nupic_hypersearch_maturityMaxSlope":"0.1",
                "NTA_CONF_PROP_nupic_hypersearch_enableSwarmTermination":"0",
                "NTA_CONF_PROP_nupic_hypersearch_bestModelMinRecords":"0"};
  }


  def testMatureInterleaved(self)
  {
    """ Test to make sure that the best model continues running even when it has
    matured. The 2nd model (constant) will be marked as mature first and will
    continue to run till the end. The 2nd model reaches maturity and should
    stop before all the records are consumed, and should be the best model
    because it has a lower error
    """;
    this._printTestHeader();
    this.expDir =  os.path.join(g_myEnv.testSrcExpDir,
                               "dummy_multi_v%d" % 2);
    this.env["NTA_TEST_max_num_models"] = "2";
    jobID,_,_,_,_ = this.runPermutations(this.expDir, hsImp=this.hsImp, maxModels=2,
                                loggingLevel = g_myEnv.options.logLevel,
                                env = this.env,
                                onCluster = True,
                                dummyModel={"metricFunctions":
                                              ["lambda x: -10*math.log10(x+1) +100",
                                               "lambda x: 100.0"],

                                            "delay": [2.0,
                                                      0.0 ],
                                            "waitTime":[0.05,
                                                        0.01],
                                            "iterations":500,
                                            "experimentDirectory":this.expDir,
                                });

    cjDB = ClientJobsDAO.get();

    modelIDs, records, completionReasons, matured = \
                    zip(*this.getModelFields( jobID, ["numRecords",
                                                           "completionReason",
                                                            "engMatured"]));

    results = cjDB.jobGetFields(jobID, ["results"])[0];
    results = json.loads(results);

    this.assertEqual(results["bestModel"], modelIDs[0]);

    this.assertEqual(records[1], 500);
    this.assertTrue(records[0] > 100 and records[0] < 500,
                    "Model 2 num records: 100 < %d < 500 " % records[1]);

    this.assertEqual(completionReasons[1], cjDB.CMPL_REASON_EOF);
    this.assertEqual(completionReasons[0], cjDB.CMPL_REASON_STOPPED);

    this.assertTrue(matured[0], True);
  }


  def testConstant(self)
  {
    """ Sanity check to make sure that when only 1 model is running, it continues
    to run even when it has reached maturity """;
    this._printTestHeader();
    jobID,_,_,_,_ = this.runPermutations(this.expDir, hsImp=this.hsImp, maxModels=1,
                                loggingLevel = g_myEnv.options.logLevel,
                                env = this.env,
                                dummyModel={"metricFunctions":
                                              ["lambda x: 100"],
                                            "iterations":350,
                                            "experimentDirectory":this.expDir,
                                });


    cjDB = ClientJobsDAO.get();

    modelIDs = cjDB.jobGetModelIDs(jobID);

    dbResults = cjDB.modelsGetFields(modelIDs, ["numRecords", "completionReason",
                                                "engMatured"]);
    modelIDs = [x[0] for x in dbResults];
    records = [x[1][0] for x in dbResults];
    completionReasons = [x[1][1] for x in dbResults];
    matured = [x[1][2] for x in dbResults];

    results = cjDB.jobGetFields(jobID, ["results"])[0];
    results = json.loads(results);

    this.assertEqual(results["bestModel"], min(modelIDs));
    this.assertEqual(records[0], 350);
    this.assertEqual(completionReasons[0], cjDB.CMPL_REASON_EOF);
    this.assertEqual(matured[0], True);
  }


  def getModelFields(self, jobID, fields)
  {
    cjDB = ClientJobsDAO.get();
    modelIDs = cjDB.jobGetModelIDs(jobID);
    modelParams = cjDB.modelsGetFields(modelIDs, ["params"]+fields);
    modelIDs = [e[0] for e in modelParams];

    modelOrders = [json.loads(e[1][0])["structuredParams"]["__model_num"] for e in modelParams];
    modelFields = [];

    for( f in xrange(len(fields)))
    {
      modelFields.append([e[1][f+1] for e in modelParams]);
    }

    modelInfo = zip(modelOrders, modelIDs, *tuple(modelFields));
    modelInfo.sort(key=lambda info:info[0]);

    return [e[1:] for e in sorted(modelInfo, key=lambda info:info[0])];
  }
}



class SwarmTerminatorTests(ExperimentTestBaseClass)
{
  """
  """;
  // AWS tests attribute required for tagging via automatic test discovery via
  // nosetests
  engineAWSClusterTest=True;


  def setUp(self)
  {
    this.env = {"NTA_CONF_PROP_nupic_hypersearch_enableModelMaturity":"0",
                "NTA_CONF_PROP_nupic_hypersearch_enableModelTermination":"0",
                "NTA_CONF_PROP_nupic_hypersearch_enableSwarmTermination":"1",
                "NTA_TEST_recordSwarmTerminations":"1"};
  }


  def testSimple(self, useCluster=False)
  {
    """Run with one really bad swarm to see if terminator picks it up correctly""";

    if( not g_myEnv.options.runInProc)
    {
      this.skipTest("Skipping One Node test since runInProc is not specified");
    }
    this._printTestHeader();
    expDir = os.path.join(g_myEnv.testSrcExpDir, "swarm_v2");

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=None,
                                onCluster=useCluster,
                                env=this.env,
                                dummyModel={"iterations":200});

    cjDB = ClientJobsDAO.get();
    jobResultsStr = cjDB.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    terminatedSwarms = jobResults["terminatedSwarms"];

    swarmMaturityWindow = int(configuration.Configuration.get(
        "nupic.hypersearch.swarmMaturityWindow"));

    prefix = "modelParams|sensorParams|encoders|";
    for( swarm, (generation, scores) in terminatedSwarms.iteritems())
    {
      if( prefix + "gym" in swarm.split("."))
      {
        this.assertEqual(generation, swarmMaturityWindow-1);
      }
      else
      {
        this.assertEqual(generation, swarmMaturityWindow-1+4);
      }
    }
  }


  def testMaturity(self, useCluster=False)
  {
    if( not g_myEnv.options.runInProc)
    {
      this.skipTest("Skipping One Node test since runInProc is not specified");
    }
    this._printTestHeader();
    this.env["NTA_CONF_PROP_enableSwarmTermination"] = "0";
    expDir = os.path.join(g_myEnv.testSrcExpDir, "swarm_maturity_v2");

    (jobID, jobInfo, resultInfos, metricResults, minErrScore) \
         = this.runPermutations(expDir,
                                hsImp="v2",
                                loggingLevel=g_myEnv.options.logLevel,
                                maxModels=None,
                                onCluster=useCluster,
                                env=this.env,
                                dummyModel={"iterations":200});

    cjDB = ClientJobsDAO.get();
    jobResultsStr = cjDB.jobGetFields(jobID, ["results"])[0];
    jobResults = json.loads(jobResultsStr);
    terminatedSwarms = jobResults["terminatedSwarms"];

    swarmMaturityWindow = int(configuration.Configuration.get(
        "nupic.hypersearch.swarmMaturityWindow"));

    prefix = "modelParams|sensorParams|encoders|";
    for( swarm, (generation, scores) in terminatedSwarms.iteritems())
    {
      encoders = swarm.split(".");
      if( prefix + "gym" in encoders)
      {
        this.assertEqual(generation, swarmMaturityWindow-1 + 3);
      }

      else if( prefix + "address" in encoders)
      {
        this.assertEqual(generation, swarmMaturityWindow-1);
      }

      else
      {
        this.assertEqual(generation, swarmMaturityWindow-1 + 7);
      }
    }
  }


  def testSimpleMN(self)
  {
    this.testSimple(useCluster=True);
  }


  def testMaturityMN(self)
  {
    this.testMaturity(useCluster=True);
  }
}



def getHypersearchWinningModelID(jobID)
{
  """
  Parameters:
  -------------------------------------------------------------------
  jobID:            jobID of successfully-completed Hypersearch job

  retval:           modelID of the winning model
  """;

  cjDAO = ClientJobsDAO.get();
  jobResults = cjDAO.jobGetFields(jobID, ["results"])[0];
  print "Hypersearch job results: %r" % (jobResults,);
  jobResults = json.loads(jobResults);
  return jobResults["bestModel"];
}



def _executeExternalCmdAndReapStdout(args)
{
  """
  args:     Args list as defined for the args parameter in subprocess.Popen()

  Returns:  result dicionary:
              {
                "exitStatus":<exit-status-of-external-command>,
                "stdoutData":"string",
                "stderrData":"string"
              }
  """;

  _debugOut(("_executeExternalCmdAndReapStdout: Starting...\n<%s>") % \
                (args,));

  p = subprocess.Popen(args,
                       env=os.environ,
                       stdout=subprocess.PIPE,
                       stderr=subprocess.PIPE);
  _debugOut(("Process started for <%s>") % (args,));

  (stdoutData, stderrData) = p.communicate();
  _debugOut(("Process completed for <%s>: exit status=%s, stdoutDataType=%s, " + \
             "stdoutData=<%s>, stderrData=<%s>") % \
                (args, p.returncode, type(stdoutData), stdoutData, stderrData));

  result = dict(
    exitStatus = p.returncode,
    stdoutData = stdoutData,
    stderrData = stderrData,
  );

  _debugOut(("_executeExternalCmdAndReapStdout for <%s>: result=\n%s") % \
                (args, pprint.pformat(result, indent=4)));

  return result;
}



def _debugOut(text)
{
  global g_debug;
  if( g_debug)
  {
    print text;
    sys.stdout.flush();
  }

  return;
}



def _getTestList()
{
  """ Get the list of tests that can be run from this module""";

  suiteNames = [
                "OneNodeTests",
                "MultiNodeTests",
                "ModelMaturityTests",
                "SwarmTerminatorTests",
               ];

  testNames = [];
  for( suite in suiteNames)
  {
    for( f in dir(eval(suite)))
    {
      if( f.startswith("test"))
      {
        testNames.append("%s.%s" % (suite, f));
      }
    }
  }

  return testNames;
}

class _ArgParser(object)
{
  """Class which handles command line arguments and arguments passed to the test
  """;
  args = [];

  @classmethod;
  def _processArgs(cls)
  {
    """
    Parse our command-line args/options and strip them from sys.argv
    Returns the tuple (parsedOptions, remainingArgs)
    """;
    helpString = \
    """%prog [options...] [-- unittestoptions...] [suitename.testname | suitename]
    Run the Hypersearch unit tests. To see unit test framework options, enter:
    python %prog -- --help

    Example usages:
      python %prog MultiNodeTests
      python %prog MultiNodeTests.testOrphanedModel
      python %prog -- MultiNodeTests.testOrphanedModel
      python %prog -- --failfast
      python %prog -- --failfast OneNodeTests.testOrphanedModel

    Available suitename.testnames: """;

    // Update help string
    allTests = _getTestList();
    for( test in allTests)
    {
      helpString += "\n    %s" % (test);
    }

    // ============================================================================
    // Process command line arguments
    parser = OptionParser(helpString,conflict_handler="resolve");


    parser.add_option("--verbosity", default=0, type="int",
          help="Verbosity level, either 0, 1, 2, or 3 [default: %default].");

    parser.add_option("--runInProc", action="store_true", default=False,
        help="Run inProc tests, currently inProc are not being run by default "
             " running. [default: %default].");

    parser.add_option("--logLevel", action="store", type="int",
          default=logging.INFO,
          help="override default log level. Pass in an integer value that "
          "represents the desired logging level (10=logging.DEBUG, "
          "20=logging.INFO, etc.) [default: %default].");

    parser.add_option("--hs", dest="hsVersion", default=2, type="int",
                      help=("Hypersearch version (only 2 supported; 1 was "
                            "deprecated) [default: %default]."));
    return parser.parse_args(args=cls.args);
  }

  @classmethod;
  def parseArgs(cls)
  {
    """ Returns the test arguments after parsing
    """;
    return cls._processArgs()[0];
  }

  @classmethod;
  def consumeArgs(cls)
  {
    """ Consumes the test arguments and returns the remaining arguments meant
    for unittest.man
    """;
    return cls._processArgs()[1];
  }
}



def setUpModule()
{
  print "\nCURRENT DIRECTORY:", os.getcwd();

  initLogging(verbose=True);

  global g_myEnv;
  // Setup our environment
  g_myEnv = MyTestEnvironment();
}

if( __name__ == "__main__")
{
  // Form the command line for the unit test framework
  // Consume test specific arguments and pass remaining to unittest.main
  _ArgParser.args = sys.argv[1:];
  args = [sys.argv[0]] + _ArgParser.consumeArgs();

  // Run the tests if called using python
  unittest.main(argv=args);
}




class MyTestEnvironment(object)
{

  // =======================================================================
  def __init__(self)
  {

    // Save all command line options
    this.options = _ArgParser.parseArgs();

    // Create the path to our source experiments
    thisFile = __file__;
    testDir = os.path.split(os.path.abspath(thisFile))[0];
    this.testSrcExpDir = os.path.join(testDir, "experiments");
    this.testSrcDataDir = os.path.join(testDir, "data");

    return;
  }
}



class ExperimentTestBaseClass(HelperTestCaseBase)
{


  def setUp(self)
  {
    """ Method called to prepare the test fixture. This is called by the
    unittest framework immediately before calling the test method; any exception
    raised by this method will be considered an error rather than a test
    failure. The default implementation does nothing.
    """;
    pass;
  }


  def tearDown(self)
  {
    """ Method called immediately after the test method has been called and the
    result recorded. This is called even if the test method raised an exception,
    so the implementation in subclasses may need to be particularly careful
    about checking internal state. Any exception raised by this method will be
    considered an error rather than a test failure. This method will only be
    called if the setUp() succeeds, regardless of the outcome of the test
    method. The default implementation does nothing.
    """;
    // Reset our log items
    this.resetExtraLogItems();
  }


  def shortDescription(self)
  {
    """ Override to force unittest framework to use test method names instead
    of docstrings in the report.
    """;
    return None;
  }


  def _printTestHeader(self)
  {
    """ Print out what test we are running
    """;

    print "###############################################################";
    print "Running test: %s.%s..." % (this.__class__, this._testMethodName);
  }
  

  def _setDataPath(self, env)
  {
    """ Put the path to our datasets int the NTA_DATA_PATH variable which
    will be used to set the environment for each of the workers

    Parameters:
    ---------------------------------------------------------------------
    env: The current environment dict
    """;

    assert env is not None;

    // If already have a path, concatenate to it
    if( "NTA_DATA_PATH" in env)
    {
      newPath = "%s:%s" % (env["NTA_DATA_PATH"], g_myEnv.testSrcDataDir);
    }
    else
    {
      newPath = g_myEnv.testSrcDataDir;
    }

    env["NTA_DATA_PATH"] = newPath;
  }


  def _launchWorkers(self, cmdLine, numWorkers)
  {
    """ Launch worker processes to execute the given command line

    Parameters:
    -----------------------------------------------
    cmdLine: The command line for each worker
    numWorkers: number of workers to launch
    retval: list of workers

    """;

    workers = [];
    for( i in range(numWorkers))
    {
      args = ["bash", "-c", cmdLine];
      stdout = tempfile.TemporaryFile();
      stderr = tempfile.TemporaryFile();
      p = subprocess.Popen(args, bufsize=1, env=os.environ, shell=False,
                           stdin=None, stdout=stdout, stderr=stderr);
      workers.append(p);
    }

    return workers;
  }

  def _getJobInfo(self, cjDAO, workers, jobID)
  {
    """ Return the job info for a job

    Parameters:
    -----------------------------------------------
    cjDAO:   client jobs database instance
    workers: list of workers for this job
    jobID:   which job ID

    retval: job info
    """;

    // Get the job info
    jobInfo = cjDAO.jobInfo(jobID);

    // Since we"re running outside of the Nupic engine, we launched the workers
    //  ourself, so see how many are still running and jam the correct status
    //  into the job info. When using the Nupic engine, it would do this
    //  for us.
    runningCount = 0;
    for( worker in workers)
    {
      retCode = worker.poll();
      if( retCode is None)
      {
        runningCount += 1;
      }
    }

    if( runningCount > 0)
    {
      status = ClientJobsDAO.STATUS_RUNNING;
    }
    else
    {
      status = ClientJobsDAO.STATUS_COMPLETED;
    }

    jobInfo = jobInfo._replace(status=status);
    if( status == ClientJobsDAO.STATUS_COMPLETED)
    {
      jobInfo = jobInfo._replace(
                        completionReason=ClientJobsDAO.CMPL_REASON_SUCCESS);
    }
    return jobInfo;
  }

  def _generateHSJobParams(self,
                           expDirectory=None,
                           hsImp="v2",
                           maxModels=2,
                           predictionCacheMaxRecords=None,
                           dataPath=None,
                           maxRecords=10)
  {
    """
    This method generates a canned Hypersearch Job Params structure based
    on some high level options

    Parameters:
    ---------------------------------------------------------------------
    predictionCacheMaxRecords:
                   If specified, determine the maximum number of records in
                   the prediction cache.
    dataPath:      When expDirectory is not specified, this is the data file
                   to be used for the operation. If this value is not specified,
                   it will use the /extra/qa/hotgym/qa_hotgym.csv.
    """;


    if( expDirectory is not None)
    {
      descriptionPyPath = os.path.join(expDirectory, "description.py");
      permutationsPyPath = os.path.join(expDirectory, "permutations.py");

      permutationsPyContents = open(permutationsPyPath, "rb").read();
      descriptionPyContents = open(descriptionPyPath, "rb").read();

      jobParams = {"persistentJobGUID" : generatePersistentJobGUID(),
                   "permutationsPyContents": permutationsPyContents,
                   "descriptionPyContents": descriptionPyContents,
                   "maxModels": maxModels,
                   "hsVersion": hsImp};

      if( predictionCacheMaxRecords is not None)
      {
        jobParams["predictionCacheMaxRecords"] = predictionCacheMaxRecords;
      }
    }

    else
    {


      // Form the stream definition
      if( dataPath is None)
      {
        dataPath = resource_filename("nupic.data",
                                     os.path.join("extra", "qa", "hotgym",
                                                  "qa_hotgym.csv"));
      }

      streamDef = dict(
        version = 1,
        info = "TestHypersearch",
        streams = [
          dict(source="file://%s" % (dataPath),
               info=dataPath,
               columns=["*"],
               first_record=0,
               last_record=maxRecords),
          ],
      );


      // Generate the experiment description
      expDesc = {
        "predictionField": "consumption",
        "streamDef": streamDef,
        "includedFields": [
          { "fieldName": "gym",
            "fieldType": "string"
          },
          { "fieldName": "consumption",
            "fieldType": "float",
            "minValue": 0,
            "maxValue": 200,
          },
        ],
        "iterationCount": maxRecords,
        "resetPeriod": {
          "weeks": 0,
          "days": 0,
          "hours": 8,
          "minutes": 0,
          "seconds": 0,
          "milliseconds": 0,
          "microseconds": 0,
        },
      };


      jobParams = {
        "persistentJobGUID": _generatePersistentJobGUID(),
        "description":expDesc,
        "maxModels": maxModels,
        "hsVersion": hsImp,
      };

      if( predictionCacheMaxRecords is not None)
      {
        jobParams["predictionCacheMaxRecords"] = predictionCacheMaxRecords;
      }
    }

    return jobParams;
  }

  def _runPermutationsLocal(self, jobParams, loggingLevel=logging.INFO,
                            env=None, waitForCompletion=True,
                            continueJobId=None, ignoreErrModels=False)
  {
    """ This runs permutations on the given experiment using just 1 worker
    in the current process

    Parameters:
    -------------------------------------------------------------------
    jobParams:        filled in job params for a hypersearch
    loggingLevel:    logging level to use in the Hypersearch worker
    env:             if not None, this is a dict of environment variables
                        that should be sent to each worker process. These can
                        aid in re-using the same description/permutations file
                        for different tests.
    waitForCompletion: If True, wait for job to complete before returning
                       If False, then return resultsInfoForAllModels and
                       metricResults will be None
    continueJobId:    If not None, then this is the JobId of a job we want
                      to continue working on with another worker.
    ignoreErrModels:  If true, ignore erred models
    retval:          (jobId, jobInfo, resultsInfoForAllModels, metricResults)
    """;


    print;
    print "==================================================================";
    print "Running Hypersearch job using 1 worker in current process";
    print "==================================================================";

    // Plug in modified environment variables
    if( env is not None)
    {
      saveEnvState = copy.deepcopy(os.environ);
      os.environ.update(env);
    }

    // Insert the job entry into the database in the pre-running state
    cjDAO = ClientJobsDAO.get();
    if( continueJobId is None)
    {
      jobID = cjDAO.jobInsert(client="test", cmdLine="<started manually>",
              params=json.dumps(jobParams),
              alreadyRunning=True, minimumWorkers=1, maximumWorkers=1,
              jobType = cjDAO.JOB_TYPE_HS);
    }
    else
    {
      jobID = continueJobId;
    }

    // Command line args.
    args = ["ignoreThis", "--jobID=%d" % (jobID),
            "--logLevel=%d" % (loggingLevel)];
    if( continueJobId is None)
    {
      args.append("--clearModels");
    }

    // Run it in the current process
    try
    {
      HypersearchWorker.main(args);
    }

    // The dummy model runner will call sys.exit(0) when
    //  NTA_TEST_sysExitAfterNIterations is set
    catch( SystemExit)
    {
      pass;
    }
    catch()
    {
      raise;
    }

    // Restore environment
    if( env is not None)
    {
      os.environ = saveEnvState;
    }

    // ----------------------------------------------------------------------
    // Make sure all models completed successfully
    models = cjDAO.modelsGetUpdateCounters(jobID);
    modelIDs = [model.modelId for model in models];
    if( len(modelIDs) > 0)
    {
      results = cjDAO.modelsGetResultAndStatus(modelIDs);
    }
    else
    {
      results = [];
    }

    metricResults = [];
    for( result in results)
    {
      if( result.results is not None)
      {
        metricResults.append(json.loads(result.results)[1].values()[0]);
      }
      else
      {
        metricResults.append(None);
      }
      if( not ignoreErrModels)
      {
        this.assertNotEqual(result.completionReason, cjDAO.CMPL_REASON_ERROR,
            "Model did not complete successfully:\n%s" % (result.completionMsg));
      }
    }

    // Print worker completion message
    jobInfo = cjDAO.jobInfo(jobID);

    return (jobID, jobInfo, results, metricResults);
  }


  def _runPermutationsCluster(self, jobParams, loggingLevel=logging.INFO,
                              maxNumWorkers=4, env=None,
                              waitForCompletion=True, ignoreErrModels=False,
                              timeoutSec=DEFAULT_JOB_TIMEOUT_SEC)
  {
    """ Given a prepared, filled in jobParams for a hypersearch, this starts
    the job, waits for it to complete, and returns the results for all
    models.

    Parameters:
    -------------------------------------------------------------------
    jobParams:        filled in job params for a hypersearch
    loggingLevel:    logging level to use in the Hypersearch worker
    maxNumWorkers:    max // of worker processes to use
    env:             if not None, this is a dict of environment variables
                        that should be sent to each worker process. These can
                        aid in re-using the same description/permutations file
                        for different tests.
    waitForCompletion: If True, wait for job to complete before returning
                       If False, then return resultsInfoForAllModels and
                       metricResults will be None
    ignoreErrModels:  If true, ignore erred models
    retval:          (jobID, jobInfo, resultsInfoForAllModels, metricResults)
    """;

    print;
    print "==================================================================";
    print "Running Hypersearch job on cluster";
    print "==================================================================";

    // --------------------------------------------------------------------
    // Submit the job
    if( env is not None and len(env) > 0)
    {
      envItems = [];
      for (key, value) in env.iteritems()
      {
        envItems.append("export %s=%s" % (key, value));
      }
      envStr = "%s;" % (";".join(envItems));
    }
    else
    {
      envStr = "";
    }

    cmdLine = "%s python -m nupic.swarming.HypersearchWorker " \
                          "--jobID={JOBID} --logLevel=%d" \
                          % (envStr, loggingLevel);

    cjDAO = ClientJobsDAO.get();
    jobID = cjDAO.jobInsert(client="test", cmdLine=cmdLine,
            params=json.dumps(jobParams),
            minimumWorkers=1, maximumWorkers=maxNumWorkers,
            jobType = cjDAO.JOB_TYPE_HS);

    // Launch the workers ourself if necessary (no nupic engine running).
    workerCmdLine = "%s python -m nupic.swarming.HypersearchWorker " \
                          "--jobID=%d --logLevel=%d" \
                          % (envStr, jobID, loggingLevel);
    workers = this._launchWorkers(cmdLine=workerCmdLine, numWorkers=maxNumWorkers);

    print "Successfully submitted new test job, jobID=%d" % (jobID);
    print "Each of %d workers executing the command line: " % (maxNumWorkers), \
            cmdLine;

    if( not waitForCompletion)
    {
      return (jobID, None, None);
    }

    if( timeoutSec is None)
    {
      timeout=DEFAULT_JOB_TIMEOUT_SEC;
    }
    else
    {
      timeout=timeoutSec;
    }

    // --------------------------------------------------------------------
    // Wait for it to complete
    startTime = time.time();
    lastUpdate = time.time();
    lastCompleted = 0;
    lastCompletedWithError = 0;
    lastCompletedAsOrphan = 0;
    lastStarted = 0;
    lastJobStatus = "NA";
    lastJobResults = None;
    lastActiveSwarms = None;
    lastEngStatus = None;
    modelIDs = [];
    print "\n%-15s    %-15s %-15s %-15s %-15s" % ("jobStatus", "modelsStarted",
                                "modelsCompleted", "modelErrs", "modelOrphans");
    print "-------------------------------------------------------------------";
    while (lastJobStatus != ClientJobsDAO.STATUS_COMPLETED) \
          and (time.time() - lastUpdate < timeout)
    {

      printUpdate = False;
      if( g_myEnv.options.verbosity == 0)
      {
        time.sleep(0.5);
      }

      // --------------------------------------------------------------------
      // Get the job status
      jobInfo = this._getJobInfo(cjDAO, workers, jobID);
      if( jobInfo.status != lastJobStatus)
      {
        if( jobInfo.status == ClientJobsDAO.STATUS_RUNNING \
            and lastJobStatus != ClientJobsDAO.STATUS_RUNNING)
        {
          print "// Swarm job now running. jobID=%s" \
                % (jobInfo.jobId);
        }

        lastJobStatus = jobInfo.status;
        printUpdate = True;
      }

      if( g_myEnv.options.verbosity >= 1)
      {
        if( jobInfo.engWorkerState is not None)
        {
          activeSwarms = json.loads(jobInfo.engWorkerState)["activeSwarms"];
          if( activeSwarms != lastActiveSwarms)
          {
            #print "-------------------------------------------------------"
            print ">> Active swarms:\n   ", "\n    ".join(activeSwarms);
            lastActiveSwarms = activeSwarms;
            print;
          }
        }

        if( jobInfo.results != lastJobResults)
        {
          #print "-------------------------------------------------------"
          print ">> New best:", jobInfo.results, "###";
          lastJobResults = jobInfo.results;
        }

        if( jobInfo.engStatus != lastEngStatus)
        {
          print ">> Status: "%s"" % jobInfo.engStatus;
          print;
          lastEngStatus = jobInfo.engStatus;
        }
      }


      // --------------------------------------------------------------------
      // Get the list of models created for this job
      modelCounters = cjDAO.modelsGetUpdateCounters(jobID);
      if( len(modelCounters) != lastStarted)
      {
        modelIDs = [x.modelId for x in modelCounters];
        lastStarted = len(modelCounters);
        printUpdate = True;
      }

      // --------------------------------------------------------------------
      // See how many have finished
      if( len(modelIDs) > 0)
      {
        completed = 0;
        completedWithError = 0;
        completedAsOrphan = 0;
        infos = cjDAO.modelsGetResultAndStatus(modelIDs);
        for( info in infos)
        {
          if( info.status == ClientJobsDAO.STATUS_COMPLETED)
          {
            completed += 1;
            if( info.completionReason == ClientJobsDAO.CMPL_REASON_ERROR)
            {
              completedWithError += 1;
            }
            if( info.completionReason == ClientJobsDAO.CMPL_REASON_ORPHAN)
            {
              completedAsOrphan += 1;
            }
          }
        }


        if( completed != lastCompleted \
              or completedWithError != lastCompletedWithError \
              or completedAsOrphan != lastCompletedAsOrphan)
        {
          lastCompleted = completed;
          lastCompletedWithError = completedWithError;
          lastCompletedAsOrphan = completedAsOrphan;
          printUpdate = True;
        }
      }

      // --------------------------------------------------------------------
      // Print update?
      if( printUpdate)
      {
        lastUpdate = time.time();
        if( g_myEnv.options.verbosity >= 1)
        {
          print ">>",;
        }
        print "%-15s %-15d %-15d %-15d %-15d" % (lastJobStatus, lastStarted,
                lastCompleted,
                lastCompletedWithError,
                lastCompletedAsOrphan);
      }
    }


    // ========================================================================
    // Final total
    print "\n<< %-15s %-15d %-15d %-15d %-15d" % (lastJobStatus, lastStarted,
                lastCompleted,
                lastCompletedWithError,
                lastCompletedAsOrphan);

    // Success?
    jobInfo = this._getJobInfo(cjDAO, workers, jobID);

    if( not ignoreErrModels)
    {
      this.assertEqual (jobInfo.completionReason,
                      ClientJobsDAO.CMPL_REASON_SUCCESS);
    }

    // Get final model results
    models = cjDAO.modelsGetUpdateCounters(jobID);
    modelIDs = [model.modelId for model in models];
    if( len(modelIDs) > 0)
    {
      results = cjDAO.modelsGetResultAndStatus(modelIDs);
    }
    else
    {
      results = [];
    }

    metricResults = [];
    for( result in results)
    {
      if( result.results is not None)
      {
        metricResults.append(json.loads(result.results)[1].values()[0]);
      }
      else
      {
        metricResults.append(None);
      }
      if( not ignoreErrModels)
      {
        this.assertNotEqual(result.completionReason, cjDAO.CMPL_REASON_ERROR,
          "Model did not complete successfully:\n%s" % (result.completionMsg));
      }
    }


    return (jobID, jobInfo, results, metricResults);
  }


  def runPermutations(self, expDirectory, hsImp="v2", maxModels=2,
                      maxNumWorkers=4, loggingLevel=logging.INFO,
                      onCluster=False, env=None, waitForCompletion=True,
                      continueJobId=None, dataPath=None, maxRecords=None,
                      timeoutSec=None, ignoreErrModels=False,
                      predictionCacheMaxRecords=None, **kwargs)
  {
    """ This runs permutations on the given experiment using just 1 worker

    Parameters:
    -------------------------------------------------------------------
    expDirectory:    directory containing the description.py and permutations.py
    hsImp:           which implementation of Hypersearch to use
    maxModels:       max // of models to generate
    maxNumWorkers:   max // of workers to use, N/A if onCluster is False
    loggingLevel:    logging level to use in the Hypersearch worker
    onCluster:       if True, run on the Hadoop cluster
    env:             if not None, this is a dict of environment variables
                        that should be sent to each worker process. These can
                        aid in re-using the same description/permutations file
                        for different tests.
    waitForCompletion: If True, wait for job to complete before returning
                       If False, then return resultsInfoForAllModels and
                       metricResults will be None
    continueJobId:    If not None, then this is the JobId of a job we want
                      to continue working on with another worker.
    ignoreErrModels:  If true, ignore erred models
    maxRecords:       This value is passed to the function, _generateHSJobParams(),
                      to represent the maximum number of records to generate for
                      the operation.
    dataPath:         This value is passed to the function, _generateHSJobParams(),
                      which points to the data file for the operation.
    predictionCacheMaxRecords:
                      If specified, determine the maximum number of records in
                      the prediction cache.

    retval:          (jobID, jobInfo, resultsInfoForAllModels, metricResults,
                        minErrScore)
    """;

    // Put in the path to our datasets
    if( env is None)
    {
      env = dict();
    }
    this._setDataPath(env);

    // ----------------------------------------------------------------
    // Prepare the jobParams
    jobParams = this._generateHSJobParams(expDirectory=expDirectory,
                                          hsImp=hsImp, maxModels=maxModels,
                                          maxRecords=maxRecords,
                                          dataPath=dataPath,
                                          predictionCacheMaxRecords=predictionCacheMaxRecords);

    jobParams.update(kwargs);

    if( onCluster)
    {
      (jobID, jobInfo, resultInfos, metricResults) \
        =  this._runPermutationsCluster(jobParams=jobParams,
                                        loggingLevel=loggingLevel,
                                        maxNumWorkers=maxNumWorkers,
                                        env=env,
                                        waitForCompletion=waitForCompletion,
                                        ignoreErrModels=ignoreErrModels,
                                        timeoutSec=timeoutSec);
    }

    else
    {
      (jobID, jobInfo, resultInfos, metricResults) \
        = this._runPermutationsLocal(jobParams=jobParams,
                                     loggingLevel=loggingLevel,
                                     env=env,
                                     waitForCompletion=waitForCompletion,
                                     continueJobId=continueJobId,
                                     ignoreErrModels=ignoreErrModels);
    }

    if( not waitForCompletion)
    {
      return (jobID, jobInfo, resultInfos, metricResults, None);
    }

    // Print job status
    print "\n------------------------------------------------------------------";
    print "Hadoop completion reason: %s" % (jobInfo.completionReason);
    print "Worker completion reason: %s" % (jobInfo.workerCompletionReason);
    print "Worker completion msg: %s" % (jobInfo.workerCompletionMsg);

    if( jobInfo.engWorkerState is not None)
    {
      print "\nEngine worker state:";
      print "---------------------------------------------------------------";
      pprint.pprint(json.loads(jobInfo.engWorkerState));
    }


    // Print out best results
    minErrScore=None;
    metricAmts = [];
    for( result in metricResults)
    {
      if( result is None)
      {
        metricAmts.append(numpy.inf);
      }
      else
      {
        metricAmts.append(result);
      }
    }

    metricAmts = numpy.array(metricAmts);
    if( len(metricAmts) > 0)
    {
      minErrScore = metricAmts.min();
      minModelID = resultInfos[metricAmts.argmin()].modelId;

      // Get model info
      cjDAO = ClientJobsDAO.get();
      modelParams = cjDAO.modelsGetParams([minModelID])[0].params;
      print "Model params for best model: \n%s" \
                              % (pprint.pformat(json.loads(modelParams)));
      print "Best model result: %f" % (minErrScore);
    }

    else
    {
      print "No models finished";
    }


    return (jobID, jobInfo, resultInfos, metricResults, minErrScore);
  }
}


    */
}