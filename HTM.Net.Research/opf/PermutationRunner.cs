using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HTM.Net.Util;

namespace HTM.Ported.Research.opf
{
    public enum Verbosity : int
    {
        WARNING = 0,
        INFO = 1,
        DEBUG = 2
    }

    public static class PermutationRunner
    {
        public static Map<string, object> DEFAULT_OPTIONS = new Map<string, object>
        {
            {"overwrite", false},
            {"expDescJsonPath", null},
            {"expDescConfig", null},
            {"permutationsScriptPath", null},
            {"outputLabel", "swarm_out"},
            {"outDir", null},
            {"permWorkDir", null},
            {"action", "run"},
            {"searchMethod", "v2"},
            {"timeout", null},
            {"exports", null},
            {"useTerminators", false},
            {"maxWorkers", 2},
            {"replaceReport", false},
            {"maxPermutations", null},
            {"genTopNDescriptions", 1},
        };

        public static Verbosity g_currentVerbosityLevel = 0;
        internal static HyperSearchRunner gCurrentSearch = null;

        public static Action _termHandler = () =>
        {
            try
            {
                var jobrunner = gCurrentSearch;
                var jobID = jobrunner._HyperSearchRunner.__searchJob.getJobID();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }

            Console.WriteLine("Canceling jobs due to receiving SIGTERM");
            cjdao.ClientJobsDAO.get().jobCancel(jobID);
        };

        private static bool _verbosityEnabled(Verbosity verbosityLevel)
        {
            return verbosityLevel <= g_currentVerbosityLevel;
        }

        private static void _emit(Verbosity verbosityLevel, string info)
        {
            if (_verbosityEnabled(verbosityLevel))
            {
                Console.WriteLine(info);
            }
        }

        /// <summary>
        /// Escape commas, tabs, newlines and dashes in a string Commas are encoded as tabs
        /// </summary>
        /// <param name="s"></param>
        private static string _escape(string s)
        {
            s = s.Replace("\\", "\\\\");
            s = s.Replace("\n", "\\n");
            s = s.Replace("\t", "\\t");
            s = s.Replace(",", "\t");
            return s;
        }

        private static object _runHyperSearch(Map<string, object> runOptions)
        {
            // Run HyperSearch
            DateTime startTime = DateTime.Now;
            var search = new HyperSearchRunner(runOptions);
            // Save in global for the signal handler.
            gCurrentSearch = search;
            if (new[] { "run", "dryRun" }.Contains(runOptions["action"] as string))
            {
                search.runNewSearch();
            }
            else
            {
                search.pickupSearch();
            }

            // Generate reports
            // Print results and generate report csv file
            var modelParams = HyperSearchRunner.generateReport(
              options: runOptions,
              replaceReport: runOptions["replaceReport"] as bool?,
              hyperSearchJob: search.peekSearchJob(),
              metricsKeys: search.getDiscoveredMetricsKeys());
            var secs = (DateTime.Now - startTime).TotalSeconds;
            var hours = (int)secs / (60 * 60);
            secs -= hours * (60 * 60);
            var minutes = (int)secs / 60;
            secs -= minutes * 60;
            Console.WriteLine("Elapsed time (h:mm:ss): {0}:{1}:{2}", hours, minutes, secs));
            var jobID = search.peekSearchJob().getJobID();
            Console.WriteLine("Hypersearch ClientJobs job ID: "+ jobID);

