using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Data;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;

namespace HTM.Net.Research.Swarming.HyperSearch
{
    /// <summary>
    ///   This class encapsulates the Hypersearch state which we share with all
    ///   other workers. This state gets serialized into a JSON dict and written to
    ///   the engWorkerState field of the job record.
    /// 
    ///   Whenever a worker changes this state, it does an atomic setFieldIfEqual to
    ///   insure it has the latest state as updated by any other worker as a base.
    /// 
    ///   Here is an example snapshot of this state information:
    ///   swarms = {'a': {'status': 'completed',        // 'active','completing','completed',
    ///                                                // or 'killed'
    ///                    'bestModelId': <modelID>,   // Only set for 'completed' swarms
    ///                    'bestErrScore': <errScore>, // Only set for 'completed' swarms
    ///                    'sprintIdx': 0,
    ///                    },
    ///            'a.b': {'status': 'active',
    ///                    'bestModelId': None,
    ///                    'bestErrScore': None,
    ///                    'sprintIdx': 1,
    ///                   }
    ///            }
    /// 
    ///   sprints = [{'status': 'completed',      // 'active','completing','completed'
    ///               'bestModelId': <modelID>,   // Only set for 'completed' sprints
    ///               'bestErrScore': <errScore>, // Only set for 'completed' sprints
    ///              },
    ///              {'status': 'completing',
    ///               'bestModelId': <None>,
    ///               'bestErrScore': <None>
    ///              }
    ///              {'status': 'active',
    ///               'bestModelId': None
    ///               'bestErrScore': None
    ///              }
    ///              ]
    /// </summary>
    public class HsState
    {
        internal readonly ILog logger;
        private HypersearchV2 _hsObj;
        private bool _dirty;
        internal HsStateModel _state;
        private string _priorStateJSON;
        private ulong? _modelId;

        /// <summary>
        /// Create our state object.
        /// </summary>
        /// <param name="hsObj">Reference to the HypersesarchV2 instance</param>
        public HsState(HypersearchV2 hsObj)
        {
            // Save constructor parameters
            this._hsObj = hsObj;

            // Convenient access to the logger
            this.logger = this._hsObj.logger;

            // This contains our current state, and local working changes
            this._state = null;

            // This contains the state we last read from the database
            this._priorStateJSON = null;

            // Set when we make a change to our state locally
            this._dirty = false;

            // Read in the initial state
            this.readStateFromDB();

        }

        /// <summary>
        /// Return true if our local copy of the state has changed since the last time we read from the DB.
        /// </summary>
        /// <returns></returns>
        public bool isDirty()
        {
            return this._dirty;
        }

        /// <summary>
        /// Return true if the search should be considered over.
        /// </summary>
        /// <returns></returns>
        public bool isSearchOver()
        {
            return this._state.searchOver;
        }

        /// <summary>
        /// Set our state to that obtained from the engWorkerState field of the job record.
        /// </summary>
        public void readStateFromDB()
        {
            this._priorStateJSON = (string)this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID, new[] { "engWorkerState" })[0];

            // Init if no prior state yet
            if (this._priorStateJSON == null)
            {
                Dictionary<string, SwarmEncoderState> swarms = new Dictionary<string, SwarmEncoderState>();

                // Fast Swarm, first and only sprint has one swarm for each field
                // in fixedFields
                if (this._hsObj._fixedFields != null)
                {
                    Console.WriteLine(this._hsObj._fixedFields);
                    List<string> encoderSet = new List<string>();
                    foreach (string field in this._hsObj._fixedFields)
                    {
                        if (field == "_classifierInput")
                        {
                            continue;
                        }
                        string encoderName = this.getEncoderKeyFromName(field);

                        Debug.Assert(this._hsObj._encoderNames.Contains(encoderName),
                            string.Format(
                                "The field {0} specified in the fixedFields list is not present in this model.", field));
                        //      assert encoderName in this._hsObj._encoderNames, "The field '%s' " \
                        //" specified in the fixedFields list is not present in this " \
                        //" model." % (field);
                        encoderSet.Add(encoderName);
                    }
                    encoderSet.Sort();
                    swarms[string.Join(".", encoderSet)] = new SwarmEncoderState
                    {
                        status = SwarmStatus.active,
                        bestModelId = null,
                        bestErrScore = null,
                        sprintIdx = 0,
                    };
                }
                // Temporal prediction search, first sprint has N swarms of 1 field each,
                //  the predicted field may or may not be that one field.
                else if (this._hsObj._searchType == HsSearchType.temporal)
                {
                    foreach (var encoderName in this._hsObj._encoderNames)
                    {
                        swarms[encoderName] = new SwarmEncoderState
                        {
                            status = SwarmStatus.active,
                            bestModelId = null,
                            bestErrScore = null,
                            sprintIdx = 0,
                        };
                    }
                }


                // Classification prediction search, first sprint has N swarms of 1 field
                //  each where this field can NOT be the predicted field.
                else if (this._hsObj._searchType == HsSearchType.classification)
                {
                    foreach (string encoderName in this._hsObj._encoderNames)
                    {
                        if (encoderName == this._hsObj._predictedFieldEncoder)
                        {
                            continue;
                        }
                        swarms[encoderName] = new SwarmEncoderState
                        {
                            status = SwarmStatus.active,
                            bestModelId = null,
                            bestErrScore = null,
                            sprintIdx = 0,
                        };
                    }
                }

                // Legacy temporal. This is either a model that uses reconstruction or
                //  an older multi-step model that doesn't have a separate
                //  'classifierOnly' encoder for the predicted field. Here, the predicted
                //  field must ALWAYS be present and the first sprint tries the predicted
                //  field only
                else if (this._hsObj._searchType == HsSearchType.legacyTemporal)
                {
                    swarms[this._hsObj._predictedFieldEncoder] = new SwarmEncoderState
                    {
                        status = SwarmStatus.active,
                        bestModelId = null,
                        bestErrScore = null,
                        sprintIdx = 0,
                    };
                }

                else
                {
                    throw new InvalidOperationException(string.Format("Unsupported search type: {0}",
                        this._hsObj._searchType));
                }

                // Initialize the state.
                this._state = new HsStateModel
                {
                    // The last time the state was updated by a worker.
                    lastUpdateTime = DateTime.Now,

                    // Set from within setSwarmState() if we detect that the sprint we just
                    //  completed did worse than a prior sprint. This stores the index of
                    //  the last good sprint.
                    lastGoodSprint = null,

                    // Set from within setSwarmState() if lastGoodSprint is True and all
                    //  sprints have completed.
                    searchOver = false,

                    // This is a summary of the active swarms - this information can also
                    //  be obtained from the swarms entry that follows, but is summarized here
                    //  for easier reference when viewing the state as presented by
                    //  log messages and prints of the hsState data structure (by
                    //  permutations_runner).
                    activeSwarms = swarms.Keys.ToList(),

                    // All the swarms that have been created so far.
                    swarms = swarms,

                    // All the sprints that have completed or are in progress.
                    sprints = new List<SwarmEncoderState>
                    {
                        new SwarmEncoderState
                        {
                            status = SwarmStatus.active,
                            bestModelId = null,
                            bestErrScore = null
                        }
                    },

                    // The list of encoders we have "blacklisted" because they
                    //  performed so poorly.
                    blackListedEncoders = new List<string>(),
                };

                // This will do nothing if the value of engWorkerState is not still None.
                this._hsObj._cjDAO.jobSetFieldIfEqual(_hsObj._jobID, "engWorkerState", Json.Serialize(_state), null);

                this._priorStateJSON = (string)this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID, new[] { "engWorkerState" })[0];
                Debug.Assert(this._priorStateJSON != null);
            }

