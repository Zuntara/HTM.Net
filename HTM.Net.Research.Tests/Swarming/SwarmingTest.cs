using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Research.Tests.Swarming.Experiments;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;
using log4net;
using log4net.Config;
using log4net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

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
        /// Put the Path to our datasets int the NTA_DATA_PATH variable which will 
        /// be used to set the environment for each of the workers
        /// </summary>
        public void _setDataPath(/*env*/)
        {
            //assert env is not None;
            string newPath;
            // If already have a Path, concatenate to it
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
        protected Map<string, object> _generateHSJobParams((ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters)? expDirectory = null, string hsImp = "v2", int? maxModels = 2,
                           int? predictionCacheMaxRecords = null, string dataPath = null, int? maxRecords = 10)
        {
            Map<string, object> jobParams = null;
            if (expDirectory != null)
            {
                //string descriptionPyPath = Path.Combine(expDirectory, "description.py");
                //string permutationsPyPath = Path.Combine(expDirectory, "permutations.py");

                var permutationsPyContents = expDirectory.Value.permutationParameters;//File.ReadAllText(permutationsPyPath);
                var descriptionPyContents = expDirectory.Value.experimentParameters;//File.ReadAllText(descriptionPyPath);

                jobParams = new Map<string, object>
                {
                    {"persistentJobGUID", Utils.generatePersistentJobGUID()},
                    {"permutationsPyContents", permutationsPyContents},
                    {"descriptionPyContents", descriptionPyContents},
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
                    //dataPath = resource_filename("nupic.data", os.Path.join("extra", "qa", "hotgym", "qa_hotgym.csv"));
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
                            uint? continueJobId = null, bool ignoreErrModels = false)
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
            uint? jobID = null;
            if (!continueJobId.HasValue)
            {
                jobID = (uint)cjDAO.jobInsert(client: "test", cmdLine: "<started manually>",
                        @params: JsonConvert.SerializeObject(jobParams, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All
                        }),
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
                HyperSearchWorker.Main(args.ToArray());
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
            var models = cjDAO.modelsGetUpdateCounters((uint?)jobID);
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
                if (result.resultsSerialized != null)
                {
                    result.results = JsonConvert.DeserializeObject<Tuple>(result.resultsSerialized);
                }
                if (result.results != null)
                {
                    metricResults.Add(((Map<string, double?>)result.results.Get(1)).Values.First());
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
            var jobInfo = cjDAO.jobInfo((uint?)jobID);

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
        public PermutationsLocalResult RunPermutations((ExperimentParameters experimentParameters, ExperimentPermutationParameters permutationParameters) expDirectory, string hsImp = "v2", int? maxModels = 2,
                      int maxNumWorkers = 4, bool onCluster = false, bool waitForCompletion = true,
                      uint? continueJobId = null, string dataPath = null, int? maxRecords = null,
                      int? timeoutSec = null, bool ignoreErrModels = false,
                      int? predictionCacheMaxRecords = null, KWArgsModel kwargs = null)
        {

            // Put in the Path to our datasets
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

            uint? jobID = permutationResult.jobID;
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
                ulong minModelID = resultInfos[ArrayUtils.Argmin(metricAmts.ToArray())].modelId;

                // Get model info
                var cjDAO = BaseClientJobDao.Create();
                var modelParams = cjDAO.modelsGetParams(new List<ulong> { minModelID })[0]["params"];
                Console.WriteLine("Model params for best model: \n{0}", JsonConvert.DeserializeObject<ModelParams>(modelParams as string));
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
            public uint? jobID { get; set; }
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

            // Initialize log4net.
            XmlConfigurator.Configure();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("NTA_TEST_numIterations", null);
            Environment.SetEnvironmentVariable("NTA_TEST_exitAfterNModels", null);
            Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow", null);
        }

        /// <summary>
        /// Try running simple permutations
        /// </summary>
        /// <param name="onCluster"></param>
        /// <param name="kwargs"></param>
        private void TestSimpleV2Internal(bool onCluster = false, KWArgsModel kwargs = null)
        {
            //this._printTestHeader();
            var expDir = (new SimpleV2DescriptionParameters(), new SimpleV2PermutationParameters());
            // Test it out
            //if (env is None)
            //{
            //    env = dict();
            //}
            Environment.SetEnvironmentVariable("NTA_TEST_numIterations", "99");
            Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow", "5");
            //env["NTA_TEST_numIterations"] = "99";
            //env["NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow"] = "%d" % (g_repeatableSwarmMaturityWindow);

            //(jobID, jobInfo, resultInfos, metricResults, minErrScore) 
            var permutationResult = this.RunPermutations(expDirectory: expDir,
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

        /// <summary>
        /// Try running a simple permutations with delta encoder
        /// Test which tests the delta encoder.Runs a swarm of the sawtooth dataset
        /// With a functioning delta encoder this should give a perfect result
        /// DEBUG: disabled temporarily because this test takes too long!!!
        /// </summary>
        /// <param name="onCluster"></param>
        private void TestDeltaV2Internal(bool onCluster = false, KWArgsModel kwargs = null)
        {
            var expDir = (new DeltaDescriptionParameters(), new DeltaPermutationParameters());

            Environment.SetEnvironmentVariable("NTA_TEST_exitAfterNModels", "20");
            Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow", "5");

            var permutationResult = this.RunPermutations(expDirectory: expDir,
                                   hsImp: "v2",
                                   //loggingLevel = g_myEnv.options.logLevel,
                                   onCluster: onCluster,
                                   //env = env,
                                   maxModels: null,
                                   kwargs: kwargs);
            Assert.IsTrue(permutationResult.minErrScore < 0.002);
        }

        private void TestCLAModelV2(bool onCluster = false, KWArgsModel kwargs = null, int? maxModels = 2)
        {
            //this._printTestHeader();
            var expDir = (new DummyV2DescriptionParameters(), new DummyV2PermutationParameters());

            //Environment.SetEnvironmentVariable("NTA_TEST_numIterations", "99");
            //Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow", "5");

            //(jobID, jobInfo, resultInfos, metricResults, minErrScore) 
            var permutationResult = this.RunPermutations(expDirectory: expDir,
                                   hsImp: "v2",
                                   onCluster: onCluster,
                                   maxModels: maxModels,
                                   kwargs: kwargs);


            Assert.AreEqual(maxModels, permutationResult.results.Count);
        }

        // nupic/src/nupic/datafiles/swarming/test_data.csv
        //[TestMethod]
        //[DeploymentItem("Resources\\swarming\\test_data.csv")]
        public void TestSimpleV2()
        {
            TestSimpleV2Internal();
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\swarming\\sawtooth.csv")]
        public void TestDeltaV2()
        {
            TestDeltaV2Internal();
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\swarming\\test_data.csv")]
        public void TestCLAModelV2()
        {
            TestCLAModelV2(maxModels: 4);
        }

        [TestMethod]
        [DeploymentItem("Resources\\swarming\\linear.csv")]
        [Ignore("Not finished yet")]
        public void TestLinearOpfInternal()
        {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);

            var config = BenchMarkLinear(-1);
            config.swarmSize = SwarmDefinition.SwarmSize.Medium;
            config.maxModels = 100;

            // Convert config to parameters
            // set encoders in place
            var description = new ExpGenerator(config).GenerateParams();

            description.experimentParameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] {128});
            description.experimentParameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] {128 });

            description.permutationParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, new PermuteFloat(0.1,1.2));
            //((PermuteEncoder)description.Item2.Encoders["number"]).n = new PermuteInt(13, 500, 20);

            
            var expDir = (description.experimentParameters, description.permutationParameters);

            // Test it out
            var permutationResult = this.RunPermutations(expDirectory: expDir,
                                   hsImp: "v2",
                                   //loggingLevel = g_myEnv.options.logLevel,
                                   onCluster: false,
                                   //env = env,
                                   maxModels: null,
                                   kwargs: null);


            Assert.AreEqual(20, permutationResult.minErrScore);
            Assert.IsTrue(permutationResult.results.Count < 350);
            //this.assertLess(len(resultInfos), 350);
        }

        public SwarmDefinition BenchMarkLinear(int recordsToProcess)
        {
            StreamDef streamDef = new StreamDef
            {
                info = "test_NoProviders",
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] { "number" },
                        info = "linear.csv",
                        source = "linear.csv"
                    }
                },
                version = 1
            };

            SwarmDefinition expDesc = new SwarmDefinition
            {
                inferenceType = InferenceType.TemporalNextStep,
                inferenceArgs = new InferenceArgsDescription
                {
                    predictedField = "number",
                    predictionSteps = new[] { 1 }
                },
                streamDef = streamDef,
                includedFields = new List<SwarmDefinition.SwarmDefIncludedField>
                {
                    new SwarmDefinition.SwarmDefIncludedField
                    {
                        fieldName = "number",
                        fieldType = FieldMetaType.Integer,
                        minValue = 0,
                        maxValue = 10
                    }
                },
                //metrics = new[]
                //{
                //    new MetricSpec(field: "consumption",
                //        inferenceElement: InferenceElement.MultiStepBestPredictions,
                //        metric: "multiStep", @params: new Map<string, object>
                //        {
                //            {"window", 1000},
                //            {"steps", new[] {0}},
                //            {"errorMetric", "avg_err"}
                //        })
                //},
                iterationCount = recordsToProcess
            };

            return expDesc;
        }

        /// <summary>
        /// Try running a spatial classification swarm
        /// </summary>
        [TestMethod]
        [DeploymentItem("Resources\\swarming\\test_data.csv")]
        [Ignore("Not finished yet")]
        public void TestSpatialClassification()
        {
            var expDir = (new SpatialClassificationDescriptionParameters(), new SpatialClassificationPermutationParameters());

            Environment.SetEnvironmentVariable("NTA_TEST_numIterations", "99");
            Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_swarmMaturityWindow", "5");

            // spatial_classification
            var permutationResult = this.RunPermutations(expDirectory: expDir,
                                   hsImp: "v2",
                                   //loggingLevel = g_myEnv.options.logLevel,
                                   onCluster: false,
                                   //env = env,
                                   maxModels: null,
                                   kwargs: null);

            Assert.AreEqual(20, permutationResult.minErrScore);
            Assert.IsTrue(permutationResult.results.Count < 350);

            //  Check the expected field contributions
            var cjDAO = BaseClientJobDao.Create();
            var jobResultsStr = cjDAO.jobGetFields(permutationResult.jobID, new[] { "results" })[0] as string;
            var jobResults = JsonConvert.DeserializeObject<Map<string, object>>(jobResultsStr);
            var bestModel = cjDAO.modelsInfo(new List<ulong> { TypeConverter.Convert<ulong>(jobResults["bestModel"]) })[0];
            var @params = JsonConvert.DeserializeObject<ModelParams>(bestModel.@params);

            var actualFieldContributions = (Map<string, double>)jobResults["fieldContributions"];
            Console.WriteLine(Arrays.ToString(actualFieldContributions));
            var expectedFieldContributions = new Map<string, double>
            {
                {"address", 100 * (90.0-30)/90.0},
                {"gym", 100 * (90.0-40)/90.0},
                {"timestamp_dayOfWeek", 100 * (90.0-80.0)/90.0},
                {"timestamp_timeOfDay", 100 * (90.0-90.0)/90.0},
            };
            foreach (var expectedFieldContribution in expectedFieldContributions)
            {
                Assert.AreEqual(actualFieldContributions[expectedFieldContribution.Key], expectedFieldContribution.Value);
            }

            Console.WriteLine(@params.particleState.swarmId);
            Assert.AreEqual("Encoders|address.Encoders|gym", @params.particleState.swarmId);
        }

        [TestMethod]
        public void TestDescriptionSerialization()
        {
            SimpleV2DescriptionParameters parameters = new SimpleV2DescriptionParameters();

            string json = JsonConvert.SerializeObject(parameters, Formatting.Indented);
            Console.WriteLine(json);

            var deserialized = JsonConvert.DeserializeObject<SimpleV2DescriptionParameters>(json);
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(SimpleV2DescriptionParameters));
            Assert.AreEqual(parameters, deserialized);
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
            SimpleV2PermutationParameters permutationParameters = new SimpleV2PermutationParameters();

            string json = JsonConvert.SerializeObject(permutationParameters, Formatting.Indented);
            Console.WriteLine("json:");
            Console.WriteLine(json);

            var deserialized = JsonConvert.DeserializeObject<SimpleV2PermutationParameters>(json);
            Assert.IsNotNull(deserialized);
            Assert.IsInstanceOfType(deserialized, typeof(SimpleV2PermutationParameters));

            Assert.AreEqual(7, ((PermuteEncoder)permutationParameters.Encoders["consumption"]).KwArgs["w"]);
            Assert.IsInstanceOfType(((PermuteEncoder)permutationParameters.Encoders["consumption"]).KwArgs["n"], typeof(PermuteInt));

            SimpleV2PermutationParameters des = (SimpleV2PermutationParameters)deserialized;
            Assert.AreEqual((long)7, ((PermuteEncoder)des.Encoders["consumption"]).KwArgs["w"]);
            Assert.IsInstanceOfType(((PermuteEncoder)des.Encoders["consumption"]).KwArgs["n"], typeof(PermuteInt));
        }

        [TestMethod]
        public void TestParamsDeserialisation()
        {
            var expDir = (new SimpleV2DescriptionParameters(), new SimpleV2PermutationParameters());

            var jobParamsDict = this._generateHSJobParams(expDirectory: expDir, hsImp: "v2");
            jobParamsDict.Update(null);

            string jobParamsJson = JsonConvert.SerializeObject(jobParamsDict, Formatting.Indented);
            Console.WriteLine(jobParamsJson);

            var jobParams = JsonConvert.DeserializeObject<Map<string, object>>(jobParamsJson);

            HyperSearchSearchParams p = new HyperSearchSearchParams();
            p.Populate(jobParams);
        }


    }

    //[TestClass]
    //public class SwarmTerminatorTests : ExperimentTestBaseClass
    //{
    //    [TestInitialize]
    //    public void SetUpTests()
    //    {
    //        base.SetUp();
    //        g_myEnv = new MyTestEnvironment();

    //        // Initialize log4net.
    //        XmlConfigurator.Configure();
    //    }

    //    protected override void SetUp()
    //    {
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableModelMaturity", "0");
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableModelTermination", "0");
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableSwarmTermination", "1");
    //        Environment.SetEnvironmentVariable("NTA_TEST_recordSwarmTerminations", "1");
    //    }

    //    [TestCleanup]
    //    public void Teardown()
    //    {
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableModelMaturity", null);
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableModelTermination", null);
    //        Environment.SetEnvironmentVariable("NTA_CONF_PROP_nupic_hypersearch_enableSwarmTermination", null);
    //        Environment.SetEnvironmentVariable("NTA_TEST_recordSwarmTerminations", null);
    //    }

    //    /// <summary>
    //    /// Run with one really bad swarm to see if terminator picks it up correctly
    //    /// </summary>
    //    /// <param name="useCluster"></param>
    //    private void TestSimple(bool useCluster = false)
    //    {
    //        var expDir = new Tuple<ExperimentParameters, ExperimentPermutationParameters>(
    //            new DummyV2DescriptionParameters(), new DummyV2PermutationParameters());

    //        expDir.Item1.Control.IterationCount = 200;

    //        var permutationResult = this.RunPermutations(expDirectory: expDir,
    //                               hsImp: "v2",
    //                               //loggingLevel = g_myEnv.options.logLevel,
    //                               onCluster: useCluster,
    //                               //env = env,
    //                               maxModels: null);

    //        var jobId = permutationResult.jobID;
    //        var cjDb = BaseClientJobDao.Create();
    //        string jobResultsStr = cjDb.jobGetFields(jobId, new[] {"results"})[0] as string;
    //        var jobResults = Json.Deserialize<Map<string,object>>(jobResultsStr);
    //        var terminatedSwarms = jobResults["terminatedSwarms"];

    //        var swarmMaturityWindow = SwarmConfiguration.swarmMaturityWindow;

    //        string prefix = "modelParams|sensorParams|encoders";

    //    }

    //    [TestMethod]
    //    [DeploymentItem("Resources\\swarming\\test_data.csv")]
    //    public void TestSimpleV2()
    //    {
    //        TestSimple();
    //    }
    //}
}