            return modelParams;
        }
    }

    internal class HyperSearchRunner
    {
        public HyperSearchJob __searchJob;
        public HyperSearchRunner _HyperSearchRunner;
    }

    public class HyperSearchJob
    {

    }

    /*



def _injectDefaultOptions(options)
{
  return dict(DEFAULT_OPTIONS, **options);
}



def _validateOptions(options)
{
  if( "expDescJsonPath" not in options \
    and "expDescConfig" not in options \
    and "permutationsScriptPath" not in options)
  {
    raise Exception("Options must contain one of the following: "
                    "expDescJsonPath, expDescConfig, or "
                    "permutationsScriptPath.");
  }
}



def _generateExpFilesFromSwarmDescription(swarmDescriptionJson, outDir)
{
  // The expGenerator expects the JSON without newlines for an unknown reason.
  expDescConfig = json.dumps(swarmDescriptionJson);
  expDescConfig = expDescConfig.splitlines();
  expDescConfig = "".join(expDescConfig);

  expGenerator([
    "--description=%s" % (expDescConfig),
    "--outDir=%s" % (outDir)]);
}



def _runAction(runOptions)
{
  if( not os.path.exists(runOptions["outDir"]))
  {
    os.makedirs(runOptions["outDir"]);
  }
  if( not os.path.exists(runOptions["permWorkDir"]))
  {
    os.makedirs(runOptions["permWorkDir"]);
  }

  action = runOptions["action"];
  // Print Nupic HyperSearch results from the current or last run
  if( action == "report")
  {
    returnValue = _HyperSearchRunner.generateReport(
        options=runOptions,
        replaceReport=runOptions["replaceReport"],
        hyperSearchJob=None,
        metricsKeys=None);
  }
  // Run HyperSearch
  else if( action in ("run", "dryRun", "pickup"))
  {
    returnValue = _runHyperSearch(runOptions);
  }
  else
  {
    raise Exception("Unhandled action: %s" % action);
  }
  return returnValue;
}



def _checkOverwrite(options, outDir)
{
  overwrite = options["overwrite"];
  if( not overwrite)
  {
    for( name in ("description.py", "permutations.py"))
    {
      if( os.path.exists(os.path.join(outDir, name)))
      {
        raise RuntimeError("The %s file already exists and will be "
                           "overwritten by this tool. If it is OK to overwrite "
                           "this file, use the --overwrite option." % \
                           os.path.join(outDir, "description.py"));
      }
    }
  }
  // The overwrite option has already been used, so should be removed from the
  // config at this point.
  del options["overwrite"];
}



def runWithConfig(swarmConfig, options,
                  outDir=None, outputLabel="default",
                  permWorkDir=None, verbosity=1)
{
  """
  Starts a swarm, given an dictionary configuration.
  @param swarmConfig {dict} A complete [swarm description](https://github.com/numenta/nupic/wiki/Running-Swarms#the-swarm-description) object.
  @param outDir {string} Optional path to write swarm details (defaults to
                         current working directory).
  @param outputLabel {string} Optional label for output (defaults to "default").
  @param permWorkDir {string} Optional location of working directory (defaults
                              to current working directory).
  @param verbosity {int} Optional (1,2,3) increasing verbosity of output.

  @returns {object} Model parameters
  """;
  global g_currentVerbosityLevel;
  g_currentVerbosityLevel = verbosity;

  // Generate the description and permutations.py files in the same directory
  //  for reference.
  if( outDir is None)
  {
    outDir = os.getcwd();
  }
  if( permWorkDir is None)
  {
    permWorkDir = os.getcwd();
  }

  _checkOverwrite(options, outDir);

  _generateExpFilesFromSwarmDescription(swarmConfig, outDir);

  options["expDescConfig"] = swarmConfig;
  options["outputLabel"] = outputLabel;
  options["outDir"] = outDir;
  options["permWorkDir"] = permWorkDir;

  runOptions = _injectDefaultOptions(options);
  _validateOptions(runOptions);

  return _runAction(runOptions);
}



def runWithJsonFile(expJsonFilePath, options, outputLabel, permWorkDir)
{
  """
  Starts a swarm, given a path to a JSON file containing configuration.

  This function is meant to be used with a CLI wrapper that passes command line
  arguments in through the options parameter.

  @param expJsonFilePath {string} Path to a JSON file containing the complete
                                 [swarm description](https://github.com/numenta/nupic/wiki/Running-Swarms#the-swarm-description).
  @param options {dict} CLI options.
  @param outputLabel {string} Label for output.
  @param permWorkDir {string} Location of working directory.

  @returns {int} Swarm job id.
  """;
  if( "verbosityCount" in options)
  {
    verbosity = options["verbosityCount"];
    del options["verbosityCount"];
  }
  else
  {
    verbosity = 1;
  }

  _setupInterruptHandling();

  // with open(expJsonFilePath, "r") as jsonFile:
  expJsonConfig = json.loads(jsonFile.read());

  outDir = os.path.dirname(expJsonFilePath);
  return runWithConfig(expJsonConfig, options, outDir=outDir,
                       outputLabel=outputLabel, permWorkDir=permWorkDir,
                       verbosity=verbosity);
}



def runWithPermutationsScript(permutationsFilePath, options,
                                 outputLabel, permWorkDir)
{
  """
  Starts a swarm, given a path to a permutations.py script.

  This function is meant to be used with a CLI wrapper that passes command line
  arguments in through the options parameter.

  @param permutationsFilePath {string} Path to permutations.py.
  @param options {dict} CLI options.
  @param outputLabel {string} Label for output.
  @param permWorkDir {string} Location of working directory.

  @returns {object} Model parameters.
  """;
  global g_currentVerbosityLevel;
  if( "verbosityCount" in options)
  {
    g_currentVerbosityLevel = options["verbosityCount"];
    del options["verbosityCount"];
  }
  else
  {
    g_currentVerbosityLevel = 1;
  }

  _setupInterruptHandling();

  options["permutationsScriptPath"] = permutationsFilePath;
  options["outputLabel"] = outputLabel;
  options["outDir"] = permWorkDir;
  options["permWorkDir"] = permWorkDir;

  // Assume it's a permutations python script
  runOptions = _injectDefaultOptions(options);
  _validateOptions(runOptions);

  return _runAction(runOptions);
}



def runPermutations(_)
{
  """
  DEPRECATED. Use @ref runWithConfig.
  """;
  raise DeprecationWarning(
    "nupic.swarming.permutations_runner.runPermutations() is no longer "
    "implemented. It has been replaced with a simpler function for library "
    "usage: nupic.swarming.permutations_runner.runWithConfig(). See docs "
    "at https://github.com/numenta/nupic/wiki/Running-Swarms#running-a-swarm-"
    "programmatically for details.");
}



def _setUpExports(exports)
{
  ret = "";
  if(  exports is None)
  {
    return ret;
  }
  exportDict = json.loads(exports);
  for( key in exportDict.keys())
  {
    if (sys.platform.startswith('win'))
    {
      ret+= "set \"%s=%s\" & " % (str(key), str(exportDict[key]));
    }
    else
    {
      ret+= "export %s=%s;" % (str(key), str(exportDict[key]));
    }
  }
  return ret;
}



def _clientJobsDB()
{
  """
  Returns: The shared cjdao.ClientJobsDAO instance
  """;
  return cjdao.ClientJobsDAO.get();
}



def _nupicHyperSearchHasErrors(hyperSearchJob)
{
  """Check whether any experiments failed in our latest hypersearch

  Parameters:
    hyperSearchJob: _HyperSearchJob instance; if None, will get it from saved
                    jobID, if any

  Returns: False if all models succeeded, True if one or more had errors
  """;
  // TODO flesh me out

  // Get search ID for our latest hypersearch

  // Query Nupic for experiment failures in the given search

  return False;
}



class _HyperSearchRunner(object)
{
  """ @private
  Manages one instance of HyperSearch""";


  def __init__(self, options)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    options:        NupicRunPermutations options dict
    retval:         nothing
    """;

    this.__cjDAO = _clientJobsDB();

    this._options = options;

    // _HyperSearchJob instance set up by runNewSearch() and pickupSearch()
    this.__searchJob = None;

    this.__foundMetrcsKeySet = set();

    // If we are instead relying on the engine to launch workers for us, this
    // will stay as None, otherwise it becomes an array of subprocess Popen
    // instances.
    this._workers = None;

    return;
  }



  def runNewSearch(self)
  {
    """Start a new hypersearch job and monitor it to completion
    Parameters:
    ----------------------------------------------------------------------
    retval:         nothing
    """;
    this.__searchJob = this.__startSearch();

    this.monitorSearchJob();
  }



  def pickupSearch(self)
  {
    """Pick up the latest search from a saved jobID and monitor it to completion
    Parameters:
    ----------------------------------------------------------------------
    retval:         nothing
    """;
    this.__searchJob = this.loadSavedHyperSearchJob(
      permWorkDir=this._options["permWorkDir"],
      outputLabel=this._options["outputLabel"]);


    this.monitorSearchJob();
  }



  def monitorSearchJob(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         nothing
    """;
    assert this.__searchJob is not None;

    jobID = this.__searchJob.getJobID();

    startTime = time.time();
    lastUpdateTime = datetime.now();

    // Monitor HyperSearch and report progress

    // NOTE: may be -1 if it can't be determined
    expectedNumModels = this.__searchJob.getExpectedNumModels(
                                searchMethod = this._options["searchMethod"]);

    lastNumFinished = 0;
    finishedModelIDs = set();

    finishedModelStats = _ModelStats();

    // Keep track of the worker state, results, and milestones from the job
    // record
    lastWorkerState = None;
    lastJobResults = None;
    lastModelMilestones = None;
    lastEngStatus = None;

    hyperSearchFinished = False;
    while( not hyperSearchFinished)
    {
      jobInfo = this.__searchJob.getJobStatus(this._workers);

      // Check for job completion BEFORE processing models; NOTE: this permits us
      // to process any models that we may not have accounted for in the
      // previous iteration.
      hyperSearchFinished = jobInfo.isFinished();

      // Look for newly completed models, and process them
      modelIDs = this.__searchJob.queryModelIDs();
      _emit(Verbosity.DEBUG,
            "Current number of models is %d (%d of them completed)" % (
              len(modelIDs), len(finishedModelIDs)));

      if( len(modelIDs) > 0)
      {
        // Build a list of modelIDs to check for completion
        checkModelIDs = [];
        for( modelID in modelIDs)
        {
          if( modelID not in finishedModelIDs)
          {
            checkModelIDs.append(modelID);
          }
        }

        del modelIDs;

        // Process newly completed models
        if( checkModelIDs)
        {
          _emit(Verbosity.DEBUG,
                "Checking %d models..." % (len(checkModelIDs)));
          errorCompletionMsg = None;
          for (i, modelInfo) in enumerate(_iterModels(checkModelIDs))
          {
            _emit(Verbosity.DEBUG,
                  "[%s] Checking completion: %s" % (i, modelInfo));
            if( modelInfo.isFinished())
            {
              finishedModelIDs.add(modelInfo.getModelID());

              finishedModelStats.update(modelInfo);

              if (modelInfo.getCompletionReason().isError() and
                  not errorCompletionMsg)
              {
                errorCompletionMsg = modelInfo.getCompletionMsg();
              }

              // Update the set of all encountered metrics keys (we will use
              // these to print column names in reports.csv)
              metrics = modelInfo.getReportMetrics();
              this.__foundMetrcsKeySet.update(metrics.keys());
            }
          }
        }

        numFinished = len(finishedModelIDs);

        // Print current completion stats
        if( numFinished != lastNumFinished)
        {
          lastNumFinished = numFinished;

          if( expectedNumModels is None)
          {
            expModelsStr = "";
          }
          else
          {
            expModelsStr = "of %s" % (expectedNumModels);
          }

          stats = finishedModelStats;
          print ("<jobID: %s> %s %s models finished [success: %s; %s: %s; %s: "
                 "%s; %s: %s; %s: %s; %s: %s; %s: %s]" % (
                     jobID,
                     numFinished,
                     expModelsStr,
                     #stats.numCompletedSuccess,
                     (stats.numCompletedEOF+stats.numCompletedStopped),
                     "EOF" if stats.numCompletedEOF else "eof",
                     stats.numCompletedEOF,
                     "STOPPED" if stats.numCompletedStopped else "stopped",
                     stats.numCompletedStopped,
                     "KILLED" if stats.numCompletedKilled else "killed",
                     stats.numCompletedKilled,
                     "ERROR" if stats.numCompletedError else "error",
                     stats.numCompletedError,
                     "ORPHANED" if stats.numCompletedError else "orphaned",
                     stats.numCompletedOrphaned,
                     "UNKNOWN" if stats.numCompletedOther else "unknown",
                     stats.numCompletedOther));

          // Print the first error message from the latest batch of completed
          // models
          if( errorCompletionMsg)
          {
            print "ERROR MESSAGE: %s" % errorCompletionMsg;
          }
        }

        // Print the new worker state, if it changed
        workerState = jobInfo.getWorkerState();
        if( workerState != lastWorkerState)
        {
          print "##>> UPDATED WORKER STATE: \n%s" % (pprint.pformat(workerState,
                                                           indent=4));
          lastWorkerState = workerState;
        }

        // Print the new job results, if it changed
        jobResults = jobInfo.getResults();
        if( jobResults != lastJobResults)
        {
          print "####>> UPDATED JOB RESULTS: \n%s (elapsed time: %g secs)" \
              % (pprint.pformat(jobResults, indent=4), time.time()-startTime);
          lastJobResults = jobResults;
        }

        // Print the new model milestones if they changed
        modelMilestones = jobInfo.getModelMilestones();
        if( modelMilestones != lastModelMilestones)
        {
          print "##>> UPDATED MODEL MILESTONES: \n%s" % (
              pprint.pformat(modelMilestones, indent=4));
          lastModelMilestones = modelMilestones;
        }

        // Print the new engine status if it changed
        engStatus = jobInfo.getEngStatus();
        if( engStatus != lastEngStatus)
        {
          print "##>> UPDATED STATUS: \n%s" % (engStatus);
          lastEngStatus = engStatus;
        }
      }

      // Sleep before next check
      if( not hyperSearchFinished)
      {
        if( this._options["timeout"] != None)
        {
          if ((datetime.now() - lastUpdateTime) >
              timedelta(minutes=this._options["timeout"]))
          {
            print "Timeout reached, exiting";
            this.__cjDAO.jobCancel(jobID);
            sys.exit(1);
          }
        }
        time.sleep(1);
      }
    }

    // Tabulate results
    modelIDs = this.__searchJob.queryModelIDs();
    print "Evaluated %s models" % len(modelIDs);
    print "HyperSearch finished!";

    jobInfo = this.__searchJob.getJobStatus(this._workers);
    print "Worker completion message: %s" % (jobInfo.getWorkerCompletionMsg());
  }



  def _launchWorkers(self, cmdLine, numWorkers)
  {
    """ Launch worker processes to execute the given command line

    Parameters:
    -----------------------------------------------
    cmdLine: The command line for each worker
    numWorkers: number of workers to launch
    """;

    this._workers = [];
    for( i in range(numWorkers))
    {
      stdout = tempfile.TemporaryFile();
      stderr = tempfile.TemporaryFile();
      p = subprocess.Popen(cmdLine, bufsize=1, env=os.environ, shell=True,
                           stdin=None, stdout=stdout, stderr=stderr);
      this._workers.append(p);
    }
  }



  def __startSearch(self)
  {
    """Starts HyperSearch as a worker or runs it inline for the "dryRun" action

    Parameters:
    ----------------------------------------------------------------------
    retval:         the new _HyperSearchJob instance representing the
                    HyperSearch job
    """;
    // This search uses a pre-existing permutations script
    params = _ClientJobUtils.makeSearchJobParamsDict(options=this._options,
                                                     forRunning=True);

    if( this._options["action"] == "dryRun")
    {
      args = [sys.argv[0], "--params=%s" % (json.dumps(params))];

      print;
      print "==================================================================";
      print "RUNNING PERMUTATIONS INLINE as \"DRY RUN\"...";
      print "==================================================================";
      jobID = HypersearchWorker.main(args);
    }

    else
    {
      cmdLine = _setUpExports(this._options["exports"]);
      // Begin the new search. The {JOBID} string is replaced by the actual
      // jobID returned from jobInsert.
      cmdLine += "$HYPERSEARCH";
      maxWorkers = this._options["maxWorkers"];

      jobID = this.__cjDAO.jobInsert(
        client="GRP",
        cmdLine=cmdLine,
        params=json.dumps(params),
        minimumWorkers=1,
        maximumWorkers=maxWorkers,
        jobType=this.__cjDAO.JOB_TYPE_HS);

      cmdLine = "python -m nupic.swarming.HypersearchWorker" \
                 " --jobID=%d" % (jobID);
      this._launchWorkers(cmdLine, maxWorkers);
    }

    searchJob = _HyperSearchJob(jobID);

    // Save search ID to file (this is used for report generation)
    this.__saveHyperSearchJobID(
      permWorkDir=this._options["permWorkDir"],
      outputLabel=this._options["outputLabel"],
      hyperSearchJob=searchJob);

    if( this._options["action"] == "dryRun")
    {
      print "Successfully executed \"dry-run\" hypersearch, jobID=%d" % (jobID);
    }
    else
    {
      print "Successfully submitted new HyperSearch job, jobID=%d" % (jobID);
      _emit(Verbosity.DEBUG,
            "Each worker executing the command line: %s" % (cmdLine,));
    }

    return searchJob;
  }



  def peekSearchJob(self)
  {
    """Retrieves the runner's _HyperSearchJob instance; NOTE: only available
    after run().

    Parameters:
    ----------------------------------------------------------------------
    retval:         _HyperSearchJob instance or None
    """;
    assert this.__searchJob is not None;
    return this.__searchJob;
  }



  def getDiscoveredMetricsKeys(self)
  {
    """Returns a tuple of all metrics keys discovered while running HyperSearch.

    NOTE: This is an optimization so that our client may
        use this info for generating the report csv file without having
        to pre-scan all modelInfos

    Parameters:
    ----------------------------------------------------------------------
    retval:         Tuple of metrics keys discovered while running
                    HyperSearch;
    """;
    return tuple(this.__foundMetrcsKeySet);
  }



  @classmethod;
  def printModels(cls, options)
  {
    """Prints a listing of experiments that would take place without
    actually executing them.

    Parameters:
    ----------------------------------------------------------------------
    options:        NupicRunPermutations options dict
    retval:         nothing
    """;
    print "Generating experiment requests...";

    searchParams = _ClientJobUtils.makeSearchJobParamsDict(options=options);
  }


  @classmethod;
  def generateReport(cls,
                     options,
                     replaceReport,
                     hyperSearchJob,
                     metricsKeys)
  {
    """Prints all available results in the given HyperSearch job and emits
    model information to the permutations report csv.

    The job may be completed or still in progress.

    Parameters:
    ----------------------------------------------------------------------
    options:        NupicRunPermutations options dict
    replaceReport:  True to replace existing report csv, if any; False to
                    append to existing report csv, if any
    hyperSearchJob: _HyperSearchJob instance; if None, will get it from saved
                    jobID, if any
    metricsKeys:    sequence of report metrics key names to include in report;
                    if None, will pre-scan all modelInfos to generate a complete
                    list of metrics key names.
    retval:         model parameters
    """;
    // Load _HyperSearchJob instance from storage, if not provided
    if( hyperSearchJob is None)
    {
      hyperSearchJob = cls.loadSavedHyperSearchJob(
          permWorkDir=options["permWorkDir"],
          outputLabel=options["outputLabel"]);
    }

    modelIDs = hyperSearchJob.queryModelIDs();
    bestModel = None;

    // If metricsKeys was not provided, pre-scan modelInfos to create the list;
    // this is needed by _ReportCSVWriter
    // Also scan the parameters to generate a list of encoders and search
    // parameters
    metricstmp = set();
    searchVar = set();
    for( modelInfo in _iterModels(modelIDs))
    {
      if( modelInfo.isFinished())
      {
        vars = modelInfo.getParamLabels().keys();
        searchVar.update(vars);
        metrics = modelInfo.getReportMetrics();
        metricstmp.update(metrics.keys());
      }
    }
    if( metricsKeys is None)
    {
      metricsKeys = metricstmp;
    }
    // Create a csv report writer
    reportWriter = _ReportCSVWriter(hyperSearchJob=hyperSearchJob,
                                    metricsKeys=metricsKeys,
                                    searchVar=searchVar,
                                    outputDirAbsPath=options["permWorkDir"],
                                    outputLabel=options["outputLabel"],
                                    replaceReport=replaceReport);

    // Tallies of experiment dispositions
    modelStats = _ModelStats();
    #numCompletedOther = long(0)

    print "\nResults from all experiments:";
    print "----------------------------------------------------------------";

    // Get common optimization metric info from permutations script
    searchParams = hyperSearchJob.getParams();

    (optimizationMetricKey, maximizeMetric) = (
      _PermutationUtils.getOptimizationMetricInfo(searchParams));

    // Print metrics, while looking for the best model
    formatStr = None;
    // NOTE: we may find additional metrics if HyperSearch is still running
    foundMetricsKeySet = set(metricsKeys);
    sortedMetricsKeys = [];

    // pull out best Model from jobs table
    jobInfo = _clientJobsDB().jobInfo(hyperSearchJob.getJobID());

    // Try to return a decent error message if the job was cancelled for some
    // reason.
    if( jobInfo.cancel == 1)
    {
      raise Exception(jobInfo.workerCompletionMsg);
    }

    try
    {
      results = json.loads(jobInfo.results);
    }
    catch( Exception, e)
    {
      print "json.loads(jobInfo.results) raised an exception.  " \
            "Here is some info to help with debugging:";
      print "jobInfo: ", jobInfo;
      print "jobInfo.results: ", jobInfo.results;
      print "EXCEPTION: ", e;
      raise;
    }

    bestModelNum = results["bestModel"];
    bestModelIterIndex = None;

    // performance metrics for the entire job
    totalWallTime = 0;
    totalRecords = 0;

    // At the end, we will sort the models by their score on the optimization
    // metric
    scoreModelIDDescList = [];
    for (i, modelInfo) in enumerate(_iterModels(modelIDs))
    {

      // Output model info to report csv
      reportWriter.emit(modelInfo);

      // Update job metrics
      totalRecords+=modelInfo.getNumRecords();
      format = "%Y-%m-%d %H:%M:%S";
      startTime = modelInfo.getStartTime();
      if( modelInfo.isFinished())
      {
        endTime = modelInfo.getEndTime();
        st = datetime.strptime(startTime, format);
        et = datetime.strptime(endTime, format);
        totalWallTime+=(et-st).seconds;
      }

      // Tabulate experiment dispositions
      modelStats.update(modelInfo);

      // For convenience
      expDesc = modelInfo.getModelDescription();
      reportMetrics = modelInfo.getReportMetrics();
      optimizationMetrics = modelInfo.getOptimizationMetrics();
      if( modelInfo.getModelID() == bestModelNum)
      {
        bestModel = modelInfo;
        bestModelIterIndex=i;
        bestMetric = optimizationMetrics.values()[0];
      }

      // Keep track of the best-performing model
      if( optimizationMetrics)
      {
        assert len(optimizationMetrics) == 1, (
            "expected 1 opt key, but got %d (%s) in %s" % (
                len(optimizationMetrics), optimizationMetrics, modelInfo));
      }

      // Append to our list of modelIDs and scores
      if( modelInfo.getCompletionReason().isEOF())
      {
        scoreModelIDDescList.append((optimizationMetrics.values()[0],
                                    modelInfo.getModelID(),
                                    modelInfo.getGeneratedDescriptionFile(),
                                    modelInfo.getParamLabels()));
      }

      print "[%d] Experiment %s\n(%s):" % (i, modelInfo, expDesc);
      if (modelInfo.isFinished() and
          not (modelInfo.getCompletionReason().isStopped or
               modelInfo.getCompletionReason().isEOF()))
      {
        print ">> COMPLETION MESSAGE: %s" % modelInfo.getCompletionMsg();
      }

      if( reportMetrics)
      {
        // Update our metrics key set and format string
        foundMetricsKeySet.update(reportMetrics.iterkeys());
        if( len(sortedMetricsKeys) != len(foundMetricsKeySet))
        {
          sortedMetricsKeys = sorted(foundMetricsKeySet);

          maxKeyLen = max([len(k) for k in sortedMetricsKeys]);
          formatStr = "  %%-%ds" % (maxKeyLen+2);
        }

        // Print metrics
        for( key in sortedMetricsKeys)
        {
          if( key in reportMetrics)
          {
            if( key == optimizationMetricKey)
            {
              m = "%r (*)" % reportMetrics[key];
            }
            else
            {
              m = "%r" % reportMetrics[key];
            }
            print formatStr % (key+":"), m;
          }
        }
        print;
      }
    }

    // Summarize results
    print "--------------------------------------------------------------";
    if( len(modelIDs) > 0)
    {
      print "%d experiments total (%s).\n" % (
          len(modelIDs),
          ("all completed successfully"
           if (modelStats.numCompletedKilled + modelStats.numCompletedEOF) ==
               len(modelIDs)
           else "WARNING: %d models have not completed or there were errors" % (
               len(modelIDs) - (
                   modelStats.numCompletedKilled + modelStats.numCompletedEOF +
                   modelStats.numCompletedStopped))));

      if( modelStats.numStatusOther > 0)
      {
        print "ERROR: models with unexpected status: %d" % (
            modelStats.numStatusOther);
      }

      print "WaitingToStart: %d" % modelStats.numStatusWaitingToStart;
      print "Running: %d" % modelStats.numStatusRunning;
      print "Completed: %d" % modelStats.numStatusCompleted;
      if( modelStats.numCompletedOther > 0)
      {
        print "    ERROR: models with unexpected completion reason: %d" % (
            modelStats.numCompletedOther);
      }
      print "    ran to EOF: %d" % modelStats.numCompletedEOF;
      print "    ran to stop signal: %d" % modelStats.numCompletedStopped;
      print "    were orphaned: %d" % modelStats.numCompletedOrphaned;
      print "    killed off: %d" % modelStats.numCompletedKilled;
      print "    failed: %d" % modelStats.numCompletedError;

      assert modelStats.numStatusOther == 0, "numStatusOther=%s" % (
          modelStats.numStatusOther);
      assert modelStats.numCompletedOther == 0, "numCompletedOther=%s" % (
          modelStats.numCompletedOther);
    }

    else
    {
      print "0 experiments total.";
    }

    // Print out the field contributions
    print;
    global gCurrentSearch;
    jobStatus = hyperSearchJob.getJobStatus(gCurrentSearch._workers);
    jobResults = jobStatus.getResults();
    if( "fieldContributions" in jobResults)
    {
      print "Field Contributions:";
      pprint.pprint(jobResults["fieldContributions"], indent=4);
    }
    else
    {
      print "Field contributions info not available";
    }

    // Did we have an optimize key?
    if( bestModel is not None)
    {
      maxKeyLen = max([len(k) for k in sortedMetricsKeys]);
      maxKeyLen = max(maxKeyLen, len(optimizationMetricKey));
      formatStr = "  %%-%ds" % (maxKeyLen+2);
      bestMetricValue = bestModel.getOptimizationMetrics().values()[0];
      optimizationMetricName = bestModel.getOptimizationMetrics().keys()[0];
      print;
      print "Best results on the optimization metric %s (maximize=%s):" % (
          optimizationMetricName, maximizeMetric);
      print "[%d] Experiment %s (%s):" % (
          bestModelIterIndex, bestModel, bestModel.getModelDescription());
      print formatStr % (optimizationMetricName+":"), bestMetricValue;
      print;
      print "Total number of Records processed: %d"  % totalRecords;
      print;
      print "Total wall time for all models: %d" % totalWallTime;

      hsJobParams = hyperSearchJob.getParams();
    }

    // Were we asked to write out the top N model description files?
    if( options["genTopNDescriptions"] > 0)
    {
      print "\nGenerating description files for top %d models..." % (
              options["genTopNDescriptions"]);
      scoreModelIDDescList.sort();
      scoreModelIDDescList = scoreModelIDDescList[
          0:options["genTopNDescriptions"]];

      i = -1;
      for (score, modelID, description, paramLabels) in scoreModelIDDescList
      {
        i += 1;
        outDir = os.path.join(options["permWorkDir"], "model_%d" % (i));
        print "Generating description file for model %s at %s" % \
          (modelID, outDir);
        if( not os.path.exists(outDir))
        {
          os.makedirs(outDir);
        }

        // Fix up the location to the base description file.
        // importBaseDescription() chooses the file relative to the calling file.
        // The calling file is in outDir.
        // The base description is in the user-specified "outDir"
        base_description_path = os.path.join(options["outDir"],
          "description.py");
        base_description_relpath = os.path.relpath(base_description_path,
          start=outDir);
        description = description.replace(
              "importBaseDescription('base.py', config)",
              "importBaseDescription('%s', config)" % base_description_relpath);
        fd = open(os.path.join(outDir, "description.py"), "wb");
        fd.write(description);
        fd.close();

        // Generate a csv file with the parameter settings in it
        fd = open(os.path.join(outDir, "params.csv"), "wb");
        writer = csv.writer(fd);
        colNames = paramLabels.keys();
        colNames.sort();
        writer.writerow(colNames);
        row = [paramLabels[x] for x in colNames];
        writer.writerow(row);
        fd.close();

        print "Generating model params file...";
        // Generate a model params file alongside the description.py
        mod = imp.load_source("description", os.path.join(outDir,
                                                          "description.py"));
        model_description = mod.descriptionInterface.getModelDescription();
        fd = open(os.path.join(outDir, "model_params.py"), "wb");
        fd.write("%s\nMODEL_PARAMS = %s" % (utils.getCopyrightHead(),
                                            pprint.pformat(model_description)));
        fd.close();
      }

      print;
    }

    reportWriter.finalize();
    return model_description;
  }



  @classmethod;
  def loadSavedHyperSearchJob(cls, permWorkDir, outputLabel)
  {
    """Instantiates a _HyperSearchJob instance from info saved in file

    Parameters:
    ----------------------------------------------------------------------
    permWorkDir: Directory path for saved jobID file
    outputLabel: Label string for incorporating into file name for saved jobID
    retval:      _HyperSearchJob instance; raises exception if not found
    """;
    jobID = cls.__loadHyperSearchJobID(permWorkDir=permWorkDir,
                                       outputLabel=outputLabel);

    searchJob = _HyperSearchJob(nupicJobID=jobID);
    return searchJob;
  }



  @classmethod;
  def __saveHyperSearchJobID(cls, permWorkDir, outputLabel, hyperSearchJob)
  {
    """Saves the given _HyperSearchJob instance's jobID to file

    Parameters:
    ----------------------------------------------------------------------
    permWorkDir:   Directory path for saved jobID file
    outputLabel:   Label string for incorporating into file name for saved jobID
    hyperSearchJob: _HyperSearchJob instance
    retval:        nothing
    """;
    jobID = hyperSearchJob.getJobID();
    filePath = cls.__getHyperSearchJobIDFilePath(permWorkDir=permWorkDir,
                                                 outputLabel=outputLabel);

    if( os.path.exists(filePath))
    {
      _backupFile(filePath);
    }

    d = dict(hyperSearchJobID = jobID);

    // with open(filePath, "wb") as jobIdPickleFile:
    pickle.dump(d, jobIdPickleFile);
  }



  @classmethod;
  def __loadHyperSearchJobID(cls, permWorkDir, outputLabel)
  {
    """Loads a saved jobID from file

    Parameters:
    ----------------------------------------------------------------------
    permWorkDir:  Directory path for saved jobID file
    outputLabel:  Label string for incorporating into file name for saved jobID
    retval:       HyperSearch jobID; raises exception if not found.
    """;
    filePath = cls.__getHyperSearchJobIDFilePath(permWorkDir=permWorkDir,
                                                 outputLabel=outputLabel);

    jobID = None;
    // with open(filePath, "r") as jobIdPickleFile:
    jobInfo = pickle.load(jobIdPickleFile);
    jobID = jobInfo["hyperSearchJobID"];

    return jobID;
  }



  @classmethod;
  def __getHyperSearchJobIDFilePath(cls, permWorkDir, outputLabel)
  {
    """Returns filepath where to store HyperSearch JobID

    Parameters:
    ----------------------------------------------------------------------
    permWorkDir: Directory path for saved jobID file
    outputLabel: Label string for incorporating into file name for saved jobID
    retval:      Filepath where to store HyperSearch JobID
    """;
    // Get the base path and figure out the path of the report file.
    basePath = permWorkDir;

    // Form the name of the output csv file that will contain all the results
    filename = "%s_HyperSearchJobID.pkl" % (outputLabel,);
    filepath = os.path.join(basePath, filename);

    return filepath;
  }
}



class _ModelStats(object)
{
  """ @private
  """;


  def __init__(self)
  {
    // Tallies of experiment dispositions
    this.numStatusWaitingToStart = long(0);
    this.numStatusRunning = long(0);
    this.numStatusCompleted = long(0);
    this.numStatusOther = long(0);
    #this.numCompletedSuccess = long(0)
    this.numCompletedKilled = long(0);
    this.numCompletedError = long(0);
    this.numCompletedStopped = long(0);
    this.numCompletedEOF = long(0);
    this.numCompletedOther = long(0);
    this.numCompletedOrphaned = long(0);
  }



  def update(self, modelInfo)
  {
    // Tabulate experiment dispositions
    if( modelInfo.isWaitingToStart())
    {
      this.numStatusWaitingToStart += 1;
    }
    else if( modelInfo.isRunning())
    {
      this.numStatusRunning += 1;
    }
    else if( modelInfo.isFinished())
    {
      this.numStatusCompleted += 1;

      reason = modelInfo.getCompletionReason();
    }
  }
}
//      if reason.isSuccess():
//        this.numCompletedSuccess += 1
{
      if( reason.isEOF())
      {
        this.numCompletedEOF += 1;
      }
      else if( reason.isKilled())
      {
        this.numCompletedKilled += 1;
      }
      else if( reason.isStopped())
      {
        this.numCompletedStopped += 1;
      }
      else if( reason.isError())
      {
        this.numCompletedError += 1;
      }
      else if( reason.isOrphaned())
      {
        this.numCompletedOrphaned += 1;
      }
      else
      {
        this.numCompletedOther += 1;
      }
}
//    else:
//      this.numStatusOther += 1



class _ReportCSVWriter(object)
{
  """ @private
  """;


  __totalModelTime = timedelta();


  def __init__(self,
               hyperSearchJob,
               metricsKeys,
               searchVar,
               outputDirAbsPath,
               outputLabel,
               replaceReport)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    hyperSearchJob: _HyperSearchJob instance
    metricsKeys:    sequence of report metrics key names to include in report
    outputDirAbsPath:
                    Directory for creating report CSV file (absolute path)
    outputLabel:    A string label to incorporate into report CSV file name
    replaceReport:  True to replace existing report csv, if any; False to
                    append to existing report csv, if any
    retval:         nothing
    """;
    this.__searchJob = hyperSearchJob;
    this.__searchJobID = hyperSearchJob.getJobID();
    this.__sortedMetricsKeys = sorted(metricsKeys);
    this.__outputDirAbsPath = os.path.abspath(outputDirAbsPath);
    this.__outputLabel = outputLabel;
    this.__replaceReport = replaceReport;
    this.__sortedVariableNames=searchVar;
    // These are set up by __openAndInitCSVFile
    this.__csvFileObj = None;
    this.__reportCSVPath = None;
    this.__backupCSVPath = None;
  }



  def emit(self, modelInfo)
  {
    """Emit model info to csv file

    Parameters:
    ----------------------------------------------------------------------
    modelInfo:      _NupicModelInfo instance
    retval:         nothing
    """;
    // Open/init csv file, if needed
    if( this.__csvFileObj is None)
    {
      // sets up this.__sortedVariableNames and this.__csvFileObj
      this.__openAndInitCSVFile(modelInfo);
    }

    csv = this.__csvFileObj;

    // Emit model info row to report.csv
    print >> csv, "%s, " % (this.__searchJobID),;
    print >> csv, "%s, " % (modelInfo.getModelID()),;
    print >> csv, "%s, " % (modelInfo.statusAsString()),;
    if( modelInfo.isFinished())
    {
      print >> csv, "%s, " % (modelInfo.getCompletionReason()),;
    }
    else
    {
      print >> csv, "NA, ",;
    }
    if( not modelInfo.isWaitingToStart())
    {
      print >> csv, "%s, " % (modelInfo.getStartTime()),;
    }
    else
    {
      print >> csv, "NA, ",;
    }
    if( modelInfo.isFinished())
    {
      dateFormat = "%Y-%m-%d %H:%M:%S";
      startTime = modelInfo.getStartTime();
      endTime = modelInfo.getEndTime();
      print >> csv, "%s, " % endTime,;
      st = datetime.strptime(startTime, dateFormat);
      et = datetime.strptime(endTime, dateFormat);
      print >> csv, "%s, " % (str((et - st).seconds)),;
    }
    else
    {
      print >> csv, "NA, ",;
      print >> csv, "NA, ",;
    }
    print >> csv, "%s, " % str(modelInfo.getModelDescription()),;
    print >> csv, "%s, " % str(modelInfo.getNumRecords()),;
    paramLabelsDict = modelInfo.getParamLabels();
    for( key in this.__sortedVariableNames)
    {
      // Some values are complex structures,.. which need to be represented as
      // strings
      if( key in paramLabelsDict)
      {
        print >> csv, "%s, " % (paramLabelsDict[key]),;
      }
      else
      {
        print >> csv, "None, ",;
      }
    }
    metrics = modelInfo.getReportMetrics();
    for( key in this.__sortedMetricsKeys)
    {
      value = metrics.get(key, "NA");
      value = str(value);
      value = value.replace("\n", " ");
      print >> csv, "%s, " % (value),;
    }

    print >> csv;
  }



  def finalize(self)
  {
    """Close file and print report/backup csv file paths

    Parameters:
    ----------------------------------------------------------------------
    retval:         nothing
    """;
    if( this.__csvFileObj is not None)
    {
      // Done with file
      this.__csvFileObj.close();
      this.__csvFileObj = None;

      print "Report csv saved in %s" % (this.__reportCSVPath,);

      if( this.__backupCSVPath)
      {
        print "Previous report csv file was backed up to %s" % \
                (this.__backupCSVPath,);
      }
    }
    else
    {
      print "Nothing was written to report csv file.";
    }
  }



  def __openAndInitCSVFile(self, modelInfo)
  {
    """
    - Backs up old report csv file;
    - opens the report csv file in append or overwrite mode (per
      this.__replaceReport);
    - emits column fields;
    - sets up this.__sortedVariableNames, this.__csvFileObj,
      this.__backupCSVPath, and this.__reportCSVPath

    Parameters:
    ----------------------------------------------------------------------
    modelInfo:      First _NupicModelInfo instance passed to emit()
    retval:         nothing
    """;
    // Get the base path and figure out the path of the report file.
    basePath = this.__outputDirAbsPath;

    // Form the name of the output csv file that will contain all the results
    reportCSVName = "%s_Report.csv" % (this.__outputLabel,);
    reportCSVPath = this.__reportCSVPath = os.path.join(basePath, reportCSVName);

    // If a report CSV file already exists, back it up
    backupCSVPath = None;
    if( os.path.exists(reportCSVPath))
    {
      backupCSVPath = this.__backupCSVPath = _backupFile(reportCSVPath);
    }


    // Open report file
    if( this.__replaceReport)
    {
      mode = "w";
    }
    else
    {
      mode = "a";
    }
    csv = this.__csvFileObj = open(reportCSVPath, mode);

    // If we are appending, add some blank line separators
    if( not this.__replaceReport and backupCSVPath)
    {
      print >> csv;
      print >> csv;
    }

    // Print the column names
    print >> csv, "jobID, ",;
    print >> csv, "modelID, ",;
    print >> csv, "status, " ,;
    print >> csv, "completionReason, ",;
    print >> csv, "startTime, ",;
    print >> csv, "endTime, ",;
    print >> csv, "runtime(s), " ,;
    print >> csv, "expDesc, ",;
    print >> csv, "numRecords, ",;

    for( key in this.__sortedVariableNames)
    {
      print >> csv, "%s, " % key,;
    }
    for( key in this.__sortedMetricsKeys)
    {
      print >> csv, "%s, " % key,;
    }
    print >> csv;
  }
}



class _NupicJob(object)
{
  """ @private
  Our Nupic Job abstraction""";


  def __init__(self, nupicJobID)
  {
    """_NupicJob constructor

    Parameters:
    ----------------------------------------------------------------------
    retval:         Nupic Client JobID of the job
    """;
    this.__nupicJobID = nupicJobID;

    jobInfo = _clientJobsDB().jobInfo(nupicJobID);
    assert jobInfo is not None, "jobID=%s not found" % nupicJobID;
    assert jobInfo.jobId == nupicJobID, "%s != %s" % (jobInfo.jobId, nupicJobID);
    _emit(Verbosity.DEBUG, "_NupicJob: \n%s" % pprint.pformat(jobInfo, indent=4));

    if( jobInfo.params is not None)
    {
      this.__params = json.loads(jobInfo.params);
    }
    else
    {
      this.__params = None;
    }
  }



  def __repr__(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         representation of this _NupicJob instance
    """;
    return "%s(jobID=%s)" % (this.__class__.__name__, this.__nupicJobID);
  }



  def getJobStatus(self, workers)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    workers:  If this job was launched outside of the nupic job engine, then this
               is an array of subprocess Popen instances, one for each worker
    retval:         _NupicJob.JobStatus instance

    """;
    jobInfo = this.JobStatus(this.__nupicJobID, workers);
    return jobInfo;
  }



  def getJobID(self)
  {
    """Semi-private method for retrieving the jobId

    Parameters:
    ----------------------------------------------------------------------
    retval:         Nupic Client JobID of this _NupicJob instance
    """;
    return this.__nupicJobID;
  }



  def getParams(self)
  {
    """Semi-private method for retrieving the job-specific params

    Parameters:
    ----------------------------------------------------------------------
    retval:         Job params dict corresponding to the JSON params value
                    returned by ClientJobsDAO.jobInfo()
    """;
    return this.__params;
  }



  class JobStatus(object)
  {
    """ @private
    Our Nupic Job Info abstraction class""";

    // Job Status values (per ClientJobsDAO.py):
    __nupicJobStatus_NotStarted  = cjdao.ClientJobsDAO.STATUS_NOTSTARTED;
    __nupicJobStatus_Starting    = cjdao.ClientJobsDAO.STATUS_STARTING;
    __nupicJobStatus_running     = cjdao.ClientJobsDAO.STATUS_RUNNING;
    __nupicJobStatus_completed   = cjdao.ClientJobsDAO.STATUS_COMPLETED;


    def __init__(self, nupicJobID, workers)
    {
      """_NupicJob.JobStatus Constructor

      Parameters:
      ----------------------------------------------------------------------
      nupicJobID:    Nupic ClientJob ID
      workers:  If this job was launched outside of the Nupic job engine, then this
               is an array of subprocess Popen instances, one for each worker
      retval:       nothing
      """;

      jobInfo = _clientJobsDB().jobInfo(nupicJobID);
      assert jobInfo.jobId == nupicJobID, "%s != %s" % (jobInfo.jobId, nupicJobID);

      // If we launched the workers ourself, set the job status based on the
      //  workers that are still running
      if( workers is not None)
      {
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
          status = cjdao.ClientJobsDAO.STATUS_RUNNING;
        }
        else
        {
          status = cjdao.ClientJobsDAO.STATUS_COMPLETED;
        }

        jobInfo = jobInfo._replace(status=status);
      }

      _emit(Verbosity.DEBUG, "JobStatus: \n%s" % pprint.pformat(jobInfo,
                                                                indent=4));

      this.__jobInfo = jobInfo;
    }



    def __repr__(self)
    {
      return "%s(jobId=%s, status=%s, completionReason=%s, " \
             "startTime=%s, endTime=%s)" % (
                this.__class__.__name__, this.__jobInfo.jobId,
                this.statusAsString(), this.__jobInfo.completionReason,
                this.__jobInfo.startTime, this.__jobInfo.endTime);
    }



    def statusAsString(self)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      retval:       Job status as a human-readable string
      """;
      return this.__jobInfo.status;
    }



    def isWaitingToStart(self)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      retval:       True if the job has not been started yet
      """;
      waiting = (this.__jobInfo.status == this.__nupicJobStatus_NotStarted);
      return waiting;
    }



    def isStarting(self)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      retval:         True if the job is starting
      """;
      starting = (this.__jobInfo.status == this.__nupicJobStatus_Starting);
      return starting;
    }



    def isRunning(self)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      retval:         True if the job is running
      """;
      running = (this.__jobInfo.status == this.__nupicJobStatus_running);
      return running;
    }



    def isFinished(self)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      retval:         True if the job has finished (either with success or
                      failure)
      """;
      done = (this.__jobInfo.status == this.__nupicJobStatus_completed);
      return done;
    }



    def getCompletionReason(self)
    {
      """Returns _JobCompletionReason.
      NOTE: it's an error to call this method if isFinished() would return
      False.

      Parameters:
      ----------------------------------------------------------------------
      retval:         _JobCompletionReason instance
      """;
      assert this.isFinished(), "Too early to tell: %s" % self;
      return _JobCompletionReason(this.__jobInfo.completionReason);
    }



    def getCompletionMsg(self)
    {
      """Returns job completion message.

      NOTE: it's an error to call this method if isFinished() would return
      False.

      Parameters:
      ----------------------------------------------------------------------
      retval:         completion message
      """;
      assert this.isFinished(), "Too early to tell: %s" % self;
      return "%s" % this.__jobInfo.completionMsg;
    }



    def getWorkerCompletionMsg(self)
    {
      """Returns the worker generated completion message.

      NOTE: it's an error to call this method if isFinished() would return
      False.

      Parameters:
      ----------------------------------------------------------------------
      retval:         completion message
      """;
      assert this.isFinished(), "Too early to tell: %s" % self;
      return "%s" % this.__jobInfo.workerCompletionMsg;
    }



    def getStartTime(self)
    {
      """Returns job start time.

      NOTE: it's an error to call this method if isWaitingToStart() would
      return True.

      Parameters:
      ----------------------------------------------------------------------
      retval:         job processing start time
      """;
      assert not this.isWaitingToStart(), "Too early to tell: %s" % self;
      return "%s" % this.__jobInfo.startTime;
    }



    def getEndTime(self)
    {
      """Returns job end time.

      NOTE: it's an error to call this method if isFinished() would return
      False.

      Parameters:
      ----------------------------------------------------------------------
      retval:         job processing end time
      """;
      assert this.isFinished(), "Too early to tell: %s" % self;
      return "%s" % this.__jobInfo.endTime;
    }



    def getWorkerState(self)
    {
      """Returns the worker state field.

      Parameters:
      ----------------------------------------------------------------------
      retval:         worker state field as a dict
      """;
      if( this.__jobInfo.engWorkerState is not None)
      {
        return json.loads(this.__jobInfo.engWorkerState);
      }
      else
      {
        return None;
      }
    }



    def getResults(self)
    {
      """Returns the results field.

      Parameters:
      ----------------------------------------------------------------------
      retval:         job results field as a dict
      """;
      if( this.__jobInfo.results is not None)
      {
        return json.loads(this.__jobInfo.results);
      }
      else
      {
        return None;
      }
    }



    def getModelMilestones(self)
    {
      """Returns the model milestones field.

      Parameters:
      ----------------------------------------------------------------------
      retval:        model milestones as a dict
      """;
      if( this.__jobInfo.engModelMilestones is not None)
      {
        return json.loads(this.__jobInfo.engModelMilestones);
      }
      else
      {
        return None;
      }
    }



    def getEngStatus(self)
    {
      """Returns the engine status field - used for progress messages

      Parameters:
      ----------------------------------------------------------------------
      retval:        engine status field as string
      """;
      return this.__jobInfo.engStatus;
    }
  }
}



class _JobCompletionReason(object)
{
  """ @private
  Represents completion reason for Client Jobs and Models""";


  def __init__(self, reason)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    reason:   completion reason value from ClientJobsDAO.jobInfo()
    """;
    this.__reason = reason;
  }



  def __str__(self)
  {
    return "%s" % this.__reason;
  }



  def __repr__(self)
  {
    return "%s(reason=%s)" % (this.__class__.__name__, this.__reason);
  }



  def isEOF(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_EOF;
  }



  def isSuccess(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_SUCCESS;
  }



  def isStopped(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_STOPPED;
  }



  def isKilled(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_KILLED;
  }



  def isOrphaned(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_ORPHAN;
  }



  def isError(self)
  {
    return this.__reason == cjdao.ClientJobsDAO.CMPL_REASON_ERROR;
  }
}



class _HyperSearchJob(_NupicJob)
{
  """ @private
  This class represents a single running Nupic HyperSearch job""";


  def __init__(self, nupicJobID)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    nupicJobID:      Nupic Client JobID of a HyperSearch job
    retval:         nothing
    """;
    super(_HyperSearchJob, self).__init__(nupicJobID);

    // Cache of the total count of expected models or -1 if it can't be
    // deteremined.
    #
    // Set by getExpectedNumModels()
    #
    // TODO: update code to handle non-ronomatic search algorithms
    this.__expectedNumModels = None;
  }



  def queryModelIDs(self)
  {
    """Queuries DB for model IDs of all currently instantiated models
    associated with this HyperSearch job.

    See also: _iterModels()

    Parameters:
    ----------------------------------------------------------------------
    retval:         A sequence of Nupic modelIDs
    """;
    jobID = this.getJobID();
    modelCounterPairs = _clientJobsDB().modelsGetUpdateCounters(jobID);
    modelIDs = tuple(x[0] for x in modelCounterPairs);

    return modelIDs;
  }



  def getExpectedNumModels(self, searchMethod)
  {
    """Returns:  the total number of expected models if known, -1 if it can't
    be determined.

    NOTE: this can take a LONG time to complete for HyperSearches with a huge
          number of possible permutations.

    Parameters:
    ----------------------------------------------------------------------
    searchMethod:   "v2" is the only method currently supported
    retval:         The total number of expected models, if known; -1 if unknown
    """;
    return this.__expectedNumModels;
  }
}



class _ClientJobUtils(object)
{
  """ @private
  Our Nupic Client Job utilities""";


  @classmethod;
  def makeSearchJobParamsDict(cls, options, forRunning=False)
  {
    """Constructs a dictionary of HyperSearch parameters suitable for converting
    to json and passing as the params argument to ClientJobsDAO.jobInsert()
    Parameters:
    ----------------------------------------------------------------------
    options:        NupicRunPermutations options dict
    forRunning:     True if the params are for running a Hypersearch job; False
                    if params are for introspection only.

    retval:         A dictionary of HyperSearch parameters for
                    ClientJobsDAO.jobInsert()
    """;
    if( options["searchMethod"] == "v2")
    {
      hsVersion = "v2";
    }
    else
    {
      raise Exception("Unsupported search method: %r" % options["searchMethod"]);
    }

    maxModels = options["maxPermutations"];
    if( options["action"] == "dryRun" and maxModels is None)
    {
      maxModels = 1;
    }

    useTerminators = options["useTerminators"];
    if( useTerminators is None)
    {
      params = {
              "hsVersion":          hsVersion,
              "maxModels":          maxModels,
             };
    }
    else
    {
      params = {
              "hsVersion":          hsVersion,
              "useTerminators":     useTerminators,
              "maxModels":          maxModels,
             };
    }

    if( forRunning)
    {
      params["persistentJobGUID"] = str(uuid.uuid1());
    }

    if( options["permutationsScriptPath"])
    {
      params["permutationsPyFilename"] = options["permutationsScriptPath"];
    }
    else if( options["expDescConfig"])
    {
      params["description"] = options["expDescConfig"];
    }
    else
    {
      // with open(options["expDescJsonPath"], mode="r") as fp:
      params["description"] = json.load(fp);
    }

    return params;
  }
}



class _PermutationUtils(object)
{
  """ @private
  Utilities for running permutations""";


  @classmethod;
  def getOptimizationMetricInfo(cls, searchJobParams)
  {
    """Retrives the optimization key name and optimization function.

    Parameters:
    ---------------------------------------------------------
    searchJobParams:
                    Parameter for passing as the searchParams arg to
                    Hypersearch constructor.
    retval:       (optimizationMetricKey, maximize)
                  optimizationMetricKey: which report key to optimize for
                  maximize: True if we should try and maximize the optimizeKey
                    metric. False if we should minimize it.
    """;
    if( searchJobParams["hsVersion"] == "v2")
    {
      search = HypersearchV2(searchParams=searchJobParams);
    }
    else
    {
      raise RuntimeError("Unsupported hypersearch version \"%s\"" % \
                         (searchJobParams["hsVersion"]));
    }

    info = search.getOptimizationMetricInfo();
    return info;
  }
}



def _backupFile(filePath)
{
  """Back up a file

  Parameters:
  ----------------------------------------------------------------------
  retval:         Filepath of the back-up
  """;
  assert os.path.exists(filePath);

  stampNum = 0;
  (prefix, suffix) = os.path.splitext(filePath);
  while( True)
  {
    backupPath = "%s.%d%s" % (prefix, stampNum, suffix);
    stampNum += 1;
    if( not os.path.exists(backupPath))
    {
      break;
    }
  }
  shutil.copyfile(filePath, backupPath);

  return backupPath;
}



def _getOneModelInfo(nupicModelID)
{
  """A convenience function that retrieves inforamtion about a single model

  See also: _iterModels()

  Parameters:
  ----------------------------------------------------------------------
  nupicModelID:      Nupic modelID
  retval:           _NupicModelInfo instance for the given nupicModelID.
  """;
  return _iterModels([nupicModelID]).next();
}



def _iterModels(modelIDs)
{
  """Creates an iterator that returns ModelInfo elements for the given modelIDs

  WARNING:      The order of ModelInfo elements returned by the iterator
                may not match the order of the given modelIDs

  Parameters:
  ----------------------------------------------------------------------
  modelIDs:       A sequence of model identifiers (e.g., as returned by
                  _HyperSearchJob.queryModelIDs()).
  retval:         Iterator that returns ModelInfo elements for the given
                  modelIDs (NOTE:possibly in a different order)
  """;

  class ModelInfoIterator(object)
  {
    """ModelInfo iterator implementation class
    """;

    // Maximum number of ModelInfo elements to load into cache whenever
    // cache empties
    __CACHE_LIMIT = 1000;

    debug=False;


    def __init__(self, modelIDs)
    {
      """
      Parameters:
      ----------------------------------------------------------------------
      modelIDs:     a sequence of Nupic model identifiers for which this
                    iterator will return _NupicModelInfo instances.
                    NOTE: The returned instances are NOT guaranteed to be in
                    the same order as the IDs in modelIDs sequence.
      retval:       nothing
      """;
      // Make our own copy in case caller changes model id list during iteration
      this.__modelIDs = tuple(modelIDs);

      if( this.debug)
      {
        _emit(Verbosity.DEBUG,
              "MODELITERATOR: __init__; numModelIDs=%s" % len(this.__modelIDs));
      }

      this.__nextIndex = 0;
      this.__modelCache = collections.deque();
      return;
    }


    def __iter__(self)
    {
      """Iterator Protocol function

      Parameters:
      ----------------------------------------------------------------------
      retval:         self
      """;
      return self;
    }



    def next(self)
    {
      """Iterator Protocol function

      Parameters:
      ----------------------------------------------------------------------
      retval:       A _NupicModelInfo instance or raises StopIteration to
                    signal end of iteration.
      """;
      return this.__getNext();
    }



    def __getNext(self)
    {
      """Implementation of the next() Iterator Protocol function.

      When the modelInfo cache becomes empty, queries Nupic and fills the cache
      with the next set of NupicModelInfo instances.

      Parameters:
      ----------------------------------------------------------------------
      retval:       A _NupicModelInfo instance or raises StopIteration to
                    signal end of iteration.
      """;

      if( this.debug)
      {
        _emit(Verbosity.DEBUG,
              "MODELITERATOR: __getNext(); modelCacheLen=%s" % (
                  len(this.__modelCache)));
      }

      if( not this.__modelCache)
      {
        this.__fillCache();
      }

      if( not this.__modelCache)
      {
        raise StopIteration();
      }

      return this.__modelCache.popleft();
    }



    def __fillCache(self)
    {
      """Queries Nupic and fills an empty modelInfo cache with the next set of
      _NupicModelInfo instances

      Parameters:
      ----------------------------------------------------------------------
      retval:       nothing
      """;
      assert (not this.__modelCache);

      // Assemble a list of model IDs to look up
      numModelIDs = len(this.__modelIDs) if this.__modelIDs else 0;

      if( this.__nextIndex >= numModelIDs)
      {
        return;
      }

      idRange = this.__nextIndex + this.__CACHE_LIMIT;
      if( idRange > numModelIDs)
      {
        idRange = numModelIDs;
      }

      lookupIDs = this.__modelIDs[this.__nextIndex:idRange];

      this.__nextIndex += (idRange - this.__nextIndex);

      // Query Nupic for model info of all models in the look-up list
      // NOTE: the order of results may not be the same as lookupIDs
      infoList = _clientJobsDB().modelsInfo(lookupIDs);
      assert len(infoList) == len(lookupIDs), \
            "modelsInfo returned %s elements; expected %s." % \
            (len(infoList), len(lookupIDs));

      // Create _NupicModelInfo instances and add them to cache
      for( rawInfo in infoList)
      {
        modelInfo = _NupicModelInfo(rawInfo=rawInfo);
        this.__modelCache.append(modelInfo);
      }

      assert len(this.__modelCache) == len(lookupIDs), \
             "Added %s elements to modelCache; expected %s." % \
             (len(this.__modelCache), len(lookupIDs));

      if( this.debug)
      {
        _emit(Verbosity.DEBUG,
              "MODELITERATOR: Leaving __fillCache(); modelCacheLen=%s" % \
                (len(this.__modelCache),));
      }
    }
  }


  return ModelInfoIterator(modelIDs);
}



class _NupicModelInfo(object)
{
  """ @private
  This class represents information obtained from ClientJobManager about a
  model
  """;


  __nupicModelStatus_notStarted  = cjdao.ClientJobsDAO.STATUS_NOTSTARTED;
  __nupicModelStatus_running     = cjdao.ClientJobsDAO.STATUS_RUNNING;
  __nupicModelStatus_completed   = cjdao.ClientJobsDAO.STATUS_COMPLETED;
  __rawInfo = None;



  def __init__(self, rawInfo)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    rawInfo:        A single model information element as returned by
                    ClientJobsDAO.modelsInfo()
    retval:         nothing.
    """;
    // Namedtuple returned by ClientJobsDAO.modelsInfo()
    this.__rawInfo = rawInfo;

    // Cached model metrics (see __unwrapResults())
    this.__cachedResults = None;

    assert this.__rawInfo.params is not None;
    // Cached model params (see __unwrapParams())
    this.__cachedParams = None;
  }



  def __repr__(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         Representation of this _NupicModelInfo instance.
    """;
    return ("%s(jobID=%s, modelID=%s, status=%s, completionReason=%s, "
            "updateCounter=%s, numRecords=%s)" % (
                "_NupicModelInfo",
                this.__rawInfo.jobId,
                this.__rawInfo.modelId,
                this.__rawInfo.status,
                this.__rawInfo.completionReason,
                this.__rawInfo.updateCounter,
                this.__rawInfo.numRecords));
  }



  def getModelID(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         Nupic modelID associated with this model info.
    """;
    return this.__rawInfo.modelId;
  }



  def statusAsString(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:    Human-readable string representation of the model's status.
    """;
    return "%s" % this.__rawInfo.status;
  }



  def getModelDescription(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         Printable description of the model.
    """;
    params = this.__unwrapParams();

    if( "experimentName" in params)
    {
      return params["experimentName"];
    }

    else
    {
      paramSettings = this.getParamLabels();
      // Form a csv friendly string representation of this model
      items = [];
      for( key, value in paramSettings.items())
      {
        items.append("%s_%s" % (key, value));
      }
      return ".".join(items);
    }
  }



  def getGeneratedDescriptionFile(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         Contents of the sub-experiment description file for
                        this model
    """;
    return this.__rawInfo.genDescription;
  }



  def getNumRecords(self)
  {
    """
    Paramets:
    ----------------------------------------------------------------------
    retval:         The number of records processed by the model.
    """;
    return this.__rawInfo.numRecords;
  }



  def getParamLabels(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         a dictionary of model parameter labels. For each entry
                    the key is the name of the parameter and the value
                    is the value chosen for it.
    """;
    params = this.__unwrapParams();

    // Hypersearch v2 stores the flattened parameter settings in "particleState"
    if( "particleState" in params)
    {
      retval = dict();
      queue = [(pair, retval) for pair in
               params["particleState"]["varStates"].iteritems()];
      while( len(queue) > 0)
      {
        pair, output = queue.pop();
        k, v = pair;
        if ("position" in v and "bestPosition" in v and
            "velocity" in v)
        {
          output[k] = v["position"];
        }
        else
        {
          if( k not in output)
          {
            output[k] = dict();
          }
          queue.extend((pair, output[k]) for pair in v.iteritems());
        }
      }
      return retval;
    }
  }



  def __unwrapParams(self)
  {
    """Unwraps this.__rawInfo.params into the equivalent python dictionary
    and caches it in this.__cachedParams. Returns the unwrapped params

    Parameters:
    ----------------------------------------------------------------------
    retval:         Model params dictionary as correpsonding to the json
                    as returned in ClientJobsDAO.modelsInfo()[x].params
    """;
    if( this.__cachedParams is None)
    {
      this.__cachedParams = json.loads(this.__rawInfo.params);
      assert this.__cachedParams is not None, \
             "%s resulted in None" % this.__rawInfo.params;
    }

    return this.__cachedParams;
  }



  def getReportMetrics(self)
  {
    """Retrives a dictionary of metrics designated for report
    Parameters:
    ----------------------------------------------------------------------
    retval: a dictionary of metrics that were collected for the model or
            an empty dictionary if there aren't any.
    """;
    return this.__unwrapResults().reportMetrics;
  }



  def getOptimizationMetrics(self)
  {
    """Retrives a dictionary of metrics designagted for optimization
    Parameters:
    ----------------------------------------------------------------------
    retval:         a dictionary of optimization metrics that were collected
                    for the model or an empty dictionary if there aren't any.
    """;
    return this.__unwrapResults().optimizationMetrics;
  }



  def getAllMetrics(self)
  {
    """Retrives a dictionary of metrics that combines all report and
    optimization metrics

    Parameters:
    ----------------------------------------------------------------------
    retval:         a dictionary of optimization metrics that were collected
                    for the model; an empty dictionary if there aren't any.
    """;
    result = this.getReportMetrics();
    result.update(this.getOptimizationMetrics());
    return result;
  }


  ModelResults = collections.namedtuple("ModelResultsTuple",
                                        ["reportMetrics",
                                         "optimizationMetrics"]);
  """Each element is a dictionary: property name is the metric name and
  property value is the metric value as generated by the model
  """;



  def __unwrapResults(self)
  {
    """Unwraps this.__rawInfo.results and caches it in this.__cachedResults;
    Returns the unwrapped params

    Parameters:
    ----------------------------------------------------------------------
    retval:         ModelResults namedtuple instance
    """;
    if( this.__cachedResults is None)
    {
      if( this.__rawInfo.results is not None)
      {
        resultList = json.loads(this.__rawInfo.results);
        assert len(resultList) == 2, \
               "Expected 2 elements, but got %s (%s)." % (
                len(resultList), resultList);
        this.__cachedResults = this.ModelResults(
          reportMetrics=resultList[0],
          optimizationMetrics=resultList[1]);
      }
      else
      {
        this.__cachedResults = this.ModelResults(
          reportMetrics={},
          optimizationMetrics={});
      }
    }


    return this.__cachedResults;
  }



  def isWaitingToStart(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:       True if the job has not been started yet
    """;
    waiting = (this.__rawInfo.status == this.__nupicModelStatus_notStarted);
    return waiting;
  }



  def isRunning(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:       True if the job has not been started yet
    """;
    running = (this.__rawInfo.status == this.__nupicModelStatus_running);
    return running;
  }



  def isFinished(self)
  {
    """
    Parameters:
    ----------------------------------------------------------------------
    retval:         True if the model's processing has completed (either with
                    success or failure).
    """;
    finished = (this.__rawInfo.status == this.__nupicModelStatus_completed);
    return finished;
  }



  def getCompletionReason(self)
  {
    """Returns _ModelCompletionReason.

    NOTE: it's an error to call this method if isFinished() would return False.

    Parameters:
    ----------------------------------------------------------------------
    retval:         _ModelCompletionReason instance
    """;
    assert this.isFinished(), "Too early to tell: %s" % self;
    return _ModelCompletionReason(this.__rawInfo.completionReason);
  }



  def getCompletionMsg(self)
  {
    """Returns model completion message.

    NOTE: it's an error to call this method if isFinished() would return False.

    Parameters:
    ----------------------------------------------------------------------
    retval:         completion message
    """;
    assert this.isFinished(), "Too early to tell: %s" % self;
    return this.__rawInfo.completionMsg;
  }



  def getStartTime(self)
  {
    """Returns model evaluation start time.

    NOTE: it's an error to call this method if isWaitingToStart() would
    return True.

    Parameters:
    ----------------------------------------------------------------------
    retval:         model evaluation start time
    """;
    assert not this.isWaitingToStart(), "Too early to tell: %s" % self;
    return "%s" % this.__rawInfo.startTime;
  }



  def getEndTime(self)
  {
    """Returns mode evaluation end time.

    NOTE: it's an error to call this method if isFinished() would return False.

    Parameters:
    ----------------------------------------------------------------------
    retval:         model evaluation end time
    """;
    assert this.isFinished(), "Too early to tell: %s" % self;
    return "%s" % this.__rawInfo.endTime;
  }
}



class _ModelCompletionReason(_JobCompletionReason)
{
  """ @private
  """;
  pass;
}

    */
    /*

    from nupic.swarming import object_json as json;
    import nupic.database.ClientJobsDAO as cjdao;
    from nupic.swarming import HypersearchWorker;
    from nupic.swarming.hypersearch import utils;
    from nupic.swarming.HypersearchV2 import HypersearchV2;
    from nupic.swarming.exp_generator.ExpGenerator import expGenerator;


    g_currentVerbosityLevel = 0;
    gCurrentSearch = None;
    DEFAULT_OPTIONS = {"overwrite": False,
                      "expDescJsonPath": None,
                      "expDescConfig": None,
                      "permutationsScriptPath": None,
                      "outputLabel": "swarm_out",
                      "outDir": None,
                      "permWorkDir": None,
                      "action": "run",
                      "searchMethod": "v2",
                      "timeout": None,
                      "exports": None,
                      "useTerminators": False,
                      "maxWorkers": 2,
                      "replaceReport": False,
                      "maxPermutations": None,
                      "genTopNDescriptions": 1};



    class Verbosity(object)
    {
      """ @private
      """;
      WARNING = 0;
      INFO = 1;
      DEBUG = 2;
    }

    def _termHandler(signal, frame)
    {
      try
      {
        jobrunner = gCurrentSearch;
        jobID = jobrunner._HyperSearchRunner__searchJob.getJobID();
      }
      catch( Exception as exc)
      {
        print exc;
      }
      else
      {
        print "Canceling jobs due to receiving SIGTERM";
        cjdao.ClientJobsDAO.get().jobCancel(jobID);
      }
    }
    
def _setupInterruptHandling()
{
  signal.signal(signal.SIGTERM, _termHandler);
  signal.signal(signal.SIGINT, _termHandler);
}



def _verbosityEnabled(verbosityLevel)
{
  return verbosityLevel <= g_currentVerbosityLevel;
}



def _emit(verbosityLevel, info)
{
  if( _verbosityEnabled(verbosityLevel))
  {
    print info;
  }
}



def _escape(s)
{
  """Escape commas, tabs, newlines and dashes in a string

  Commas are encoded as tabs
  """;
  assert isinstance(s, str), \
        "expected %s but got %s; value=%s" % (type(str), type(s), s);
  s = s.replace("\\", "\\\\");
  s = s.replace("\n", "\\n");
  s = s.replace("\t", "\\t");
  s = s.replace(",", "\t");
  return s;
}

def _engineServicesRunning()
{
  """ Return true if the engine services are running
  """;
  process = subprocess.Popen(["ps", "aux"], stdout=subprocess.PIPE);

  stdout = process.communicate()[0];
  result = process.returncode;
  if( result != 0)
  {
    raise RuntimeError("Unable to check for running client job manager");
  }

  // See if the CJM is running
  running = False;
  for( line in stdout.split("\n"))
  {
    if( "python" in line and "clientjobmanager.client_job_manager" in line)
    {
      running = True;
      break;
    }
  }

  return running;
}

    
def _runHyperSearch(runOptions)
{
  global gCurrentSearch;
  // Run HyperSearch
  startTime = time.time();
  search = _HyperSearchRunner(runOptions);
  // Save in global for the signal handler.
  gCurrentSearch = search;
  if( runOptions["action"] in ("run", "dryRun"))
  {
    search.runNewSearch();
  }
  else
  {
    search.pickupSearch();
  }

  // Generate reports
  // Print results and generate report csv file
  modelParams = _HyperSearchRunner.generateReport(
    options=runOptions,
    replaceReport=runOptions["replaceReport"],
    hyperSearchJob=search.peekSearchJob(),
    metricsKeys=search.getDiscoveredMetricsKeys());
  secs = time.time() - startTime;
  hours = int(secs) / (60 * 60);
  secs -= hours * (60 * 60);
  minutes = int(secs) / 60;
  secs -= minutes * 60;
  print "Elapsed time (h:mm:ss): %d:%02d:%02d" % (hours, minutes, int(secs));
  jobID = search.peekSearchJob().getJobID();
  print "Hypersearch ClientJobs job ID: ", jobID;

  return modelParams;
}

    */
}
