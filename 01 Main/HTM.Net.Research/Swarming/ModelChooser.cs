using System;
using System.Collections.Generic;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;

namespace HTM.Net.Research.Swarming
{
    // https://github.com/numenta/nupic/blob/master/src/nupic/swarming/
    /// <summary>
    /// Utility Class to help with the selection of the 'best' model
    /// during hypersearch for a particular job.
    /// The main interface method is updateResultsForJob(), which is to
    /// be called periodically from the hypersearch worker.
    /// When called, the model chooser first tries to update the
    /// 
    /// _eng_last_selection_sweep_time field in the jobs table.If it
    /// is successful, it then tries to find the model with the maximum   
    /// metric.
    /// Note : Altough that there are many model choosers for a
    /// given job, only 1 will update the results because only one
    /// chooser will be  able to update the _eng_last_selection_sweep_time
    /// within a given interval
    /// </summary>
    public class ModelChooser
    {
        const int _MIN_UPDATE_THRESHOLD = 100;
        const int _MIN_UPDATE_INTERVAL = 5;

        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(ModelChooser));
        private uint _jobID;
        private DateTime _lastUpdateAttemptTime;
        private BaseClientJobDao _cjDB;

        public ModelChooser(uint jobId, BaseClientJobDao jobsDao)
        {
            _jobID = jobId;
            _cjDB = jobsDao;
            _lastUpdateAttemptTime = DateTime.MinValue;
            _jobID = jobId;
            LOGGER.Info("Created new ModelChooser for job " + jobId);
        }

        public void updateResultsForJob(bool forceUpdate = true)
        {
            // Chooses the best model for a given job.
            //Parameters
            //---------------------------------------------------------------------- -
            //forceUpdate:  (True / False).If True, the update will ignore all the
            //restrictions on the minimum time to update and the minimum
            //number of records to update.This should typically only be
            //set to true if the model has completed running
            var updateInterval = (DateTime.Now - _lastUpdateAttemptTime).TotalSeconds;
            if (updateInterval < _MIN_UPDATE_INTERVAL && !forceUpdate)
                return;

            LOGGER.Info(string.Format("Attempting model selection for jobID={0}: time={1}lastUpdate={2}", _jobID,
                DateTime.Now, _lastUpdateAttemptTime));

            bool timestampUpdated = _cjDB.jobUpdateSelectionSweep(_jobID, _MIN_UPDATE_INTERVAL);
            if (!timestampUpdated)
                LOGGER.Info(string.Format("Unable to update selection sweep timestamp: jobID={0} updateTime={1}",
                    _jobID, _lastUpdateAttemptTime));
            if (!forceUpdate)
                return;

            _lastUpdateAttemptTime = DateTime.Now;
            LOGGER.Info(string.Format("Succesfully updated selection sweep timestamp jobid={0} updateTime={1}",
                _jobID, _lastUpdateAttemptTime));

            int minUpdateRecords = _MIN_UPDATE_THRESHOLD;

            object jobResults = _getJobResults();
            if (forceUpdate || jobResults == null)
                minUpdateRecords = 0;

            //candidateIDs, bestMetric = this._cjDB.modelsGetCandidates(this._jobID, minUpdateRecords);
            Util.Tuple tuple = this._cjDB.modelsGetCandidates(this._jobID, minUpdateRecords);
            List<int> candidateIDs = (List<int>) tuple.Item1;
            double bestMetric = (double) tuple.Item2;

            LOGGER.Info(string.Format("Candidate models={0}, metric={1}, jobID={2}", Arrays.ToString(candidateIDs), bestMetric, _jobID));

            if (candidateIDs.Count == 0)
                return;

            _jobUpdateCandidate(candidateIDs[0], bestMetric, resultsObj: jobResults);
        }

        private void _jobUpdateCandidate(int candidateID, double metricValue, object resultsObj)
        {
            bool nullResults = (resultsObj == null);
            NamedTuple results;
            if (nullResults)
            {
                results = new NamedTuple(new [] { "bestModel", "bestValue" }, null, null);
            }
            else
            {
                results = JsonConvert.DeserializeObject<NamedTuple>(resultsObj as string);
                LOGGER.Debug(string.Format("Updating old results {0}", results));
            }
            int oldCandidateID = (int) results["bestModel"];
            double oldMetricValue = (double) results["bestValue"];

            results["bestModel"] = candidateID;
            results["bestValue"] = metricValue;
            bool isUpdated = candidateID == oldCandidateID;

            if (isUpdated)
            {
                LOGGER.Info(string.Format("Choosing new model. Old candidate: (id={0}, value={1}) New candidate: (id={2}, value={3})",
                oldCandidateID, oldMetricValue, candidateID, metricValue));
            }
            else
            {
                LOGGER.Info(string.Format("Same model as before. id={0}, metric={1}", candidateID,
                    metricValue));
            }

            LOGGER.Debug(string.Format("New Results {0}", results));
            _cjDB.jobUpdateResults(_jobID, JsonConvert.SerializeObject(results));
        }