            // Read state from the database
            this._state = Json.Deserialize<HsStateModel>(this._priorStateJSON);
            this._dirty = false;
        }

        /// <summary>
        /// Update the state in the job record with our local changes (if any).
        /// If we don't have the latest state in our priorStateJSON, then re-load
        /// in the latest state and return False.If we were successful writing out
        /// our changes, return True
        /// </summary>
        /// <returns>True if we were successful writing out our changes
        /// False if our priorState is not the latest that was in the DB.
        /// In this case, we will re - load our state from the DB
        /// </returns>
        public bool writeStateToDB()
        {
            // If no changes, do nothing
            if (!this._dirty)
            {
                return true;
            }

            // Set the update time
            this._state.lastUpdateTime = DateTime.Now;
            var newStateJSON = JsonConvert.SerializeObject(_state, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            bool success = this._hsObj._cjDAO.jobSetFieldIfEqual(this._hsObj._jobID,
                "engWorkerState", newStateJSON.ToString(), this._priorStateJSON.ToString());

            if (success)
            {
                this.logger.Debug(string.Format("Success changing hsState to: \n{0} ", this._state.ToString()));
                this._priorStateJSON = newStateJSON;
            }

            // If no success, read in the current state from the DB
            else
            {
                this.logger.Debug(string.Format("Failed to change hsState to: \n{0} ", this._state.ToString()));

                this._priorStateJSON = (string)this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID, new[] { "engWorkerState" })[0];
                this._state = (HsStateModel)JsonConvert.DeserializeObject(_priorStateJSON, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                this.logger.Info("New hsState has been set by some other worker to: " + this._state.ToString());
            }

            return success;
        }

        /// <summary>
        /// Given an encoder dictionary key, get the encoder name.
        /// Encoders are a sub-dict within model params, and in HSv2, their key
        /// is structured like this for example:
        /// 'modelParams|sensorParams|encoders|home_winloss'
        /// The encoderName is the last word in the | separated key name
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string getEncoderNameFromKey(string key)
        {
            return key.Split('|').LastOrDefault();
        }

        /// <summary>
        /// Given an encoder name, get the key.
        /// Encoders are a sub-dict within model params, and in HSv2, their key
        /// is structured like this for example:
        ///     'modelParams|sensorParams|encoders|home_winloss'
        /// The encoderName is the last word in the | separated key name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string getEncoderKeyFromName(string name)
        {
            return string.Format("modelParams|sensorParams|encoders|{0}", name);
        }

        /// <summary>
        /// Return the field contributions statistics.
        /// </summary>
        /// <returns>
        /// Dictionary where the keys are the field names and the values
        /// are how much each field contributed to the best score.
        /// </returns>
        public Tuple<Map<string, double>, Map<string, double>> getFieldContributions()
        {
            // in the fast swarm, there is only 1 sprint and field contributions are
            // not defined
            if (this._hsObj._fixedFields != null)
            {
                return new Tuple<Map<string, double>, Map<string, double>>(
                    new Map<string, double>(), new Map<string, double>());
            }
            // Get the predicted field encoder name
            string predictedEncoderName = this._hsObj._predictedFieldEncoder;

            // -----------------------------------------------------------------------
            // Collect all the single field scores
            List<Tuple<double?, string>> fieldScores = new List<Tuple<double?, string>>();
            //for (swarmId, info in this._state["swarms"].iteritems())
            foreach (var state in this._state.swarms)
            {
                string swarmId = state.Key;
                SwarmEncoderState info = state.Value;
                string[] encodersUsed = swarmId.Split('.');
                if (encodersUsed.Length != 1)
                {
                    continue;
                }
                string field = this.getEncoderNameFromKey(encodersUsed[0]);
                double? bestScore = info.bestErrScore;

                // If the bestScore is None, this swarm hasn't completed yet (this could
                //  happen if we're exiting because of maxModels), so look up the best
                //  score so far
                if (bestScore == null)
                {
                    //(_modelId, bestScore) = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                    var tuple = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                    _modelId = tuple.Item1;
                    bestScore = tuple.Item2;
                }

                fieldScores.Add(new Tuple<double?, string>(bestScore, field));
            }


            // -----------------------------------------------------------------------
            // If we only have 1 field that was tried in the first sprint, then use that
            //  as the base and get the contributions from the fields in the next sprint.
            double? baseErrScore = null;
            string baseField = null;
            if (this._hsObj._searchType == HsSearchType.legacyTemporal)
            {
                Debug.Assert(fieldScores.Count == 1);
                //(baseErrScore, baseField) = fieldScores[0];
                var fieldScoreEl = fieldScores.ElementAt(0);
                baseErrScore = fieldScoreEl.Item1;
                baseField = fieldScoreEl.Item2;
                foreach (var state in this._state.swarms)
                {
                    string swarmId = state.Key;
                    SwarmEncoderState info = state.Value;
                    var encodersUsed = swarmId.Split('.');
                    if (encodersUsed.Length != 2)
                    {
                        continue;
                    }

                    var fields = encodersUsed.Select(this.getEncoderNameFromKey).ToList();
                    //[this.getEncoderNameFromKey(name) for name in encodersUsed];
                    fields.Remove(baseField);

                    fieldScores.Add(new Tuple<double?, string>(info.bestErrScore, fields[0]));
                }
            }

            // The first sprint tried a bunch of fields, pick the worst performing one
            //  (within the top this._hsObj._maxBranching ones) as the base
            else
            {
                //fieldScores.Sort(reverse = true);
                fieldScores = fieldScores.OrderByDescending(fs => fs.Item1).ToList();
                // If maxBranching was specified, pick the worst performing field within
                //  the top maxBranching+1 fields as our base, which will give that field
                //  a contribution of 0.
                if (this._hsObj._maxBranching > 0 && fieldScores.Count > this._hsObj._maxBranching)
                {
                    baseErrScore = fieldScores[-this._hsObj._maxBranching - 1].Item1;
                }
                else
                {
                    baseErrScore = fieldScores[0].Item1;
                }
            }


            // -----------------------------------------------------------------------
            // Prepare and return the fieldContributions dict
            var pctFieldContributionsDict = new Map<string, double>();
            var absFieldContributionsDict = new Map<string, double>();

            // If we have no base score, can't compute field contributions. This can
            //  happen when we exit early due to maxModels or being cancelled
            if (baseErrScore != null)
            {

                // If the base error score is 0, we can't compute a percent difference
                //  off of it, so move it to a very small float
                if (Math.Abs(baseErrScore.Value) < 0.00001)
                {
                    baseErrScore = 0.00001;
                }
                //for (errScore, field) in fieldScores
                foreach (var fieldScore in fieldScores)
                {
                    double? errScore = fieldScore.Item1;
                    string field = fieldScore.Item2;

                    double pctBetter = 0;
                    if (errScore != null)
                    {
                        pctBetter = (double)((baseErrScore - errScore) * 100.0 / baseErrScore);
                    }
                    else
                    {
                        pctBetter = 0.0;
                        errScore = baseErrScore; // for absFieldContribution
                    }

                    pctFieldContributionsDict[field] = pctBetter;
                    absFieldContributionsDict[field] = (double)(baseErrScore - errScore);
                }
            }

            this.logger.Debug(string.Format("FieldContributions: {0}", pctFieldContributionsDict));
            //return pctFieldContributionsDict, absFieldContributionsDict;
            return new Tuple<Map<string, double>, Map<string, double>>(
                pctFieldContributionsDict, absFieldContributionsDict);
        }

        /// <summary>
        /// Return the list of all swarms in the given sprint.
        /// </summary>
        /// <param name="sprintIdx">list of active swarm Ids in the given sprint</param>
        /// <returns></returns>
        public List<string> getAllSwarms(int sprintIdx)
        {
            List<string> swarmIds = new List<string>();
            //for (swarmId, info in this._state.swarms)
            foreach (var pair in this._state.swarms)
            {
                if (pair.Value.sprintIdx == sprintIdx)
                {
                    swarmIds.Add(pair.Key);
                }
            }

            return swarmIds;
        }

        /// <summary>
        /// Return the list of active swarms in the given sprint. These are swarms
        /// which still need new particles created in them.
        /// </summary>
        /// <param name="sprintIdx">which sprint to query. If None, get active swarms from all sprints</param>
        /// <returns>list of active swarm Ids in the given sprint</returns>
        public List<string> getActiveSwarms(int? sprintIdx = null)
        {
            List<string> swarmIds = new List<string>();
            //for (swarmId, info in this._state["swarms"].iteritems())
            foreach (var pair in this._state.swarms)
            {
                if (sprintIdx != null && pair.Value.sprintIdx != sprintIdx)
                {
                    continue;
                }
                if (pair.Value.status == SwarmStatus.active)
                {
                    swarmIds.Add(pair.Key);
                }
            }

            return swarmIds;
        }

        /// <summary>
        /// Return the list of swarms in the given sprint that were not killed.
        /// This is called when we are trying to figure out which encoders to carry
        /// forward to the next sprint.We don't want to carry forward encoder
        /// combintations which were obviously bad(in killed swarms).
        /// </summary>
        /// <param name="sprintIdx"></param>
        /// <returns>list of active swarm Ids in the given sprint</returns>
        public List<string> getNonKilledSwarms(int sprintIdx)
        {
            List<string> swarmIds = new List<string>();
            //for (swarmId, info in this._state["swarms"].iteritems())
            foreach (var pair in this._state.swarms)
            {
                if (pair.Value.sprintIdx == sprintIdx && pair.Value.status != SwarmStatus.killed)
                {
                    swarmIds.Add(pair.Key);
                }
            }

            return swarmIds;
        }

        /// <summary>
        /// Return the list of all completed swarms.
        /// </summary>
        /// <returns>list of active swarm Ids</returns>
        public List<string> getCompletedSwarms()
        {
            List<string> swarmIds = new List<string>();
            foreach (var pair in this._state.swarms)
            {
                if (pair.Value.status == SwarmStatus.completed)
                {
                    swarmIds.Add(pair.Key);
                }
            }

            return swarmIds;
        }

        /// <summary>
        /// Return the list of all completing swarms.
        /// </summary>
        /// <returns>list of active swarm Ids</returns>
        public List<string> getCompletingSwarms()
        {
            List<string> swarmIds = new List<string>();
            foreach (var pair in this._state.swarms)
            {
                if (pair.Value.status == SwarmStatus.completing)
                {
                    swarmIds.Add(pair.Key);
                }
            }

            return swarmIds;
        }

        /// <summary>
        /// Return the best model ID and it's errScore from the given swarm.
        /// If the swarm has not completed yet, the bestModelID will be None.
        /// </summary>
        /// <param name="swarmId"></param>
        /// <returns>(modelId, errScore)</returns>
        public Tuple<ulong?, double?> bestModelInCompletedSwarm(string swarmId)
        {
            SwarmEncoderState swarmInfo = this._state.swarms[swarmId];
            return new Tuple<ulong?, double?>(swarmInfo.bestModelId,
                swarmInfo.bestErrScore);
        }

        /// <summary>
        /// Return the best model ID and it's errScore from the given sprint.
        /// If the sprint has not completed yet, the bestModelID will be None.
        /// </summary>
        /// <param name="sprintIdx"></param>
        /// <returns>(modelId, errScore)</returns>
        public Tuple<ulong?, double?> bestModelInCompletedSprint(int sprintIdx)
        {
            SwarmEncoderState sprintInfo = this._state.sprints[sprintIdx];
            return new Tuple<ulong?, double?>(sprintInfo.bestModelId,
                sprintInfo.bestErrScore);
        }

        /// <summary>
        /// Return the best model ID and it's errScore from the given sprint,
        /// which may still be in progress.This returns the best score from all models
        /// in the sprint which have matured so far.
        /// </summary>
        /// <param name="sprintIdx"></param>
        /// <returns>(modelId, errScore)</returns>
        public Tuple<ulong?, double?> bestModelInSprint(int sprintIdx)
        {
            // Get all the swarms in this sprint
            List<string> swarms = this.getAllSwarms(sprintIdx);

            // Get the best model and score from each swarm
            ulong? bestModelId = null;
            double? bestErrScore = double.PositiveInfinity;
            foreach (string swarmId in swarms)
            {
                //(modelId, errScore) = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                var idAndScore = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                if (idAndScore.Item2 < bestErrScore)
                {
                    bestModelId = idAndScore.Item1;
                    bestErrScore = idAndScore.Item2;
                }
            }

            return new Tuple<ulong?, double?>(bestModelId, bestErrScore);
        }

        /// <summary>
        /// Change the given swarm's state to 'newState'. If 'newState' is
        /// 'completed', then bestModelId and bestErrScore must be provided.
        /// </summary>
        /// <param name="swarmId">swarm Id</param>
        /// <param name="newStatus">new status, either 'active', 'completing', 'completed', or 'killed'</param>
        public void setSwarmState(string swarmId, SwarmStatus newStatus)
        {
            Debug.Assert(newStatus != SwarmStatus.none);

            // Set the swarm status
            SwarmEncoderState swarmInfo = this._state.swarms[swarmId];
            if (swarmInfo.status == newStatus)
            {
                return;
            }

            // If some other worker noticed it as completed, setting it to completing
            //  is obviously old information....
            if (swarmInfo.status == SwarmStatus.completed && newStatus == SwarmStatus.completing)
            {
                return;
            }

            this._dirty = true;
            swarmInfo.status = newStatus;
            if (newStatus == SwarmStatus.completed)
            {
                //(modelId, errScore) = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                var pair = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
                swarmInfo.bestModelId = pair.Item1;
                swarmInfo.bestErrScore = pair.Item2;
            }

            // If no longer active, remove it from the activeSwarms entry
            if (newStatus != SwarmStatus.active && this._state.activeSwarms.Contains(swarmId))
            {
                this._state.activeSwarms.Remove(swarmId);
            }

            // If new status is 'killed', kill off any running particles in that swarm
            if (newStatus == SwarmStatus.killed)
            {
                this._hsObj.killSwarmParticles(swarmId);
            }

            // In case speculative particles are enabled, make sure we generate a new
            //  swarm at this time if all of the swarms in the current sprint have
            //  completed. This will insure that we don't mark the sprint as completed
            //  before we've created all the possible swarms.
            int sprintIdx = swarmInfo.sprintIdx;
            this.isSprintActive(sprintIdx);

            // Update the sprint status. Check all the swarms that belong to this sprint.
            //  If they are all completed, the sprint is completed.
            SwarmEncoderState sprintInfo = this._state.sprints[sprintIdx];

            Dictionary<SwarmStatus, int> statusCounts = new Dictionary<SwarmStatus, int>
            {
                {SwarmStatus.active, 0},
                {SwarmStatus.completing, 0},
                {SwarmStatus.completed, 0},
                {SwarmStatus.killed, 0}
            };

            List<ulong> bestModelIds = new List<ulong>();
            List<double> bestErrScores = new List<double>();
            foreach (var infoPair in this._state.swarms)
            {
                var info = infoPair.Value;
                if (info.sprintIdx != sprintIdx)
                {
                    continue;
                }
                statusCounts[info.status] += 1;
                if (info.status == SwarmStatus.completed)
                {
                    bestModelIds.Add(info.bestModelId.GetValueOrDefault());
                    bestErrScores.Add(info.bestErrScore.GetValueOrDefault());
                }
            }
            SwarmStatus sprintStatus;
            if (statusCounts[SwarmStatus.active] > 0)
            {
                sprintStatus = SwarmStatus.active;
            }
            else if (statusCounts[SwarmStatus.completing] > 0)
            {
                sprintStatus = SwarmStatus.completing;
            }
            else
            {
                sprintStatus = SwarmStatus.completed;
            }
            sprintInfo.status = sprintStatus;

            // If the sprint is complete, get the best model from all of its swarms and
            //  store that as the sprint best
            if (sprintStatus == SwarmStatus.completed)
            {
                if (bestErrScores.Count > 0)
                {
                    int whichIdx = ArrayUtils.Argmin(bestErrScores.ToArray()); //numpy.array(bestErrScores).argmin();
                    sprintInfo.bestModelId = bestModelIds[whichIdx];
                    sprintInfo.bestErrScore = bestErrScores[whichIdx];
                }
                else
                {
                    // This sprint was empty, most likely because all particles were
                    //  killed. Give it a huge error score
                    sprintInfo.bestModelId = 0;
                    sprintInfo.bestErrScore = double.PositiveInfinity;
                }


                // See if our best err score got NO BETTER as compared to a previous
                //  sprint. If so, stop exploring subsequent sprints (lastGoodSprint
                //  is no longer None).
                double? bestPrior = double.PositiveInfinity;
                double? errScore = null;
                foreach (int idx in ArrayUtils.Range(0, sprintIdx))
                {
                    if (this._state.sprints[idx].status == SwarmStatus.completed)
                    {
                        //(_, errScore) = this.bestModelInCompletedSprint(idx);
                        errScore = this.bestModelInCompletedSprint(idx).Item2;
                        if (errScore == null)
                        {
                            errScore = double.PositiveInfinity;
                        }
                    }
                    else
                    {
                        errScore = double.PositiveInfinity;
                    }
                    if (errScore < bestPrior)
                    {
                        bestPrior = errScore;
                    }
                }

                if (sprintInfo.bestErrScore >= bestPrior)
                {
                    this._state.lastGoodSprint = sprintIdx - 1;
                }

                // If ALL sprints up to the last good one are done, the search is now over
                if (this._state.lastGoodSprint != null && !this.anyGoodSprintsActive())
                {
                    this._state.searchOver = true;
                }
            }
        }

        /// <summary>
        /// Return True if there are any more good sprints still being explored.
        /// A 'good' sprint is one that is earlier than where we detected an increase
        /// in error from sprint to subsequent sprint.
        /// </summary>
        /// <returns></returns>
        public bool anyGoodSprintsActive()
        {
            List<SwarmEncoderState> goodSprints = null;

            if (this._state.lastGoodSprint != null)
            {
                //goodSprints = this._state.sprints[0:this._state["lastGoodSprint"] + 1];
                goodSprints = this._state.sprints.Take(_state.lastGoodSprint.Value + 1).ToList();
            }
            else
            {
                goodSprints = this._state.sprints;
            }

            return goodSprints.Any(sprint => sprint.status == SwarmStatus.active);
        }
        /// <summary>
        /// Return True if the given sprint has completed.
        /// </summary>
        /// <param name="sprintIdx"></param>
        /// <returns></returns>
        public bool isSprintCompleted(int sprintIdx)
        {
            int numExistingSprints = this._state.sprints.Count;
            if (sprintIdx >= numExistingSprints)
            {
                return false;
            }

            return (this._state.sprints[sprintIdx].status == SwarmStatus.completed);
        }

        private class SwarmDefinition : IComparable<SwarmDefinition>
        {
            public string swarmName;
            public SwarmEncoderState swarmState;
            public double? swarmBestErrScore;

            #region Implementation of IComparable<in SwarmDefinition>

            public int CompareTo(SwarmDefinition other)
            {
                return this.swarmBestErrScore.GetValueOrDefault().CompareTo(other.swarmBestErrScore.GetValueOrDefault());
            }

            #endregion
        }
        /// <summary>
        /// See if we can kill off some speculative swarms. If an earlier sprint
        /// has finally completed, we can now tell which fields should* really*be present
        /// in the sprints we've already started due to speculation, and kill off the
        /// swarms that should not have been included.
        /// </summary>
        public void killUselessSwarms()
        {
            // Get number of existing sprints
            int numExistingSprints = this._state.sprints.Count;

            // Should we bother killing useless swarms?
            if (this._hsObj._searchType == HsSearchType.legacyTemporal)
            {
                if (numExistingSprints <= 2)
                {
                    return;
                }
            }
            else
            {
                if (numExistingSprints <= 1)
                {
                    return;
                }
            }

            // Form completedSwarms as a list of tuples, each tuple contains:
            //  (swarmName, swarmState, swarmBestErrScore)
            // ex. completedSwarms:
            //    [('a', {...}, 1.4),
            //     ('b', {...}, 2.0),
            //     ('c', {...}, 3.0)]
            List<string> completedSwarms1 = this.getCompletedSwarms();
            List<SwarmDefinition> completedSwarms = completedSwarms1
                .Select(swarm => new SwarmDefinition
                {
                    swarmBestErrScore = _state.swarms[swarm].bestErrScore,
                    swarmName = swarm,
                    swarmState = _state.swarms[swarm]
                })
                .ToList();
            //completedSwarms = [(swarm, this._state["swarms"][swarm],
            //                    this._state["swarms"][swarm]["bestErrScore"]) for swarm in completedSwarms];

            // Form the completedMatrix. Each row corresponds to a sprint. Each row
            //  contains the list of swarm tuples that belong to that sprint, sorted
            //  by best score. Each swarm tuple contains (swarmName, swarmState,
            //  swarmBestErrScore).
            // ex. completedMatrix:
            //    [(('a', {...}, 1.4), ('b', {...}, 2.0), ('c', {...}, 3.0)),
            //     (('a.b', {...}, 3.0), ('b.c', {...}, 4.0))]
            //completedMatrix = [[] for i in range(numExistingSprints)];
            var completedMatrix = ArrayUtils.Range(0, numExistingSprints).Select(i => new List<SwarmDefinition>()).ToArray();
            foreach (SwarmDefinition swarm in completedSwarms)
            {
                completedMatrix[swarm.swarmState.sprintIdx].Add(swarm);
            }
            foreach (List<SwarmDefinition> sprint in completedMatrix)
            {
                sprint.Sort();
            }

            // Form activeSwarms as a list of tuples, each tuple contains:
            //  (swarmName, swarmState, swarmBestErrScore)
            // Include all activeSwarms and completingSwarms
            // ex. activeSwarms:
            //    [('d', {...}, 1.4),
            //     ('e', {...}, 2.0),
            //     ('f', {...}, 3.0)]
            var activeSwarms1 = this.getActiveSwarms();
            // Append the completing swarms
            activeSwarms1.AddRange(this.getCompletingSwarms());
            var activeSwarms = activeSwarms1.Select(swarm => new SwarmDefinition
            {
                swarmBestErrScore = _state.swarms[swarm].bestErrScore,
                swarmName = swarm,
                swarmState = _state.swarms[swarm]
            }).ToList();
            //activeSwarms = [(swarm, this._state["swarms"][swarm],
            //                 this._state["swarms"][swarm]["bestErrScore"]) \
            //                                        for swarm in activeSwarms];

            // Form the activeMatrix. Each row corresponds to a sprint. Each row
            //  contains the list of swarm tuples that belong to that sprint, sorted
            //  by best score. Each swarm tuple contains (swarmName, swarmState,
            //  swarmBestErrScore)
            // ex. activeMatrix:
            //    [(('d', {...}, 1.4), ('e', {...}, 2.0), ('f', {...}, 3.0)),
            //     (('d.e', {...}, 3.0), ('e.f', {...}, 4.0))]
            //activeMatrix = [[] for i in range(numExistingSprints)];
            var activeMatrix = ArrayUtils.Range(0, numExistingSprints).Select(i => new List<SwarmDefinition>()).ToArray();
            foreach (var swarm in activeSwarms)
            {
                activeMatrix[swarm.swarmState.sprintIdx].Add(swarm);
            }
            foreach (var sprint in activeMatrix)
            {
                sprint.Sort();
            }


            // Figure out which active swarms to kill
            var toKill = new List<SwarmDefinition>();
            foreach (int i in ArrayUtils.Range(1, numExistingSprints))
            {
                foreach (var swarm in activeMatrix[i])
                {
                    string[] curSwarmEncoders = swarm.swarmName.Split('.');

                    // If previous sprint is complete, get the best swarm and kill all active
                    //  sprints that are not supersets
                    if (activeMatrix[i - 1].Count == 0)
                    {
                        // If we are trying all possible 3 field combinations, don't kill any
                        //  off in sprint 2
                        if (i == 2 && (this._hsObj._tryAll3FieldCombinations || this._hsObj._tryAll3FieldCombinationsWTimestamps))
                        {
                            continue;
                        }
                        else
                        {
                            var bestInPrevious = completedMatrix[i - 1][0];
                            var bestEncoders = bestInPrevious.swarmName.Split('.');
                            foreach (var encoder in bestEncoders)
                            {
                                if (!curSwarmEncoders.Contains(encoder))
                                {
                                    toKill.Add(swarm);
                                }
                            }
                        }
                    }

                    // if there are more than two completed encoders sets that are complete and
                    // are worse than at least one active swarm in the previous sprint. Remove
                    // any combinations that have any pair of them since they cannot have the best encoder.
                    // elif(len(completedMatrix[i-1])>1):
                    //  for completedSwarm in completedMatrix[i-1]:
                    //    activeMatrix[i-1][0][2]<completed
                }
            }

            // Mark the bad swarms as killed
            if (toKill.Count > 0)
            {
                Debug.WriteLine("ParseMe: Killing encoders:" + Arrays.ToString(toKill));
            }

            foreach (var swarm in toKill)
            {
                this.setSwarmState(swarm.swarmName, SwarmStatus.killed);
            }

            return;
        }
        /// <summary>
        /// If the given sprint exists and is active, return active=True.
        ///
        ///  If the sprint does not exist yet, this call will create it (and return
        ///  active=True). If it already exists, but is completing or complete, return
        ///  active=False.
        ///
        ///  If sprintIdx is past the end of the possible sprints, return
        ///    active=False, noMoreSprints=True
        ///
        ///  IMPORTANT: When speculative particles are enabled, this call has some
        ///  special processing to handle speculative sprints:
        ///
        ///    * When creating a new speculative sprint (creating sprint N before
        ///    sprint N-1 has completed), it initially only puts in only ONE swarm into
        ///    the sprint.
        ///
        ///    * Every time it is asked if sprint N is active, it also checks to see if
        ///    it is time to add another swarm to the sprint, and adds a new swarm if
        ///    appropriate before returning active=True
        ///
        ///    * We decide it is time to add a new swarm to a speculative sprint when ALL
        ///    of the currently active swarms in the sprint have all the workers they
        ///    need (number of running (not mature) particles is _minParticlesPerSwarm).
        ///    This means that we have capacity to run additional particles in a new
        ///    swarm.
        ///
        ///  It is expected that the sprints will be checked IN ORDER from 0 on up. (It
        ///  is an error not to) The caller should always try to allocate from the first
        ///  active sprint it finds. If it can't, then it can call this again to
        ///  find/create the next active sprint.
        ///
        ///  Parameters:
        ///  ---------------------------------------------------------------------
        ///  retval:   (active, noMoreSprints)
        ///              active: True if the given sprint is active
        ///              noMoreSprints: True if there are no more sprints possible
        /// </summary>
        /// <param name="sprintIdx"></param>
        /// <returns></returns>
        public Tuple<bool, bool> isSprintActive(int sprintIdx)
        {
            while (true)
            {
                bool active = false;
                int numExistingSprints = this._state.sprints.Count;

                // If this sprint already exists, see if it is active
                if (sprintIdx <= numExistingSprints - 1)
                {

                    // With speculation off, it's simple, just return whether or not the
                    //  asked for sprint has active status
                    if (!this._hsObj._speculativeParticles)
                    {
                        active = (this._state.sprints[sprintIdx].status == SwarmStatus.active);
                        return new Tuple<bool, bool>(active, false);
                    }

                    // With speculation on, if the sprint is still marked active, we also
                    //  need to see if it's time to add a new swarm to it.
                    else
                    {
                        active = (this._state.sprints[sprintIdx].status == SwarmStatus.active);
                        if (!active)
                        {
                            return new Tuple<bool, bool>(active, false);
                        }

                        // See if all of the existing swarms are at capacity (have all the
                        // workers they need):
                        var activeSwarmIds = this.getActiveSwarms(sprintIdx);
                        var swarmSizes = activeSwarmIds.Select(swarmId => this._hsObj._resultsDB.getParticleInfos(swarmId, matured: false).particleStates).ToList();
                        //swarmSizes = [this._hsObj._resultsDB.getParticleInfos(swarmId,matured = False)[0] 
                        //              for swarmId in activeSwarmIds];
                        var notFullSwarms = swarmSizes
                            .Where(swarm => swarm.Count < this._hsObj._minParticlesPerSwarm)
                            .Select(s=>s.Count)
                            .ToList();
                        //  notFullSwarms = [len(swarm) for swarm in swarmSizes \
                        //       if len(swarm) < this._hsObj._minParticlesPerSwarm];

                        // If some swarms have room return that the swarm is active.
                        if (notFullSwarms.Count > 0)
                        {
                            return new Tuple<bool, bool>(true, false);
                        }

                        // If the existing swarms are at capacity, we will fall through to the
                        //  logic below which tries to add a new swarm to the sprint.
                    }
                }

                // Stop creating new sprints?
                if (this._state.lastGoodSprint != null)
                {
                    return new Tuple<bool, bool>(false, true);
                }

                // if fixedFields is set, we are running a fast swarm and only run sprint0
                if (this._hsObj._fixedFields != null)
                {
                    return new Tuple<bool, bool>(false, true);
                }

                // ----------------------------------------------------------------------
                // Get the best model (if there is one) from the prior sprint. That gives
                // us the base encoder set for the next sprint. For sprint zero make sure
                // it does not take the last sprintidx because of wrapping.
                string bestSwarmId = null;
                List<string> baseEncoderSets = null;
                ParticleStateModel particleState = null;
                int baseSprintIdx = 0;
                if (sprintIdx > 0 && this._state.sprints[sprintIdx - 1].status == SwarmStatus.completed)
                {
                    ulong bestModelId = this.bestModelInCompletedSprint(sprintIdx - 1).Item1.GetValueOrDefault();
                    //(bestModelId, _) = this.bestModelInCompletedSprint(sprintIdx - 1);
                    particleState = this._hsObj._resultsDB.getParticleInfo(bestModelId).particleState;
                    //(particleState, _, _, _, _) = this._hsObj._resultsDB.getParticleInfo(bestModelId);
                    bestSwarmId = particleState.swarmId;
                    baseEncoderSets = bestSwarmId.Split('.').ToList();
                }

                // If there is no best model yet, then use all encoder sets from the prior
                //  sprint that were not killed
                else
                {
                    bestSwarmId = null;
                    particleState = null;
                    // Build up more combinations, using ALL of the sets in the current
                    //  sprint.
                    baseEncoderSets = new List<string>();
                    foreach (var swarmId in this.getNonKilledSwarms(sprintIdx - 1))
                    {
                        baseEncoderSets.AddRange(swarmId.Split('.'));
                    }
                }

                // ----------------------------------------------------------------------
                // Which encoders should we add to the current base set?
                List<string> encoderAddSet = new List<string>();

                // If we have constraints on how many fields we carry forward into
                // subsequent sprints (either nupic.hypersearch.max.field.branching or
                // nupic.hypersearch.min.field.contribution was set), then be more
                // picky about which fields we add in.
                bool limitFields = false;
                if (this._hsObj._maxBranching > 0 || this._hsObj._minFieldContribution >= 0)
                {
                    if (this._hsObj._searchType == HsSearchType.temporal ||
                        this._hsObj._searchType == HsSearchType.classification)
                    {
                        if (sprintIdx >= 1)
                        {
                            limitFields = true;
                            baseSprintIdx = 0;
                        }
                    }
                    else if (this._hsObj._searchType == HsSearchType.legacyTemporal)
                    {
                        if (sprintIdx >= 2)
                        {
                            limitFields = true;
                            baseSprintIdx = 1;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Unimplemented search type " + this._hsObj._searchType);
                    }
                }


                // Only add top _maxBranching encoders to the swarms?
                if (limitFields)
                {

                    // Get field contributions to filter added fields
                    //pctFieldContributions, absFieldContributions = this.getFieldContributions();
                    var contributions = this.getFieldContributions();
                    var pctFieldContributions = contributions.Item1;
                    var absFieldContributions = contributions.Item2;
                    List<string> toRemove = new List<string>();
                    this.logger.Debug("FieldContributions min: " + this._hsObj._minFieldContribution);
                    foreach (var fieldname in pctFieldContributions.Keys)
                    {
                        if (pctFieldContributions[fieldname] < this._hsObj._minFieldContribution)
                        {
                            this.logger.Debug("FieldContributions removing: " + (fieldname));
                            toRemove.Add(this.getEncoderKeyFromName(fieldname));
                        }
                        else
                        {
                            this.logger.Debug("FieldContributions keeping: " + (fieldname));
                        }
                    }


                    // Grab the top maxBranching base sprint swarms.
                    var swarms = this._state.swarms;
                    //sprintSwarms = [(swarm, swarms[swarm]["bestErrScore"]) \
                    //    for swarm in swarms if swarms[swarm]["sprintIdx"] == baseSprintIdx];
                    var sprintSwarmsQuery = swarms.Where(swarm => swarm.Value.sprintIdx == baseSprintIdx)
                        .Select(swarm => new { swarm = swarm.Key, swarm.Value.bestErrScore })
                        .OrderBy(s => s.bestErrScore);
                    var sprintSwarms = sprintSwarmsQuery.ToList();
                    //sprintSwarms = sorted(sprintSwarms, key = itemgetter(1));
                    if (this._hsObj._maxBranching > 0)
                    {
                        sprintSwarms = sprintSwarmsQuery.Take(_hsObj._maxBranching).ToList();
                        //sprintSwarms = sprintSwarms[0:this._hsObj._maxBranching];
                    }

                    // Create encoder set to generate further swarms.
                    foreach (var swarm in sprintSwarms)
                    {
                        var swarmEncoders = swarm.swarm.Split('.');
                        foreach (var encoder in swarmEncoders)
                        {
                            if (!encoderAddSet.Contains(encoder))
                            {
                                encoderAddSet.Add(encoder);
                            }
                        }
                    }
                    encoderAddSet = encoderAddSet.Where(encoder => !toRemove.Contains(encoder)).ToList();
                    //encoderAddSet = [encoder for encoder in encoderAddSet \
                    //         if not str(encoder) in toRemove];
                }

                // If no limit on the branching or min contribution, simply use all of the
                // encoders.
                else
                {
                    encoderAddSet = this._hsObj._encoderNames;
                }


                // -----------------------------------------------------------------------
                // Build up the new encoder combinations for the next sprint.
                var newSwarmIds = new List<string>();
                var newEncoders = new List<string>();

                // See if the caller wants to try more extensive field combinations with
                //  3 fields.
                if ((this._hsObj._searchType == HsSearchType.temporal || this._hsObj._searchType == HsSearchType.legacyTemporal)
                    && sprintIdx == 2 && (this._hsObj._tryAll3FieldCombinations || this._hsObj._tryAll3FieldCombinationsWTimestamps))
                {

                    if (this._hsObj._tryAll3FieldCombinations)
                    {
                        newEncoders = new List<string>(this._hsObj._encoderNames);
                        if (newEncoders.Contains(this._hsObj._predictedFieldEncoder))
                        {
                            newEncoders.Remove(this._hsObj._predictedFieldEncoder);
                        }
                    }
                    else
                    {
                        // Just make sure the timestamp encoders are part of the mix
                        newEncoders = new List<string>(encoderAddSet);
                        if (newEncoders.Contains(this._hsObj._predictedFieldEncoder))
                        {
                            newEncoders.Remove(this._hsObj._predictedFieldEncoder);
                        }
                        foreach (var encoder in this._hsObj._encoderNames)
                        {
                            if (encoder.EndsWith("_timeOfDay") || encoder.EndsWith("_weekend") || encoder.EndsWith("_dayOfWeek"))
                            {
                                newEncoders.Add(encoder);
                            }
                        }
                    }

                    //allCombos = list(itertools.combinations(newEncoders, 2));
                    List<string[]> allCombos = ArrayUtils.Combinations(newEncoders, 2).ToList();
                    foreach (var combo in allCombos)
                    {
                        var newSet = combo.ToList();
                        newSet.Add(this._hsObj._predictedFieldEncoder);
                        newSet.Sort();
                        string newSwarmId = string.Join(".", newSet);
                        if (!this._state.swarms.ContainsKey(newSwarmId))
                        {
                            newSwarmIds.Add(newSwarmId);

                            // If a speculative sprint, only add the first encoder, if not add
                            //   all of them.
                            if (this.getActiveSwarms(sprintIdx - 1).Count > 0)
                            {
                                break;
                            }
                        }
                    }
                }

                // Else, we only build up by adding 1 new encoder to the best combination(s)
                //  we've seen from the prior sprint
                else
                {
                    foreach (string baseEncoderSet in baseEncoderSets)
                    {
                        foreach (string encoder in encoderAddSet)
                        {
                            if (!this._state.blackListedEncoders.Contains(encoder) && !baseEncoderSet.Contains(encoder))
                            {
                                var newSet = new List<string> { baseEncoderSet };
                                newSet.Add(encoder);
                                newSet.Sort();
                                string newSwarmId = string.Join(".", newSet);
                                if (!this._state.swarms.ContainsKey(newSwarmId))
                                {
                                    newSwarmIds.Add(newSwarmId);

                                    // If a speculative sprint, only add the first encoder, if not add
                                    //   all of them.
                                    if (this.getActiveSwarms(sprintIdx - 1).Count > 0)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }


                // ----------------------------------------------------------------------
                // Sort the new swarm Ids
                newSwarmIds.Sort();

                // If no more swarms can be found for this sprint...
                if (newSwarmIds.Count == 0)
                {
                    // if sprint is not an empty sprint return that it is active but do not
                    //  add anything to it.
                    if (this.getAllSwarms(sprintIdx).Count > 0)
                    {
                        return new Tuple<bool, bool>(true, false);
                    }

                    // If this is an empty sprint and we couldn't find any new swarms to
                    //   add (only bad fields are remaining), the search is over
                    else
                    {
                        return new Tuple<bool, bool>(false, true);
                    }
                }

                // Add this sprint and the swarms that are in it to our state
                this._dirty = true;

                // Add in the new sprint if necessary
                if (this._state.sprints.Count == sprintIdx)
                {
                    this._state.sprints.Add(new SwarmEncoderState
                    {
                        status = SwarmStatus.active,
                        bestModelId = null,
                        bestErrScore = null
                    });
                }

                // Add in the new swarm(s) to the sprint
                foreach (var swarmId in newSwarmIds)
                {
                    this._state.swarms[swarmId] = new SwarmEncoderState
                    {
                        status = SwarmStatus.active,
                        bestModelId = null,
                        bestErrScore = null,
                        sprintIdx = sprintIdx
                    };
                }

                // Update the list of active swarms
                this._state.activeSwarms = this.getActiveSwarms();

                // Try to set new state
                bool success = this.writeStateToDB();

                // Return result if successful
                if (success)
                {
                    return new Tuple<bool, bool>(true, false);
                }

                // No success, loop back with the updated state and try again
            }
        }
    }
}