        private object _getJobResults()
        {
            List<object> queryResults = _cjDB.jobGetFields(_jobID, new[] { "results" });
            if (queryResults.Count == 0)
                throw new InvalidOperationException("Trying to update results for non-existent job");

            object results = queryResults[0];
            return results;
        }
    }

    /*
    class ModelChooser(object)
{
  """Utility Class to help with the selection of the 'best' model
  during hypersearch for a particular job.
  The main interface method is updateResultsForJob(), which is to
  be called periodically from the hypersearch worker.
  When called, the model chooser first tries to update the
  _eng_last_selection_sweep_time field in the jobs table. If it
  is successful, it then tries to find the model with the maximum
  metric.
  Note : Altough that there are many model choosers for a
  given job, only 1 will update the results because only one
  chooser will be  able to update the _eng_last_selection_sweep_time
  within a given interval
  """;

  _MIN_UPDATE_THRESHOLD = 100;
  _MIN_UPDATE_INTERVAL = 5;


  def __init__(self,  jobID, jobsDAO, logLevel = None)
  {
    """TODO: Documentation """;

    this._jobID = jobID;
    this._cjDB = jobsDAO;
    this._lastUpdateAttemptTime = 0;
    initLogging(verbose = True);
    this.logger = logging.getLogger(".".join( ['com.numenta',
                       this.__class__.__module__, this.__class__.__name__]));
    if( logLevel is not None)
    {
      this.logger.setLevel(logLevel);
    }

    this.logger.info("Created new ModelChooser for job %s" % str(jobID));
  }


  def updateResultsForJob(self, forceUpdate=True)
  {
    """ Chooses the best model for a given job.
    Parameters
    -----------------------------------------------------------------------
    forceUpdate:  (True/False). If True, the update will ignore all the
                  restrictions on the minimum time to update and the minimum
                  number of records to update. This should typically only be
                  set to true if the model has completed running
    """;
    updateInterval = time.time() - this._lastUpdateAttemptTime;
    if( updateInterval < this._MIN_UPDATE_INTERVAL and not forceUpdate)
    {
      return;
    }

    this.logger.info("Attempting model selection for jobID=%d: time=%f"\
                     "  lastUpdate=%f"%(this._jobID,
                                        time.time(),
                                        this._lastUpdateAttemptTime));

    timestampUpdated = this._cjDB.jobUpdateSelectionSweep(this._jobID,
                                                          this._MIN_UPDATE_INTERVAL);
    if( not timestampUpdated)
    {
      this.logger.info("Unable to update selection sweep timestamp: jobID=%d" \
                       " updateTime=%f"%(this._jobID, this._lastUpdateAttemptTime));
      if( not forceUpdate)
      {
        return;
      }
    }

    this._lastUpdateAttemptTime = time.time();
    this.logger.info("Succesfully updated selection sweep timestamp jobid=%d updateTime=%f"\
                     %(this._jobID, this._lastUpdateAttemptTime));

    minUpdateRecords = this._MIN_UPDATE_THRESHOLD;

    jobResults = this._getJobResults();
    if( forceUpdate or jobResults is None)
    {
      minUpdateRecords = 0;
    }

    candidateIDs, bestMetric = this._cjDB.modelsGetCandidates(this._jobID, minUpdateRecords);

    this.logger.info("Candidate models=%s, metric=%s, jobID=%s"\
                     %(candidateIDs, bestMetric, this._jobID));

    if( len(candidateIDs) == 0)
    {
      return;
    }

    this._jobUpdateCandidate(candidateIDs[0], bestMetric, results=jobResults);
  }


  def _jobUpdateCandidate(self, candidateID, metricValue, results)
  {

    nullResults = results is None;
    if( nullResults)
    {
      results = {'bestModel':None, 'bestValue':None};
    }
    else
    {
      results = json.loads(results);
      this.logger.debug("Updating old results %s"%(results));
    }

    oldCandidateID = results['bestModel'];
    oldMetricValue = results['bestValue'];

    results['bestModel'] = candidateID;
    results['bestValue'] = metricValue;
    isUpdated = candidateID == oldCandidateID;

    if( isUpdated)
    {
      this.logger.info("Choosing new model. Old candidate: (id=%s, value=%s)"\
                   " New candidate: (id=%s, value=%f)"%\
                   (oldCandidateID, oldMetricValue, candidateID, metricValue));
    }

    else
    {
      this.logger.info("Same model as before. id=%s, "\
                       "metric=%f"%(candidateID, metricValue));
    }


    this.logger.debug("New Results %s"%(results));
    this._cjDB.jobUpdateResults(this._jobID, json.dumps(results));
  }


  def _getJobResults(self)
  {
    queryResults = this._cjDB.jobGetFields(this._jobID, ['results']);
    if(  len(queryResults) == 0)
    {
      raise RuntimeError("Trying to update results for non-existent job");
    }

    results = queryResults[0];
    return results;
  }
}



        */

}