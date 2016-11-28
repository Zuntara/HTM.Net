using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HTM.Net.Encoders;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    /// <summary>
    /// https://github.com/numenta/nupic/blob/c710feb4001233a3f1e6d322436dbcefb186320e/src/nupic/support/nupic-default.xml
    /// </summary>
    public static class SwarmConfiguration
    {
        // Terminator
        public static int swarmMaturityWindow = 0;
        public static int swarmMaxGenerations = 1;
        public static bool enableSwarmTermination = true;
        public static bool speculativeParticlesDefault = true;
        public static double speculativeParticlesSleepSecondsMax = 10.0;
        public static int maxFieldBranching = 5;
        public static double minFieldContribution = 0.2;
        public static bool enableModelTermination;
        public static bool enableModelMaturity = false;
        public static int maxUniqueModelAttempts = 10;
        public static int modelOrphanIntervalSecs = 180;
        public static double maxPctErrModels = 0.20;
        public static int minParticlesPerSwarm = 5;
        public static double inertia = 0.25;
        public static double cogRate = 0.25;
        public static double socRate = 1.0;
        public static double randomLowerBound = 0.8;
        public static double randomUpperBound = 1.2;
        public static int? bestModelMinRecords = 1000;
        public static double? maturityPctChange = 0.005;
        public static int? maturityNumPoints = 10;
        public static double? opf_metricWindow = 1000;
    }

    /// <summary>
    /// Class that records the performane of swarms in a sprint and makes 
    /// decisions about which swarms should stop running.This is a usful optimization 
    /// that identifies field combinations that no longer need to be run.
    /// </summary>
    public class SwarmTerminator
    {
        private int MATURITY_WINDOW;
        private int? MAX_GENERATIONS = null;
        private HashSet<double> _DEFAULT_MILESTONES;
        public Dictionary<string, List<double>> swarmScores;
        private Dictionary<string, List<double>> swarmBests;
        private HashSet<string> terminatedSwarms;
        private bool _isTerminationEnabled;
        private HashSet<double> milestones;
        private readonly ILog _logger = LogManager.GetLogger(typeof(SwarmTerminator));

        public SwarmTerminator(HashSet<double> milestones = null)
        {
            // Set class constants.
            MATURITY_WINDOW = SwarmConfiguration.swarmMaturityWindow;
            MAX_GENERATIONS = SwarmConfiguration.swarmMaxGenerations;

            //_DEFAULT_MILESTONES = [1.0 / (x + 1) for x in xrange(12)]

            _DEFAULT_MILESTONES = new HashSet<double>(ArrayUtils.Range(0, 12).Select((x => 1.0 / (x + 1))).ToArray());

            if (MAX_GENERATIONS < 0)
            {
                MAX_GENERATIONS = null;
            }

            // Set up instsance variables.

            _isTerminationEnabled = SwarmConfiguration.enableSwarmTermination;

            swarmBests = new Dictionary<string, List<double>>();
            swarmScores = new Dictionary<string, List<double>>();
            terminatedSwarms = new HashSet<string>();

            //_logger = logging.getLogger(".".join(["com.numenta', this.__class__.__module__, this.__class__.__name__]));

            if (milestones != null)
            {
                this.milestones = milestones;
            }
            else
            {
                this.milestones = new HashSet<double>(this._DEFAULT_MILESTONES);
            }

        }

        public HashSet<string> recordDataPoint(string swarmId, int generation, double errScore)
        {
            /*  Record the best score for a swarm's generation index (x)
                Returns list of swarmIds to terminate.    */
            ;
            HashSet<string> terminatedSwarms = new HashSet<string>();

            // Append score to existing swarm.
            if (swarmScores.ContainsKey(swarmId))
            {
                var entry = swarmScores[swarmId];
                Debug.Assert(entry.Count == generation);
                entry.Add(errScore);

                entry = swarmBests[swarmId];
                entry.Add(Math.Min(errScore, entry[-1]));

                Debug.Assert(this.swarmBests[swarmId].Count == this.swarmScores[swarmId].Count);
            }
            else
            {
                // Create list of scores for a new swarm
                Debug.Assert(generation == 0);
                this.swarmScores[swarmId] = new List<double> { errScore };
                this.swarmBests[swarmId] = new List<double> { errScore };
            }

            // If the current swarm hasn't completed at least MIN_GENERATIONS, it should
            // not be candidate for maturation or termination. This prevents the initial
            // allocation of particles in PSO from killing off a field combination too
            // early.
            if (generation + 1 < this.MATURITY_WINDOW)
            {
                return terminatedSwarms;
            }

            // If the swarm has completed more than MAX_GENERATIONS, it should be marked
            // as mature, regardless of how its value is changing.
            if (this.MAX_GENERATIONS != null && generation > this.MAX_GENERATIONS)
            {
                this._logger.Info(string.Format("Swarm {0} has matured (more than {1} generations). Stopping", swarmId, this.MAX_GENERATIONS));
                terminatedSwarms.Add(swarmId);
            }

            if (this._isTerminationEnabled)
            {
                terminatedSwarms.UnionWith(this._getTerminatedSwarms(generation));
            }

            // Return which swarms to kill when we've reached maturity
            // If there is no change in the swarm's best for some time,
            // Mark it dead
            List<double> cumulativeBestScores = this.swarmBests[swarmId];
            if (cumulativeBestScores.LastOrDefault() == cumulativeBestScores[cumulativeBestScores.Count - 1 - this.MATURITY_WINDOW])
            {
                this._logger.Info(string.Format("Swarm {0} has matured (no change in {1} generations). Stopping...", swarmId, this.MATURITY_WINDOW));
                terminatedSwarms.Add(swarmId);
            }

            this.terminatedSwarms.UnionWith(terminatedSwarms);
            return terminatedSwarms;
        }

        public int numDataPoints(string swarmId)
        {
            if (this.swarmScores.ContainsKey(swarmId))
            {
                return this.swarmScores[swarmId].Count;
            }
            else
            {
                return 0;
            }
        }

        private HashSet<string> _getTerminatedSwarms(int generation)
        {
            terminatedSwarms = new HashSet<string>();
            var generationScores = new Dictionary<string, double>();
            foreach (var swarmScores in this.swarmScores)
            //for (swarm, scores in this.swarmScores.iteritems())
            {
                //if (len(scores) > generation and swarm not in this.terminatedSwarms)
                // {
                //    generationScores[swarm] = scores[generation];
                //}
                if (swarmScores.Value.Count > generation && !this.terminatedSwarms.Contains(swarmScores.Key))
                {
                    generationScores[swarmScores.Key] = swarmScores.Value[generation];
                }
            }

            if (generationScores.Count == 0)
            {
                return new HashSet<string>();
            }

            var bestScore = generationScores.Values.Min();
            var tolerance = this.milestones.ElementAt(generation);

            foreach (var swarmScores in generationScores)
            //for (swarm, score in generationScores.iteritems())
            {
                var swarm = swarmScores.Key;
                var score = swarmScores.Value;

                if (score > (1 + tolerance) * bestScore)
                {
                    this._logger.Info(string.Format("Swarm {0} is doing poorly at generation {1}.\n Current Score:{2} \n Best Score:{3} \n Tolerance:{4}. Stopping...",
                                      swarm, generation, score, bestScore, tolerance));
                    terminatedSwarms.Add(swarm);
                }
            }
            return terminatedSwarms;
        }
    }

    public class KeyHelper
    {
        public string FlattenKeys(IEnumerable<string> keys)
        {
            return string.Join("|", keys);
        }
    }



    public class ParticleStateModel
    {
        public string id;
        public int genIdx;
        public string swarmId;
        public Dictionary<string, VarState> varStates;
    }

    public class VarState
    {
        public int? position;
        public double? _position;
        public double? velocity;
        public double? bestPosition;
        public double? bestResult;

        public VarState Clone()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This class holds all the information we have accumulated on completed models, which particles were used, etc.
    /// When we get updated results sent to us (via recordModelProgress), 
    /// we record it here for access later by various functions in this module.
    /// </summary>
    public class ResultsDB
    {
        private HypersearchV2 _hsObj;
        private List<Dictionary<string, object>> _allResults;
        private HashSet<ulong> _errModels;
        private int _numErrModels;
        private HashSet<object> _completedModels;
        private int _numCompletedModels;
        private Dictionary<ulong, int?> _modelIDToIdx;
        private double? _bestResult;
        private ulong? _bestModelID;
        private Dictionary<string, List<Tuple<ulong?, double?>>> _swarmBestOverall;
        private Dictionary<string, List<int>> _swarmNumParticlesPerGeneration;
        private HashSet<Tuple<string, int>> _modifiedSwarmGens;
        private HashSet<Tuple<string, int>> _maturedSwarmGens;
        private Map<string, Tuple<double?, Map<string, int>>> _particleBest;
        private Dictionary<string, int> _particleLatestGenIdx;
        private Dictionary<string, List<int>> _swarmIdToIndexes;
        private Dictionary<string, int?> _paramsHashToIndexes;

        private readonly ILog _logger = LogManager.GetLogger(typeof(ResultsDB));


        public ResultsDB(HypersearchV2 hsObj)
        {
            /*
            Instantiate our results database

            Parameters:
            --------------------------------------------------------------------
            hsObj:        Reference to the HypersearchV2 instance
            */
            this._hsObj = hsObj;

            // This list holds all the results we have so far on every model. In
            //  addition, we maintain mutliple other data structures which provide
            //  faster access into portions of this list
            this._allResults = new List<Dictionary<string, object>>();

            // Models that completed with errors and all completed.
            // These are used to determine when we should abort because of too many
            //   errors
            this._errModels = new HashSet<ulong>();
            this._numErrModels = 0;
            this._completedModels = new HashSet<object>();
            this._numCompletedModels = 0;

            // Map of the model ID to index of result in _allResults
            this._modelIDToIdx = new Dictionary<ulong, int?>();

            // The global best result on the optimize metric so far, and the model ID
            this._bestResult = double.PositiveInfinity;
            this._bestModelID = (ulong?)null;

            // This is a dict of dicts. The top level dict has the swarmId as the key.
            // Each entry is a dict of genIdx: (modelId, errScore) entries.
            this._swarmBestOverall = new Dictionary<string, List<Tuple<ulong?, double?>>>();

            // For each swarm, we keep track of how many particles we have per generation
            // The key is the swarmId, the value is a list of the number of particles
            // at each generation
            this._swarmNumParticlesPerGeneration = new Dictionary<string, List<int>>();

            // The following variables are used to support the
            // getMaturedSwarmGenerations() call.

            // The _modifiedSwarmGens set contains the set of (swarmId, genIdx) tuples
            // that have had results reported to them since the last time
            // getMaturedSwarmGenerations() was called.

            // The maturedSwarmGens contains (swarmId,genIdx) tuples, one for each
            // swarm generation index which we have already detected has matured. This
            // insures that if by chance we get a rogue report from a model in a swarm
            // generation index which we have already assumed was matured that we won't
            // report on it again.
            this._modifiedSwarmGens = new HashSet<Tuple<string, int>>();
            this._maturedSwarmGens = new HashSet<Tuple<string, int>>();

            // For each particle, we keep track of it's best score (across all
            // generations) and the position it was at when it got that score. The keys
            // in this dict are the particleId, the values are (bestResult, position),
            // where position is a dict with varName:position items in it.
            this._particleBest = new Map<string, Tuple<double?, Map<string, int>>>();

            // For each particle, we keep track of it's latest generation index.
            this._particleLatestGenIdx = new Dictionary<string, int>();

            // For each swarm, we keep track of which models are in it. The key
            // is the swarmId, the value is a list of indexes into this._allResults.
            this._swarmIdToIndexes = new Dictionary<string, List<int>>();

            // ParamsHash to index mapping
            this._paramsHashToIndexes = new Dictionary<string, int?>();
        }

        /// <summary>
        /// Insert a new entry or update an existing one. 
        /// If this is an update of an existing entry, then modelParams will be None
        /// </summary>
        /// <param name="modelId">globally unique modelID of this model</param>
        /// <param name="modelParams">params dict for this model, or None if this is just an update of a model that it already previously reported on.
        /// See the comments for the createModels() method for a description of this dict.</param>
        /// <param name="modelParamsHash">hash of the modelParams dict, generated by the worker that put it into the model database.</param>
        /// <param name="metricResult">value on the optimizeMetric for this model. May be None if we have no results yet.</param>
        /// <param name="completed">True if the model has completed evaluation, False if it is still running(and these are online results)</param>
        /// <param name="completionReason">One of the ClientJobsDAO.CMPL_REASON_XXX equates</param>
        /// <param name="matured">True if this model has matured</param>
        /// <param name="numRecords">Number of records that have been processed so far by this model.</param>
        /// <returns>Canonicalized result on the optimize metric</returns>
        public double? update(ulong modelId, ModelParams modelParams, string modelParamsHash, double? metricResult,
           bool completed, string completionReason, bool matured, uint numRecords)
        {
            /* 
            modelParams is a dictionary containing the following elements:

                   structuredParams: dictionary containing all variables for
                     this model, with encoders represented as a dict within
                     this dict (or None if they are not included.

                   particleState: dictionary containing the state of this
                     particle. This includes the position and velocity of
                     each of it's variables, the particleId, and the particle
                     generation index. It contains the following keys:

                     id: The particle Id of the particle we are using to
                           generate/track this model. This is a string of the
                           form <hypesearchWorkerId>.<particleIdx>
                     genIdx: the particle's generation index. This starts at 0
                           and increments every time we move the particle to a
                           new position.
                     swarmId: The swarmId, which is a string of the form
                       <encoder>.<encoder>... that describes this swarm
                     varStates: dict of the variable states. The key is the
                         variable name, the value is a dict of the variable's
                         position, velocity, bestPosition, bestResult, etc.
            */

            // The modelParamsHash must always be provided - it can change after a
            //  model is inserted into the models table if it got detected as an
            //  orphan
            Debug.Assert(modelParamsHash != null);

            // We consider a model metricResult as "final" if it has completed or
            //  matured. By default, assume anything that has completed has matured
            if (completed)
            {
                matured = true;
            }

            // Get the canonicalized optimize metric results. For this metric, lower
            //  is always better
            double? errScore;
            if (metricResult != null && matured
                && new[] { BaseClientJobDao.CMPL_REASON_EOF, BaseClientJobDao.CMPL_REASON_STOPPED }.Contains(completionReason))
            {
                // Canonicalize the error score so that lower is better
                if (this._hsObj._maximize)
                {
                    errScore = -1 * metricResult;
                }
                else
                {
                    errScore = metricResult;
                }

                if (errScore < this._bestResult)
                {
                    this._bestResult = errScore;
                    this._bestModelID = modelId;
                    this._hsObj.logger.Info(string.Format("New best model after {0} evaluations: errScore {1} on model {2}"
                        , this._allResults.Count, this._bestResult, this._bestModelID));
                }
            }
            else
            {
                errScore = double.PositiveInfinity;
            }

            // If this model completed with an unacceptable completion reason, set the
            //  errScore to infinite and essentially make this model invisible to
            //  further queries
            bool hidden;
            if (completed && completionReason == BaseClientJobDao.CMPL_REASON_ORPHAN)
            {
                errScore = double.PositiveInfinity;
                hidden = true;
            }
            else
            {
                hidden = false;
            }

            // Update our set of erred models and completed models. These are used
            //  to determine if we should abort the search because of too many errors
            if (completed)
            {
                this._completedModels.Add(modelId);
                this._numCompletedModels = this._completedModels.Count;
                if (completionReason == BaseClientJobDao.CMPL_REASON_ERROR)
                {
                    this._errModels.Add(modelId);
                    this._numErrModels = this._errModels.Count;
                }
            }

            // Are we creating a new entry?
            bool wasHidden = false;
            string swarmId;
            if (!this._modelIDToIdx.ContainsKey(modelId))
            {
                Debug.Assert(modelParams != null);

                var entry = new Dictionary<string, object>
                {
                    {"modelID", modelId },
                    {"modelParams", modelParams },
                    {"modelParamsHash", modelParamsHash },
                    {"errScore", errScore },
                    {"completed", completed },
                    {"matured", matured },
                    {"numRecords", numRecords },
                    {"hidden", hidden },
                };

                //entry = dict(modelID = modelID, modelParams = modelParams,
                //             modelParamsHash = modelParamsHash,
                //             errScore = errScore, completed = completed,
                //             matured = matured, numRecords = numRecords, hidden = hidden);
                this._allResults.Add(entry);
                int entryIdx = this._allResults.Count - 1;
                this._modelIDToIdx[modelId] = entryIdx;

                this._paramsHashToIndexes[modelParamsHash] = entryIdx;

                swarmId = modelParams.particleState.swarmId;
                if (!hidden)
                {
                    // Update the list of particles in each swarm
                    if (this._swarmIdToIndexes.ContainsKey(swarmId))
                    {
                        this._swarmIdToIndexes[swarmId].Add(entryIdx);
                    }
                    else
                    {
                        this._swarmIdToIndexes[swarmId] = new List<int> { entryIdx };
                    }

                    // Update number of particles at each generation in this swarm
                    int genIdx1 = modelParams.particleState.genIdx;
                    List<int> numPsEntry = this._swarmNumParticlesPerGeneration.Get(swarmId, new List<int> { 0 });
                    while (genIdx1 >= numPsEntry.Count)
                    {
                        numPsEntry.Add(0);
                    }
                    numPsEntry[genIdx1] += 1;
                    this._swarmNumParticlesPerGeneration[swarmId] = numPsEntry;
                }
            }

            // Replacing an existing one
            else
            {
                int? entryIdx = this._modelIDToIdx.Get(modelId, null);
                Debug.Assert(entryIdx != null);
                Dictionary<string, object> entry = this._allResults[entryIdx.GetValueOrDefault()];
                wasHidden = (bool)entry["hidden"];

                // If the paramsHash changed, note that. This can happen for orphaned
                //  models
                if ((string)entry["modelParamsHash"] != modelParamsHash)
                {

                    this._paramsHashToIndexes.Remove((string)entry["modelParamsHash"]);
                    this._paramsHashToIndexes[modelParamsHash] = entryIdx.GetValueOrDefault();
                    entry["modelParamsHash"] = modelParamsHash;
                }

                // Get the model params, swarmId, and genIdx
                modelParams = (ModelParams)entry["modelParams"];
                swarmId = modelParams.particleState.swarmId;
                int genIdx2 = modelParams.particleState.genIdx;

                // If this particle just became hidden, remove it from our swarm counts
                if (hidden && !wasHidden)
                {
                    Debug.Assert(this._swarmIdToIndexes[swarmId].Contains(entryIdx.GetValueOrDefault()));
                    this._swarmIdToIndexes[swarmId].Remove(entryIdx.GetValueOrDefault());
                    this._swarmNumParticlesPerGeneration[swarmId][genIdx2] -= 1;
                }

                // Update the entry for the latest info
                entry["errScore"] = errScore;
                entry["completed"] = completed;
                entry["matured"] = matured;
                entry["numRecords"] = numRecords;
                entry["hidden"] = hidden;
            }

            // Update the particle best errScore
            string particleId = modelParams.particleState.id;
            int genIdx = modelParams.particleState.genIdx;
            if (matured && !hidden)
            {
                //(oldResult, pos) = this._particleBest.get(particleId, (numpy.inf, None));
                Tuple<double?, Map<string, int>> oldResultPos = this._particleBest.Get(particleId, new Tuple<double?, Map<string, int>>(double.PositiveInfinity, null));// TODO check!
                if (errScore < oldResultPos.Item1 /*oldResult*/)
                {
                    Map<string, int> pos1 = Particle.getPositionFromState(modelParams.particleState);
                    this._particleBest[particleId] = new Tuple<double?, Map<string, int>>(errScore, pos1);
                    // this._particleBest[particleId] = (errScore, pos1);
                }
            }

            // Update the particle latest generation index
            int prevGenIdx = this._particleLatestGenIdx.Get(particleId, -1);
            if (!hidden && genIdx > prevGenIdx)
            {
                this._particleLatestGenIdx[particleId] = genIdx;
            }
            else if (hidden && !wasHidden && genIdx == prevGenIdx)
            {
                this._particleLatestGenIdx[particleId] = genIdx - 1;
            }

            // Update the swarm best score
            if (!hidden)
            {
                swarmId = modelParams.particleState.swarmId;
                if (!this._swarmBestOverall.ContainsKey(swarmId))
                {
                    //this._swarmBestOverall[swarmId] = [];
                    this._swarmBestOverall[swarmId] = new List<Tuple<ulong?, double?>>();
                }

                List<Tuple<ulong?, double?>> bestScores = this._swarmBestOverall[swarmId];

                while (genIdx >= bestScores.Count)
                {
                    bestScores.Add(new Tuple<ulong?, double?>(null, double.PositiveInfinity));
                    //bestScores.append((None, numpy.inf));
                }
                if (errScore < bestScores[genIdx].Item2)
                {
                    bestScores[genIdx] = new Tuple<ulong?, double?>(modelId, errScore);
                }
            }

            // Update the this._modifiedSwarmGens flags to support the
            //   getMaturedSwarmGenerations() call.
            if (!hidden)
            {
                var key = new Tuple<string, int>(swarmId, genIdx);
                if (!this._maturedSwarmGens.Contains(key))
                {
                    this._modifiedSwarmGens.Add(key);
                }
            }

            return errScore;
        }

        /// <summary>
        /// Return number of models that completed with errors.
        /// </summary>
        /// <returns></returns>
        public int getNumErrModels()
        {
            return this._numErrModels;
        }
        /// <summary>
        /// Return list of models IDs that completed with errors.
        /// </summary>
        /// <returns></returns>
        public List<ulong> getErrModelIds()
        {
            return this._errModels.ToList();
        }
        /// <summary>
        /// Return total number of models that completed.
        /// </summary>
        /// <returns></returns>
        public int getNumCompletedModels()
        {
            return this._numCompletedModels;
        }
        /// <summary>
        /// Return the modelID of the model with the given paramsHash, or None if not found.
        /// </summary>
        /// <param name="paramsHash">paramsHash to look for</param>
        /// <returns>modelId, or None if not found</returns>
        public ulong? getModelIDFromParamsHash(string paramsHash)
        {
            int? entryIdx = this._paramsHashToIndexes.Get(paramsHash, null);
            if (entryIdx.HasValue)
            {
                return (ulong?)this._allResults[entryIdx.Value]["modelID"];
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Return the total // of models we have in our database (if swarmId is None) or in a specific swarm.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <param name="includeHidden">If False, this will only return the number of models that are not hidden(i.e.orphanned, etc.)</param>
        /// <returns>numModels</returns>
        public int numModels(string swarmId = null, bool includeHidden = false)
        {
            // Count all models
            if (includeHidden)
            {
                if (string.IsNullOrWhiteSpace(swarmId))
                {
                    return (this._allResults).Count;
                }

                else
                {
                    return (this._swarmIdToIndexes.Get(swarmId, new List<int>())).Count; ;
                }
            }
            // Only count non-hidden models
            else
            {
                List<Dictionary<string, object>> entries;
                if (string.IsNullOrWhiteSpace(swarmId))
                {
                    entries = this._allResults;
                }
                else
                {
                    //entries = [this._allResults[entryIdx]
                    //         for entryIdx in this._swarmIdToIndexes.get(swarmId,[])];
                    var entryIdxs = _swarmIdToIndexes.Get(swarmId, new List<int>());
                    entries = _allResults.Where((v, i) => entryIdxs.Contains(i)).ToList();
                }

                //return len([entry for entry in entries if not entry["hidden"]]);
                return entries.Count(e => (bool)e["hidden"]);
            }
        }
        /// <summary>
        /// Return the model ID of the model with the best result so far and
        /// it's score on the optimize metric. If swarm is None, then it returns
        /// the global best, otherwise it returns the best for the given swarm
        /// for all generatons up to and including genIdx.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <param name="genIdx">consider the best in all generations up to and including this generation if not None.</param>
        /// <returns>(modelID, result)</returns>
        public Tuple<ulong?, double?> bestModelIdAndErrScore(string swarmId = null, int? genIdx = null)
        {
            if (string.IsNullOrWhiteSpace(swarmId))
            {
                return new Tuple<ulong?, double?>(this._bestModelID, this._bestResult);
            }

            else
            {
                if (!this._swarmBestOverall.ContainsKey(swarmId))
                {
                    return new Tuple<ulong?, double?>(null, double.PositiveInfinity);
                }


                // Get the best score, considering the appropriate generations
                var genScores = this._swarmBestOverall[swarmId];
                ulong? bestModelId = null;
                double? bestScore = double.PositiveInfinity;

                //for (i, (modelId, errScore)) in enumerate(genScores)
                for (int i = 0; i < genScores.Count; i++)
                //foreach (var item in genScores)
                {
                    var modelId = genScores[i].Item1;
                    var errScore = genScores[i].Item2;
                    if (genIdx.HasValue && i > genIdx)
                    {
                        break;
                    }
                    if (errScore < bestScore)
                    {
                        bestScore = errScore;
                        bestModelId = modelId;
                    }
                }

                return new Tuple<ulong?, double?>(bestModelId, bestScore);
            }
        }
        /// <summary>
        /// Return particle info for a specific modelId.
        /// </summary>
        /// <param name="modelId">which model Id</param>
        /// <returns>(particleState, modelId, errScore, completed, matured)</returns>
        public ParticleInfo getParticleInfo(ulong modelId)
        {
            ParticleInfo retVal = new ParticleInfo();

            Dictionary<string, object> entry = this._allResults[this._modelIDToIdx[modelId].GetValueOrDefault()];

            retVal.particleState = ((ModelParams)entry["modelParams"]).particleState;
            retVal.modelId = modelId;
            retVal.errScore = (double)entry["errScore"];
            retVal.completed = (bool)entry["completed"];
            retVal.matured = (bool)entry["matured"];

            //entry = this._allResults[this._modelIDToIdx[modelId]];
            //return (entry["modelParams"]["particleState"], modelId, entry["errScore"],
            //entry["completed"], entry["matured"]);
            return retVal;
        }

        public class ParticleInfo
        {
            public ParticleStateModel particleState { get; set; }
            public ulong modelId { get; set; }
            public double errScore { get; set; }
            public bool completed { get; set; }
            public bool matured { get; set; }
        }

        /// <summary>
        /// Return a list of particleStates for all particles we know about 
        /// in the given swarm, their model Ids, and metric results.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <param name="genIdx">If not None, only return particles at this specific generation index</param>
        /// <param name="completed">If not None, only return particles of the given state (either completed if 'completed' is True, or running if 'completed' is false</param>
        /// <param name="matured">If not None, only return particles of the given state 
        /// (either matured if 'matured' is True, or not matured if 'matured' is false.Note that any model which has completed is also
        /// considered matured.</param>
        /// <param name="lastDescendent">If True, only return particles that are the last descendent, that is, the highest generation index for a given particle Id</param>
        /// <returns>(particleStates, modelIds, errScores, completed, matured) 
        /// particleStates: list of particleStates
        /// modelIds: list of modelIds
        /// errScores: list of errScores, numpy.inf is plugged in if we don't have a result yet
        /// completed: list of completed booleans
        /// matured: list of matured booleans
        /// </returns>
        public ParticleInfos getParticleInfos(string swarmId = null, int? genIdx = null, bool? completed = null,
                       bool? matured = null, bool lastDescendent = false)
        {
            // The indexes of all the models in this swarm. This list excludes hidden
            //  (orphaned) models.
            List<int> entryIdxs;
            if (!string.IsNullOrWhiteSpace(swarmId))
            {
                entryIdxs = this._swarmIdToIndexes.Get(swarmId, new List<int>());
            }
            else
            {
                entryIdxs = ArrayUtils.Range(0, (this._allResults).Count).ToList();
            }
            if (entryIdxs.Count == 0)
            {
                return new ParticleInfos();
            }

            // Get the particles of interest
            ParticleInfos infos = new ParticleInfos();
            foreach (int idx in entryIdxs)
            {
                var entry = this._allResults[idx];

                // If this entry is hidden (i.e. it was an orphaned model), it should
                //  not be in this list
                if (!string.IsNullOrWhiteSpace(swarmId))
                {
                    Debug.Assert(((bool)entry["hidden"]) == false);
                }

                // Get info on this model
                var modelParams = (ModelParams)entry["modelParams"];
                bool isCompleted = (bool)entry["completed"];
                bool isMatured = (bool)entry["matured"];
                ParticleStateModel particleState = modelParams.particleState;
                int particleGenIdx = particleState.genIdx;
                string particleId = particleState.id;

                if (genIdx.HasValue && particleGenIdx != genIdx)
                {
                    continue;
                }

                if (completed.HasValue && (completed != isCompleted))
                {
                    continue;
                }

                if (matured.HasValue && (matured != isMatured))
                {
                    continue;
                }

                if (lastDescendent && (this._particleLatestGenIdx[particleId] != particleGenIdx))
                {
                    continue;
                }

                // Incorporate into return values
                infos.particleStates.Add(particleState);
                infos.modelIds.Add((ulong)entry["modelID"]);
                infos.errScores.Add((double)entry["errScore"]);
                infos.completedFlags.Add(isCompleted);
                infos.maturedFlags.Add(isMatured);
            }

            return infos;
        }

        public class ParticleInfos
        {
            public ParticleInfos()
            {
                particleStates = new List<ParticleStateModel>();
                modelIds = new List<ulong>();
                errScores = new List<double>();
                completedFlags = new List<bool>();
                maturedFlags = new List<bool>();
            }
            public List<ParticleStateModel> particleStates { get; set; }
            public List<ulong> modelIds { get; set; }
            public List<double> errScores { get; set; }
            public List<bool> completedFlags { get; set; }
            public List<bool> maturedFlags { get; set; }
        }

        /// <summary>
        /// Return a list of particleStates for all particles in the given swarm generation that have been orphaned.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <param name="genIdx">If not None, only return particles at this specific generation index.</param>
        /// <returns>
        /// (particleStates, modelIds, errScores, completed, matured)
        /// particleStates: list of particleStates
        /// modelIds: list of modelIds
        /// errScores: list of errScores, numpy.inf is plugged in if we don't have a result yet
        /// completed: list of completed booleans
        /// matured: list of matured booleans
        /// </returns>
        public ParticleInfos getOrphanParticleInfos(string swarmId, int? genIdx)
        {
            var entryIdxs = ArrayUtils.Range(0, this._allResults.Count).ToList();
            if (entryIdxs.Count == 0)
            {
                return new ParticleInfos();
            }

            // Get the particles of interest
            ParticleInfos infos = new ParticleInfos();
            foreach (var idx in entryIdxs)
            {
                // Get info on this model
                var entry = this._allResults[idx];
                if (!(bool)entry["hidden"])
                {
                    continue;
                }

                var modelParams = (ModelParams)entry["modelParams"];
                if (modelParams.particleState.swarmId != swarmId)
                {
                    continue;
                }

                bool isCompleted = (bool)entry["completed"];
                bool isMatured = (bool)entry["matured"];
                ParticleStateModel particleState = modelParams.particleState;
                int particleGenIdx = particleState.genIdx;
                string particleId = particleState.id;

                if (genIdx.HasValue && particleGenIdx != genIdx)
                {
                    continue;
                }

                // Incorporate into return values
                infos.particleStates.Add(particleState);
                infos.modelIds.Add((ulong)entry["modelID"]);
                infos.errScores.Add((double)entry["errScore"]);
                infos.completedFlags.Add(isCompleted);
                infos.maturedFlags.Add(isMatured);
            }

            return infos;
        }
        /// <summary>
        /// Return a list of swarm generations that have completed and the best(minimal) errScore seen for each of them.
        /// </summary>
        /// <returns>
        /// list of tuples.Each tuple is of the form (swarmId, genIdx, bestErrScore)
        /// </returns>
        public List<MaturedSwarmTuple> getMaturedSwarmGenerations()
        {
            // Return results go in this list
            var result = new List<MaturedSwarmTuple>();


            // For each of the swarm generations which have had model result updates
            // since the last time we were called, see which have completed.
            var modifiedSwarmGens = this._modifiedSwarmGens.OrderBy(i => i.Item1).ToList();

            // Walk through them in order from lowest to highest generation index
            foreach (var key in modifiedSwarmGens)
            {
                //(swarmId, genIdx) = key;
                string swarmId = key.Item1;
                int genIdx = key.Item2;

                // Skip it if we've already reported on it. This should happen rarely, if
                //  ever. It means that some worker has started and completed a model in
                //  this generation after we've determined that the generation has ended.
                if (this._maturedSwarmGens.Contains(key))
                {
                    this._modifiedSwarmGens.Remove(key);
                    continue;
                }

                // If the previous generation for this swarm is not complete yet, don't
                //  bother evaluating this one.
                if (genIdx >= 1 && !this._maturedSwarmGens.Contains(new Tuple<string, int>(swarmId, genIdx - 1)))
                {
                    continue;
                }

                // We found a swarm generation that had some results reported since last
                // time, see if it's complete or not
                var particleInfos = this.getParticleInfos(swarmId, genIdx);
                var maturedFlags = particleInfos.maturedFlags.ToArray();
                int numMatured = maturedFlags.Count(f => f);
                if (numMatured >= this._hsObj._minParticlesPerSwarm && numMatured == maturedFlags.Length)
                {
                    var errScores = particleInfos.errScores.ToArray();
                    double bestScore = errScores.Min();

                    this._maturedSwarmGens.Add(key);
                    this._modifiedSwarmGens.Remove(key);
                    result.Add(new MaturedSwarmTuple { swarmId = swarmId, genIdx = genIdx, bestScore = bestScore });
                }
            }

            // Return results
            return result;
        }

        public class MaturedSwarmTuple
        {
            public string swarmId;
            public int genIdx;
            public double bestScore;
        }
        /// <summary>
        /// Return the generation index of the first generation in the given swarm that does not have numParticles particles in it, 
        /// either still in the running state or completed.This does not include orphaned particles.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <param name="minNumParticles">minium number of partices required for a full generation.</param>
        /// <returns>generation index, or None if no particles at all.</returns>
        public int? firstNonFullGeneration(string swarmId, int? minNumParticles)
        {
            if (!this._swarmNumParticlesPerGeneration.ContainsKey(swarmId))
            {
                return null;
            }

            var numPsPerGen = this._swarmNumParticlesPerGeneration[swarmId].ToArray();

            //numPsPerGen = numpy.array(numPsPerGen);
            //firstNonFull = numpy.where(numPsPerGen < minNumParticles)[0];
            var firstNonFull = numPsPerGen.Where(x => x < minNumParticles).ToArray();
            if (firstNonFull.Length == 0)
            {
                return numPsPerGen.Length;
            }
            else
            {
                return firstNonFull[0];
            }
        }
        /// <summary>
        /// Return the generation index of the highest generation in the given swarm.
        /// </summary>
        /// <param name="swarmId">A string representation of the sorted list of encoders in this swarm.For example '__address_encoder.__gym_encoder'</param>
        /// <returns>generation index</returns>
        public int highestGeneration(string swarmId)
        {
            var numPsPerGen = this._swarmNumParticlesPerGeneration[swarmId];
            return numPsPerGen.Count - 1;
        }
        /// <summary>
        /// Return the best score and position for a given particle. The position
        /// is given as a dict, with varName:varPosition items in it.
        /// </summary>
        /// <param name="particleId">which particle</param>
        /// <returns>(bestResult, bestPosition)</returns>
        public Tuple<double?, Map<string, int>> getParticleBest(string particleId)
        {
            return this._particleBest.Get(particleId, null);
        }
        /// <summary>
        /// Return a dict of the errors obtained on models that were run with each value from a PermuteChoice variable.
        /// 
        /// For example, if a PermuteChoice variable has the following choices:
        /// ["a', 'b', 'c"]
        /// The dict will have 3 elements.The keys are the stringified choiceVars,
        /// and each value is tuple containing(choiceVar, errors) where choiceVar is
        /// the original form of the choiceVar (before stringification) and errors is
        /// the list of errors received from models that used the specific choice:
        /// retval:
        /// ["a':('a', [0.1, 0.2, 0.3]), 'b':('b', [0.5, 0.1, 0.6]), 'c':('c', [])]
        /// </summary>
        /// <param name="swarmId">swarm Id of the swarm to retrieve info from</param>
        /// <param name="maxGenIdx">max generation index to consider from other models, ignored if None</param>
        /// <param name="varName">which variable to retrieve</param>
        /// <returns>list of the errors obtained from each choice.</returns>
        public IList<Tuple<int, List<double>>> getResultsPerChoice(string swarmId, int? maxGenIdx, string varName)
        {

            var results = new List<Tuple<int, List<double>>>();
            // Get all the completed particles in this swarm
            //(allParticles, _, resultErrs, _, _) = this.getParticleInfos(swarmId,
            //                                          genIdx = None, matured = True);
            var particleInfos = this.getParticleInfos(swarmId, genIdx: null, matured: true);

            //for( particleState, resultErr in itertools.izip(allParticles, resultErrs))
            foreach (var tuple in ArrayUtils.Zip(particleInfos.particleStates, particleInfos.errScores))
            {
                ParticleStateModel particleState = (ParticleStateModel)tuple.Get(0);
                double resultErr = (double)tuple.Get(1);
                // Consider this generation?
                if (maxGenIdx.HasValue)
                {
                    if (particleState.genIdx > maxGenIdx)
                    {
                        continue;
                    }
                }

                // Ignore unless this model completed successfully
                if (resultErr == double.PositiveInfinity)
                {
                    continue;
                }

                var position = Particle.getPositionFromState(particleState);
                int varPosition = position[varName];
                //var varPositionStr = varPosition.ToString();
                //if (results.ContainsKey(varPositionStr))
                if (results.Any(r => r.Item1 == varPosition))
                {
                    results[varPosition].Item2.Add(resultErr);
                    //results[varPositionStr][1].Add(resultErr);
                }
                else
                {
                    //results[varPositionStr] = (varPosition, [resultErr]);
                    //results[varPositionStr] = new Dictionary<int, List<double>> { { varPosition, new List<double> { resultErr } } };
                    results[varPosition] = new Tuple<int, List<double>>(varPosition, new List<double> { resultErr });
                }
            }

            return results;
        }

    }

    /// <summary>
    /// Construct a particle. Each particle evaluates one or more models
    /// serially.Each model represents a position that the particle is evaluated at.
    /// 
    /// Each position is a set of values chosen for each of the permutation variables.
    /// The particle's best position is the value of the permutation variables when it
    /// did best on the optimization metric.
    /// 
    /// 
    /// Some permutation variables are treated like traditional particle swarm
    /// variables - that is they have a position and velocity. Others are simply
    /// choice variables, for example a list of strings.We follow a different
    /// methodology for choosing each permutation variable value depending on its type.
    /// 
    /// A particle belongs to 1 and only 1 swarm.A swarm is a collection of particles
    /// that all share the same global best position. A swarm is identified by its
    /// specific combination of fields.If we are evaluating multiple different field
    /// combinations, then there will be multiple swarms. A Hypersearch Worker (HSW)
    /// will only instantiate and run one particle at a time.When done running a
    /// particle, another worker can pick it up, pick a new position, for it and run
    /// it based on the particle state information which is stored in each model table
    /// entry.
    /// 
    /// Each particle has a generationIdx. It starts out at generation #0. Every time
    /// a model evaluation completes and the particle is moved to a different position
    /// (to evaluate a different model), the generation index is incremented.
    /// 
    /// Every particle that is created has a unique particleId.The particleId
    /// is a string formed as '<workerConnectionId>.<particleIdx>', where particleIdx
    /// starts at 0 for each worker and increments by 1 every time a new particle
    /// is created by that worker.
    /// </summary>
    public class Particle
    {
        private static int _nextParticleID = 0;
        private HypersearchV2 _hsObj;
        internal readonly ILog logger = LogManager.GetLogger(typeof(Particle));
        private ResultsDB _resultsDB;
        private MersenneTwister _rng;

        private Dictionary<string, PermuteVariable> permuteVars;
        private int genIdx;
        private string swarmId;
        private string particleId;

        /// <summary>
        /// Create a particle.
        ///   There are 3 fundamentally different methods of instantiating a particle:
        ///   1.) You can instantiate a new one from scratch, at generation index #0. This
        ///         particle gets a new particleId.
        ///           required: swarmId
        ///           optional: newFarFrom
        ///           must be None: evolveFromState, newFromClone
        /// 
        ///   2.) You can instantiate one from savedState, in which case it's generation
        ///         index is incremented(from the value stored in the saved state) and
        ///        its particleId remains the same.
        ///          required: evolveFromState
        ///          optional:
        /// 
        ///          must be None: flattenedPermuteVars, swarmId, newFromClone
        /// 
        ///   3.) You can clone another particle, creating a new particle at the same
        ///         generationIdx but a different particleId.This new particle will end
        ///        up at exactly the same position as the one it was cloned from.If
        /// 
        ///        you want to move it to the next position, or just jiggle it a bit, call
        ///        newPosition() or agitate() after instantiation.
        ///          required: newFromClone
        ///          optional:
        /// 
        ///          must be None: flattenedPermuteVars, swarmId, evolveFromState
        /// </summary>
        /// <param name="hsObj">The HypersearchV2 instance</param>
        /// <param name="resultsDB">the ResultsDB instance that holds all the model results</param>
        /// <param name="flattenedPermuteVars">dict() containing the (key, PermuteVariable) pairs
        /// of the  flattened permutation variables as read from the permutations file</param>
        /// <param name="swarmId">String that represents the encoder names of the encoders that are 
        /// to be included in this particle's model. Of the form 'encoder1.encoder2'.
        /// Required for creation method #1.
        /// </param>
        /// <param name="newFarFrom">If not None, this is a list of other particleState dicts in the
        /// swarm that we want to be as far away from as possible.
        /// Optional argument for creation method #1.</param>
        /// <param name="evolveFromState">If not None, evolve an existing particle. 
        /// This is a dict containing the particle's state. Preserve the particleId, 
        /// but increment the generation index.Required for creation method #2.</param>
        /// <param name="newFromClone">If not None, clone this other particle's position and generation
        /// index, with small random perturbations.This is a dict containing the
        /// particle's state. Required for creation method #3.</param>
        /// <param name="newParticleId">Only applicable when newFromClone is True. Give the clone a new particle ID.</param>
        public Particle(HypersearchV2 hsObj, ResultsDB resultsDB, Dictionary<string, PermuteVariable> flattenedPermuteVars,
                   string swarmId = null, List<ParticleStateModel> newFarFrom = null, ParticleStateModel evolveFromState = null,
                   ParticleStateModel newFromClone = null, bool newParticleId = false)
        {
            // Save constructor arguments
            this._hsObj = hsObj;
            this.logger = hsObj.logger;
            this._resultsDB = resultsDB;

            // See the random number generator used for all the variables in this
            // particle. We will seed it differently based on the construction method,
            // below.
            this._rng = new MersenneTwister(42);
            //this._rng.seed(42);

            Action<Dictionary<string, PermuteVariable>> _setupVars = flattenedPermVars =>
            {
                var allowedEncoderNames = (swarmId ?? "").Split('.');
                this.permuteVars = new Dictionary<string, PermuteVariable>(flattenedPermVars); // copy.deepcopy(flattenedPermuteVars);

                // Remove fields we don't want.
                var varNames = flattenedPermVars.Keys;
                foreach (string varName in varNames)
                {
                    // Remove encoders we're not using
                    if (varName.Contains(":"))    // if an encoder
                    {
                        if (!allowedEncoderNames.Contains(varName.Split(':')[0]))
                        {
                            this.permuteVars.Remove(varName);
                            continue;
                        }
                    }

                    // All PermuteChoice variables need to know all prior results obtained
                    // with each choice.
                    if (this.permuteVars[varName] is PermuteChoices)
                    {
                        int? maxGenIdx;
                        if (this._hsObj._speculativeParticles)
                        {
                            maxGenIdx = null;
                        }
                        else
                        {
                            maxGenIdx = this.genIdx - 1;
                        }

                        var resultsPerChoice = this._resultsDB.getResultsPerChoice(
                            swarmId: swarmId, maxGenIdx: maxGenIdx, varName: varName);
                        ((PermuteChoices)this.permuteVars[varName]).setResultsPerChoice(resultsPerChoice);
                    }
                }
            };

            #region Method #1
            // Method #1
            // Create from scratch, optionally pushing away from others that already exist.
            if (!string.IsNullOrWhiteSpace(swarmId))
            {
                Debug.Assert(evolveFromState == null);
                Debug.Assert(newFromClone == null);

                // Save construction param
                this.swarmId = swarmId;

                // Assign a new unique ID to this particle
                this.particleId = string.Format("{0}.{1}", this._hsObj._workerID, _nextParticleID);
                _nextParticleID += 1;

                // Init the generation index
                this.genIdx = 0;

                // Setup the variables to initial locations.
                _setupVars(flattenedPermuteVars);

                // Push away from other particles?
                if (newFarFrom != null)
                {
                    foreach (var varName in this.permuteVars.Keys)
                    {
                        var otherPositions = new List<double>();
                        foreach (var particleState in newFarFrom)
                        {
                            otherPositions.Add(particleState.varStates[varName].position.GetValueOrDefault());
                        }
                        this.permuteVars[varName].pushAwayFrom(otherPositions, this._rng);

                        // Give this particle a unique seed.
                        this._rng = new MersenneTwister(otherPositions.GetHashCode());
                        //this._rng.Seed(str(otherPositions));
                    }
                }
            }
            #endregion

            #region Method #2

            // Method #2
            // Instantiate from saved state, preserving particleId but incrementing
            //  generation index.
            else if (evolveFromState != null)
            {
                Debug.Assert(swarmId == null);
                Debug.Assert(newFarFrom == null);
                Debug.Assert(newFromClone == null);

                // Setup other variables from saved state
                this.particleId = evolveFromState.id;
                this.genIdx = evolveFromState.genIdx + 1;
                this.swarmId = evolveFromState.swarmId;

                // Setup the variables to initial locations.
                _setupVars(flattenedPermuteVars);

                // Override the position and velocity of each variable from
                //  saved state
                this.initStateFrom(this.particleId, evolveFromState, newBest: true);

                // Move it to the next position. We need the swarm best for this.
                this.newPosition();
            }

            #endregion

            #region Method #3

            // Method #3
            // Clone another particle, producing a new particle at the same genIdx with
            //  the same particleID. This is used to re-run an orphaned model.
            else if (newFromClone != null)
            {
                Debug.Assert(swarmId == null);
                Debug.Assert(newFarFrom == null);
                Debug.Assert(evolveFromState == null);

                // Setup other variables from clone particle
                this.particleId = newFromClone.id;
                if (newParticleId)
                {
                    this.particleId = string.Format("{0}.{1}", this._hsObj._workerID, _nextParticleID);
                    _nextParticleID += 1;
                }

                this.genIdx = newFromClone.genIdx;
                this.swarmId = newFromClone.swarmId;

                // Setup the variables to initial locations.
                _setupVars(flattenedPermuteVars);

                // Override the position and velocity of each variable from
                //  the clone
                this.initStateFrom(this.particleId, newFromClone, newBest: false);
            }

            #endregion

            else
            {
                Debug.Assert(false, "invalid creation parameters");
            }

            // Log it
            this.logger.Debug(string.Format("Created particle: {0}", this));
        }

        /// <summary>
        /// Get the particle state as a dict. This is enough information to instantiate this particle on another worker.
        /// </summary>
        /// <returns></returns>
        public ParticleStateModel getState()
        {
            Dictionary<string, VarState> varStates = new Dictionary<string, VarState>();
            foreach (KeyValuePair<string, PermuteVariable> permuteVar in this.permuteVars)
            {
                varStates[permuteVar.Key] = permuteVar.Value.getState();
            }
            //for (varName, var in this.permuteVars.iteritems())
            //{
            //    varStates[varName] = var.getState();
            //}

            return new ParticleStateModel
            {
                id = this.particleId,
                genIdx = this.genIdx,
                swarmId = this.swarmId,
                varStates = varStates
            };
        }

        /// <summary>
        /// Init all of our variable positions, velocities, and optionally the best
        /// result and best position from the given particle.
        /// If newBest is true, we get the best result and position for this new
        /// generation from the resultsDB, This is used when evoloving a particle
        /// because the bestResult and position as stored in was the best AT THE TIME
        /// THAT PARTICLE STARTED TO RUN and does not include the best since that
        /// particle completed.
        /// </summary>
        /// <param name="particleId"></param>
        /// <param name="particleState"></param>
        /// <param name="newBest"></param>
        private void initStateFrom(string particleId, ParticleStateModel particleState, bool newBest)
        {
            // Get the update best position and result?
            double? bestResult;
            Dictionary<string, int> bestPosition;
            if (newBest)
            {
                var tuplePositions = this._resultsDB.getParticleBest(particleId);
                bestResult = tuplePositions.Item1;
                bestPosition = tuplePositions.Item2;
                //(bestResult, bestPosition) = this._resultsDB.getParticleBest(particleId);
            }
            else
            {
                bestResult = null;
                bestPosition = null;
            }

            // Replace with the position and velocity of each variable from
            //  saved state
            Dictionary<string, VarState> varStates = particleState.varStates;
            foreach (var varName in varStates.Keys)
            {
                VarState varState = varStates[varName].Clone();  //copy.deepcopy(varStates[varName]);
                if (newBest)
                {
                    varState.bestResult = bestResult;
                }
                if (bestPosition != null)
                {
                    varState.bestPosition = bestPosition[varName];
                }
                this.permuteVars[varName].setState(varState);
            }
        }
        /// <summary>
        /// Copy all encoder variables from particleState into this particle.
        /// </summary>
        /// <param name="particleState">dict produced by a particle's getState() method</param>
        public void copyEncoderStatesFrom(ParticleStateModel particleState)
        {
            // Set this to false if you don't want the variable to move anymore
            //  after we set the state
            bool allowedToMove = true;

            foreach (var varName in particleState.varStates.Keys)
            {
                if (varName.Contains(":"))    // if an encoder
                {

                    // If this particle doesn't include this field, don't copy it
                    if (!this.permuteVars.ContainsKey(varName))
                    {
                        continue;
                    }

                    // Set the best position to the copied position
                    VarState state = particleState.varStates[varName].Clone();
                    state._position = state.position;
                    state.bestPosition = state.position;

                    if (!allowedToMove)
                    {
                        state.velocity = 0;
                    }

                    // Set the state now
                    this.permuteVars[varName].setState(state);

                    if (allowedToMove)
                    {
                        // Let the particle move in both directions from the best position
                        //  it found previously and set it's initial velocity to a known
                        //  fraction of the total distance.
                        this.permuteVars[varName].resetVelocity(this._rng);
                    }
                }
            }
        }

        /// <summary>
        /// Copy specific variables from particleState into this particle.
        /// </summary>
        /// <param name="particleState">dict produced by a particle's getState() method</param>
        /// <param name="varNames">which variables to copy</param>
        public void copyVarStatesFrom(ParticleStateModel particleState, List<string> varNames)
        {
            // Set this to false if you don't want the variable to move anymore
            //  after we set the state
            bool allowedToMove = true;

            foreach (var varName in particleState.varStates.Keys)
            {
                if (varNames.Contains(varName))
                {
                    // If this particle doesn't include this field, don't copy it
                    if (!this.permuteVars.ContainsKey(varName))
                    {
                        continue;
                    }

                    // Set the best position to the copied position
                    var state = particleState.varStates[varName].Clone();
                    state._position = state.position;
                    state.bestPosition = state.position;

                    if (!allowedToMove)
                    {
                        state.velocity = 0;
                    }

                    // Set the state now
                    this.permuteVars[varName].setState(state);

                    if (allowedToMove)
                    {
                        // Let the particle move in both directions from the best position
                        //  it found previously and set it's initial velocity to a known
                        //  fraction of the total distance.
                        this.permuteVars[varName].resetVelocity(this._rng);
                    }
                }
            }
        }
        /// <summary>
        /// Return the position of this particle. This returns a dict() of key 
        /// value pairs where each key is the name of the flattened permutation
        /// variable and the value is its chosen value.
        /// </summary>
        /// <returns>dict() of flattened permutation choices</returns>
        public Map<string, object> getPosition()
        {
            Map<string, object> result = new Map<string, object>();
            foreach (var pair in this.permuteVars)
            //for (varName, value) in this.permuteVars.iteritems()
            {
                result[pair.Key] = pair.Value.getPosition();
            }

            return result;
        }
        /// <summary>
        /// Agitate this particle so that it is likely to go to a new position.
        /// Every time agitate is called, the particle is jiggled an even greater
        /// amount.
        /// </summary>
        public void agitate()
        {
            foreach (var pair in this.permuteVars)
            //for (varName, var) in this.permuteVars.iteritems()
            {
                pair.Value.agitate();
            }

            this.newPosition();
        }
        /// <summary>
        /// Choose a new position based on results obtained so far from all other particles.
        /// </summary>
        /// <param name="whichVars">If not None, only move these variables</param>
        /// <returns>new position</returns>
        public Dictionary<string, object> newPosition(List<string> whichVars = null)
        {
            // TODO: incorporate data from choice variables....
            // TODO: make sure we're calling this when appropriate.

            // Get the global best position for this swarm generation
            Dictionary<string, int> globalBestPosition = null;
            // If speculative particles are enabled, use the global best considering
            //  even particles in the current generation. This gives better results
            //  but does not provide repeatable results because it depends on
            //  worker timing
            if (this._hsObj._speculativeParticles)
            {
                genIdx = this.genIdx;
            }
            else
            {
                genIdx = this.genIdx - 1;
            }

            if (genIdx >= 0)
            {
                //(bestModelId, _) = this._resultsDB.bestModelIdAndErrScore(this.swarmId, genIdx);
                var tuple = this._resultsDB.bestModelIdAndErrScore(this.swarmId, genIdx);
                var bestModelId = tuple.Item1;
                if (bestModelId != null)
                {
                    var particleInfo = this._resultsDB.getParticleInfo(bestModelId.Value);
                    globalBestPosition = getPositionFromState(particleInfo.particleState);
                }
            }

            // Update each variable
            foreach (var pair in this.permuteVars)
            {
                string varName = pair.Key;
                PermuteVariable var = pair.Value;
                if (whichVars != null && !whichVars.Contains(varName))
                {
                    continue;
                }
                if (globalBestPosition == null)
                {
                    var.newPosition(null, this._rng);
                }
                else
                {
                    var.newPosition(globalBestPosition[varName], this._rng);
                }
            }

            // get the new position
            Dictionary<string, object> position = this.getPosition();

            // Log the new position
            if (this.logger.IsDebugEnabled)
            {
                var msg = new StringBuilder();
                //msg = StringIO.StringIO();
                msg.AppendFormat("New particle position: {0}", Arrays.ToString(position));
                //print >> msg, "New particle position: \n%s" % (pprint.pformat(position,
                //                                                indent = 4));
                msg.Append("Particle variables:");
                //print >> msg, "Particle variables:";
                foreach (var pair in this.permuteVars)
                {
                    msg.AppendFormat("  {0}: {1}", pair.Key, pair.Value);
                }
                //for (varName, var) in this.permuteVars.iteritems()
                //{
                //    print >> msg, "  %s: %s" % (varName, str(var));
                //}
                this.logger.Debug(msg.ToString());
                //msg.close();
            }

            return position;
        }
        /// <summary>
        /// Return the position of a particle given its state dict.
        /// </summary>
        /// <param name="pState"></param>
        /// <returns>dict() of particle position, keys are the variable names, values are their positions</returns>
        public static Map<string, int> getPositionFromState(ParticleStateModel pState)
        {
            Map<string, int> result = new Map<string, int>();
            foreach (var pair in pState.varStates)
            //for (varName, value) in pState["varStates"].iteritems()
            {
                result[pair.Key] = pair.Value.position.GetValueOrDefault();
            }

            return result;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("Particle(swarmId={0}) [particleId={1}, genIdx={2}d, permuteVars=\n{3}]",
                this.swarmId, this.particleId, this.genIdx, Arrays.ToString(this.permuteVars));
        }

        #endregion
    }

    public class SwarmEncoderState
    {
        public SwarmStatus status { get; set; }
        public ulong? bestModelId { get; set; }
        public double? bestErrScore { get; set; }
        public int sprintIdx { get; set; }
    }

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
                this._hsObj._cjDAO.jobSetFieldIfEqual(
                    this._hsObj._jobID, "engWorkerState", JsonConvert.SerializeObject(this._state, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }), null);

                this._priorStateJSON = (string)this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID, new[] { "engWorkerState" })[0];
                Debug.Assert(this._priorStateJSON != null);
            }

            // Read state from the database
            this._state = (HsStateModel)JsonConvert.DeserializeObject(this._priorStateJSON, typeof(HsStateModel), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
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

    public enum SwarmStatus
    {
        none,
        active,
        completing,
        completed,
        killed
    }

    public class HsStateModel
    {
        public DateTime lastUpdateTime;
        public bool searchOver;
        public List<string> activeSwarms;
        public Dictionary<string, SwarmEncoderState> swarms;
        public List<string> blackListedEncoders;
        public List<SwarmEncoderState> sprints;
        public int? lastGoodSprint;
    }

    public enum HsSearchType
    {
        temporal,
        classification,
        legacyTemporal
    }

    [Serializable]
    public class HyperSearchSearchParams
    {
        public string persistentJobGUID;
        public string permutationsPyFilename;
        public BasePermutations permutationsPyContents;
        [NonSerialized]
        public JObject description;
        public bool? createCheckpoints;
        public bool? useTerminators;
        public int? maxModels;
        public bool? dummyModel;
        public bool? speculativeParticles;
        public int? predictionCacheMaxRecords;
        // Added myself, is the description holder (file)
        public string baseDescriptionFileName;
        public string hsVersion = "v2";
        public BaseDescription descriptionPyContents;

        public void Populate(Map<string, object> jobParamsMap)
        {
            this.persistentJobGUID = (string)jobParamsMap["persistentJobGUID"];
            this.descriptionPyContents = (BaseDescription)jobParamsMap["descriptionPyContents"];
            this.permutationsPyContents = (BasePermutations)jobParamsMap["permutationsPyContents"];
            this.maxModels = TypeConverter.Convert<int?>(jobParamsMap.Get("maxModels"));
            this.hsVersion = (string)jobParamsMap["hsVersion"];
        }
    }


    [Flags]
    public enum InferenceType
    {
        None = 0,
        TemporalNextStep = 1,
        TemporalClassification = 2,
        NontemporalClassification = 4,
        TemporalAnomaly = 8,
        NontemporalAnomaly = 16,
        TemporalMultiStep = 32,
        NontemporalMultiStep = 64,
        MultiStep = 128
    }

    public static class InferenceTypeHelper
    {
        public static bool IsTemporal(InferenceType infType)
        {
            return new[]
            {
                InferenceType.TemporalNextStep,
                InferenceType.TemporalClassification,
                InferenceType.TemporalAnomaly,
                InferenceType.TemporalMultiStep,
                InferenceType.NontemporalMultiStep
            }.Contains(infType);
        }

    }

    /// <summary>
    /// The v2 Hypersearch implementation. This is one example of a Hypersearch
    /// implementation that can be used by the HypersearchWorker. Other implementations
    /// just have to implement the following methods:
    /// 
    ///   createModels()
    ///   recordModelProgress()
    ///   getPermutationVariables()
    ///   getComplexVariableLabelLookupDict()
    /// 
    /// This implementation uses a hybrid of Particle Swarm Optimization (PSO) and
    /// the old "ronamatic" logic from Hypersearch V1. Variables which are lists of
    /// choices (i.e. string values, integer values that represent different
    /// categories) are searched using the ronamatic logic whereas floats and
    /// integers that represent a range of values are searched using PSO.
    /// 
    /// For prediction experiments, this implementation starts out evaluating only
    /// single encoder models that encode the predicted field. This is the first
    /// "sprint". Once it finds the optimum set of variables for that, it starts to
    /// build up by adding in combinations of 2 fields (the second "sprint"), where
    /// one of them is the predicted field. Once the top 2-field combination(s) are
    /// discovered, it starts to build up on those by adding in a 3rd field, etc.
    /// Each new set of field combinations is called a sprint.
    /// 
    /// For classification experiments, this implementation starts out evaluating two
    /// encoder models, where one of the encoders is the classified field. This is the
    /// first "sprint". Once it finds the optimum set of variables for that, it starts
    /// to  build up by evauating combinations of 3 fields (the second "sprint"), where
    /// two of them are the best 2 fields found in the first sprint (one of those of
    /// course being the classified field). Once the top 3-field combination(s) are
    /// discovered, it starts to build up on those by adding in a 4th field, etc.
    /// In classification models, the classified field, although it has an encoder, is
    /// not sent "into" the network. Rather, the encoded value just goes directly to
    /// the classifier as the classifier input.
    /// 
    /// At any one time, there are 1 or more swarms being evaluated at the same time -
    /// each swarm representing a certain field combination within the sprint. We try
    /// to load balance the swarms and have the same number of models evaluated for
    /// each swarm at any one time. Each swarm contains N particles, and we also try
    /// to keep N >= some mininum number. Each position of a particle corresponds to a
    /// model.
    /// 
    /// When a worker is ready to evaluate a new model, it first picks the swarm with
    /// the least number of models so far (least number of evaluated particle
    /// positions). If that swarm does not have the min number of particles in it yet,
    /// or does not yet have a particle created by this worker, the worker will create
    /// a new particle, else it will choose another particle from that swarm that it
    /// had created in the past which has the least number of evaluated positions so
    /// far.
    /// </summary>
    public class HypersearchV2
    {
        internal readonly ILog logger = LogManager.GetLogger(typeof(HypersearchV2));

        public bool _maximize;
        public int? _minParticlesPerSwarm;
        public bool _speculativeParticles;
        public string _workerID;
        public BaseClientJobDao _cjDAO;
        public List<string> _fixedFields;
        public List<string> _encoderNames;
        public HsSearchType _searchType;
        public string _predictedFieldEncoder;
        public ResultsDB _resultsDB;
        public int _maxBranching;
        public uint? _jobID;
        public bool _tryAll3FieldCombinations;
        public bool _tryAll3FieldCombinationsWTimestamps;
        public double _minFieldContribution;
        private HyperSearchSearchParams _searchParams;
        private bool _createCheckpoints;
        private int? _maxModels;
        private int? _predictionCacheMaxRecords;
        private double _speculativeWaitSecondsMax;
        private bool _jobCancelled;
        private object _dummyModel;
        private string _tempDir;
        private HsState _hsState;
        private SwarmTerminator _swarmTerminator;
        //private string _basePath;
        private BaseDescription _baseDescription;
        private int _baseDescriptionHash;
        private int _maxUniqueModelAttempts;
        private double _modelOrphanIntervalSecs;
        private double _maxPctErrModels;
        private bool _killUselessSwarms;
        private InputPredictedField _inputPredictedField;
        private string _predictedField;
        private Dictionary<string, PermuteVariable> _flattenedPermutations;
        private string _optimizeKey;
        private string[] _reportKeys;
        private Func<PermutationModelParameters, bool> _filterFunc;
        private Func<PermutationModelParameters, IDictionary<string, object>> _dummyModelParamsFunc;
        private PermutationModelParameters _fastSwarmModelParams;
        private PermutationModelParameters _permutations;

        /// <summary>
        ///  Instantiate the HyperseachV2 instance.
        /// 
        ///  Parameters:
        ///  ----------------------------------------------------------------------
        ///  searchParams:   a dict of the job's search parameters. The format is:
        /// 
        ///    persistentJobGUID:  REQUIRED.
        ///                        Persistent, globally-unique identifier for this job
        ///                        for use in constructing persistent model checkpoint
        ///                        keys. MUST be compatible with S3 key-naming rules, but
        ///                        MUST NOT contain forward slashes. This GUID is
        ///                        expected to retain its global uniqueness across
        ///                        clusters and cluster software updates (unlike the
        ///                        record IDs in the Engine's jobs table, which recycle
        ///                        upon table schema change and software update). In the
        ///                        future, this may also be instrumental for checkpoint
        ///                        garbage collection.
        /// 
        ///    permutationsPyFilename:
        ///                        OPTIONAL - path to permutations.py file
        ///    permutationsPyContents:
        ///                        OPTIONAL - JSON encoded string with
        ///                                    contents of permutations.py file
        ///    descriptionPyContents:
        ///                        OPTIONAL - JSON encoded string with
        ///                                    contents of base description.py file
        ///    description:        OPTIONAL - JSON description of the search
        ///    createCheckpoints:  OPTIONAL - Whether to create checkpoints
        ///    useTerminators      OPTIONAL - True of False (default config.xml). When set
        ///                                   to False, the model and swarm terminators
        ///                                   are disabled
        ///    maxModels:          OPTIONAL - max // of models to generate
        ///                                  NOTE: This is a deprecated location for this
        ///                                  setting. Now, it should be specified through
        ///                                  the maxModels variable within the permutations
        ///                                  file, or maxModels in the JSON description
        ///    dummyModel:         OPTIONAL - Either (True/False) or a dict of parameters
        ///                                   for a dummy model. If this key is absent,
        ///                                   a real model is trained.
        ///                                   See utils.py/OPFDummyModel runner for the
        ///                                   schema of the dummy parameters
        ///    speculativeParticles OPTIONAL - True or False (default obtained from
        ///                                   nupic.hypersearch.speculative.particles.default
        ///                                   configuration property). See note below.
        /// 
        ///    NOTE: The caller must provide just ONE of the following to describe the
        ///    hypersearch:
        ///          1.) permutationsPyFilename
        ///      OR  2.) permutationsPyContents & permutationsPyContents
        ///      OR  3.) description
        /// 
        ///    The schema for the description element can be found at:
        ///     "py/nupic/frameworks/opf/expGenerator/experimentDescriptionSchema.json"
        /// 
        ///    NOTE about speculativeParticles: If true (not 0), hypersearch workers will
        ///    go ahead and create and run particles in subsequent sprints and
        ///    generations before the current generation or sprint has been completed. If
        ///    false, a worker will wait in a sleep loop until the current generation or
        ///    sprint has finished before choosing the next particle position or going
        ///    into the next sprint. When true, the best model can be found faster, but
        ///    results are less repeatable due to the randomness of when each worker
        ///    completes each particle. This property can be overridden via the
        ///    speculativeParticles element of the Hypersearch job params.
        /// 
        /// 
        ///  workerID:   our unique Hypersearch worker ID
        ///  cjDAO:      ClientJobsDB Data Access Object
        ///  jobID:      job ID for this hypersearch job
        ///  logLevel:   override logging level to this value, if not None
        /// </summary>
        /// <param name="searchParams"></param>
        /// <param name="workerID"></param>
        /// <param name="cjDAO"></param>
        /// <param name="jobID"></param>
        public HypersearchV2(HyperSearchSearchParams searchParams, string workerID = null, BaseClientJobDao cjDAO = null
            , uint? jobID = null)
        {
            // Init random seed
            MersenneTwister random = new MersenneTwister(42);

            // Save the search info
            this._searchParams = searchParams;
            this._workerID = workerID;
            this._cjDAO = cjDAO;
            this._jobID = jobID;

            // Log search params
            this.logger.Info("searchParams: \n" + searchParams.ToString());

            this._createCheckpoints = this._searchParams.createCheckpoints.GetValueOrDefault();
            this._maxModels = this._searchParams.maxModels;
            if (this._maxModels == -1)
            {
                this._maxModels = null;
            }
            this._predictionCacheMaxRecords = this._searchParams.predictionCacheMaxRecords;

            // Speculative particles?
            //this._speculativeParticles = this._searchParams.get('speculativeParticles', bool(int(Configuration.get('nupic.hypersearch.speculative.particles.default'))));
            //this._speculativeWaitSecondsMax = float(Configuration.get('nupic.hypersearch.speculative.particles.sleepSecondsMax'));
            this._speculativeParticles = this._searchParams.speculativeParticles
                .GetValueOrDefault(SwarmConfiguration.speculativeParticlesDefault);
            this._speculativeWaitSecondsMax = SwarmConfiguration.speculativeParticlesSleepSecondsMax;

            // Maximum Field Branching
            this._maxBranching = SwarmConfiguration.maxFieldBranching; // int(Configuration.get('nupic.hypersearch.max.field.branching'));

            // Minimum Field Contribution
            this._minFieldContribution = SwarmConfiguration.minFieldContribution; //float(Configuration.get('nupic.hypersearch.min.field.contribution'));

            // This gets set if we detect that the job got cancelled
            this._jobCancelled = false;

            // Use terminators (typically set by permutations_runner.py)
            if (this._searchParams.useTerminators.HasValue)
            {
                bool useTerminators = this._searchParams.useTerminators.GetValueOrDefault();
                //useTerminators = str(int(useTerminators));

                SwarmConfiguration.enableModelTermination = useTerminators;
                SwarmConfiguration.enableModelMaturity = useTerminators;
                SwarmConfiguration.enableSwarmTermination = useTerminators;
                //Configuration.set('nupic.hypersearch.enableModelTermination', useTerminators);
                //Configuration.set('nupic.hypersearch.enableModelMaturity', useTerminators);
                //Configuration.set('nupic.hypersearch.enableSwarmTermination', useTerminators);
            }

            // Special test mode?
            if (Environment.GetEnvironmentVariable("NTA_TEST_exitAfterNModels") != null)
            {
                this._maxModels = int.Parse(Environment.GetEnvironmentVariable("NTA_TEST_exitAfterNModels"));
            }
            //if ('NTA_TEST_exitAfterNModels' in os.environ)
            //{
            //    this._maxModels = int(os.environ["NTA_TEST_exitAfterNModels"]);
            //}

            this._dummyModel = this._searchParams.dummyModel;

            // Holder for temporary directory, if any, that needs to be cleaned up
            // in our close() method.
            this._tempDir = null;
            try
            {
                // Get the permutations info. This can be either:
                //  1.) JSON encoded search description (this will be used to generate a
                //       permutations.py and description.py files using ExpGenerator)
                //  2.) path to a pre-generated permutations.py file. The description.py is
                //       assumed to be in the same directory
                //  3.) contents of the permutations.py and descrption.py files.
                string permutationsScript = null;
                if (this._searchParams.description != null)
                {
                    if (this._searchParams.permutationsPyFilename != null
                        || this._searchParams.permutationsPyContents != null
                        || this._searchParams.descriptionPyContents != null)
                    {
                        throw new InvalidOperationException(
                            @"Either 'description', 'permutationsPyFilename' or
                            'permutationsPyContents' & 'permutationsPyContents' should be
                            specified, but not two or more of these at once.");
                    }

                    // Calculate training period for anomaly models
                    var searchParamObj = this._searchParams;
                    JObject anomalyParams = (JObject)searchParamObj.description["anomalyParams"];//?? new Dictionary<string, string>();
                    //anomalyParams = searchParamObj.description.get("anomalyParams", dict());

                    // This is used in case searchParamObj["description"]["anomalyParams"]
                    // is set to None.
                    if (anomalyParams == null)
                    {
                        anomalyParams = new JObject();
                    }

                    //if ((!anomalyParams.Properties().Any(p => p.Name == "autoDetectWaitRecords")) ||
                    //    (anomalyParams["autoDetectWaitRecords"] == null))
                    //{
                    //    var streamDef = this._getStreamDef(searchParamObj.description);

                    //    //from nupic.data.stream_reader import StreamReader;

                    //    try
                    //    {
                    //        var streamReader = new StreamReader(streamDef/*, isBlocking = False,
                    //                                       maxTimeout = 0, eofOnTimeout = True*/);
                    //        anomalyParams["autoDetectWaitRecords"] = streamReader.getDataRowCount();
                    //    }
                    //    catch (Exception)
                    //    {
                    //        anomalyParams["autoDetectWaitRecords"] = null;
                    //    }
                    //    this._searchParams.description["anomalyParams"] = anomalyParams;
                    //}


                    // Call the experiment generator to generate the permutations and base
                    // description file.
                    string outDir = this._tempDir = @"C:\temp\" + Path.GetRandomFileName();//tempfile.mkdtemp();
                    //expGenerator([
                    //    '--description=%s' % (
                    //        json.dumps(this._searchParams["description"])),
                    //    '--version=v2',
                    //    '--outDir=%s' % (outDir)]);

                    // Get the name of the permutations script.
                    //permutationsScript = Path.Combine(outDir, "permutations.py");
                    throw new NotImplementedException("to be reviewed, generate IPermutionFilter above");
                }

                else if (this._searchParams.permutationsPyFilename != null)
                {
                    if (this._searchParams.description != null
                        || this._searchParams.permutationsPyContents != null
                        || this._searchParams.descriptionPyContents != null)
                    {

                        throw new InvalidOperationException(
                            @"Either 'description', 'permutationsPyFilename' or 
                            'permutationsPyContents' & 'permutationsPyContents' should be 
                            specified, but not two or more of these at once.");
                    }
                    throw new NotImplementedException("to be reviewed filename should be read and loaded in to IPermutionFilter");
                    permutationsScript = this._searchParams.permutationsPyFilename;
                }

                else if (this._searchParams.permutationsPyContents != null) //if ('permutationsPyContents' in this._searchParams)
                {
                    if (this._searchParams.description != null
                        || this._searchParams.permutationsPyFilename != null)
                    {
                        throw new InvalidOperationException(
                            @"Either 'description', 'permutationsPyFilename' or
                            'permutationsPyContents' & 'permutationsPyContents' should be 
                            specified, but not two or more of these at once.");
                    }

                    Debug.Assert(this._searchParams.permutationsPyContents != null);
                    permutationsScript = Json.Serialize(this._searchParams.permutationsPyContents);
                    // Generate the permutations.py and description.py files
                    //string outDir = this._tempDir = @"C:\temp\" + Path.GetRandomFileName();//tempfile.mkdtemp();
                    //permutationsScript = Path.Combine(outDir, "permutations.py");
                    //var fd = new StreamWriter(permutationsScript, false);
                    //fd.WriteLine(this._searchParams.permutationsPyContents);
                    //fd.Close();
                    //fd = new StreamWriter(Path.Combine(outDir, "description.py"), false);
                    //fd.WriteLine(this._searchParams.descriptionPyContents);
                    //fd.Close();
                    //throw new NotImplementedException("to be reviewed, deserialize from JSON IPermutionFilter above");
                }
                else
                {
                    throw new InvalidOperationException("Either 'description' or 'permutationsScript' must be specified");
                }

                // Get the base path of the experiment and read in the base description
                //this._basePath = Path.GetDirectoryName(permutationsScript);
                //this._baseDescription = new StreamReader(Path.Combine(this._basePath, "description.py")).ReadToEnd();
                //this._baseDescriptionHash = GetMd5Hash(MD5.Create(), this._baseDescription); // hashlib.md5(this._baseDescription).digest();
                this._baseDescription = searchParams.descriptionPyContents;
                this._baseDescriptionHash = _baseDescription.GetHashCode();


                // Read the model config to figure out the inference type
                //modelDescription, _ = opfhelpers.loadExperiment(this._basePath);
                //Tuple<ModelDescription, object> modelDescrPair = new Tuple<ModelDescription, object>(null, null);// opfhelpers.loadExperiment(this._basePath);
                //throw new NotImplementedException("Check line above");
                ConfigModelDescription modelDescription = _baseDescription.modelConfig;

                // Read info from permutations file. This sets up the following member
                // variables:
                //   _predictedField
                //   _permutations
                //   _flattenedPermutations
                //   _encoderNames
                //   _reportKeys
                //   _filterFunc
                //   _optimizeKey
                //   _maximize
                //   _dummyModelParamsFunc
                this._readPermutationsFile(permutationsScript, modelDescription);

                // Fill in and save the base description and permutations file contents
                //  if they haven't already been filled in by another worker
                if (this._cjDAO != null)
                {
                    bool updated = this._cjDAO.jobSetFieldIfEqual(
                        jobID: this._jobID,
                        fieldName: "genBaseDescription",
                        curValue: null,
                        newValue: Json.Serialize(this._baseDescription));
                    if (updated)
                    {
                        //string permContents = new StreamReader(permutationsScript).ReadToEnd();
                        string permContents = permutationsScript;
                        this._cjDAO.jobSetFieldIfEqual(jobID: this._jobID,
                                                       fieldName: "genPermutations",
                                                       curValue: null,
                                                       newValue: permContents);
                    }
                }

                // if user provided an artificialMetric, force use of the dummy model
                //if (this._dummyModelParamsFunc != null)
                //{
                //    if (this._dummyModel == null)
                //    {
                //        this._dummyModel = dict();
                //    }
                //}

                // If at DEBUG log level, print out permutations info to the log
                if (this.logger.IsDebugEnabled)
                {
                    //msg = StringIO.StringIO();
                    //print >> msg, "Permutations file specifications: ";
                    //info = dict();
                    //for (key in ["_predictedField', '_permutations',
                    //            '_flattenedPermutations', '_encoderNames',
                    //            '_reportKeys', '_optimizeKey', '_maximize"])
                    //{
                    //    info[key] = getattr(self, key);
                    //}
                    //print >> msg, pprint.pformat(info);
                    //this.logger.debug(msg.getvalue());
                    //msg.close();
                }

                // Instantiate our database to hold the results we received so far
                this._resultsDB = new ResultsDB(this);

                // Instantiate the Swarm Terminator
                this._swarmTerminator = new SwarmTerminator();

                // Initial hypersearch state
                this._hsState = null;

                // The Max // of attempts we will make to create a unique model before
                //  giving up.
                //this._maxUniqueModelAttempts = int(Configuration.get('nupic.hypersearch.maxUniqueModelAttempts'));
                this._maxUniqueModelAttempts = SwarmConfiguration.maxUniqueModelAttempts;

                // The max amount of time allowed before a model is considered orphaned.
                this._modelOrphanIntervalSecs = SwarmConfiguration.modelOrphanIntervalSecs;

                // The max percent of models that can complete with errors
                this._maxPctErrModels = SwarmConfiguration.maxPctErrModels;
            }
            catch (Exception ex)
            {
                // Clean up our temporary directory, if any
                if (this._tempDir != null)
                {
                    //shutil.rmtree(this._tempDir);
                    Directory.Delete(this._tempDir, true);
                    this._tempDir = null;
                }

                throw;
            }
        }
        /// <summary>
        /// Destructor; NOTE: this is not guaranteed to be called (bugs like circular references could prevent it from being called).
        /// </summary>
        ~HypersearchV2()
        {
            this.close();
        }

        public string _flattenKeys(string[] keys)
        {
            return string.Join("|", keys);// '|'.join(keys);
        }

        /// <summary>
        ///  Generate stream definition based on
        /// </summary>
        /// <param name="modelDescription"></param>
        /// <returns></returns>
        private JToken _getStreamDef(JObject modelDescription)
        {
            //--------------------------------------------------------------------------
            // Generate the string containing the aggregation settings.
            var aggregationPeriod = new Dictionary<string, int>
            {
                {"days", 0},
                {"hours", 0},
                {"microseconds", 0},
                {"milliseconds", 0},
                {"minutes", 0},
                {"months", 0},
                {"seconds", 0},
                {"weeks", 0},
                {"years", 0},
            };

            // Honor any overrides provided in the stream definition
            var aggFunctionsDict = new Dictionary<string, string>();
            if (modelDescription["streamDef"].Any(t => ((JObject)t).Properties().Any(p => p.Name == "aggregation")))
            {
                foreach (string key in aggregationPeriod.Keys)
                {
                    //if (key in modelDescription["streamDef"]["aggregation"])
                    if (modelDescription["streamDef"]["aggregation"].Any(t => ((JObject)t).Properties().Any(p => p.Name == key)))
                    {
                        aggregationPeriod[key] = modelDescription["streamDef"]["aggregation"][key].Value<int>();
                    }
                }
                //if ('fields' in modelDescription["streamDef"]["aggregation"])
                if (modelDescription["streamDef"]["aggregation"].Any(t => ((JObject)t).Properties().Any(p => p.Name == "fields")))
                {
                    //for (fieldName, func) in modelDescription["streamDef"]["aggregation"]["fields"]
                    foreach (KeyValuePair<string, JToken> key in (JObject)modelDescription["streamDef"]["aggregation"]["fields"])
                    {
                        string fieldName = key.Key;
                        string func = key.Value.Value<string>();
                        aggFunctionsDict[fieldName] = func;
                    }
                }
            }

            // Do we have any aggregation at all?
            bool hasAggregation = false;
            foreach (var v in aggregationPeriod.Values)
            {
                if (v != 0)
                {
                    hasAggregation = true;
                    break;
                }
            }

            // Convert the aggFunctionsDict to a list
            var aggFunctionList = aggFunctionsDict.ToList();
            var aggregationInfo = new Dictionary<string, int>(aggregationPeriod);
            //aggregationInfo["fields"] = aggFunctionList;
            throw new NotImplementedException("Check aggregation and fields");
            var streamDef = modelDescription["streamDef"].DeepClone();
            //streamDef["aggregation"] = copy.deepcopy(aggregationInfo);
            return streamDef;
        }
        /// <summary>
        /// Deletes temporary system objects/files. 
        /// </summary>
        public void close()
        {
            if (this._tempDir != null && Directory.Exists(this._tempDir))
            {
                this.logger.Debug("Removing temporary directory " + this._tempDir);
                Directory.Delete(this._tempDir, true);
                this._tempDir = null;
            }
        }
        /// <summary>
        ///  Read the permutations file and initialize the following member variables:
        ///      _predictedField: field name of the field we are trying to
        ///        predict
        ///      _permutations: Dict containing the full permutations dictionary.
        ///      _flattenedPermutations: Dict containing the flattened version of
        ///        _permutations. The keys leading to the value in the dict are joined
        ///        with a period to create the new key and permute variables within
        ///        encoders are pulled out of the encoder.
        ///      _encoderNames: keys from this._permutations of only the encoder
        ///        variables.
        ///      _reportKeys:   The 'report' list from the permutations file.
        ///        This is a list of the items from each experiment's pickled
        ///        results file that should be included in the final report. The
        ///        format of each item is a string of key names separated by colons,
        ///        each key being one level deeper into the experiment results
        ///        dict. For example, 'key1:key2'.
        ///      _filterFunc: a user-supplied function that can be used to
        ///        filter out specific permutation combinations.
        ///      _optimizeKey: which report key to optimize for
        ///      _maximize: True if we should try and maximize the optimizeKey
        ///        metric. False if we should minimize it.
        ///      _dummyModelParamsFunc: a user-supplied function that can be used to
        ///        artificially generate CLA model results. When supplied,
        ///        the model is not actually run through the OPF, but instead is run
        ///        through a "Dummy Model" (nupic.swarming.ModelRunner.
        ///        OPFDummyModelRunner). This function returns the params dict used
        ///        to control various options in the dummy model (the returned metric,
        ///        the execution time, etc.). This is used for hypersearch algorithm
        ///        development.
        /// 
        ///  Parameters:
        ///  ---------------------------------------------------------
        ///  filename:     Name of permutations file
        ///  retval:       None
        /// </summary>
        /// <param name="filename">Name of permutations file</param>
        /// <param name="modelDescription"></param>
        /// <returns>None</returns>
        private void _readPermutationsFile(string permFileJson, ConfigModelDescription modelDescription)
        {
            // Open and execute the permutations file
            //Dictionary<string, object> vars = new Dictionary<string, object>();

            //permFile = execfile(filename, globals(), vars);
            IPermutionFilter permFile = Json.Deserialize<BasePermutations>(permFileJson);

            // Read in misc info.
            this._reportKeys = permFile.report; // vars.Get("report", []);
            this._filterFunc = permFile.permutationFilter; //vars.Get("permutationFilter", null);
            this._dummyModelParamsFunc = permFile.dummyModelParams;// vars.Get("dummyModelParams", null);
            this._predictedField = null;   // default
            this._predictedFieldEncoder = null;   // default
            this._fixedFields = null; // default

            // The fastSwarm variable, if present, contains the params from a best
            //  model from a previous swarm. If present, use info from that to seed
            //  a fast swarm
            this._fastSwarmModelParams = permFile.fastSwarmModelParams; // vars.Get("fastSwarmModelParams", null);
            if (this._fastSwarmModelParams != null)
            {
                Map<string, object> encoders = this._fastSwarmModelParams.modelParams.sensorParams.encoders;
                this._fixedFields = new List<string>();
                foreach (var fieldName in encoders.Keys)
                {
                    if (encoders[fieldName] != null)
                    {
                        this._fixedFields.Add(fieldName);
                    }
                }
            }

            if (permFile.fixedFields != null)
            {
                this._fixedFields = permFile.fixedFields;
            }

            // Get min number of particles per swarm from either permutations file or
            // config.
            this._minParticlesPerSwarm = (int?)permFile.minParticlesPerSwarm;
            if (this._minParticlesPerSwarm == null)
            {
                this._minParticlesPerSwarm = SwarmConfiguration.minParticlesPerSwarm;
            }
            //this._minParticlesPerSwarm = int(this._minParticlesPerSwarm);

            // Enable logic to kill off speculative swarms when an earlier sprint
            //  has found that it contains poorly performing field combination?
            this._killUselessSwarms = (bool)(permFile.killUselessSwarms ?? true);// vars.Get("killUselessSwarms", true);

            // The caller can request that the predicted field ALWAYS be included ("yes")
            //  or optionally include ("auto"). The setting of "no" is N/A and ignored
            //  because in that case the encoder for the predicted field will not even
            //  be present in the permutations file.
            // When set to "yes", this will force the first sprint to try the predicted
            //  field only (the legacy mode of swarming).
            // When set to "auto", the first sprint tries all possible fields (one at a
            //  time) in the first sprint.
            this._inputPredictedField = permFile.inputPredictedField ?? InputPredictedField.Yes; //vars.Get("inputPredictedField", "yes");

            // Try all possible 3-field combinations? Normally, we start with the best
            //  2-field combination as a base. When this flag is set though, we try
            //  all possible 3-field combinations which takes longer but can find a
            //  better model.
            this._tryAll3FieldCombinations = (bool)(permFile.tryAll3FieldCombinations ?? false);//vars.Get("tryAll3FieldCombinations", false);

            // Always include timestamp fields in the 3-field swarms?
            // This is a less compute intensive version of tryAll3FieldCombinations.
            // Instead of trying ALL possible 3 field combinations, it just insures
            // that the timestamp fields (dayOfWeek, timeOfDay, weekend) are never left
            // out when generating the 3-field swarms.
            this._tryAll3FieldCombinationsWTimestamps = (bool)(permFile.tryAll3FieldCombinationsWTimestamps ?? false);//vars.Get("tryAll3FieldCombinationsWTimestamps", false);

            // Allow the permutations file to override minFieldContribution. This would
            //  be set to a negative number for large swarms so that you don't disqualify
            //  a field in an early sprint just because it did poorly there. Sometimes,
            //  a field that did poorly in an early sprint could help accuracy when
            //  added in a later sprint
            int? minFieldContribution = (int?)(permFile.minFieldContribution ?? null);//vars.Get("minFieldContribution", null);
            if (minFieldContribution != null)
            {
                this._minFieldContribution = minFieldContribution.GetValueOrDefault();
            }

            // Allow the permutations file to override maxBranching.
            var maxBranching = (int?)(permFile.maxFieldBranching ?? null);//vars.Get("maxFieldBranching", null);
            if (maxBranching != null)
            {
                this._maxBranching = maxBranching.GetValueOrDefault();
            }

            // Read in the optimization info.
            if (permFile.maximize != null)
            {
                this._optimizeKey = permFile.maximize;
                this._maximize = true;
            }
            else if (permFile.minimize != null)
            {
                this._optimizeKey = permFile.minimize;
                this._maximize = false;
            }
            else
            {
                throw new InvalidOperationException("Permutations file '%s' does not include a maximize or minimize metric.");
            }

            // The permutations file is the new location for maxModels. The old location,
            //  in the jobParams is deprecated.
            int? maxModels = (int?)permFile.maxModels;
            if (maxModels != null)
            {
                if (this._maxModels == null)
                {
                    this._maxModels = maxModels;
                }
                else
                {
                    throw new InvalidOperationException("It is an error to specify maxModels both in the job params AND in the permutations file.");
                }
            }


            // Figure out if what kind of search this is:
            //
            //  If it's a temporal prediction search:
            //    the first sprint has 1 swarm, with just the predicted field
            //  elif it's a spatial prediction search:
            //    the first sprint has N swarms, each with predicted field + one
            //    other field.
            //  elif it's a classification search:
            //    the first sprint has N swarms, each with 1 field
            string sIinferenceType = modelDescription.modelParams.inferenceType.ToString();
            InferenceType inferenceType;
            if (!Enum.TryParse(sIinferenceType, true, out inferenceType))
            {
                throw new ArgumentOutOfRangeException("inferenceType", "Invalid inference type " + sIinferenceType);
            }

            if (new[] { InferenceType.TemporalMultiStep, InferenceType.NontemporalMultiStep }.Contains(inferenceType))
            {
                // If it does not have a separate encoder for the predicted field that
                //  goes to the classifier, it is a legacy multi-step network
                EncoderSetting classifierOnlyEncoder = null;

                foreach (var encoder in modelDescription.modelParams.sensorParams.encoders.Values)
                {
                    if ((bool)encoder.classifierOnly.GetValueOrDefault() && encoder.fieldName == permFile.predictedField)
                    {
                        classifierOnlyEncoder = encoder;
                        break;
                    }
                }

                if (classifierOnlyEncoder == null || this._inputPredictedField == InputPredictedField.Yes)
                {
                    // If we don't have a separate encoder for the classifier (legacy
                    //  MultiStep) or the caller explicitly wants to include the predicted
                    //  field, then use the legacy temporal search methodology.
                    this._searchType = HsSearchType.legacyTemporal;
                }
                else
                {
                    this._searchType = HsSearchType.temporal;
                }
            }


            else if (new[] { InferenceType.TemporalNextStep,
                             InferenceType.TemporalAnomaly}.Contains(inferenceType))
            {
                this._searchType = HsSearchType.legacyTemporal;
            }
            else if (new[] { InferenceType.TemporalClassification,
                             InferenceType.NontemporalClassification}.Contains(inferenceType))
            {
                this._searchType = HsSearchType.classification;
            }

            else
            {
                throw new InvalidOperationException("Unsupported inference type: " + inferenceType);
            }

            // Get the predicted field. Note that even classification experiments
            //  have a "predicted" field - which is the field that contains the
            //  classification value.
            this._predictedField = (string)permFile.predictedField;
            if (this._predictedField == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Permutations file '{0}' does not have the required 'predictedField' variable", typeof(IPermutionFilter).Name));
            }

            // Read in and validate the permutations dict
            if (permFile.permutations == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Permutations file '{0}' does not define permutations", typeof(IPermutionFilter).Name));
            }

            //if (!(vars["permutations"] is IDictionary))
            //{
            //    throw new InvalidOperationException(string.Format(
            //        "Permutations file '{0}' defines a permutations variable but it is not a dict", typeof(IPermutionFilter).Name));
            //}

            this._encoderNames = new List<string>();
            this._permutations = permFile.permutations; //vars["permutations"];
            this._flattenedPermutations = new Dictionary<string, PermuteVariable>();

            Action<object, string[]> flattenPermutations = (value, keys) =>
            {
                if (keys.Contains(":"))
                {
                    throw new InvalidOperationException(
                        string.Format("The permutation variable '{0}' contains a ':' character, which is not allowed.",
                            Arrays.ToString(keys)));
                }
                string flatKey = _flattenKeys(keys);
                if (value is PermuteEncoder)
                {
                    var permEncValue = (PermuteEncoder)value;
                    this._encoderNames.Add(flatKey);

                    // If this is the encoder for the predicted field, save its name.
                    if (permEncValue.fieldName == this._predictedField)
                    {
                        this._predictedFieldEncoder = flatKey;
                    }

                    // Store the flattened representations of the variables within the
                    // encoder.
                    foreach (var pair in permEncValue.kwArgs)
                    {
                        string encKey = pair.Key;
                        object encValue = pair.Value;
                        //encKey, encValue
                        if (encValue is PermuteVariable)
                        {
                            this._flattenedPermutations[string.Format("{0}:{1}", flatKey, encKey)] = encValue as PermuteVariable;
                        }
                    }
                }
                else if (value is PermuteVariable)
                {
                    this._flattenedPermutations[flatKey] = value as PermuteVariable;
                }
                else
                {
                    if (value is PermuteVariable)
                    {
                        this._flattenedPermutations[flatKey/*key*/] = value as PermuteVariable;
                    }
                }
            };

            Utils.rApply(this._permutations, flattenPermutations);
        }
        /// <summary>
        /// Computes the number of models that are expected to complete as part of this instances's HyperSearch.
        /// NOTE: This is compute - intensive for HyperSearches with a huge number of combinations.
        /// NOTE / TODO:  THIS ONLY WORKS FOR RONOMATIC: This method is exposed for the benefit of perutations_runner.py for use in progress reporting.
        /// </summary>
        /// <returns>The total number of expected models, if known; -1 if unknown</returns>
        public int getExpectedNumModels()
        {
            return -1;
        }
        /// <summary>
        /// Generates a list of model names that are expected to complete as part of
        /// this instances's HyperSearch.
        /// 
        /// NOTE: This is compute - intensive for HyperSearches with a huge number of
        /// combinations.
        /// 
        /// NOTE / TODO:  THIS ONLY WORKS FOR RONOMATIC: This method is exposed for the
        /// benefit of perutations_runner.py.
        /// </summary>
        /// <returns>List of model names for this HypersearchV2 instance, or None of not applicable</returns>
        public List<string> getModelNames()
        {
            return null;
        }
        /// <summary>
        /// Returns a dictionary of permutation variables.
        /// </summary>
        /// <returns>
        /// A dictionary of permutation variables; keys are flat permutation variable names 
        /// and each value is a sub-class of PermuteVariable.
        /// </returns>
        public Dictionary<string, PermuteVariable> getPermutationVariables()
        {
            return this._flattenedPermutations;
        }
        /// <summary>
        /// Generates a lookup dictionary of permutation variables whose values
        /// are too complex for labels, so that artificial labels have to be generated
        /// for them.
        /// </summary>
        /// <returns>A look - up dictionary of permutation
        /// variables whose values are too complex for labels, so
        /// artificial labels were generated instead(e.g., "Choice0",
        /// "Choice1", etc.); the key is the name of the complex variable
        /// and the value is:
        /// dict(labels =< list_of_labels >, values =< list_of_values >).
        /// </returns>
        public object getComplexVariableLabelLookupDict()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Retrieves the optimization key name and optimization function.
        /// </summary>
        /// <returns>
        /// (optimizationMetricKey, maximize)
        /// optimizationMetricKey: which report key to optimize for
        ///     maximize: True if we should try and maximize the optimizeKey
        ///                 metric. False if we should minimize it.
        /// </returns>
        public Tuple<string, bool> getOptimizationMetricInfo()
        {
            return new Tuple<string, bool>(this._optimizeKey, this._maximize);
        }
        /// <summary>
        /// If there are any models that haven't been updated in a while, consider
        /// them dead, and mark them as hidden in our resultsDB.We also change the
        /// paramsHash and particleHash of orphaned models so that we can
        /// re - generate that particle and / or model again if we desire.
        /// </summary>
        private void _checkForOrphanedModels()
        {
            this.logger.Debug("Checking for orphaned models older than " + (this._modelOrphanIntervalSecs));

            while (true)
            {
                ulong? orphanedModelId = this._cjDAO.modelAdoptNextOrphan(this._jobID, this._modelOrphanIntervalSecs);
                if (orphanedModelId == null)
                {
                    break;
                }

                this.logger.Info("Removing orphaned model: " + (orphanedModelId));

                // Change the model hash and params hash as stored in the models table so
                //  that we can insert a new model with the same paramsHash
                string paramsHash = "";
                foreach (int attempt in ArrayUtils.Range(0, 100))
                {
                    bool success = false;
                    paramsHash = GetMd5Hash(MD5.Create(),
                        string.Format("OrphanParams.{0}.{1}", orphanedModelId, attempt));
                    //hashlib.md5("OrphanParams.%d.%d" % (orphanedModelId,attempt)).digest();
                    string particleHash = GetMd5Hash(MD5.Create(),
                        string.Format("OrphanParticle.{0}.{1}", orphanedModelId, attempt));
                    //hashlib.md5("OrphanParticle.%d.%d" % (orphanedModelId,attempt)).digest();
                    try
                    {
                        this._cjDAO.modelSetFields(orphanedModelId, new Dictionary<string, object>
                        {
                            {"engParamsHash", paramsHash},
                            {"engParticleHash", particleHash}
                        });
                        success = true;
                    }
                    catch (Exception)
                    {
                        success = false;
                    }
                    if (success)
                    {
                        break;
                    }

                    if (!success)
                    {
                        throw new InvalidOperationException(
                            "Unexpected failure to change paramsHash and particleHash of orphaned model");
                    }
                }
                // Mark this model as complete, with reason "orphaned"
                this._cjDAO.modelSetCompleted(modelID: orphanedModelId,
                    completionReason: BaseClientJobDao.CMPL_REASON_ORPHAN,
                    completionMsg: "Orphaned");

                // Update our results DB immediately, rather than wait for the worker
                //  to inform us. This insures that the getParticleInfos() calls we make
                //  below don't include this particle. Setting the metricResult to None
                //  sets it to worst case
                this._resultsDB.update(modelId: orphanedModelId.GetValueOrDefault(),
                    modelParams: null,
                    modelParamsHash: paramsHash,
                    metricResult: null,
                    completed: true,
                    completionReason: BaseClientJobDao.CMPL_REASON_ORPHAN,
                    matured: true,
                    numRecords: 0);
            }

        }
        /// <summary>
        ///  Periodically, check to see if we should remove a certain field combination
        ///  from evaluation (because it is doing so poorly) or move on to the next
        ///  sprint(add in more fields).
        /// 
        ///  This method is called from _getCandidateParticleAndSwarm(), which is called
        ///  right before we try and create a new model to run.
        /// </summary>
        /// <param name="exhaustedSwarmId">If not None, force a change to the current set of active 
        /// swarms by removing this swarm.This is used in situations where we can't find any new unique models to create in
        /// this swarm.In these situations, we update the hypersearch
        /// state regardless of the timestamp of the last time another
        /// worker updated it.</param>
        private void _hsStatePeriodicUpdate(string exhaustedSwarmId = null)
        {
            if (this._hsState == null)
            {
                this._hsState = new HsState(this);
            }

            // Read in current state from the DB
            this._hsState.readStateFromDB();

            // This will hold the list of completed swarms that we find
            var completedSwarms = new List<string>();
            SwarmStatus exhaustedSwarmStatus = SwarmStatus.none;
            // Mark the exhausted swarm as completing/completed, if any
            if (exhaustedSwarmId != null)
            {
                this.logger.Info(string.Format("Removing swarm {0} from the active set " +
                                 "because we can't find any new unique particle positions", exhaustedSwarmId));
                // Is it completing or completed?
                var particlesInfo = this._resultsDB.getParticleInfos(swarmId: exhaustedSwarmId, matured: false);
                if (particlesInfo.particleStates.Count > 0)
                {
                    exhaustedSwarmStatus = SwarmStatus.completing;
                }
                else
                {
                    exhaustedSwarmStatus = SwarmStatus.completed;
                }
            }

            // Kill all swarms that don't need to be explored based on the most recent
            // information.
            if (this._killUselessSwarms)
            {
                this._hsState.killUselessSwarms();
            }

            // For all swarms that were in the 'completing' state, see if they have
            // completed yet.
            //
            // Note that we are not quite sure why this doesn't automatically get handled
            // when we receive notification that a model finally completed in a swarm.
            // But, we ARE running into a situation, when speculativeParticles is off,
            // where we have one or more swarms in the 'completing' state even though all
            // models have since finished. This logic will serve as a failsafe against
            // this situation.
            List<string> completingSwarms = this._hsState.getCompletingSwarms();
            foreach (string swarmId in completingSwarms)
            {
                // Is it completed?
                var particles = this._resultsDB.getParticleInfos(swarmId: swarmId, matured: false).particleStates;
                if (particles.Count == 0)
                {
                    completedSwarms.Add(swarmId);
                }
            }

            // Are there any swarms we can remove (because they have matured)?
            var completedSwarmGens = this._resultsDB.getMaturedSwarmGenerations();
            var priorCompletedSwarms = this._hsState.getCompletedSwarms();
            //for (swarmId, genIdx, errScore) in completedSwarmGens
            foreach (var tuple in completedSwarmGens)
            {
                var swarmId = tuple.swarmId;
                var genIdx = tuple.genIdx;
                var errScore = tuple.bestScore;
                // Don't need to report it if the swarm already completed
                if (priorCompletedSwarms.Contains(swarmId))
                {
                    continue;
                }

                var completedList = this._swarmTerminator.recordDataPoint(
                    swarmId: swarmId, generation: genIdx, errScore: errScore);

                // Update status message
                string statusMsg = string.Format("Completed generation #{0} of swarm '{1}' with a best" +
                      " errScore of {2}", genIdx, swarmId, errScore);
                if (completedList.Count > 0)
                {
                    statusMsg = string.Format("{0}. Matured swarm(s): {1}", statusMsg, completedList);
                }
                this.logger.Info(statusMsg);
                this._cjDAO.jobSetFields(jobID: this._jobID,
                                          fields: new Dictionary<string, object> { { "engStatus", statusMsg } },
                                          useConnectionID: false,
                                          ignoreUnchanged: true);

                // Special test mode to check which swarms have terminated
                //if ("NTA_TEST_recordSwarmTerminations" in os.environ)
                if (Environment.GetEnvironmentVariable("NTA_TEST_recordSwarmTerminations") != null)
                {
                    while (true)
                    {
                        Dictionary<string, Dictionary<string, Tuple<int, List<double>>>> results;
                        string resultsStr = (string)this._cjDAO.jobGetFields(this._jobID, new[] { "results" })[0];
                        if (resultsStr == null)
                        {
                            results = new Dictionary<string, Dictionary<string, Tuple<int, List<double>>>>();
                        }
                        else
                        {
                            results = (Dictionary<string, Dictionary<string, Tuple<int, List<double>>>>)JsonConvert.DeserializeObject(resultsStr, new JsonSerializerSettings
                            {
                                TypeNameHandling = TypeNameHandling.All
                            });
                        }
                        if (!results.ContainsKey("terminatedSwarms"))
                        {
                            results["terminatedSwarms"] = new Dictionary<string, Tuple<int, List<double>>>();
                        }

                        foreach (string swarm in completedList)
                        {
                            if (!results["terminatedSwarms"].ContainsKey(swarm))
                            {
                                results["terminatedSwarms"][swarm] = new Tuple<int, List<double>>(genIdx, this._swarmTerminator.swarmScores[swarm]);
                            }
                        }

                        string newResultsStr = JsonConvert.SerializeObject(results, new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.All
                        });
                        if (newResultsStr == resultsStr)
                        {
                            break;
                        }
                        bool updated = this._cjDAO.jobSetFieldIfEqual(jobID: this._jobID,
                                                                 fieldName: "results",
                                                                 curValue: resultsStr,
                                                                 newValue: newResultsStr);
                        if (updated)
                        {
                            break;
                        }
                    }
                }

                if (completedList.Count > 0)
                {
                    foreach (var name in completedList)
                    {
                        this.logger.Info(string.Format("Swarm matured: {0}. Score at generation {1}: {2}", name, genIdx, errScore));
                    }
                    completedSwarms = completedSwarms.Union(completedList).ToList();
                }
            }

            if (completedSwarms.Count == 0 && (exhaustedSwarmId == null))
            {
                return;
            }

            // We need to mark one or more swarms as completed, keep trying until
            //  successful, or until some other worker does it for us.
            while (true)
            {

                if (exhaustedSwarmId != null)
                {
                    this._hsState.setSwarmState(exhaustedSwarmId, exhaustedSwarmStatus);
                }

                // Mark the completed swarms as completed
                foreach (string swarmId in completedSwarms)
                {
                    this._hsState.setSwarmState(swarmId, SwarmStatus.completed);
                }

                // If nothing changed, we're done
                if (!this._hsState.isDirty())
                {
                    return;
                }

                // Update the shared Hypersearch state now
                // This will do nothing and return False if some other worker beat us to it
                bool success = this._hsState.writeStateToDB();

                if (success)
                {
                    // Go through and cancel all models that are still running, except for
                    // the best model. Once the best model changes, the one that used to be
                    // best (and has  matured) will notice that and stop itself at that point.
                    string jobResultsStr = (string)this._cjDAO.jobGetFields(this._jobID, new[] { "results" })[0];
                    Map<string, object> jobResults;
                    ulong? bestModelId;
                    if (jobResultsStr != null)
                    {
                        jobResults = Json.Deserialize<Map<string, object>>(jobResultsStr);
                        bestModelId = TypeConverter.Convert<ulong?>(jobResults.Get("bestModel", null));
                    }
                    else
                    {
                        bestModelId = null;
                    }

                    foreach (string swarmId in completedSwarms.ToList())
                    {
                        //(_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(
                        //                                swarmId = swarmId, completed = False);
                        var modelIds = this._resultsDB.getParticleInfos(swarmId, completed: false).modelIds;
                        if (modelIds.Contains(bestModelId.GetValueOrDefault()))
                        {
                            modelIds.Remove(bestModelId.GetValueOrDefault());
                        }
                        if (modelIds.Count == 0)
                        {
                            continue;
                        }
                        this.logger.Info(string.Format("Killing the following models in swarm '{0}' because" +
                                         "the swarm is being terminated: {1}", swarmId, Arrays.ToString(modelIds)));

                        foreach (var modelId in modelIds)
                        {
                            this._cjDAO.modelSetFields(modelId,
                                    new Dictionary<string, object> { { "engStop", BaseClientJobDao.STOP_REASON_KILLED } },
                                    ignoreUnchanged: true);
                        }
                    }
                    return;
                }

                // We were not able to change the state because some other worker beat us
                // to it.
                // Get the new state, and try again to apply our changes.
                this._hsState.readStateFromDB();
                this.logger.Debug("New hsState has been set by some other worker to: \n"
                    + this._hsState._state.ToString());
            }
        }
        /// <summary>
        ///   Find or create a candidate particle to produce a new model.
        /// 
        ///   At any one time, there is an active set of swarms in the current sprint, where
        ///   each swarm in the sprint represents a particular combination of fields.
        ///   Ideally, we should try to balance the number of models we have evaluated for
        ///   each swarm at any time.
        /// 
        ///   This method will see how many models have been evaluated for each active
        ///   swarm in the current active sprint(s) and then try and choose a particle
        ///   from the least represented swarm in the first possible active sprint, with
        ///   the following constraints/ rules:
        /// 
        ///   for each active sprint:
        ///     for each active swarm(preference to those with least// of models so far):
        ///       1.) The particle will be created from new (generation #0) if there are not
        ///       already this._minParticlesPerSwarm particles in the swarm.
        /// 
        ///       2.) Find the first gen that has a completed particle and evolve that
        ///       particle to the next generation.
        /// 
        ///       3.) If we got to here, we know that we have satisfied the min// of
        ///       particles for the swarm, and they are all currently running(probably at
        ///       various generation indexes).Go onto the next swarm
        /// 
        ///     If we couldn't find a swarm to allocate a particle in, go onto the next
        ///     sprint and start allocating particles there....
        /// </summary>
        /// <param name="exhaustedSwarmId">If not None, force a change to the current set of active
        ///                     swarms by marking this swarm as either 'completing' or
        ///                     'completed'.If there are still models being evaluaed in
        ///                     it, mark it as 'completing', else 'completed. This is
        ///                     used in situations where we can't find any new unique
        ///                     models to create in this swarm.In these situations, we
        ///                     force an update to the hypersearch state so no other
        ///                     worker wastes time try to use this swarm.
        /// </param>
        /// <returns> (exit, particle, swarm)
        ///      exit: If true, this worker is ready to exit (particle and
        ///              swarm will be None)
        ///      particle: Which particle to run
        ///      swarm: which swarm the particle is in
        /// 
        ///      NOTE: When particle and swarm are None and exit is False, it
        ///      means that we need to wait for one or more other worker(s) to
        ///      finish their respective models before we can pick a particle
        ///      to run.This will generally only happen when speculativeParticles
        ///      is set to False.
        /// </returns>
        private CanidateParticleAndSwarm _getCandidateParticleAndSwarm(string exhaustedSwarmId = null)
        {
            // Cancel search?
            bool jobCancel = (bool)this._cjDAO.jobGetFields(this._jobID, new[] { "cancel" })[0];
            if (jobCancel)
            {
                this._jobCancelled = true;
                // Did a worker cancel the job because of an error?
                //(workerCmpReason, workerCmpMsg)
                var jobReasonAndMsg = this._cjDAO.jobGetFields(this._jobID,
                    new[] { "workerCompletionReason', 'workerCompletionMsg" });
                var workerCmpReason = (string)jobReasonAndMsg[0];
                var workerCmpMsg = (string)jobReasonAndMsg[1];
                if (workerCmpReason == BaseClientJobDao.CMPL_REASON_SUCCESS)
                {
                    this.logger.Info("Exiting due to job being cancelled");
                    this._cjDAO.jobSetFields(this._jobID, new Dictionary<string, object>
                    {{"workerCompletionMsg", "Job was cancelled"}},
                        useConnectionID: false, ignoreUnchanged: true);
                }
                else
                {
                    this.logger.Error(string.Format("Exiting because some worker set the " +
                                      "workerCompletionReason to {0}. WorkerCompletionMsg: {1}",
                          workerCmpReason, workerCmpMsg));
                }
                return new CanidateParticleAndSwarm(true, null, null);
            }

            // Perform periodic updates on the Hypersearch state.
            List<string> priorActiveSwarms;
            if (this._hsState != null)
            {
                priorActiveSwarms = this._hsState.getActiveSwarms();
            }
            else
            {
                priorActiveSwarms = null;
            }

            // Update the HypersearchState, checking for matured swarms, and marking
            //  the passed in swarm as exhausted, if any
            this._hsStatePeriodicUpdate(exhaustedSwarmId: exhaustedSwarmId);

            // The above call may have modified this._hsState["activeSwarmIds"]
            // Log the current set of active swarms
            var activeSwarms = this._hsState.getActiveSwarms();
            if (priorActiveSwarms != null && !activeSwarms.SequenceEqual(priorActiveSwarms))
            {
                this.logger.Info(string.Format("Active swarms changed to {0} (from {1})", activeSwarms,
                                                                  priorActiveSwarms));
            }
            this.logger.Debug(string.Format("Active swarms: {0}", activeSwarms));

            // If too many model errors were detected, exit
            int totalCmpModels = this._resultsDB.getNumCompletedModels();
            if (totalCmpModels > 5)
            {
                int numErrs = this._resultsDB.getNumErrModels();
                //if ((double)numErrs / totalCmpModels > this._maxPctErrModels)
                //{
                //    // Get one of the errors
                //    List<int> errModelIds = this._resultsDB.getErrModelIds();
                //    var resInfo = this._cjDAO.modelsGetResultAndStatus(new[] { errModelIds[0] })[0];
                //    string modelErrMsg = resInfo.completionMsg;
                //    string cmpMsg = string.Format("{0}: Exiting due to receiving too many models failing" +
                //                                  " from exceptions ({1} out of {2}). \nModel Exception: {3}",
                //              ErrorCodes.tooManyModelErrs, numErrs, totalCmpModels, modelErrMsg);
                //    this.logger.Error(cmpMsg);

                //    // Cancel the entire job now, if it has not already been cancelled
                //    var workerCmpReason = this._cjDAO.jobGetFields(this._jobID, new[] { "workerCompletionReason" })[0];
                //    if (workerCmpReason == BaseClientJobDao.CMPL_REASON_SUCCESS.ToString())
                //    {
                //        this._cjDAO.jobSetFields(
                //            this._jobID,
                //            fields: new Dictionary<string, object>
                //            {
                //              {"cancel", true},
                //              {"workerCompletionReason", BaseClientJobDao.CMPL_REASON_ERROR},
                //              {"workerCompletionMsg", cmpMsg}
                //            },
                //            useConnectionID: false,
                //            ignoreUnchanged: true);
                //    }
                //    return new CanidateParticleAndSwarm(true, null, null);
                //}
            }

            // If HsState thinks the search is over, exit. It is seeing if the results
            //  on the sprint we just completed are worse than a prior sprint.
            if (this._hsState.isSearchOver())
            {
                string cmpMsg = "Exiting because results did not improve in most recently completed sprint.";
                this.logger.Info(cmpMsg);
                this._cjDAO.jobSetFields(this._jobID,
                      new Dictionary<string, object> { { "workerCompletionMsg", cmpMsg } },
                      useConnectionID: false, ignoreUnchanged: true);
                return new CanidateParticleAndSwarm(true, null, null);
            }

            // Search successive active sprints, until we can find a candidate particle
            //   to work with
            int sprintIdx = -1;
            while (true)
            {
                // Is this sprint active?
                sprintIdx += 1;
                // (active, eos)
                var activeEosPair = this._hsState.isSprintActive(sprintIdx);
                bool active = activeEosPair.Item1;
                bool eos = activeEosPair.Item2;
                // If no more sprints to explore:
                if (eos)
                {
                    // If any prior ones are still being explored, finish up exploring them
                    if (this._hsState.anyGoodSprintsActive())
                    {
                        this.logger.Info("No more sprints to explore, waiting for prior sprints to complete");
                        return new CanidateParticleAndSwarm(false, null, null);
                    }

                    // Else, we're done
                    else
                    {
                        string cmpMsg = "Exiting because we've evaluated all possible field combinations";
                        this._cjDAO.jobSetFields(this._jobID,
                                                new Dictionary<string, object> { { "workerCompletionMsg", cmpMsg } },
                                                 useConnectionID: false, ignoreUnchanged: true);
                        this.logger.Info(cmpMsg);
                        return new CanidateParticleAndSwarm(true, null, null);
                    }
                }

                if (!active)
                {
                    if (!this._speculativeParticles)
                    {
                        if (!this._hsState.isSprintCompleted(sprintIdx))
                        {
                            this.logger.Info(string.Format("Waiting for all particles in sprint {0} to " +
                                             "complete before evolving any more particles", sprintIdx));
                            return new CanidateParticleAndSwarm(false, null, null);
                        }
                    }
                    continue;
                }


                // ====================================================================
                // Look for swarms that have particle "holes" in their generations. That is,
                //  an earlier generation with less than minParticlesPerSwarm. This can
                //  happen if a model that was started eariler got orphaned. If we detect
                //  this, start a new particle in that generation.
                List<string> swarmIds = this._hsState.getActiveSwarms(sprintIdx);
                foreach (string swarmId in swarmIds)
                {
                    int? firstNonFullGenIdx = this._resultsDB.firstNonFullGeneration(
                                            swarmId: swarmId,
                                            minNumParticles: this._minParticlesPerSwarm);
                    if (firstNonFullGenIdx == null)
                    {
                        continue;
                    }

                    if (firstNonFullGenIdx < this._resultsDB.highestGeneration(swarmId))
                    {
                        this.logger.Info(string.Format("Cloning an earlier model in generation {0} of swarm " +
                                         "{1} (sprintIdx={2}) to replace an orphaned model",
                              firstNonFullGenIdx, swarmId, sprintIdx));

                        // Clone a random orphaned particle from the incomplete generation
                        //(allParticles, allModelIds, errScores, completed, matured) = 
                        var orphanedPartInfos = this._resultsDB.getOrphanParticleInfos(swarmId, firstNonFullGenIdx);
                        var allParticles = orphanedPartInfos.particleStates;
                        var allModelIds = orphanedPartInfos.modelIds;
                        var errScores = orphanedPartInfos.errScores;
                        var completed = orphanedPartInfos.completedFlags;
                        var matured = orphanedPartInfos.maturedFlags;
                        bool newParticleId = false;
                        if (allModelIds.Count > 0)
                        {
                            // We have seen instances where we get stuck in a loop incessantly
                            //  trying to clone earlier models (NUP-1511). My best guess is that
                            //  we've already successfully cloned each of the orphaned models at
                            //  least once, but still need at least one more. If we don't create
                            //  a new particleID, we will never be able to instantiate another
                            //  model (since particleID hash is a unique key in the models table).
                            //  So, on 1/8/2013 this logic was changed to create a new particleID
                            //  whenever we clone an orphan.
                            newParticleId = true;
                            this.logger.Info("Cloning an orphaned model");
                        }

                        // If there is no orphan, clone one of the other particles. We can
                        //  have no orphan if this was a speculative generation that only
                        //  continued particles completed in the prior generation.
                        else
                        {
                            newParticleId = true;
                            this.logger.Info("No orphans found, so cloning a non-orphan");
                            //(allParticles, allModelIds, errScores, completed, matured) = \
                            orphanedPartInfos = this._resultsDB.getParticleInfos(swarmId: swarmId,
                                                 genIdx: firstNonFullGenIdx);
                            allParticles = orphanedPartInfos.particleStates;
                            allModelIds = orphanedPartInfos.modelIds;
                            errScores = orphanedPartInfos.errScores;
                            completed = orphanedPartInfos.completedFlags;
                            matured = orphanedPartInfos.maturedFlags;
                        }

                        // Clone that model
                        var random = new MersenneTwister(42);
                        ulong modelId = allModelIds[random.NextInt(allModelIds.Count - 1)];// random.choice(allModelIds);
                        this.logger.Info(string.Format("Cloning model {0}", (modelId)));
                        //(particleState, _, _, _, _) = this._resultsDB.getParticleInfo(modelId);
                        var particleState = this._resultsDB.getParticleInfo(modelId).particleState;
                        var particle = new Particle(hsObj: this,
                                            resultsDB: this._resultsDB,
                                            flattenedPermuteVars: this._flattenedPermutations,
                                            newFromClone: particleState,
                                            newParticleId: newParticleId);
                        return new CanidateParticleAndSwarm(false, particle, swarmId);
                    }
                }


                // ====================================================================
                // Sort the swarms in priority order, trying the ones with the least
                //  number of models first
                //swarmSizes = numpy.array([this._resultsDB.numModels(x) for x in swarmIds]) ;
                var swarmSizes = swarmIds.Select(x => _resultsDB.numModels(x)).ToArray();
                var swarmSizeAndIdList = ArrayUtils.Zip(swarmSizes, swarmIds);
                //swarmSizeAndIdList.Sort();
                swarmSizeAndIdList = swarmSizeAndIdList.OrderBy(t => (int)t.Get(0)).ToList();
                foreach (var pair in swarmSizeAndIdList)
                {
                    string swarmId = (string)pair.Item2;
                    // -------------------------------------------------------------------
                    // 1.) The particle will be created from new (at generation #0) if there
                    //   are not already this._minParticlesPerSwarm particles in the swarm.
                    //(allParticles, allModelIds, errScores, completed, matured) = this._resultsDB.getParticleInfos(swarmId);
                    var orphanedPartInfos = this._resultsDB.getParticleInfos(swarmId: swarmId);
                    var allParticles = orphanedPartInfos.particleStates;
                    var allModelIds = orphanedPartInfos.modelIds;
                    var errScores = orphanedPartInfos.errScores;
                    //var completed = orphanedPartInfos.completedFlags;
                    //var matured = orphanedPartInfos.maturedFlags;

                    if (allParticles.Count < this._minParticlesPerSwarm)
                    {
                        var particle = new Particle(hsObj: this,
                                            resultsDB: this._resultsDB,
                                            flattenedPermuteVars: this._flattenedPermutations,
                                            swarmId: swarmId,
                                            newFarFrom: allParticles);

                        // Jam in the best encoder state found from the first sprint
                        ulong? bestPriorModel = null;
                        double? errScore = null;
                        if (sprintIdx >= 1)
                        {
                            //(bestPriorModel, errScore) = this._hsState.bestModelInSprint(0);
                            var bestInSprint = this._hsState.bestModelInSprint(0);
                            bestPriorModel = bestInSprint.Item1;
                            errScore = bestInSprint.Item2;
                        }

                        if (bestPriorModel != null)
                        {
                            this.logger.Info(string.Format("Best model and errScore from previous sprint({0}):" +
                                             " {1}, {2}", 0, bestPriorModel, errScore));
                            //(baseState, modelId, errScore, completed, matured) =
                            var particleInfo = this._resultsDB.getParticleInfo(bestPriorModel.Value);
                            var baseState = particleInfo.particleState;
                            var modelId = particleInfo.modelId;
                            errScore = particleInfo.errScore;
                            var completed = particleInfo.completed;
                            var matured = particleInfo.matured;
                            particle.copyEncoderStatesFrom(baseState);

                            // Copy the best inference type from the earlier sprint
                            particle.copyVarStatesFrom(baseState, new List<string> { "modelParams|inferenceType" });

                            // It's best to jiggle the best settings from the prior sprint, so
                            //  compute a new position starting from that previous best
                            // Only jiggle the vars we copied from the prior model
                            List<string> whichVars = new List<string>();
                            foreach (var varNamePair in baseState.varStates)
                            {
                                string varName = varNamePair.Key;
                                if (varName.Contains(":"))
                                {
                                    whichVars.Add(varName);
                                }
                            }
                            particle.newPosition(whichVars);

                            this.logger.Debug("Particle after incorporating encoder vars from best " +
                                              "model in previous sprint: \n" + particle);
                        }

                        return new CanidateParticleAndSwarm(false, particle, swarmId);
                    }

                    // -------------------------------------------------------------------
                    // 2.) Look for a completed particle to evolve
                    // Note that we use lastDescendent. We only want to evolve particles that
                    // are at their most recent generation index.
                    //(readyParticles, readyModelIds, readyErrScores, _, _) = 
                    var particleInfos = this._resultsDB.getParticleInfos(swarmId, genIdx: null, matured: true, lastDescendent: true);
                    var readyParticles = particleInfos.particleStates;
                    var readyModelIds = particleInfos.modelIds;
                    var readyErrScores = particleInfos.errScores;
                    // If we have at least 1 ready particle to evolve...
                    if (readyParticles.Count > 0)
                    {
                        //readyGenIdxs = [x["genIdx"] for x in readyParticles];
                        var readyGenIdxs = readyParticles.Select(x => x.genIdx).ToList();

                        var sortedGenIdxs = readyGenIdxs.OrderBy(k => k).ToList();
                        int genIdx = sortedGenIdxs[0];

                        // Now, genIdx has the generation of the particle we want to run,
                        // Get a particle from that generation and evolve it.
                        ParticleStateModel useParticle = null;
                        foreach (var rparticle in readyParticles)
                        {
                            if (rparticle.genIdx == genIdx)
                            {
                                useParticle = rparticle;
                                break;
                            }
                        }

                        // If speculativeParticles is off, we don't want to evolve a particle
                        // into the next generation until all particles in the current
                        // generation have completed.
                        if (!this._speculativeParticles)
                        {
                            var particles = this._resultsDB.getParticleInfos(
                                swarmId, genIdx: genIdx, matured: false).particleStates;
                            if (particles.Count > 0)
                            {
                                continue;
                            }
                        }

                        var particle = new Particle(hsObj: this,
                                            resultsDB: this._resultsDB,
                                            flattenedPermuteVars: this._flattenedPermutations,
                                            evolveFromState: useParticle);
                        return new CanidateParticleAndSwarm(false, particle, swarmId);
                    }

                    // END: for (swarmSize, swarmId) in swarmSizeAndIdList:
                    // No success in this swarm, onto next swarm
                }

                // ====================================================================
                // We couldn't find a particle in this sprint ready to evolve. If
                //  speculative particles is OFF, we have to wait for one or more other
                //  workers to finish up their particles before we can do anything.
                if (!this._speculativeParticles)
                {
                    this.logger.Info(string.Format("Waiting for one or more of the {0} swarms " +
                                     "to complete a generation before evolving any more particles", Arrays.ToString(swarmIds)));
                    return new CanidateParticleAndSwarm(false, null, null);
                }

                // END: while True:
                // No success in this sprint, into next sprint
            }
        }
        public class CanidateParticleAndSwarm
        {
            public CanidateParticleAndSwarm(bool exit, Particle particle, string swarmId)
            {
                this.exit = exit;
                this.particle = particle;
                this.swarm = swarmId;
            }
            public bool exit;
            public Particle particle;
            public string swarm;
        }
        /// <summary>
        /// Test if it's OK to exit this worker. This is only called when we run
        /// out of prospective new models to evaluate. This method sees if all models
        /// have matured yet. If not, it will sleep for a bit and return False.This
        /// will indicate to the hypersearch worker that we should keep running, and
        /// check again later. This gives this worker a chance to pick up and adopt any
        /// model which may become orphaned by another worker before it matures.
        /// 
        /// If all models have matured, this method will send a STOP message to all
        /// matured, running models (presummably, there will be just one -the model
        /// which thinks it's the best) before returning True.
        /// </summary>
        /// <returns></returns>
        private bool _okToExit()
        {
            // Send an update status periodically to the JobTracker so that it doesn't
            // think this worker is dead.
            Console.WriteLine("reporter:status:In hypersearchV2: _okToExit");
            //print >> sys.stderr, "reporter:status:In hypersearchV2: _okToExit";

            // Any immature models still running?
            if (!this._jobCancelled)
            {
                var modelIds1 = this._resultsDB.getParticleInfos(matured: false).modelIds;
                //(_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(matured = False);
                if (modelIds1.Count > 0)
                {
                    this.logger.Info("Ready to end hyperseach, but not all models have " +
                                     "matured yet. Sleeping a bit to wait for all models " +
                                     "to mature.");
                    // Sleep for a bit, no need to check for orphaned models very often
                    Thread.Sleep((int)(5.0 * new MersenneTwister().NextDouble()));
                    return false;
                }
            }

            // All particles have matured, send a STOP signal to any that are still
            // running.
            var modelIds = this._resultsDB.getParticleInfos(completed: false).modelIds;
            foreach (var modelId in modelIds)
            {
                this.logger.Info(string.Format("Stopping model {0} because the search has ended", modelId));
                this._cjDAO.modelSetFields(modelId,
                                new Dictionary<string, object> { { "engStop", BaseClientJobDao.STOP_REASON_STOPPED } },
                                ignoreUnchanged: true);
            }

            // Update the HsState to get the accurate field contributions.
            this._hsStatePeriodicUpdate();
            var contributions = this._hsState.getFieldContributions();
            var pctFieldContributions = contributions.Item1;
            var absFieldContributions = contributions.Item2;
            //pctFieldContributions, absFieldContributions = this._hsState.getFieldContributions();


            // Update the results field with the new field contributions.
            string jobResultsStr = (string)this._cjDAO.jobGetFields(this._jobID, new[] { "results" })[0];
            Dictionary<string, object> jobResults;
            if (jobResultsStr != null)
            {
                jobResults = Json.Deserialize<Dictionary<string, object>>(jobResultsStr);
            }
            else
            {
                jobResults = new Dictionary<string, object>();
            }

            // Update the fieldContributions field.
            if (!pctFieldContributions.Equals((Map<string, Double>)jobResults.Get("fieldContributions")))
            {
                jobResults["fieldContributions"] = pctFieldContributions;
                jobResults["absoluteFieldContributions"] = absFieldContributions;

                bool isUpdated = this._cjDAO.jobSetFieldIfEqual(this._jobID,
                                                             fieldName: "results",
                                                             curValue: jobResultsStr,
                                                             newValue: Json.Serialize(jobResults));
                if (isUpdated)
                {
                    this.logger.Info("Successfully updated the field contributions:" + pctFieldContributions);
                }
                else
                {
                    this.logger.Info("Failed updating the field contributions, another hypersearch worker must have updated it");
                }
            }

            return true;
        }

        public void killSwarmParticles(string swarmId)
        {
            var modelIds = this._resultsDB.getParticleInfos(
            //(_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(
            swarmId: swarmId, completed: false).modelIds;
            foreach (var modelId in modelIds)
            {
                this.logger.Info(string.Format("Killing the following models in swarm '{0}' because" +
                                 "the swarm is being terminated: {1}", swarmId, Arrays.ToString(modelIds)));
                this._cjDAO.modelSetFields(
                    modelId, new Dictionary<string, object> { { "engStop", BaseClientJobDao.STOP_REASON_KILLED } },
                    ignoreUnchanged: true);
            }
        }
        /// <summary>
        ///  Create one or more new models for evaluation. These should NOT be models
        ///  that we already know are in progress (i.e. those that have been sent to us
        ///  via recordModelProgress). We return a list of models to the caller
        ///  (HypersearchWorker) and if one can be successfully inserted into
        ///  the models table (i.e. it is not a duplicate) then HypersearchWorker will
        ///  turn around and call our runModel() method, passing in this model. If it
        ///  is a duplicate, HypersearchWorker will call this method again. A model
        ///  is a duplicate if either the  modelParamsHash or particleHash is
        ///  identical to another entry in the model table.
        /// 
        ///  The numModels is provided by HypersearchWorker as a suggestion as to how
        ///  many models to generate. This particular implementation only ever returns 1
        ///  model.
        /// 
        ///  Before choosing some new models, we first do a sweep for any models that
        ///  may have been abandonded by failed workers. If/when we detect an abandoned
        ///  model, we mark it as complete and orphaned and hide it from any subsequent
        ///  queries to our ResultsDB. This effectively considers it as if it never
        ///  existed. We also change the paramsHash and particleHash in the model record
        ///  of the models table so that we can create another model with the same
        ///  params and particle status and run it (which we then do immediately).
        /// 
        ///  The modelParamsHash returned for each model should be a hash (max allowed
        ///  size of ClientJobsDAO.hashMaxSize) that uniquely identifies this model by
        ///  it's params and the optional particleHash should be a hash of the particleId
        ///  and generation index. Every model that gets placed into the models database,
        ///  either by this worker or another worker, will have these hashes computed for
        ///  it. The recordModelProgress gets called for every model in the database and
        ///  the hash is used to tell which, if any, are the same as the ones this worker
        ///  generated.
        /// 
        ///  NOTE: We check first ourselves for possible duplicates using the paramsHash
        ///  before we return a model. If HypersearchWorker failed to insert it (because
        ///  some other worker beat us to it), it will turn around and call our
        ///  recordModelProgress with that other model so that we now know about it. It
        ///  will then call createModels() again.
        /// 
        ///  This methods returns an exit boolean and the model to evaluate. If there is
        ///  no model to evalulate, we may return False for exit because we want to stay
        ///  alive for a while, waiting for all other models to finish. This gives us
        ///  a chance to detect and pick up any possibly orphaned model by another
        ///  worker.
        /// 
        ///  Parameters:
        ///  ----------------------------------------------------------------------
        ///  numModels:   number of models to generate
        ///  retval:      (exit, models)
        ///                  exit: true if this worker should exit.
        ///                  models: list of tuples, one for each model. Each tuple contains:
        ///                    (modelParams, modelParamsHash, particleHash)
        /// 
        ///               modelParams is a dictionary containing the following elements:
        /// 
        ///                 structuredParams: dictionary containing all variables for
        ///                   this model, with encoders represented as a dict within
        ///                   this dict (or None if they are not included.
        /// 
        ///                 particleState: dictionary containing the state of this
        ///                   particle. This includes the position and velocity of
        ///                   each of it's variables, the particleId, and the particle
        ///                   generation index. It contains the following keys:
        /// 
        ///                   id: The particle Id of the particle we are using to
        ///                         generate/track this model. This is a string of the
        ///                         form <hypesearchWorkerId>.<particleIdx>
        ///                   genIdx: the particle's generation index. This starts at 0
        ///                         and increments every time we move the particle to a
        ///                         new position.
        ///                   swarmId: The swarmId, which is a string of the form
        ///                     <encoder>.<encoder>... that describes this swarm
        ///                   varStates: dict of the variable states. The key is the
        ///                       variable name, the value is a dict of the variable's
        ///                       position, velocity, bestPosition, bestResult, etc.
        /// </summary>
        /// <param name="numModels"></param>
        public Tuple<bool, List<Tuple<ModelParams, string, string>>> createModels(int numModels = 1)
        {
            // Check for and mark orphaned models
            this._checkForOrphanedModels();

            List<Tuple<ModelParams, string, string>> modelResults = new List<Tuple<ModelParams, string, string>>();
            Particle candidateParticle;
            bool exitNow = false;
            string candidateSwarm = null;
            //for (_ in xrange(numModels))
            for (int i = 0; i < ArrayUtils.XRange(0, numModels, 1).ToArray().Length; i++)
            {
                candidateParticle = null;

                // If we've reached the max // of model to evaluate, we're done.
                if (this._maxModels != null &&
                    (this._resultsDB.numModels() - this._resultsDB.getNumErrModels()) >=
                    this._maxModels)
                {

                    return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(this._okToExit(), new List<Tuple<ModelParams, string, string>>());
                }

                // If we don't already have a particle to work on, get a candidate swarm and
                // particle to work with. If None is returned for the particle it means
                // either that the search is over (if exitNow is also True) or that we need
                // to wait for other workers to finish up their models before we can pick
                // another particle to run (if exitNow is False).
                if (candidateParticle == null)
                {
                    //(exitNow, candidateParticle, candidateSwarm) = 
                    var canidatePAS = this._getCandidateParticleAndSwarm();
                    exitNow = canidatePAS.exit;
                    candidateParticle = canidatePAS.particle;
                    candidateSwarm = canidatePAS.swarm;
                }
                if (candidateParticle == null)
                {
                    if (exitNow)
                    {
                        return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(this._okToExit(), new List<Tuple<ModelParams, string, string>>());
                    }
                    else
                    {
                        // Send an update status periodically to the JobTracker so that it doesn't
                        // think this worker is dead.
                        //print >> sys.stderr, "reporter:status:In hypersearchV2: speculativeWait";
                        Console.WriteLine("reporter:status:In hypersearchV2: speculativeWait");
                        Thread.Sleep((int)(this._speculativeWaitSecondsMax * new MersenneTwister().NextDouble()));
                        return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(false, new List<Tuple<ModelParams, string, string>>());
                    }
                }
                var useEncoders = candidateSwarm.Split('.');
                int numAttempts = 0;

                ModelParams modelParams = null;
                string paramsHash = null;
                string particleHash = null;
                // Loop until we can create a unique model that we haven't seen yet.
                while (true)
                {

                    // If this is the Nth attempt with the same candidate, agitate it a bit
                    // to find a new unique position for it.
                    if (numAttempts >= 1)
                    {
                        this.logger.Debug(string.Format("Agitating particle to get unique position after {0} " +
                                          "failed attempts in a row", numAttempts));
                        candidateParticle.agitate();
                    }

                    // Create the hierarchical params expected by the base description. Note
                    // that this is where we incorporate encoders that have no permuted
                    // values in them.
                    var position = candidateParticle.getPosition();
                    PermutationModelParameters structuredParams = new PermutationModelParameters();
                    Func<object, string[], object> _buildStructuredParams = (value, keys) =>
                    {
                        string flatKey = _flattenKeys(keys);
                        // If it's an encoder, either put in None if it's not used, or replace
                        // all permuted constructor params with the actual position.
                        if (this._encoderNames.Contains(flatKey))
                        {
                            if (useEncoders.Contains(flatKey) && value is PermuteEncoder)
                            {
                                // Form encoder dict, substituting in chosen permutation values.
                                return ((PermuteEncoder)value).getDict(flatKey, position);
                            }
                            // Encoder not used.
                            else
                            {
                                return null;
                            }
                        }
                        // Regular top-level variable.
                        else if (position.ContainsKey(flatKey))
                        {
                            return position[flatKey];
                        }
                        // Fixed override of a parameter in the base description.
                        else
                        {
                            return value;
                        }
                    };

                    structuredParams = (PermutationModelParameters)Utils.rCopy(this._permutations,
                        _buildStructuredParams,
                        discardNoneKeys: false);

                    // Create the modelParams.
                    modelParams = new ModelParams
                    {
                        structuredParams = structuredParams,
                        particleState = candidateParticle.getState()
                    };

                    // And the hashes.
                    //MD5 m = MD5.Create();
                    //m.update(sortedJSONDumpS(structuredParams));
                    //m.update(this._baseDescriptionHash);
                    //paramsHash = m.digest();
                    paramsHash = GetMd5Hash(MD5.Create(), Json.Serialize(structuredParams) + Json.Serialize(_baseDescriptionHash));
                    string particleInst = string.Format("{0}.{1}", modelParams.particleState.id, modelParams.particleState.genIdx);
                    particleHash = GetMd5Hash(MD5.Create(), particleInst);// hashlib.md5(particleInst).digest();

                    // Increase attempt counter
                    numAttempts += 1;

                    // If this is a new one, and passes the filter test, exit with it.
                    // TODO: There is currently a problem with this filters implementation as
                    // it relates to this._maxUniqueModelAttempts. When there is a filter in
                    // effect, we should try a lot more times before we decide we have
                    // exhausted the parameter space for this swarm. The question is, how many
                    // more times?
                    bool valid;
                    if (this._filterFunc != null && !this._filterFunc(structuredParams))
                    {
                        valid = false;
                    }
                    else
                    {
                        valid = true;
                    }
                    if (valid && !this._resultsDB.getModelIDFromParamsHash(paramsHash).HasValue)
                    {
                        break;
                    }

                    // If we've exceeded the max allowed number of attempts, mark this swarm
                    //  as completing or completed, so we don't try and allocate any more new
                    //  particles to it, and pick another.
                    if (numAttempts >= this._maxUniqueModelAttempts)
                    {
                        //(exitNow, candidateParticle, candidateSwarm) 
                        var canidatePAS = this._getCandidateParticleAndSwarm(exhaustedSwarmId: candidateSwarm);
                        exitNow = canidatePAS.exit;
                        candidateParticle = canidatePAS.particle;
                        candidateSwarm = canidatePAS.swarm;
                        if (candidateParticle == null)
                        {
                            if (exitNow)
                            {
                                return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(_okToExit(), new List<Tuple<ModelParams, string, string>>());
                                //return (this._okToExit(), []);
                            }
                            else
                            {
                                Thread.Sleep((int)(this._speculativeWaitSecondsMax * new MersenneTwister().NextDouble()));
                                return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(false, new List<Tuple<ModelParams, string, string>>());
                            }
                        }
                        numAttempts = 0;
                        useEncoders = candidateSwarm.Split('.');
                    }
                }

                // Log message
                if (this.logger.IsDebugEnabled)
                {
                    this.logger.Debug("Submitting new potential model to HypersearchWorker: \n "
                                   + modelParams.ToString());
                }
                modelResults.Add(new Tuple<ModelParams, string, string>(modelParams, paramsHash, particleHash));
            }
            return new Tuple<bool, List<Tuple<ModelParams, string, string>>>(false, modelResults);
        }
        /// <summary>
        ///  Record or update the results for a model. This is called by the
        ///  HSW whenever it gets results info for another model, or updated results
        ///  on a model that is still running.
        /// 
        ///  The first time this is called for a given modelID, the modelParams will
        ///  contain the params dict for that model and the modelParamsHash will contain
        ///  the hash of the params. Subsequent updates of the same modelID will
        ///  have params and paramsHash values of None (in order to save overhead).
        /// 
        ///  The Hypersearch object should save these results into it's own working
        ///  memory into some table, which it then uses to determine what kind of
        ///  new models to create next time createModels() is called.
        /// 
        ///  Parameters:
        ///  ----------------------------------------------------------------------
        ///  modelID:        ID of this model in models table
        ///  modelParams:    params dict for this model, or None if this is just an update
        ///                  of a model that it already previously reported on.
        /// 
        ///                  See the comments for the createModels() method for a
        ///                  description of this dict.
        /// 
        ///  modelParamsHash:  hash of the modelParams dict, generated by the worker
        ///                  that put it into the model database.
        ///  results:        tuple containing (allMetrics, optimizeMetric). Each is a
        ///                  dict containing metricName:result pairs. .
        ///                  May be none if we have no results yet.
        ///  completed:      True if the model has completed evaluation, False if it
        ///                    is still running (and these are online results)
        ///  completionReason: One of the ClientJobsDAO.CMPL_REASON_XXX equates
        ///  matured:        True if this model has matured. In most cases, once a
        ///                  model matures, it will complete as well. The only time a
        ///                  model matures and does not complete is if it's currently
        ///                  the best model and we choose to keep it running to generate
        ///                  predictions.
        ///  numRecords:     Number of records that have been processed so far by this
        ///                    model.
        /// </summary>
        /// <param name="modelID"></param>
        /// <param name="modelParams"></param>
        /// <param name="modelParamsHash"></param>
        /// <param name="results"></param>
        /// <param name="completed"></param>
        /// <param name="completionReason"></param>
        /// <param name="matured"></param>
        /// <param name="numRecords"></param>
        public void recordModelProgress(ulong modelID, ModelParams modelParams, string modelParamsHash, Tuple results,
                             bool completed, string completionReason, bool matured, uint numRecords)
        {
            double? metricResult;
            if (results == null)
            {
                metricResult = null;
            }
            else
            {
                metricResult = ((Map<string, double?>)results.Get(1))?.Values.First();
            }

            // Update our database.
            double? errScore = this._resultsDB.update(modelId: modelID,
                        modelParams: modelParams, modelParamsHash: modelParamsHash,
                        metricResult: metricResult, completed: completed,
                        completionReason: completionReason, matured: matured,
                        numRecords: numRecords);

            // Log message.
            this.logger.Debug(string.Format("Received progress on model {0}: completed: {1}, cmpReason: {2}, " +
                                            "numRecords: {3}, errScore: {4}",
                              modelID, completed, completionReason, numRecords, errScore));

            // Log best so far.
            //(bestModelID, bestResult) = this._resultsDB.bestModelIdAndErrScore();
            var bestModelPair = this._resultsDB.bestModelIdAndErrScore();
            ulong? bestModelID = bestModelPair.Item1;
            double? bestResult = bestModelPair.Item2;
            this.logger.Debug(string.Format("Best err score seen so far: {0} on model {1}",
                         bestResult, bestModelID));
        }
        /// <summary>
        ///  Run the given model.
        /// 
        ///  This runs the model described by 'modelParams'. Periodically, it updates
        ///  the results seen on the model to the model database using the databaseAO
        ///  (database Access Object) methods.
        /// 
        ///  Parameters:
        ///  -------------------------------------------------------------------------
        ///  modelID:             ID of this model in models table
        /// 
        ///  jobID:               ID for this hypersearch job in the jobs table
        /// 
        ///  modelParams:         parameters of this specific model
        ///                       modelParams is a dictionary containing the name/value
        ///                       pairs of each variable we are permuting over. Note that
        ///                       variables within an encoder spec have their name
        ///                       structure as:
        ///                         <encoderName>.<encodrVarName>
        /// 
        ///  modelParamsHash:     hash of modelParamValues
        /// 
        ///  jobsDAO              jobs data access object - the interface to the jobs
        ///                        database where model information is stored
        /// 
        ///  modelCheckpointGUID: A persistent, globally-unique identifier for
        ///                        constructing the model checkpoint key
        /// </summary>
        /// <param name="modelID"></param>
        /// <param name="jobID"></param>
        /// <param name="modelParams"></param>
        /// <param name="modelParamsHash"></param>
        /// <param name="jobsDAO"></param>
        /// <param name="modelCheckpointGUID"></param>
        public void runModel(ulong modelID, uint? jobID, ModelParams modelParams, string modelParamsHash,
           BaseClientJobDao jobsDAO, ref string modelCheckpointGUID)
        {
            // We're going to make an assumption that if we're not using streams, that
            //  we also don't need checkpoints saved. For now, this assumption is OK
            //  (if there are no streams, we're typically running on a single machine
            //  and just save models to files) but we may want to break this out as
            //  a separate controllable parameter in the future
            if (!this._createCheckpoints)
            {
                modelCheckpointGUID = null;
            }

            // Register this model in our database
            this._resultsDB.update(modelId: modelID,
                                   modelParams: modelParams,
                                   modelParamsHash: modelParamsHash,
                                   metricResult: null,
                                   completed: false,
                                   completionReason: null,
                                   matured: false,
                                   numRecords: 0);

            // Get the structured params, which we pass to the base description
            PermutationModelParameters structuredParams = modelParams.structuredParams;

            if (this.logger.IsDebugEnabled)
            {
                this.logger.Debug(string.Format("Running Model. \nmodelParams: {0}, \nmodelID={1}, ",
                            modelParams, modelID));
            }

            // Record time.clock() so that we can report on cpu time
            var cpuTimeStart = DateTime.Now.Ticks;

            // Run the experiment.This will report the results back to the models
            // database for us as well.
            try
            {
                string cmpReason;
                string cmpMsg;
                if (this._dummyModel == null || (this._dummyModel is bool && (bool)this._dummyModel == false))
                {
                    // (cmpReason, cmpMsg)
                    var pair = Utils.runModelGivenBaseAndParams(
                                modelID: modelID,
                                jobID: jobID,
                                baseDescription: this._baseDescription,
                                @params: structuredParams,
                                predictedField: this._predictedField,
                                reportKeys: this._reportKeys,
                                optimizeKey: this._optimizeKey,
                                jobsDAO: jobsDAO,
                                modelCheckpointGUID: modelCheckpointGUID,
                                predictionCacheMaxRecords: this._predictionCacheMaxRecords);
                    cmpReason = pair.completionReason;
                    cmpMsg = pair.completionMsg;
                }
                else
                {
                    //var dummyParams = dict(this._dummyModel);
                    //dummyParams["permutationParams"] = structuredParams;
                    //if (this._dummyModelParamsFunc != null)
                    //{
                    //    var permInfo = dict(structuredParams);
                    //    permInfo["generation"] = modelParams.particleState.genIdx;
                    //    dummyParams.update(this._dummyModelParamsFunc(permInfo));
                    //}
                    //// (cmpReason, cmpMsg) =
                    //var pair = Utils.runDummyModel(
                    //              modelID: modelID,
                    //              jobID: jobID,
                    //              @params: dummyParams,
                    //              predictedField: this._predictedField,
                    //              reportKeys: this._reportKeys,
                    //              optimizeKey: this._optimizeKey,
                    //              jobsDAO: jobsDAO,
                    //              modelCheckpointGUID: modelCheckpointGUID,
                    //              predictionCacheMaxRecords: this._predictionCacheMaxRecords);
                    //cmpReason = pair.completionReason;
                    //cmpMsg = pair.completionMsg;
                    throw new NotImplementedException("Check the dummy stuff");
                }

                // Write out the completion reason and message
                jobsDAO.modelSetCompleted(modelID,
                                      completionReason: cmpReason,
                                      completionMsg: cmpMsg,
                                      cpuTime: DateTime.Now.Ticks - cpuTimeStart);
            }
            catch (Exception e)
            {
                this.logger.Warn(e);
                if (Debugger.IsAttached) Debugger.Break();
                throw;
            }
        }

        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }

    /*

      def runModel(self, modelID, jobID, modelParams, modelParamsHash,
                   jobsDAO, modelCheckpointGUID)
      {


        // We're going to make an assumption that if we're not using streams, that
        //  we also don't need checkpoints saved. For now, this assumption is OK
        //  (if there are no streams, we're typically running on a single machine
        //  and just save models to files) but we may want to break this out as
        //  a separate controllable parameter in the future
        if( not this._createCheckpoints)
        {
          modelCheckpointGUID = None;
        }

        // Register this model in our database
        this._resultsDB.update(modelID=modelID,
                               modelParams=modelParams,
                               modelParamsHash=modelParamsHash,
                               metricResult = None,
                               completed = False,
                               completionReason = None,
                               matured = False,
                               numRecords = 0);

        // Get the structured params, which we pass to the base description
        structuredParams = modelParams["structuredParams"];

        if( this.logger.getEffectiveLevel() <= logging.DEBUG)
        {
          this.logger.debug("Running Model. \nmodelParams: %s, \nmodelID=%s, " % \
                            (pprint.pformat(modelParams, indent=4), modelID));
        }

        // Record time.clock() so that we can report on cpu time
        cpuTimeStart = time.clock();

        // Run the experiment. This will report the results back to the models
        //  database for us as well.
        logLevel = this.logger.getEffectiveLevel();
        try
        {
          if( this._dummyModel is None or this._dummyModel is False)
          {
            (cmpReason, cmpMsg) = runModelGivenBaseAndParams(
                        modelID=modelID,
                        jobID=jobID,
                        baseDescription=this._baseDescription,
                        params=structuredParams,
                        predictedField=this._predictedField,
                        reportKeys=this._reportKeys,
                        optimizeKey=this._optimizeKey,
                        jobsDAO=jobsDAO,
                        modelCheckpointGUID=modelCheckpointGUID,
                        logLevel=logLevel,
                        predictionCacheMaxRecords=this._predictionCacheMaxRecords);
          }
          else
          {
            dummyParams = dict(this._dummyModel);
            dummyParams["permutationParams"] = structuredParams;
            if( this._dummyModelParamsFunc is not None)
            {
              permInfo = dict(structuredParams);
              permInfo ["generation"] = modelParams["particleState"]["genIdx"];
              dummyParams.update(this._dummyModelParamsFunc(permInfo));
            }

            (cmpReason, cmpMsg) = runDummyModel(
                          modelID=modelID,
                          jobID=jobID,
                          params=dummyParams,
                          predictedField=this._predictedField,
                          reportKeys=this._reportKeys,
                          optimizeKey=this._optimizeKey,
                          jobsDAO=jobsDAO,
                          modelCheckpointGUID=modelCheckpointGUID,
                          logLevel=logLevel,
                          predictionCacheMaxRecords=this._predictionCacheMaxRecords);
          }

          // Write out the completion reason and message
          jobsDAO.modelSetCompleted(modelID,
                                completionReason = cmpReason,
                                completionMsg = cmpMsg,
                                cpuTime = time.clock() - cpuTimeStart);
        }


        catch( InvalidConnectionException, e)
        {
          this.logger.warn("%s", e);
        }
      }
    }

    class SwarmTerminator(object)
    {
      """Class that records the performane of swarms in a sprint and makes
      decisions about which swarms should stop running. This is a usful optimization
      that identifies field combinations that no longer need to be run.
      """;
      MATURITY_WINDOW = None;
      MAX_GENERATIONS = None;
      _DEFAULT_MILESTONES = [1.0 / (x + 1) for x in xrange(12)];

      def __init__(self, milestones=None, logLevel=None)
      {
        // Set class constants.
        MATURITY_WINDOW  = int(Configuration.get(
                                          "nupic.hypersearch.swarmMaturityWindow"));
        this.MAX_GENERATIONS = int(Configuration.get(
                                          "nupic.hypersearch.swarmMaxGenerations"));
        if( this.MAX_GENERATIONS < 0)
        {
          this.MAX_GENERATIONS = None;
        }

        // Set up instsance variables.

        this._isTerminationEnabled = bool(int(Configuration.get(
            'nupic.hypersearch.enableSwarmTermination')));

        this.swarmBests = dict();
        this.swarmScores = dict();
        this.terminatedSwarms = set([]);

        this._logger = logging.getLogger(".".join(
            ["com.numenta', this.__class__.__module__, this.__class__.__name__]));

        if( milestones is not None)
        {
          this.milestones = milestones;
        }
        else
        {
          this.milestones = copy.deepcopy(this._DEFAULT_MILESTONES);
        }
      }

      def recordDataPoint(self, swarmId, generation, errScore)
      {
        """Record the best score for a swarm's generation index (x)
        Returns list of swarmIds to terminate.
        """;
        terminatedSwarms = [];

        // Append score to existing swarm.
        if( swarmId in this.swarmScores)
        {
          entry = this.swarmScores[swarmId];
          assert(len(entry) == generation);
          entry.append(errScore);

          entry = this.swarmBests[swarmId];
          entry.append(min(errScore, entry[-1]));

          assert(len(this.swarmBests[swarmId]) == len(this.swarmScores[swarmId]));
        }
        else
        {
          // Create list of scores for a new swarm
          assert (generation == 0);
          this.swarmScores[swarmId] = [errScore];
          this.swarmBests[swarmId] = [errScore];
        }

        // If the current swarm hasn't completed at least MIN_GENERATIONS, it should
        // not be candidate for maturation or termination. This prevents the initial
        // allocation of particles in PSO from killing off a field combination too
        // early.
        if( generation + 1 < this.MATURITY_WINDOW)
        {
          return terminatedSwarms;
        }

        // If the swarm has completed more than MAX_GENERATIONS, it should be marked
        // as mature, regardless of how its value is changing.
        if( this.MAX_GENERATIONS is not None and generation > this.MAX_GENERATIONS)
        {
          this._logger.info(
              'Swarm %s has matured (more than %d generations). Stopping' %
              (swarmId, this.MAX_GENERATIONS));
          terminatedSwarms.append(swarmId);
        }

        if( this._isTerminationEnabled)
        {
          terminatedSwarms.extend(this._getTerminatedSwarms(generation));
        }

        // Return which swarms to kill when we've reached maturity
        // If there is no change in the swarm's best for some time,
        // Mark it dead
        cumulativeBestScores = this.swarmBests[swarmId];
        if( cumulativeBestScores[-1] == cumulativeBestScores[-this.MATURITY_WINDOW])
        {
          this._logger.info('Swarm %s has matured (no change in %d generations).'
                            'Stopping...'% (swarmId, this.MATURITY_WINDOW));
          terminatedSwarms.append(swarmId);
        }

        this.terminatedSwarms = this.terminatedSwarms.union(terminatedSwarms);
        return terminatedSwarms;
      }

      def numDataPoints(self, swarmId)
      {
        if( swarmId in this.swarmScores)
        {
          return len(this.swarmScores[swarmId]);
        }
        else
        {
          return 0;
        }
      }

      def _getTerminatedSwarms(self, generation)
      {
        terminatedSwarms = [];
        generationScores = dict();
        for( swarm, scores in this.swarmScores.iteritems())
        {
          if( len(scores) > generation and swarm not in this.terminatedSwarms)
          {
            generationScores[swarm] = scores[generation];
          }
        }

        if( len(generationScores) == 0)
        {
          return;
        }

        bestScore = min(generationScores.values());
        tolerance = this.milestones[generation];

        for( swarm, score in generationScores.iteritems())
        {
          if( score > (1 + tolerance) * bestScore)
          {
            this._logger.info('Swarm %s is doing poorly at generation %d.\n'
                              'Current Score:%s \n'
                              'Best Score:%s \n'
                              'Tolerance:%s. Stopping...',
                              swarm, generation, score, bestScore, tolerance);
            terminatedSwarms.append(swarm);
          }
        }
        return terminatedSwarms;
      }
    }

    class ResultsDB(object)
    {
      """This class holds all the information we have accumulated on completed
      models, which particles were used, etc.

      When we get updated results sent to us (via recordModelProgress), we
      record it here for access later by various functions in this module.
      """;

      def __init__(self, hsObj)
      {
        """ Instantiate our results database

        Parameters:
        --------------------------------------------------------------------
        hsObj:        Reference to the HypersearchV2 instance
        """;
        this._hsObj = hsObj;

        // This list holds all the results we have so far on every model. In
        //  addition, we maintain mutliple other data structures which provide
        //  faster access into portions of this list
        this._allResults = [];

        // Models that completed with errors and all completed.
        // These are used to determine when we should abort because of too many
        //   errors
        this._errModels = set();
        this._numErrModels = 0;
        this._completedModels = set();
        this._numCompletedModels = 0;

        // Map of the model ID to index of result in _allResults
        this._modelIDToIdx = dict();

        // The global best result on the optimize metric so far, and the model ID
        this._bestResult = numpy.inf;
        this._bestModelID = None;

        // This is a dict of dicts. The top level dict has the swarmId as the key.
        // Each entry is a dict of genIdx: (modelId, errScore) entries.
        this._swarmBestOverall = dict();

        // For each swarm, we keep track of how many particles we have per generation
        // The key is the swarmId, the value is a list of the number of particles
        // at each generation
        this._swarmNumParticlesPerGeneration = dict();

        // The following variables are used to support the
        // getMaturedSwarmGenerations() call.
        #
        // The _modifiedSwarmGens set contains the set of (swarmId, genIdx) tuples
        // that have had results reported to them since the last time
        // getMaturedSwarmGenerations() was called.
        #
        // The maturedSwarmGens contains (swarmId,genIdx) tuples, one for each
        // swarm generation index which we have already detected has matured. This
        // insures that if by chance we get a rogue report from a model in a swarm
        // generation index which we have already assumed was matured that we won't
        // report on it again.
        this._modifiedSwarmGens = set();
        this._maturedSwarmGens = set();

        // For each particle, we keep track of it's best score (across all
        // generations) and the position it was at when it got that score. The keys
        // in this dict are the particleId, the values are (bestResult, position),
        // where position is a dict with varName:position items in it.
        this._particleBest = dict();

        // For each particle, we keep track of it's latest generation index.
        this._particleLatestGenIdx = dict();

        // For each swarm, we keep track of which models are in it. The key
        // is the swarmId, the value is a list of indexes into this._allResults.
        this._swarmIdToIndexes = dict();

        // ParamsHash to index mapping
        this._paramsHashToIndexes = dict();
      }


      def update(self, modelID, modelParams, modelParamsHash, metricResult,
                 completed, completionReason, matured, numRecords)
      {
        """ Insert a new entry or update an existing one. If this is an update
        of an existing entry, then modelParams will be None

        Parameters:
        --------------------------------------------------------------------
        modelID:       globally unique modelID of this model
        modelParams:    params dict for this model, or None if this is just an update
                        of a model that it already previously reported on.

                        See the comments for the createModels() method for
                        a description of this dict.

        modelParamsHash:  hash of the modelParams dict, generated by the worker
                        that put it into the model database.
        metricResult:   value on the optimizeMetric for this model.
                        May be None if we have no results yet.
        completed:      True if the model has completed evaluation, False if it
                          is still running (and these are online results)
        completionReason: One of the ClientJobsDAO.CMPL_REASON_XXX equates
        matured:        True if this model has matured
        numRecords:     Number of records that have been processed so far by this
                          model.

        retval: Canonicalized result on the optimize metric
        """;
        // The modelParamsHash must always be provided - it can change after a
        //  model is inserted into the models table if it got detected as an
        //  orphan
        assert (modelParamsHash is not None);

        // We consider a model metricResult as "final" if it has completed or
        //  matured. By default, assume anything that has completed has matured
        if( completed)
        {
          matured = True;
        }

        // Get the canonicalized optimize metric results. For this metric, lower
        //  is always better
        if( metricResult is not None and matured and \
                           completionReason in [ClientJobsDAO.CMPL_REASON_EOF,
                                                ClientJobsDAO.CMPL_REASON_STOPPED])
        {
          // Canonicalize the error score so that lower is better
          if( this._hsObj._maximize)
          {
            errScore = -1 * metricResult;
          }
          else
          {
            errScore = metricResult;
          }

          if( errScore < this._bestResult)
          {
            this._bestResult = errScore;
            this._bestModelID = modelID;
            this._hsObj.logger.info("New best model after %d evaluations: errScore "
                  "%g on model %s" % (len(this._allResults), this._bestResult,
                                      this._bestModelID));
          }
        }

        else
        {
          errScore = numpy.inf;
        }

        // If this model completed with an unacceptable completion reason, set the
        //  errScore to infinite and essentially make this model invisible to
        //  further queries
        if( completed and completionReason in [ClientJobsDAO.CMPL_REASON_ORPHAN])
        {
          errScore = numpy.inf;
          hidden = True;
        }
        else
        {
          hidden = False;
        }

        // Update our set of erred models and completed models. These are used
        //  to determine if we should abort the search because of too many errors
        if( completed)
        {
          this._completedModels.add(modelID);
          this._numCompletedModels = len(this._completedModels);
          if( completionReason == ClientJobsDAO.CMPL_REASON_ERROR)
          {
            this._errModels.add(modelID);
            this._numErrModels = len(this._errModels);
          }
        }

        // Are we creating a new entry?
        wasHidden = False;
        if( modelID not in this._modelIDToIdx)
        {
          assert (modelParams is not None);
          entry = dict(modelID=modelID, modelParams=modelParams,
                       modelParamsHash=modelParamsHash,
                       errScore=errScore, completed=completed,
                       matured=matured, numRecords=numRecords, hidden=hidden);
          this._allResults.append(entry);
          entryIdx = len(this._allResults) - 1;
          this._modelIDToIdx[modelID] = entryIdx;

          this._paramsHashToIndexes[modelParamsHash] = entryIdx;

          swarmId = modelParams["particleState"]["swarmId"];
          if( not hidden)
          {
            // Update the list of particles in each swarm
            if( swarmId in this._swarmIdToIndexes)
            {
              this._swarmIdToIndexes[swarmId].append(entryIdx);
            }
            else
            {
              this._swarmIdToIndexes[swarmId] = [entryIdx];
            }

            // Update number of particles at each generation in this swarm
            genIdx = modelParams["particleState"]["genIdx"];
            numPsEntry = this._swarmNumParticlesPerGeneration.get(swarmId, [0]);
            while( genIdx >= len(numPsEntry))
            {
              numPsEntry.append(0);
            }
            numPsEntry[genIdx] += 1;
            this._swarmNumParticlesPerGeneration[swarmId] = numPsEntry;
          }
        }

        // Replacing an existing one
        else
        {
          entryIdx = this._modelIDToIdx.get(modelID, None);
          assert (entryIdx is not None);
          entry = this._allResults[entryIdx];
          wasHidden = entry["hidden"];

          // If the paramsHash changed, note that. This can happen for orphaned
          //  models
          if( entry["modelParamsHash"] != modelParamsHash)
          {

            this._paramsHashToIndexes.pop(entry["modelParamsHash"]);
            this._paramsHashToIndexes[modelParamsHash] = entryIdx;
            entry["modelParamsHash"] = modelParamsHash;
          }

          // Get the model params, swarmId, and genIdx
          modelParams = entry["modelParams"];
          swarmId = modelParams["particleState"]["swarmId"];
          genIdx = modelParams["particleState"]["genIdx"];

          // If this particle just became hidden, remove it from our swarm counts
          if( hidden and not wasHidden)
          {
            assert (entryIdx in this._swarmIdToIndexes[swarmId]);
            this._swarmIdToIndexes[swarmId].remove(entryIdx);
            this._swarmNumParticlesPerGeneration[swarmId][genIdx] -= 1;
          }

          // Update the entry for the latest info
          entry["errScore"]  = errScore;
          entry["completed"] = completed;
          entry["matured"] = matured;
          entry["numRecords"] = numRecords;
          entry["hidden"] = hidden;
        }

        // Update the particle best errScore
        particleId = modelParams["particleState"]["id"];
        genIdx = modelParams["particleState"]["genIdx"];
        if( matured and not hidden)
        {
          (oldResult, pos) = this._particleBest.get(particleId, (numpy.inf, None));
          if( errScore < oldResult)
          {
            pos = Particle.getPositionFromState(modelParams["particleState"]);
            this._particleBest[particleId] = (errScore, pos);
          }
        }

        // Update the particle latest generation index
        prevGenIdx = this._particleLatestGenIdx.get(particleId, -1);
        if( not hidden and genIdx > prevGenIdx)
        {
          this._particleLatestGenIdx[particleId] = genIdx;
        }
        else if( hidden and not wasHidden and genIdx == prevGenIdx)
        {
          this._particleLatestGenIdx[particleId] = genIdx-1;
        }

        // Update the swarm best score
        if( not hidden)
        {
          swarmId = modelParams["particleState"]["swarmId"];
          if( not swarmId in this._swarmBestOverall)
          {
            this._swarmBestOverall[swarmId] = [];
          }

          bestScores = this._swarmBestOverall[swarmId];
          while( genIdx >= len(bestScores))
          {
            bestScores.append((None, numpy.inf));
          }
          if( errScore < bestScores[genIdx][1])
          {
            bestScores[genIdx] = (modelID, errScore);
          }
        }

        // Update the this._modifiedSwarmGens flags to support the
        //   getMaturedSwarmGenerations() call.
        if( not hidden)
        {
          key = (swarmId, genIdx);
          if( not key in this._maturedSwarmGens)
          {
            this._modifiedSwarmGens.add(key);
          }
        }

        return errScore;
      }

      def getNumErrModels(self)
      {
        """Return number of models that completed with errors.

        Parameters:
        ---------------------------------------------------------------------
        retval:      // if models
        """;
        return this._numErrModels;
      }

      def getErrModelIds(self)
      {
        """Return list of models IDs that completed with errors.

        Parameters:
        ---------------------------------------------------------------------
        retval:      // if models
        """;
        return list(this._errModels);
      }

      def getNumCompletedModels(self)
      {
        """Return total number of models that completed.

        Parameters:
        ---------------------------------------------------------------------
        retval:      // if models that completed
        """;
        return this._numCompletedModels;
      }

          def getModelIDFromParamsHash(self, paramsHash)
      {
        """ Return the modelID of the model with the given paramsHash, or
        None if not found.

        Parameters:
        ---------------------------------------------------------------------
        paramsHash:  paramsHash to look for
        retval:      modelId, or None if not found
        """;
        entryIdx = this. _paramsHashToIndexes.get(paramsHash, None);
        if( entryIdx is not None)
        {
          return this._allResults[entryIdx]["modelID"];
        }
        else
        {
          return None;
        }
      }

          def numModels(self, swarmId=None, includeHidden=False)
      {
        """Return the total // of models we have in our database (if swarmId is
        None) or in a specific swarm.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:        A string representation of the sorted list of encoders
                        in this swarm. For example '__address_encoder.__gym_encoder'
        includeHidden:  If False, this will only return the number of models
                        that are not hidden (i.e. orphanned, etc.)
        retval:  numModels
        """;
        // Count all models
        if( includeHidden)
        {
          if( swarmId is None)
          {
            return len(this._allResults);
          }

          else
          {
            return len(this._swarmIdToIndexes.get(swarmId, []));
          }
        }
        // Only count non-hidden models
        else
        {
          if( swarmId is None)
          {
            entries = this._allResults;
          }
          else
          {
            entries = [this._allResults[entryIdx]
                       for entryIdx in this._swarmIdToIndexes.get(swarmId,[])];
          }

          return len([entry for entry in entries if not entry["hidden"]]);
        }
      }

        def bestModelIdAndErrScore(self, swarmId=None, genIdx=None)
      {
        """Return the model ID of the model with the best result so far and
        it's score on the optimize metric. If swarm is None, then it returns
        the global best, otherwise it returns the best for the given swarm
        for all generatons up to and including genIdx.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:  A string representation of the sorted list of encoders in this
                     swarm. For example '__address_encoder.__gym_encoder'
        genIdx:   consider the best in all generations up to and including this
                    generation if not None.
        retval:  (modelID, result)
        """;
        if( swarmId is None)
        {
          return (this._bestModelID, this._bestResult);
        }

        else
        {
          if( swarmId not in this._swarmBestOverall)
          {
            return (None, numpy.inf);
          }


          // Get the best score, considering the appropriate generations
          genScores = this._swarmBestOverall[swarmId];
          bestModelId = None;
          bestScore = numpy.inf;

          for (i, (modelId, errScore)) in enumerate(genScores)
          {
            if( genIdx is not None and i > genIdx)
            {
              break;
            }
            if( errScore < bestScore)
            {
              bestScore = errScore;
              bestModelId = modelId;
            }
          }

          return (bestModelId, bestScore);
        }
      }

        def getParticleInfo(self, modelId)
      {
        """Return particle info for a specific modelId.

        Parameters:
        ---------------------------------------------------------------------
        modelId:  which model Id

        retval:  (particleState, modelId, errScore, completed, matured)
        """;
        entry = this._allResults[this._modelIDToIdx[modelId]];
        return (entry["modelParams"]["particleState"], modelId, entry["errScore"],
                entry["completed"], entry["matured"]);
      }


    def getParticleInfos(self, swarmId=None, genIdx=None, completed=None,
                           matured=None, lastDescendent=False)
      {
        """Return a list of particleStates for all particles we know about in
        the given swarm, their model Ids, and metric results.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:  A string representation of the sorted list of encoders in this
                     swarm. For example '__address_encoder.__gym_encoder'

        genIdx:  If not None, only return particles at this specific generation
                      index.

        completed:   If not None, only return particles of the given state (either
                    completed if 'completed' is True, or running if 'completed'
                    is false

        matured:   If not None, only return particles of the given state (either
                    matured if 'matured' is True, or not matured if 'matured'
                    is false. Note that any model which has completed is also
                    considered matured.

        lastDescendent: If True, only return particles that are the last descendent,
                    that is, the highest generation index for a given particle Id

        retval:  (particleStates, modelIds, errScores, completed, matured)
                  particleStates: list of particleStates
                  modelIds: list of modelIds
                  errScores: list of errScores, numpy.inf is plugged in
                                  if we don't have a result yet
                  completed: list of completed booleans
                  matured: list of matured booleans
        """;
        // The indexes of all the models in this swarm. This list excludes hidden
        //  (orphaned) models.
        if( swarmId is not None)
        {
          entryIdxs = this._swarmIdToIndexes.get(swarmId, []);
        }
        else
        {
          entryIdxs = range(len(this._allResults));
        }
        if( len(entryIdxs) == 0)
        {
          return ([], [], [], [], []);
        }

        // Get the particles of interest
        particleStates = [];
        modelIds = [];
        errScores = [];
        completedFlags = [];
        maturedFlags = [];
        for( idx in entryIdxs)
        {
          entry = this._allResults[idx];

          // If this entry is hidden (i.e. it was an orphaned model), it should
          //  not be in this list
          if( swarmId is not None)
          {
            assert (not entry["hidden"]);
          }

          // Get info on this model
          modelParams = entry["modelParams"];
          isCompleted = entry["completed"];
          isMatured = entry["matured"];
          particleState = modelParams["particleState"];
          particleGenIdx = particleState["genIdx"];
          particleId = particleState["id"];

          if( genIdx is not None and particleGenIdx != genIdx)
          {
            continue;
          }

          if( completed is not None and (completed != isCompleted))
          {
            continue;
          }

          if( matured is not None and (matured != isMatured))
          {
            continue;
          }

          if( lastDescendent \
                  and (this._particleLatestGenIdx[particleId] != particleGenIdx))
          {
            continue;
          }

          // Incorporate into return values
          particleStates.append(particleState);
          modelIds.append(entry["modelID"]);
          errScores.append(entry["errScore"]);
          completedFlags.append(isCompleted);
          maturedFlags.append(isMatured);
        }


        return (particleStates, modelIds, errScores, completedFlags, maturedFlags);
      }

    def getOrphanParticleInfos(self, swarmId, genIdx)
      {
        """Return a list of particleStates for all particles in the given
        swarm generation that have been orphaned.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:  A string representation of the sorted list of encoders in this
                     swarm. For example '__address_encoder.__gym_encoder'

        genIdx:  If not None, only return particles at this specific generation
                      index.

        retval:  (particleStates, modelIds, errScores, completed, matured)
                  particleStates: list of particleStates
                  modelIds: list of modelIds
                  errScores: list of errScores, numpy.inf is plugged in
                                  if we don't have a result yet
                  completed: list of completed booleans
                  matured: list of matured booleans
        """;

        entryIdxs = range(len(this._allResults));
        if( len(entryIdxs) == 0)
        {
          return ([], [], [], [], []);
        }

        // Get the particles of interest
        particleStates = [];
        modelIds = [];
        errScores = [];
        completedFlags = [];
        maturedFlags = [];
        for( idx in entryIdxs)
        {

          // Get info on this model
          entry = this._allResults[idx];
          if( not entry["hidden"])
          {
            continue;
          }

          modelParams = entry["modelParams"];
          if( modelParams["particleState"]["swarmId"] != swarmId)
          {
            continue;
          }

          isCompleted = entry["completed"];
          isMatured = entry["matured"];
          particleState = modelParams["particleState"];
          particleGenIdx = particleState["genIdx"];
          particleId = particleState["id"];

          if( genIdx is not None and particleGenIdx != genIdx)
          {
            continue;
          }

          // Incorporate into return values
          particleStates.append(particleState);
          modelIds.append(entry["modelID"]);
          errScores.append(entry["errScore"]);
          completedFlags.append(isCompleted);
          maturedFlags.append(isMatured);
        }

        return (particleStates, modelIds, errScores, completedFlags, maturedFlags);
      }

    def getMaturedSwarmGenerations(self)
      {
        """Return a list of swarm generations that have completed and the
        best (minimal) errScore seen for each of them.

        Parameters:
        ---------------------------------------------------------------------
        retval:  list of tuples. Each tuple is of the form:
                  (swarmId, genIdx, bestErrScore)
        """;
        // Return results go in this list
        result = [];


        // For each of the swarm generations which have had model result updates
        // since the last time we were called, see which have completed.
        modifiedSwarmGens = sorted(this._modifiedSwarmGens);

        // Walk through them in order from lowest to highest generation index
        for( key in modifiedSwarmGens)
        {
          (swarmId, genIdx) = key;

          // Skip it if we've already reported on it. This should happen rarely, if
          //  ever. It means that some worker has started and completed a model in
          //  this generation after we've determined that the generation has ended.
          if( key in this._maturedSwarmGens)
          {
            this._modifiedSwarmGens.remove(key);
            continue;
          }

          // If the previous generation for this swarm is not complete yet, don't
          //  bother evaluating this one.
          if (genIdx >= 1) and not (swarmId, genIdx-1) in this._maturedSwarmGens
          {
            continue;
          }

          // We found a swarm generation that had some results reported since last
          // time, see if it's complete or not
          (_, _, errScores, completedFlags, maturedFlags) = \
                                    this.getParticleInfos(swarmId, genIdx);
          maturedFlags = numpy.array(maturedFlags);
          numMatured = maturedFlags.sum();
          if( numMatured >= this._hsObj._minParticlesPerSwarm \
                and numMatured == len(maturedFlags))
          {
            errScores = numpy.array(errScores);
            bestScore = errScores.min();

            this._maturedSwarmGens.add(key);
            this._modifiedSwarmGens.remove(key);
            result.append((swarmId, genIdx, bestScore));
          }
        }

        // Return results
        return result;
      }

def firstNonFullGeneration(self, swarmId, minNumParticles)
      {
        """ Return the generation index of the first generation in the given
        swarm that does not have numParticles particles in it, either still in the
        running state or completed. This does not include orphaned particles.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:  A string representation of the sorted list of encoders in this
                     swarm. For example '__address_encoder.__gym_encoder'
        minNumParticles: minium number of partices required for a full
                      generation.

        retval:  generation index, or None if no particles at all.
        """;

        if( not swarmId in this._swarmNumParticlesPerGeneration)
        {
          return None;
        }

        numPsPerGen = this._swarmNumParticlesPerGeneration[swarmId];

        numPsPerGen = numpy.array(numPsPerGen);
        firstNonFull = numpy.where(numPsPerGen < minNumParticles)[0];
        if( len(firstNonFull) == 0)
        {
          return len(numPsPerGen);
        }
        else
        {
          return firstNonFull[0];
        }
      }

      def highestGeneration(self, swarmId)
      {
        """ Return the generation index of the highest generation in the given
        swarm.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:  A string representation of the sorted list of encoders in this
                     swarm. For example '__address_encoder.__gym_encoder'
        retval:  generation index
        """;
        numPsPerGen = this._swarmNumParticlesPerGeneration[swarmId];
        return len(numPsPerGen)-1;
      }

      def getParticleBest(self, particleId)
      {
        """ Return the best score and position for a given particle. The position
        is given as a dict, with varName:varPosition items in it.

        Parameters:
        ---------------------------------------------------------------------
        particleId:    which particle
        retval:        (bestResult, bestPosition)
        """;
        return this._particleBest.get(particleId, (None, None));
      }

      def getResultsPerChoice(self, swarmId, maxGenIdx, varName)
      {
        """ Return a dict of the errors obtained on models that were run with
        each value from a PermuteChoice variable.

        For example, if a PermuteChoice variable has the following choices:
          ["a', 'b', 'c"]

        The dict will have 3 elements. The keys are the stringified choiceVars,
        and each value is tuple containing (choiceVar, errors) where choiceVar is
        the original form of the choiceVar (before stringification) and errors is
        the list of errors received from models that used the specific choice:
        retval:
          ["a':('a', [0.1, 0.2, 0.3]), 'b':('b', [0.5, 0.1, 0.6]), 'c':('c', [])]


        Parameters:
        ---------------------------------------------------------------------
        swarmId:  swarm Id of the swarm to retrieve info from
        maxGenIdx: max generation index to consider from other models, ignored
                    if None
        varName:  which variable to retrieve

        retval:  list of the errors obtained from each choice.
        """;
        results = dict();
        // Get all the completed particles in this swarm
        (allParticles, _, resultErrs, _, _) = this.getParticleInfos(swarmId,
                                                  genIdx=None, matured=True);

        for( particleState, resultErr in itertools.izip(allParticles, resultErrs))
        {
          // Consider this generation?
          if( maxGenIdx is not None)
          {
            if( particleState["genIdx"] > maxGenIdx)
            {
              continue;
            }
          }

          // Ignore unless this model completed successfully
          if( resultErr == numpy.inf)
          {
            continue;
          }

          position = Particle.getPositionFromState(particleState);
          varPosition = position[varName];
          varPositionStr = str(varPosition);
          if( varPositionStr in results)
          {
            results[varPositionStr][1].append(resultErr);
          }
          else
          {
            results[varPositionStr] = (varPosition, [resultErr]);
          }
        }

        return results;
      }
    }

class Particle(object)
    {

      _nextParticleID = 0;

      def __init__(self, hsObj, resultsDB, flattenedPermuteVars,
                   swarmId=None, newFarFrom=None, evolveFromState=None,
                   newFromClone=None, newParticleId=False)
      {

        // Save constructor arguments
        this._hsObj = hsObj;
        this.logger = hsObj.logger;
        this._resultsDB = resultsDB;

        // See the random number generator used for all the variables in this
        // particle. We will seed it differently based on the construction method,
        // below.
        this._rng = random.Random();
        this._rng.seed(42);

        // Setup our variable set by taking what's in flattenedPermuteVars and
        // stripping out vars that belong to encoders we are not using.
        def _setupVars(flattenedPermuteVars) // = seperate private funcion or lambda
        {
          allowedEncoderNames = this.swarmId.split('.');
          this.permuteVars = copy.deepcopy(flattenedPermuteVars);

          // Remove fields we don't want.
          varNames = this.permuteVars.keys();
          for( varName in varNames)
          {
            // Remove encoders we're not using
            if( ':' in varName)    // if an encoder
            {
              if( varName.split(':')[0] not in allowedEncoderNames)
              {
                this.permuteVars.pop(varName);
                continue;
              }
            }

            // All PermuteChoice variables need to know all prior results obtained
            // with each choice.
            if( isinstance(this.permuteVars[varName], PermuteChoices))
            {
              if( this._hsObj._speculativeParticles)
              {
                maxGenIdx = None;
              }
              else
              {
                maxGenIdx = this.genIdx-1;
              }

              resultsPerChoice = this._resultsDB.getResultsPerChoice(
                  swarmId=this.swarmId, maxGenIdx=maxGenIdx, varName=varName);
              this.permuteVars[varName].setResultsPerChoice(
                  resultsPerChoice.values());
            }
          }
        }

        // Method #1
        // Create from scratch, optionally pushing away from others that already
        //  exist.
        if( swarmId is not None)
        {
          assert (evolveFromState is None);
          assert (newFromClone is None);

          // Save construction param
          this.swarmId = swarmId;

          // Assign a new unique ID to this particle
          this.particleId = "%s.%s" % (str(this._hsObj._workerID),
                                       str(Particle._nextParticleID));
          Particle._nextParticleID += 1;

          // Init the generation index
          this.genIdx = 0;

          // Setup the variables to initial locations.
          _setupVars(flattenedPermuteVars);

          // Push away from other particles?
          if( newFarFrom is not None)
          {
            for( varName in this.permuteVars.iterkeys())
            {
              otherPositions = [];
              for( particleState in newFarFrom)
              {
                otherPositions.append(particleState["varStates"][varName]["position"]);
              }
              this.permuteVars[varName].pushAwayFrom(otherPositions, this._rng);

              // Give this particle a unique seed.
              this._rng.seed(str(otherPositions));
            }
          }
        }

        // Method #2
        // Instantiate from saved state, preserving particleId but incrementing
        //  generation index.
        else if( evolveFromState is not None)
        {
          assert (swarmId is None);
          assert (newFarFrom is None);
          assert (newFromClone is None);

          // Setup other variables from saved state
          this.particleId = evolveFromState["id"];
          this.genIdx = evolveFromState["genIdx"] + 1;
          this.swarmId = evolveFromState["swarmId"];

          // Setup the variables to initial locations.
          _setupVars(flattenedPermuteVars);

          // Override the position and velocity of each variable from
          //  saved state
          this.initStateFrom(this.particleId, evolveFromState, newBest=True);

          // Move it to the next position. We need the swarm best for this.
          this.newPosition();
        }

        // Method #3
        // Clone another particle, producing a new particle at the same genIdx with
        //  the same particleID. This is used to re-run an orphaned model.
        else if( newFromClone is not None)
        {
          assert (swarmId is None);
          assert (newFarFrom is None);
          assert (evolveFromState is None);

          // Setup other variables from clone particle
          this.particleId = newFromClone["id"];
          if( newParticleId)
          {
            this.particleId = "%s.%s" % (str(this._hsObj._workerID),
                                         str(Particle._nextParticleID));
            Particle._nextParticleID += 1;
          }

          this.genIdx = newFromClone["genIdx"];
          this.swarmId = newFromClone["swarmId"];

          // Setup the variables to initial locations.
          _setupVars(flattenedPermuteVars);

          // Override the position and velocity of each variable from
          //  the clone
          this.initStateFrom(this.particleId, newFromClone, newBest=False);
        }

        else
        {
          assert False, "invalid creation parameters";
        }

        // Log it
        this.logger.debug("Created particle: %s" % (str(self)));
      }


      def __repr__(self)
      {
        return "Particle(swarmId=%s) [particleId=%s, genIdx=%d, " \
            "permuteVars=\n%s]" % (this.swarmId, this.particleId,
            this.genIdx, pprint.pformat(this.permuteVars, indent=4));
      }


      def getState(self)
      {
        """Get the particle state as a dict. This is enough information to
        instantiate this particle on another worker.""";
        varStates = dict();
        for( varName, var in this.permuteVars.iteritems())
        {
          varStates[varName] = var.getState();
        }

        return dict(id = this.particleId,
                    genIdx = this.genIdx,
                    swarmId = this.swarmId,
                    varStates = varStates);
      }

      def initStateFrom(self, particleId, particleState, newBest)
      {
        """Init all of our variable positions, velocities, and optionally the best
        result and best position from the given particle.

        If newBest is true, we get the best result and position for this new
        generation from the resultsDB, This is used when evoloving a particle
        because the bestResult and position as stored in was the best AT THE TIME
        THAT PARTICLE STARTED TO RUN and does not include the best since that
        particle completed.
        """;
        // Get the update best position and result?
        if( newBest)
        {
          (bestResult, bestPosition) = this._resultsDB.getParticleBest(particleId);
        }
        else
        {
          bestResult = bestPosition = None;
        }

        // Replace with the position and velocity of each variable from
        //  saved state
        varStates = particleState["varStates"];
        for( varName in varStates.keys())
        {
          varState = copy.deepcopy(varStates[varName]);
          if( newBest)
          {
            varState["bestResult"] = bestResult;
          }
          if( bestPosition is not None)
          {
            varState["bestPosition"] = bestPosition[varName];
          }
          this.permuteVars[varName].setState(varState);
        }
      }




      def copyEncoderStatesFrom(self, particleState)
      {
        """Copy all encoder variables from particleState into this particle.

        Parameters:
        --------------------------------------------------------------
        particleState:        dict produced by a particle's getState() method
        """;
        // Set this to false if you don't want the variable to move anymore
        //  after we set the state
        allowedToMove = True;

        for( varName in particleState["varStates"])
        {
          if( ':' in varName)    // if an encoder
          {

            // If this particle doesn't include this field, don't copy it
            if( varName not in this.permuteVars)
            {
              continue;
            }

            // Set the best position to the copied position
            state = copy.deepcopy(particleState["varStates"][varName]);
            state["_position"] = state["position"];
            state["bestPosition"] = state["position"];

            if( not allowedToMove)
            {
              state["velocity"] = 0;
            }

            // Set the state now
            this.permuteVars[varName].setState(state);

            if( allowedToMove)
            {
              // Let the particle move in both directions from the best position
              //  it found previously and set it's initial velocity to a known
              //  fraction of the total distance.
              this.permuteVars[varName].resetVelocity(this._rng);
            }
          }
        }
      }

    def copyVarStatesFrom(self, particleState, varNames)
      {
        """Copy specific variables from particleState into this particle.

        Parameters:
        --------------------------------------------------------------
        particleState:        dict produced by a particle's getState() method
        varNames:             which variables to copy
        """;
        // Set this to false if you don't want the variable to move anymore
        //  after we set the state
        allowedToMove = True;

        for( varName in particleState["varStates"])
        {
          if( varName in varNames)
          {

            // If this particle doesn't include this field, don't copy it
            if( varName not in this.permuteVars)
            {
              continue;
            }

            // Set the best position to the copied position
            state = copy.deepcopy(particleState["varStates"][varName]);
            state["_position"] = state["position"];
            state["bestPosition"] = state["position"];

            if( not allowedToMove)
            {
              state["velocity"] = 0;
            }

            // Set the state now
            this.permuteVars[varName].setState(state);

            if( allowedToMove)
            {
              // Let the particle move in both directions from the best position
              //  it found previously and set it's initial velocity to a known
              //  fraction of the total distance.
              this.permuteVars[varName].resetVelocity(this._rng);
            }
          }
        }
      }



      def getPosition(self)
      {
        """Return the position of this particle. This returns a dict() of key
        value pairs where each key is the name of the flattened permutation
        variable and the value is its chosen value.

        Parameters:
        --------------------------------------------------------------
        retval:     dict() of flattened permutation choices
        """;
        result = dict();
        for (varName, value) in this.permuteVars.iteritems()
        {
          result[varName] = value.getPosition();
        }

        return result;
      }



      @staticmethod;
      def getPositionFromState(pState)
      {
        """Return the position of a particle given its state dict.

        Parameters:
        --------------------------------------------------------------
        retval:     dict() of particle position, keys are the variable names,
                      values are their positions
        """;
        result = dict();
        for (varName, value) in pState["varStates"].iteritems()
        {
          result[varName] = value["position"];
        }

        return result;
      } 

      def agitate(self)
      {
        """Agitate this particle so that it is likely to go to a new position.
        Every time agitate is called, the particle is jiggled an even greater
        amount.

        Parameters:
        --------------------------------------------------------------
        retval:               None
        """;
        for (varName, var) in this.permuteVars.iteritems()
        {
          var.agitate();
        }

        this.newPosition();
      }

      def newPosition(self, whichVars=None)
      {
        // TODO: incorporate data from choice variables....
        // TODO: make sure we're calling this when appropriate.
        """Choose a new position based on results obtained so far from all other
        particles.

        Parameters:
        --------------------------------------------------------------
        whichVars:       If not None, only move these variables
        retval:               new position
        """;
        // Get the global best position for this swarm generation
        globalBestPosition = None;
        // If speculative particles are enabled, use the global best considering
        //  even particles in the current generation. This gives better results
        //  but does not provide repeatable results because it depends on
        //  worker timing
        if( this._hsObj._speculativeParticles)
        {
          genIdx = this.genIdx;
        }
        else
        {
          genIdx = this.genIdx - 1;
        }

        if( genIdx >= 0)
        {
          (bestModelId, _) = this._resultsDB.bestModelIdAndErrScore(this.swarmId, genIdx);
          if( bestModelId is not None)
          {
            (particleState, _, _, _, _) = this._resultsDB.getParticleInfo(bestModelId);
            globalBestPosition = Particle.getPositionFromState(particleState);
          }
        }

        // Update each variable
        for (varName, var) in this.permuteVars.iteritems()
        {
          if( whichVars is not None and varName not in whichVars)
          {
            continue;
          }
          if( globalBestPosition is None)
          {
            var.newPosition(None, this._rng);
          }
          else
          {
            var.newPosition(globalBestPosition[varName], this._rng);
          }
        }

        // get the new position
        position = this.getPosition();

        // Log the new position
        if( this.logger.getEffectiveLevel() <= logging.DEBUG)
        {
          msg = StringIO.StringIO();
          print >> msg, "New particle position: \n%s" % (pprint.pformat(position,
                                                          indent=4));
          print >> msg, "Particle variables:";
          for (varName, var) in this.permuteVars.iteritems()
          {
            print >> msg, "  %s: %s" % (varName, str(var));
          }
          this.logger.debug(msg.getvalue());
          msg.close();
        }

        return position;
      }
    }

    class HsState(object)
    {

      def __init__(self, hsObj)
      {
        """ Create our state object.

        Parameters:
        ---------------------------------------------------------------------
        hsObj:     Reference to the HypersesarchV2 instance
        cjDAO:     ClientJobsDAO instance
        logger:    logger to use
        jobID:     our JobID
        """;
        // Save constructor parameters
        this._hsObj = hsObj;

        // Convenient access to the logger
        this.logger = this._hsObj.logger;

        // This contains our current state, and local working changes
        this._state = None;

        // This contains the state we last read from the database
        this._priorStateJSON = None;

        // Set when we make a change to our state locally
        this._dirty = False;

        // Read in the initial state
        this.readStateFromDB();
      }


      def isDirty(self)
      {
        """Return true if our local copy of the state has changed since the
        last time we read from the DB.
        """;
        return this._dirty;
      }

      def isSearchOver(self)
      {
        """Return true if the search should be considered over.""";
        return this._state["searchOver"];
      }

      def readStateFromDB(self)
      {
        """Set our state to that obtained from the engWorkerState field of the
        job record.


        Parameters:
        ---------------------------------------------------------------------
        stateJSON:    JSON encoded state from job record

        """;
        this._priorStateJSON = this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID,
                                                        ["engWorkerState"])[0];

        // Init if no prior state yet
        if( this._priorStateJSON is None)
        {
          swarms = dict();

          // Fast Swarm, first and only sprint has one swarm for each field
          // in fixedFields
          if( this._hsObj._fixedFields is not None)
          {
            print this._hsObj._fixedFields;
            encoderSet = [];
            for( field in this._hsObj._fixedFields)
            {
                if( field =='_classifierInput')
                {
                  continue;
                }
                encoderName = this.getEncoderKeyFromName(field);
                assert encoderName in this._hsObj._encoderNames, "The field '%s' " \
                  " specified in the fixedFields list is not present in this " \
                  " model." % (field);
                encoderSet.append(encoderName);
            }
            encoderSet.sort();
            swarms[".'.join(encoderSet)] = {
                                    'status': 'active',
                                    'bestModelId': None,
                                    'bestErrScore': None,
                                    'sprintIdx': 0,
                                    };
          }
          // Temporal prediction search, first sprint has N swarms of 1 field each,
          //  the predicted field may or may not be that one field.
          else if( this._hsObj._searchType == HsSearchType.temporal)
          {
            for( encoderName in this._hsObj._encoderNames)
            {
              swarms[encoderName] = {
                                      'status': 'active',
                                      'bestModelId': None,
                                      'bestErrScore': None,
                                      'sprintIdx': 0,
                                      };
            }
          }


          // Classification prediction search, first sprint has N swarms of 1 field
          //  each where this field can NOT be the predicted field.
          else if( this._hsObj._searchType == HsSearchType.classification)
          {
            for( encoderName in this._hsObj._encoderNames)
            {
              if( encoderName == this._hsObj._predictedFieldEncoder)
              {
                continue;
              }
              swarms[encoderName] = {
                                      'status': 'active',
                                      'bestModelId': None,
                                      'bestErrScore': None,
                                      'sprintIdx': 0,
                                      };
            }
          }

          // Legacy temporal. This is either a model that uses reconstruction or
          //  an older multi-step model that doesn't have a separate
          //  'classifierOnly' encoder for the predicted field. Here, the predicted
          //  field must ALWAYS be present and the first sprint tries the predicted
          //  field only
          else if( this._hsObj._searchType == HsSearchType.legacyTemporal)
          {
            swarms[this._hsObj._predictedFieldEncoder] = {
                           'status': 'active',
                           'bestModelId': None,
                           'bestErrScore': None,
                           'sprintIdx': 0,
                           };
          }

          else
          {
            raise RuntimeError("Unsupported search type: %s" % \
                                (this._hsObj._searchType));
          }

          // Initialize the state.
          this._state = dict(
            // The last time the state was updated by a worker.
            lastUpdateTime = time.time(),

            // Set from within setSwarmState() if we detect that the sprint we just
            //  completed did worse than a prior sprint. This stores the index of
            //  the last good sprint.
            lastGoodSprint = None,

            // Set from within setSwarmState() if lastGoodSprint is True and all
            //  sprints have completed.
            searchOver = False,

            // This is a summary of the active swarms - this information can also
            //  be obtained from the swarms entry that follows, but is summarized here
            //  for easier reference when viewing the state as presented by
            //  log messages and prints of the hsState data structure (by
            //  permutations_runner).
            activeSwarms = swarms.keys(),

            // All the swarms that have been created so far.
            swarms = swarms,

            // All the sprints that have completed or are in progress.
            sprints = [{'status': 'active',
                        'bestModelId': None,
                        'bestErrScore': None}],

            // The list of encoders we have "blacklisted" because they
            //  performed so poorly.
            blackListedEncoders = [],
            );

          // This will do nothing if the value of engWorkerState is not still None.
          this._hsObj._cjDAO.jobSetFieldIfEqual(
              this._hsObj._jobID, 'engWorkerState', json.dumps(this._state), None);

          this._priorStateJSON = this._hsObj._cjDAO.jobGetFields(
              this._hsObj._jobID, ["engWorkerState"])[0];
          assert (this._priorStateJSON is not None);
        }

        // Read state from the database
        this._state = json.loads(this._priorStateJSON);
        this._dirty = False;
      }

      def writeStateToDB(self)
      {
        """Update the state in the job record with our local changes (if any).
        If we don't have the latest state in our priorStateJSON, then re-load
        in the latest state and return False. If we were successful writing out
        our changes, return True

        Parameters:
        ---------------------------------------------------------------------
        retval:    True if we were successful writing out our changes
                   False if our priorState is not the latest that was in the DB.
                   In this case, we will re-load our state from the DB
        """;
        // If no changes, do nothing
        if( not this._dirty)
        {
          return True;
        }

        // Set the update time
        this._state["lastUpdateTime"] = time.time();
        newStateJSON = json.dumps(this._state);
        success = this._hsObj._cjDAO.jobSetFieldIfEqual(this._hsObj._jobID,
                    'engWorkerState', str(newStateJSON), str(this._priorStateJSON));

        if( success)
        {
          this.logger.debug("Success changing hsState to: \n%s " % \
                           (pprint.pformat(this._state, indent=4)));
          this._priorStateJSON = newStateJSON;
        }

        // If no success, read in the current state from the DB
        else
        {
          this.logger.debug("Failed to change hsState to: \n%s " % \
                           (pprint.pformat(this._state, indent=4)));

          this._priorStateJSON = this._hsObj._cjDAO.jobGetFields(this._hsObj._jobID,
                                                          ["engWorkerState"])[0];
          this._state =  json.loads(this._priorStateJSON);

          this.logger.info("New hsState has been set by some other worker to: "
                           " \n%s" % (pprint.pformat(this._state, indent=4)));
        }

        return success;
      }

      def getEncoderNameFromKey(self, key)
      {
        """ Given an encoder dictionary key, get the encoder name.

        Encoders are a sub-dict within model params, and in HSv2, their key
        is structured like this for example:
           'modelParams|sensorParams|encoders|home_winloss'

        The encoderName is the last word in the | separated key name
        """;
        return key.split('|')[-1];
      }

      def getEncoderKeyFromName(self, name)
      {
        """ Given an encoder name, get the key.

        Encoders are a sub-dict within model params, and in HSv2, their key
        is structured like this for example:
           'modelParams|sensorParams|encoders|home_winloss'

        The encoderName is the last word in the | separated key name
        """;
        return 'modelParams|sensorParams|encoders|%s' % (name);
      }

      def getFieldContributions(self)
      {
        """Return the field contributions statistics.

        Parameters:
        ---------------------------------------------------------------------
        retval:   Dictionary where the keys are the field names and the values
                    are how much each field contributed to the best score.
        """;

        #in the fast swarm, there is only 1 sprint and field contributions are
        #not defined
        if( this._hsObj._fixedFields is not None)
        {
          return dict(), dict();
        }
        // Get the predicted field encoder name
        predictedEncoderName = this._hsObj._predictedFieldEncoder;

        // -----------------------------------------------------------------------
        // Collect all the single field scores
        fieldScores = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          encodersUsed = swarmId.split('.');
          if( len(encodersUsed) != 1)
          {
            continue;
          }
          field = this.getEncoderNameFromKey(encodersUsed[0]);
          bestScore = info["bestErrScore"];

          // If the bestScore is None, this swarm hasn't completed yet (this could
          //  happen if we're exiting because of maxModels), so look up the best
          //  score so far
          if( bestScore is None)
          {
            (_modelId, bestScore) = \
                this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
          }

          fieldScores.append((bestScore, field));
        }


        // -----------------------------------------------------------------------
        // If we only have 1 field that was tried in the first sprint, then use that
        //  as the base and get the contributions from the fields in the next sprint.
        if( this._hsObj._searchType == HsSearchType.legacyTemporal)
        {
          assert(len(fieldScores)==1);
          (baseErrScore, baseField) = fieldScores[0];

          for( swarmId, info in this._state["swarms"].iteritems())
          {
            encodersUsed = swarmId.split('.');
            if( len(encodersUsed) != 2)
            {
              continue;
            }

            fields = [this.getEncoderNameFromKey(name) for name in encodersUsed];
            fields.remove(baseField);

            fieldScores.append((info["bestErrScore"], fields[0]));
          }
        }

        // The first sprint tried a bunch of fields, pick the worst performing one
        //  (within the top this._hsObj._maxBranching ones) as the base
        else
        {
          fieldScores.sort(reverse=True);

          // If maxBranching was specified, pick the worst performing field within
          //  the top maxBranching+1 fields as our base, which will give that field
          //  a contribution of 0.
          if( this._hsObj._maxBranching > 0 \
                  and len(fieldScores) > this._hsObj._maxBranching)
          {
            baseErrScore = fieldScores[-this._hsObj._maxBranching-1][0];
          }
          else
          {
            baseErrScore = fieldScores[0][0];
          }
        }


        // -----------------------------------------------------------------------
        // Prepare and return the fieldContributions dict
        pctFieldContributionsDict = dict();
        absFieldContributionsDict = dict();

        // If we have no base score, can't compute field contributions. This can
        //  happen when we exit early due to maxModels or being cancelled
        if( baseErrScore is not None)
        {

          // If the base error score is 0, we can't compute a percent difference
          //  off of it, so move it to a very small float
          if( abs(baseErrScore) < 0.00001)
          {
            baseErrScore = 0.00001;
          }
          for (errScore, field) in fieldScores
          {
            if( errScore is not None)
            {
              pctBetter = (baseErrScore - errScore) * 100.0 / baseErrScore;
            }
            else
            {
              pctBetter = 0.0;
              errScore = baseErrScore;   // for absFieldContribution
            }

            pctFieldContributionsDict[field] = pctBetter;
            absFieldContributionsDict[field] = baseErrScore - errScore;
          }
        }

        this.logger.debug("FieldContributions: %s" % (pctFieldContributionsDict));
        return pctFieldContributionsDict, absFieldContributionsDict;
      }

      def getAllSwarms(self, sprintIdx)
      {
        """Return the list of all swarms in the given sprint.

        Parameters:
        ---------------------------------------------------------------------
        retval:   list of active swarm Ids in the given sprint
        """;
        swarmIds = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          if( info["sprintIdx"] == sprintIdx)
          {
            swarmIds.append(swarmId);
          }
        }

        return swarmIds;
      }


      def getActiveSwarms(self, sprintIdx=None)
      {
        """Return the list of active swarms in the given sprint. These are swarms
        which still need new particles created in them.

        Parameters:
        ---------------------------------------------------------------------
        sprintIdx:    which sprint to query. If None, get active swarms from all
                          sprints
        retval:   list of active swarm Ids in the given sprint
        """;
        swarmIds = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          if( sprintIdx is not None and info["sprintIdx"] != sprintIdx)
          {
            continue;
          }
          if( info["status"] == 'active')
          {
            swarmIds.append(swarmId);
          }
        }

        return swarmIds;
      }

      def getNonKilledSwarms(self, sprintIdx)
      {
        """Return the list of swarms in the given sprint that were not killed.
        This is called when we are trying to figure out which encoders to carry
        forward to the next sprint. We don't want to carry forward encoder
        combintations which were obviously bad (in killed swarms).

        Parameters:
        ---------------------------------------------------------------------
        retval:   list of active swarm Ids in the given sprint
        """;
        swarmIds = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          if( info["sprintIdx"] == sprintIdx and info["status"] != 'killed')
          {
            swarmIds.append(swarmId);
          }
        }

        return swarmIds;
      }

      def getCompletedSwarms(self)
      {
        """Return the list of all completed swarms.

        Parameters:
        ---------------------------------------------------------------------
        retval:   list of active swarm Ids
        """;
        swarmIds = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          if( info["status"] == 'completed')
          {
            swarmIds.append(swarmId);
          }
        }

        return swarmIds;
      }

      def getCompletingSwarms(self)
      {
        """Return the list of all completing swarms.

        Parameters:
        ---------------------------------------------------------------------
        retval:   list of active swarm Ids
        """;
        swarmIds = [];
        for( swarmId, info in this._state["swarms"].iteritems())
        {
          if( info["status"] == 'completing')
          {
            swarmIds.append(swarmId);
          }
        }

        return swarmIds;
      }

      def bestModelInCompletedSwarm(self, swarmId)
      {
        """Return the best model ID and it's errScore from the given swarm.
        If the swarm has not completed yet, the bestModelID will be None.

        Parameters:
        ---------------------------------------------------------------------
        retval:   (modelId, errScore)
        """;
        swarmInfo = this._state["swarms"][swarmId];
        return (swarmInfo["bestModelId"],
                swarmInfo["bestErrScore"]);
      }

      def bestModelInCompletedSprint(self, sprintIdx)
      {
        """Return the best model ID and it's errScore from the given sprint.
        If the sprint has not completed yet, the bestModelID will be None.

        Parameters:
        ---------------------------------------------------------------------
        retval:   (modelId, errScore)
        """;
        sprintInfo = this._state["sprints"][sprintIdx];
        return (sprintInfo["bestModelId"],
                sprintInfo["bestErrScore"]);
      }

      def bestModelInSprint(self, sprintIdx)
      {
        """Return the best model ID and it's errScore from the given sprint,
        which may still be in progress. This returns the best score from all models
        in the sprint which have matured so far.

        Parameters:
        ---------------------------------------------------------------------
        retval:   (modelId, errScore)
        """;
        // Get all the swarms in this sprint
        swarms = this.getAllSwarms(sprintIdx);

        // Get the best model and score from each swarm
        bestModelId = None;
        bestErrScore = numpy.inf;
        for( swarmId in swarms)
        {
          (modelId, errScore) = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
          if( errScore < bestErrScore)
          {
            bestModelId = modelId;
            bestErrScore = errScore;
          }
        }

        return (bestModelId, bestErrScore);
      }

    def setSwarmState(self, swarmId, newStatus)
      {
        """Change the given swarm's state to 'newState'. If 'newState' is
        'completed', then bestModelId and bestErrScore must be provided.

        Parameters:
        ---------------------------------------------------------------------
        swarmId:      swarm Id
        newStatus:    new status, either 'active', 'completing', 'completed', or
                        'killed'
        """;
        assert (newStatus in ["active', 'completing', 'completed', 'killed"]);

        // Set the swarm status
        swarmInfo = this._state["swarms"][swarmId];
        if( swarmInfo["status"] == newStatus)
        {
          return;
        }

        // If some other worker noticed it as completed, setting it to completing
        //  is obviously old information....
        if( swarmInfo["status"] == 'completed' and newStatus == 'completing')
        {
          return;
        }

        this._dirty = True;
        swarmInfo["status"] = newStatus;
        if( newStatus == 'completed')
        {
          (modelId, errScore) = this._hsObj._resultsDB.bestModelIdAndErrScore(swarmId);
          swarmInfo["bestModelId"] = modelId;
          swarmInfo["bestErrScore"] = errScore;
        }

        // If no longer active, remove it from the activeSwarms entry
        if( newStatus != 'active' and swarmId in this._state["activeSwarms"])
        {
          this._state["activeSwarms"].remove(swarmId);
        }

        // If new status is 'killed', kill off any running particles in that swarm
        if( newStatus=='killed')
        {
          this._hsObj.killSwarmParticles(swarmId);
        }

        // In case speculative particles are enabled, make sure we generate a new
        //  swarm at this time if all of the swarms in the current sprint have
        //  completed. This will insure that we don't mark the sprint as completed
        //  before we've created all the possible swarms.
        sprintIdx = swarmInfo["sprintIdx"];
        this.isSprintActive(sprintIdx);

        // Update the sprint status. Check all the swarms that belong to this sprint.
        //  If they are all completed, the sprint is completed.
        sprintInfo = this._state["sprints"][sprintIdx];

        statusCounts = dict(active=0, completing=0, completed=0, killed=0);
        bestModelIds = [];
        bestErrScores = [];
        for( info in this._state["swarms"].itervalues())
        {
          if( info["sprintIdx"] != sprintIdx)
          {
            continue;
          }
          statusCounts[info["status"]] += 1;
          if( info["status"] == 'completed')
          {
            bestModelIds.append(info["bestModelId"]);
            bestErrScores.append(info["bestErrScore"]);
          }
        }

        if( statusCounts["active"] > 0)
        {
          sprintStatus = 'active';
        }
        else if( statusCounts["completing"] > 0)
        {
          sprintStatus = 'completing';
        }
        else
        {
          sprintStatus = 'completed';
        }
        sprintInfo["status"] = sprintStatus;

        // If the sprint is complete, get the best model from all of its swarms and
        //  store that as the sprint best
        if( sprintStatus == 'completed')
        {
          if( len(bestErrScores) > 0)
          {
            whichIdx = numpy.array(bestErrScores).argmin();
            sprintInfo["bestModelId"] = bestModelIds[whichIdx];
            sprintInfo["bestErrScore"] = bestErrScores[whichIdx];
          }
          else
          {
            // This sprint was empty, most likely because all particles were
            //  killed. Give it a huge error score
            sprintInfo["bestModelId"] = 0;
            sprintInfo["bestErrScore"] = numpy.inf;
          }


          // See if our best err score got NO BETTER as compared to a previous
          //  sprint. If so, stop exploring subsequent sprints (lastGoodSprint
          //  is no longer None).
          bestPrior = numpy.inf;
          for( idx in range(sprintIdx))
          {
            if( this._state["sprints"][idx]["status"] == 'completed')
            {
              (_, errScore) = this.bestModelInCompletedSprint(idx);
              if( errScore is None)
              {
                errScore = numpy.inf;
              }
            }
            else
            {
              errScore = numpy.inf;
            }
            if( errScore < bestPrior)
            {
              bestPrior = errScore;
            }
          }

          if( sprintInfo["bestErrScore"] >= bestPrior)
          {
            this._state["lastGoodSprint"] = sprintIdx-1;
          }

          // If ALL sprints up to the last good one are done, the search is now over
          if( this._state["lastGoodSprint"] is not None \
                and not this.anyGoodSprintsActive())
          {
            this._state["searchOver"] = True;
          }
        }
      }


      def anyGoodSprintsActive(self)
      {
        """Return True if there are any more good sprints still being explored.
        A 'good' sprint is one that is earlier than where we detected an increase
        in error from sprint to subsequent sprint.
        """;
        if( this._state["lastGoodSprint"] is not None)
        {
          goodSprints = this._state["sprints"][0:this._state["lastGoodSprint"]+1];
        }
        else
        {
          goodSprints = this._state["sprints"];
        }

        for( sprint in goodSprints)
        {
          if( sprint["status"] == 'active')
          {
            anyActiveSprints = True;
            break;
          }
        }
        else
        {
          anyActiveSprints = False;
        }

        return anyActiveSprints;
      }

      def isSprintCompleted(self, sprintIdx)
      {
        """Return True if the given sprint has completed.""";
        numExistingSprints = len(this._state["sprints"]);
        if( sprintIdx >= numExistingSprints)
        {
          return False;
        }

        return (this._state["sprints"][sprintIdx]["status"] == 'completed');
      }

      def killUselessSwarms(self)
      {
        """See if we can kill off some speculative swarms. If an earlier sprint
        has finally completed, we can now tell which fields should *really* be present
        in the sprints we've already started due to speculation, and kill off the
        swarms that should not have been included.
        """;
        // Get number of existing sprints
        numExistingSprints = len(this._state["sprints"]);

        // Should we bother killing useless swarms?
        if( this._hsObj._searchType == HsSearchType.legacyTemporal)
        {
          if( numExistingSprints <= 2)
          {
            return;
          }
        }
        else
        {
          if( numExistingSprints <= 1)
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
        completedSwarms = this.getCompletedSwarms();
        completedSwarms = [(swarm, this._state["swarms"][swarm],
                            this._state["swarms"][swarm]["bestErrScore"]) \
                                                    for swarm in completedSwarms];

        // Form the completedMatrix. Each row corresponds to a sprint. Each row
        //  contains the list of swarm tuples that belong to that sprint, sorted
        //  by best score. Each swarm tuple contains (swarmName, swarmState,
        //  swarmBestErrScore).
        // ex. completedMatrix:
        //    [(('a', {...}, 1.4), ('b', {...}, 2.0), ('c', {...}, 3.0)),
        //     (('a.b', {...}, 3.0), ('b.c', {...}, 4.0))]
        completedMatrix = [[] for i in range(numExistingSprints)];
        for( swarm in completedSwarms)
        {
          completedMatrix[swarm[1]["sprintIdx"]].append(swarm);
        }
        for( sprint in completedMatrix)
        {
          sprint.sort(key=itemgetter(2));
        }

        // Form activeSwarms as a list of tuples, each tuple contains:
        //  (swarmName, swarmState, swarmBestErrScore)
        // Include all activeSwarms and completingSwarms
        // ex. activeSwarms:
        //    [('d', {...}, 1.4),
        //     ('e', {...}, 2.0),
        //     ('f', {...}, 3.0)]
        activeSwarms = this.getActiveSwarms();
        // Append the completing swarms
        activeSwarms.extend(this.getCompletingSwarms());
        activeSwarms = [(swarm, this._state["swarms"][swarm],
                         this._state["swarms"][swarm]["bestErrScore"]) \
                                                    for swarm in activeSwarms];

        // Form the activeMatrix. Each row corresponds to a sprint. Each row
        //  contains the list of swarm tuples that belong to that sprint, sorted
        //  by best score. Each swarm tuple contains (swarmName, swarmState,
        //  swarmBestErrScore)
        // ex. activeMatrix:
        //    [(('d', {...}, 1.4), ('e', {...}, 2.0), ('f', {...}, 3.0)),
        //     (('d.e', {...}, 3.0), ('e.f', {...}, 4.0))]
        activeMatrix = [[] for i in range(numExistingSprints)];
        for( swarm in activeSwarms)
        {
          activeMatrix[swarm[1]["sprintIdx"]].append(swarm);
        }
        for( sprint in activeMatrix)
        {
          sprint.sort(key=itemgetter(2));
        }


        // Figure out which active swarms to kill
        toKill = [];
        for( i in range(1, numExistingSprints))
        {
          for( swarm in activeMatrix[i])
          {
            curSwarmEncoders = swarm[0].split(".");

            // If previous sprint is complete, get the best swarm and kill all active
            //  sprints that are not supersets
            if(len(activeMatrix[i-1])==0)
            {
              // If we are trying all possible 3 field combinations, don't kill any
              //  off in sprint 2
              if( i==2 and (this._hsObj._tryAll3FieldCombinations or \
                    this._hsObj._tryAll3FieldCombinationsWTimestamps))
              {
                pass;
              }
              else
              {
                bestInPrevious = completedMatrix[i-1][0];
                bestEncoders = bestInPrevious[0].split('.');
                for( encoder in bestEncoders)
                {
                  if( not encoder in curSwarmEncoders)
                  {
                    toKill.append(swarm);
                  }
                }
              }
            }

            // if there are more than two completed encoders sets that are complete and
            // are worse than at least one active swarm in the previous sprint. Remove
            // any combinations that have any pair of them since they cannot have the best encoder.
            #elif(len(completedMatrix[i-1])>1):
            //  for completedSwarm in completedMatrix[i-1]:
            //    activeMatrix[i-1][0][2]<completed
          }
        }

        // Mark the bad swarms as killed
        if( len(toKill) > 0)
        {
          print "ParseMe: Killing encoders:" + str(toKill);
        }

        for( swarm in toKill)
        {
          this.setSwarmState(swarm[0], "killed");
        }

        return;
      }

      def isSprintActive(self, sprintIdx)
      {
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
      ///  """;

        while( True)
        {
          numExistingSprints = len(this._state["sprints"]);

          // If this sprint already exists, see if it is active
          if( sprintIdx <= numExistingSprints-1)
          {

            // With speculation off, it's simple, just return whether or not the
            //  asked for sprint has active status
            if( not this._hsObj._speculativeParticles)
            {
              active = (this._state["sprints"][sprintIdx]["status"] == 'active');
              return (active, False);
            }

            // With speculation on, if the sprint is still marked active, we also
            //  need to see if it's time to add a new swarm to it.
            else
            {
              active = (this._state["sprints"][sprintIdx]["status"] == 'active');
              if( not active)
              {
                return (active, False);
              }

              // See if all of the existing swarms are at capacity (have all the
              // workers they need):
              activeSwarmIds = this.getActiveSwarms(sprintIdx);
              swarmSizes = [this._hsObj._resultsDB.getParticleInfos(swarmId,
                                  matured=False)[0] for swarmId in activeSwarmIds];
              notFullSwarms = [len(swarm) for swarm in swarmSizes \
                               if len(swarm) < this._hsObj._minParticlesPerSwarm];

              // If some swarms have room return that the swarm is active.
              if( len(notFullSwarms) > 0)
              {
                return (True, False);
              }

              // If the existing swarms are at capacity, we will fall through to the
              //  logic below which tries to add a new swarm to the sprint.
            }
          }

          // Stop creating new sprints?
          if( this._state["lastGoodSprint"] is not None)
          {
            return (False, True);
          }

          // if fixedFields is set, we are running a fast swarm and only run sprint0
          if( this._hsObj._fixedFields is not None)
          {
            return (False, True);
          }

          // ----------------------------------------------------------------------
          // Get the best model (if there is one) from the prior sprint. That gives
          // us the base encoder set for the next sprint. For sprint zero make sure
          // it does not take the last sprintidx because of wrapping.
          if( sprintIdx > 0  \
                and this._state["sprints"][sprintIdx-1]["status"] == 'completed')
          {
            (bestModelId, _) = this.bestModelInCompletedSprint(sprintIdx-1);
            (particleState, _, _, _, _) = this._hsObj._resultsDB.getParticleInfo(
                                                                      bestModelId);
            bestSwarmId = particleState["swarmId"];
            baseEncoderSets = [bestSwarmId.split('.')];
          }

          // If there is no best model yet, then use all encoder sets from the prior
          //  sprint that were not killed
          else
          {
            bestSwarmId = None;
            particleState = None;
            // Build up more combinations, using ALL of the sets in the current
            //  sprint.
            baseEncoderSets = [];
            for( swarmId in this.getNonKilledSwarms(sprintIdx-1))
            {
              baseEncoderSets.append(swarmId.split('.'));
            }
          }

          // ----------------------------------------------------------------------
          // Which encoders should we add to the current base set?
          encoderAddSet = [];

          // If we have constraints on how many fields we carry forward into
          // subsequent sprints (either nupic.hypersearch.max.field.branching or
          // nupic.hypersearch.min.field.contribution was set), then be more
          // picky about which fields we add in.
          limitFields = False;
          if( this._hsObj._maxBranching > 0 \
                or this._hsObj._minFieldContribution >= 0)
          {
            if( this._hsObj._searchType == HsSearchType.temporal or \
                this._hsObj._searchType == HsSearchType.classification)
            {
              if( sprintIdx >= 1)
              {
                limitFields = True;
                baseSprintIdx = 0;
              }
            }
            else if( this._hsObj._searchType == HsSearchType.legacyTemporal)
            {
              if( sprintIdx >= 2)
              {
                limitFields = True;
                baseSprintIdx = 1;
              }
            }
            else
            {
              raise RuntimeError("Unimplemented search type %s" % \
                                      (this._hsObj._searchType));
            }
          }


          // Only add top _maxBranching encoders to the swarms?
          if( limitFields)
          {

            // Get field contributions to filter added fields
            pctFieldContributions, absFieldContributions = \
                                                    this.getFieldContributions();
            toRemove = [];
            this.logger.debug("FieldContributions min: %s" % \
                              (this._hsObj._minFieldContribution));
            for( fieldname in pctFieldContributions)
            {
              if( pctFieldContributions[fieldname] < this._hsObj._minFieldContribution)
              {
                this.logger.debug("FieldContributions removing: %s" % (fieldname));
                toRemove.append(this.getEncoderKeyFromName(fieldname));
              }
              else
              {
                this.logger.debug("FieldContributions keeping: %s" % (fieldname));
              }
            }


            // Grab the top maxBranching base sprint swarms.
            swarms = this._state["swarms"];
            sprintSwarms = [(swarm, swarms[swarm]["bestErrScore"]) \
                for swarm in swarms if swarms[swarm]["sprintIdx"] == baseSprintIdx];
            sprintSwarms = sorted(sprintSwarms, key=itemgetter(1));
            if( this._hsObj._maxBranching > 0)
            {
              sprintSwarms = sprintSwarms[0:this._hsObj._maxBranching];
            }

            // Create encoder set to generate further swarms.
            for( swarm in sprintSwarms)
            {
              swarmEncoders = swarm[0].split(".");
              for( encoder in swarmEncoders)
              {
                if( not encoder in encoderAddSet)
                {
                  encoderAddSet.append(encoder);
                }
              }
            }
            encoderAddSet = [encoder for encoder in encoderAddSet \
                             if not str(encoder) in toRemove];
          }

          // If no limit on the branching or min contribution, simply use all of the
          // encoders.
          else
          {
            encoderAddSet = this._hsObj._encoderNames;
          }


          // -----------------------------------------------------------------------
          // Build up the new encoder combinations for the next sprint.
          newSwarmIds = set();

          // See if the caller wants to try more extensive field combinations with
          //  3 fields.
          if (this._hsObj._searchType == HsSearchType.temporal \
               or this._hsObj._searchType == HsSearchType.legacyTemporal) \
              and sprintIdx == 2 \
              and (this._hsObj._tryAll3FieldCombinations or \
                   this._hsObj._tryAll3FieldCombinationsWTimestamps)
          {

            if( this._hsObj._tryAll3FieldCombinations)
            {
              newEncoders = set(this._hsObj._encoderNames);
              if( this._hsObj._predictedFieldEncoder in newEncoders)
              {
                newEncoders.remove(this._hsObj._predictedFieldEncoder);
              }
            }
            else
            {
              // Just make sure the timestamp encoders are part of the mix
              newEncoders = set(encoderAddSet);
              if( this._hsObj._predictedFieldEncoder in newEncoders)
              {
                newEncoders.remove(this._hsObj._predictedFieldEncoder);
              }
              for( encoder in this._hsObj._encoderNames)
              {
                if( encoder.endswith('_timeOfDay') or encoder.endswith('_weekend') \
                    or encoder.endswith('_dayOfWeek'))
                {
                  newEncoders.add(encoder);
                }
              }
            }

            allCombos = list(itertools.combinations(newEncoders, 2));
            for( combo in allCombos)
            {
              newSet = list(combo);
              newSet.append(this._hsObj._predictedFieldEncoder);
              newSet.sort();
              newSwarmId = '.'.join(newSet);
              if( newSwarmId not in this._state["swarms"])
              {
                newSwarmIds.add(newSwarmId);

                // If a speculative sprint, only add the first encoder, if not add
                //   all of them.
                if (len(this.getActiveSwarms(sprintIdx-1)) > 0)
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
            for( baseEncoderSet in baseEncoderSets)
            {
              for( encoder in encoderAddSet)
              {
                if( encoder not in this._state["blackListedEncoders"] \
                    and encoder not in baseEncoderSet)
                {
                  newSet = list(baseEncoderSet);
                  newSet.append(encoder);
                  newSet.sort();
                  newSwarmId = '.'.join(newSet);
                  if( newSwarmId not in this._state["swarms"])
                  {
                    newSwarmIds.add(newSwarmId);

                    // If a speculative sprint, only add the first encoder, if not add
                    //   all of them.
                    if (len(this.getActiveSwarms(sprintIdx-1)) > 0)
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
          newSwarmIds = sorted(newSwarmIds);

          // If no more swarms can be found for this sprint...
          if( len(newSwarmIds) == 0)
          {
            // if sprint is not an empty sprint return that it is active but do not
            //  add anything to it.
            if( len(this.getAllSwarms(sprintIdx)) > 0)
            {
              return (True, False);
            }

            // If this is an empty sprint and we couldn't find any new swarms to
            //   add (only bad fields are remaining), the search is over
            else
            {
              return (False, True);
            }
          }

          // Add this sprint and the swarms that are in it to our state
          this._dirty = True;

          // Add in the new sprint if necessary
          if( len(this._state["sprints"]) == sprintIdx)
          {
            this._state["sprints"].append({'status': 'active',
                                           'bestModelId': None,
                                           'bestErrScore': None});
          }

          // Add in the new swarm(s) to the sprint
          for( swarmId in newSwarmIds)
          {
            this._state["swarms"][swarmId] = {'status': 'active',
                                                'bestModelId': None,
                                                'bestErrScore': None,
                                                'sprintIdx': sprintIdx};
          }

          // Update the list of active swarms
          this._state["activeSwarms"] = this.getActiveSwarms();

          // Try to set new state
          success = this.writeStateToDB();

          // Return result if successful
          if( success)
          {
            return (True, False);
          }

          // No success, loop back with the updated state and try again
        }
      }
    }


    class HsSearchType(object)
    {
      """This class enumerates the types of search we can perform.""";
      temporal = 'temporal';
      legacyTemporal = 'legacyTemporal';
      classification = 'classification';
    }

    class HypersearchV2(object)
    {

      def __init__(self, searchParams, workerID=None, cjDAO=None, jobID=None,
                   logLevel=None)
      {

        """;

        // Instantiate our logger
        this.logger = logging.getLogger(".".join( ["com.numenta',
                            this.__class__.__module__, this.__class__.__name__]));

        // Override log level?
        if( logLevel is not None)
        {
          this.logger.setLevel(logLevel);
        }

        // This is how to check the logging level
        #if this.logger.getEffectiveLevel() <= logging.DEBUG:
        //  print "at debug level"

        // Init random seed
        random.seed(42);

        // Save the search info
        this._searchParams = searchParams;
        this._workerID = workerID;
        this._cjDAO = cjDAO;
        this._jobID = jobID;

        // Log search params
        this.logger.info("searchParams: \n%s" % (pprint.pformat(
            clippedObj(searchParams))));

        this._createCheckpoints = this._searchParams.get('createCheckpoints',
                                                         False);
        this._maxModels = this._searchParams.get('maxModels', None);
        if( this._maxModels == -1)
        {
          this._maxModels = None;
        }
        this._predictionCacheMaxRecords = this._searchParams.get('predictionCacheMaxRecords', None);

        // Speculative particles?
        this._speculativeParticles = this._searchParams.get('speculativeParticles',
            bool(int(Configuration.get(
                            'nupic.hypersearch.speculative.particles.default'))));
        this._speculativeWaitSecondsMax = float(Configuration.get(
                        'nupic.hypersearch.speculative.particles.sleepSecondsMax'));

        // Maximum Field Branching
        this._maxBranching= int(Configuration.get(
                                 'nupic.hypersearch.max.field.branching'));

        // Minimum Field Contribution
        this._minFieldContribution= float(Configuration.get(
                                 'nupic.hypersearch.min.field.contribution'));

        // This gets set if we detect that the job got cancelled
        this._jobCancelled = False;

        // Use terminators (typically set by permutations_runner.py)
        if( 'useTerminators' in this._searchParams)
        {
          useTerminators = this._searchParams["useTerminators"];
          useTerminators = str(int(useTerminators));

          Configuration.set('nupic.hypersearch.enableModelTermination', useTerminators);
          Configuration.set('nupic.hypersearch.enableModelMaturity', useTerminators);
          Configuration.set('nupic.hypersearch.enableSwarmTermination', useTerminators);
        }

        // Special test mode?
        if( 'NTA_TEST_exitAfterNModels' in os.environ)
        {
          this._maxModels = int(os.environ["NTA_TEST_exitAfterNModels"]);
        }

        this._dummyModel = this._searchParams.get('dummyModel', None);

        // Holder for temporary directory, if any, that needs to be cleaned up
        // in our close() method.
        this._tempDir = None;
        try
        {
          // Get the permutations info. This can be either:
          //  1.) JSON encoded search description (this will be used to generate a
          //       permutations.py and description.py files using ExpGenerator)
          //  2.) path to a pre-generated permutations.py file. The description.py is
          //       assumed to be in the same directory
          //  3.) contents of the permutations.py and descrption.py files.
          if( 'description' in this._searchParams)
          {
            if ('permutationsPyFilename' in this._searchParams or
                'permutationsPyContents' in this._searchParams or
                'descriptionPyContents' in this._searchParams)
            {
              raise RuntimeError(
                  "Either 'description', 'permutationsPyFilename' or"
                  "'permutationsPyContents' & 'permutationsPyContents' should be "
                  "specified, but not two or more of these at once.");
            }

            // Calculate training period for anomaly models
            searchParamObj = this._searchParams;
            anomalyParams = searchParamObj["description"].get('anomalyParams',
              dict());

            // This is used in case searchParamObj["description"]["anomalyParams"]
            // is set to None.
            if( anomalyParams is None)
            {
              anomalyParams = dict();
            }

            if (('autoDetectWaitRecords' not in anomalyParams) or
                (anomalyParams["autoDetectWaitRecords"] is None))
            {
              streamDef = this._getStreamDef(searchParamObj["description"]);

              from nupic.data.stream_reader import StreamReader;

              try
              {
                streamReader = StreamReader(streamDef, isBlocking=False,
                                               maxTimeout=0, eofOnTimeout=True);
                anomalyParams["autoDetectWaitRecords"] = \
                  streamReader.getDataRowCount();
              }
              catch( Exception)
              {
                anomalyParams["autoDetectWaitRecords"] = None;
              }
              this._searchParams["description"]["anomalyParams"] = anomalyParams;
            }


            // Call the experiment generator to generate the permutations and base
            // description file.
            outDir = this._tempDir = tempfile.mkdtemp();
            expGenerator([
                '--description=%s' % (
                    json.dumps(this._searchParams["description"])),
                '--version=v2',
                '--outDir=%s' % (outDir)]);

            // Get the name of the permutations script.
            permutationsScript = os.path.join(outDir, 'permutations.py');
          }

          else if( 'permutationsPyFilename' in this._searchParams)
          {
            if ('description' in this._searchParams or
                'permutationsPyContents' in this._searchParams or
                'descriptionPyContents' in this._searchParams)
            {
              raise RuntimeError(
                  "Either 'description', 'permutationsPyFilename' or "
                  "'permutationsPyContents' & 'permutationsPyContents' should be "
                  "specified, but not two or more of these at once.");
            }
            permutationsScript = this._searchParams["permutationsPyFilename"];
          }

          else if( 'permutationsPyContents' in this._searchParams)
          {
            if ('description' in this._searchParams or
                'permutationsPyFilename' in this._searchParams)
            {
              raise RuntimeError(
                  "Either 'description', 'permutationsPyFilename' or"
                  "'permutationsPyContents' & 'permutationsPyContents' should be "
                  "specified, but not two or more of these at once.");
            }

            assert ('descriptionPyContents' in this._searchParams);
            // Generate the permutations.py and description.py files
            outDir = this._tempDir = tempfile.mkdtemp();
            permutationsScript = os.path.join(outDir, 'permutations.py');
            fd = open(permutationsScript, 'w');
            fd.write(this._searchParams["permutationsPyContents"]);
            fd.close();
            fd = open(os.path.join(outDir, 'description.py'), 'w');
            fd.write(this._searchParams["descriptionPyContents"]);
            fd.close();
          }

          else
          {
            raise RuntimeError ("Either 'description' or 'permutationsScript' must be"
                                "specified");
          }

          // Get the base path of the experiment and read in the base description
          this._basePath = os.path.dirname(permutationsScript);
          this._baseDescription = open(os.path.join(this._basePath,
                                                   'description.py')).read();
          this._baseDescriptionHash = hashlib.md5(this._baseDescription).digest();

          // Read the model config to figure out the inference type
          modelDescription, _ = opfhelpers.loadExperiment(this._basePath);

          // Read info from permutations file. This sets up the following member
          // variables:
          //   _predictedField
          //   _permutations
          //   _flattenedPermutations
          //   _encoderNames
          //   _reportKeys
          //   _filterFunc
          //   _optimizeKey
          //   _maximize
          //   _dummyModelParamsFunc
          this._readPermutationsFile(permutationsScript, modelDescription);

          // Fill in and save the base description and permutations file contents
          //  if they haven't already been filled in by another worker
          if( this._cjDAO is not None)
          {
            updated = this._cjDAO.jobSetFieldIfEqual(jobID=this._jobID,
                                                     fieldName='genBaseDescription',
                                                     curValue=None,
                                                     newValue = this._baseDescription);
            if( updated)
            {
              permContents = open(permutationsScript).read();
              this._cjDAO.jobSetFieldIfEqual(jobID=this._jobID,
                                             fieldName='genPermutations',
                                             curValue=None,
                                             newValue = permContents);
            }
          }

          // if user provided an artificialMetric, force use of the dummy model
          if( this._dummyModelParamsFunc is not None)
          {
            if( this._dummyModel is None)
            {
              this._dummyModel = dict();
            }
          }

          // If at DEBUG log level, print out permutations info to the log
          if( this.logger.getEffectiveLevel() <= logging.DEBUG)
          {
            msg = StringIO.StringIO();
            print >> msg, "Permutations file specifications: ";
            info = dict();
            for( key in ["_predictedField', '_permutations',
                        '_flattenedPermutations', '_encoderNames',
                        '_reportKeys', '_optimizeKey', '_maximize"])
            {
              info[key] = getattr(self, key);
            }
            print >> msg, pprint.pformat(info);
            this.logger.debug(msg.getvalue());
            msg.close();
          }

          // Instantiate our database to hold the results we received so far
          this._resultsDB = ResultsDB(self);

          // Instantiate the Swarm Terminator
          this._swarmTerminator = SwarmTerminator();

          // Initial hypersearch state
          this._hsState = None;

          // The Max // of attempts we will make to create a unique model before
          //  giving up.
          this._maxUniqueModelAttempts = int(Configuration.get(
                                          'nupic.hypersearch.maxUniqueModelAttempts'));

          // The max amount of time allowed before a model is considered orphaned.
          this._modelOrphanIntervalSecs = float(Configuration.get(
                                          'nupic.hypersearch.modelOrphanIntervalSecs'));

          // The max percent of models that can complete with errors
          this._maxPctErrModels = float(Configuration.get(
                                          'nupic.hypersearch.maxPctErrModels'));
        }

        catch()
        {
          // Clean up our temporary directory, if any
          if( this._tempDir is not None)
          {
            shutil.rmtree(this._tempDir);
            this._tempDir = None;
          }

          raise;
        }

        return;
      }


      def _getStreamDef(self, modelDescription)
      {
        """
        Generate stream definition based on
        """;
        #--------------------------------------------------------------------------
        // Generate the string containing the aggregation settings.
        aggregationPeriod = {
            'days': 0,
            'hours': 0,
            'microseconds': 0,
            'milliseconds': 0,
            'minutes': 0,
            'months': 0,
            'seconds': 0,
            'weeks': 0,
            'years': 0,
        };

        // Honor any overrides provided in the stream definition
        aggFunctionsDict = {};
        if( 'aggregation' in modelDescription["streamDef"])
        {
          for( key in aggregationPeriod.keys())
          {
            if( key in modelDescription["streamDef"]["aggregation"])
            {
              aggregationPeriod[key] = modelDescription["streamDef"]["aggregation"][key];
            }
          }
          if( 'fields' in modelDescription["streamDef"]["aggregation"])
          {
            for (fieldName, func) in modelDescription["streamDef"]["aggregation"]["fields"]
            {
              aggFunctionsDict[fieldName] = str(func);
            }
          }
        }

        // Do we have any aggregation at all?
        hasAggregation = False;
        for( v in aggregationPeriod.values())
        {
          if( v != 0)
          {
            hasAggregation = True;
            break;
          }
        }

        // Convert the aggFunctionsDict to a list
        aggFunctionList = aggFunctionsDict.items();
        aggregationInfo = dict(aggregationPeriod);
        aggregationInfo["fields"] = aggFunctionList;

        streamDef = copy.deepcopy(modelDescription["streamDef"]);
        streamDef["aggregation"] = copy.deepcopy(aggregationInfo);
        return streamDef;
      }


      def __del__(self)
      {
        """Destructor; NOTE: this is not guaranteed to be called (bugs like
        circular references could prevent it from being called).
        """;
        this.close();
        return;
      }

      def close(self)
      {
        """Deletes temporary system objects/files. """;
        if( this._tempDir is not None and os.path.isdir(this._tempDir))
        {
          this.logger.debug("Removing temporary directory %r", this._tempDir);
          shutil.rmtree(this._tempDir);
          this._tempDir = None;
        }

        return;
      }



      def _readPermutationsFile(self, filename, modelDescription)
      {
        """
       ///  Read the permutations file and initialize the following member variables:
       ///      _predictedField: field name of the field we are trying to
       ///        predict
       ///      _permutations: Dict containing the full permutations dictionary.
       ///      _flattenedPermutations: Dict containing the flattened version of
       ///        _permutations. The keys leading to the value in the dict are joined
       ///        with a period to create the new key and permute variables within
       ///        encoders are pulled out of the encoder.
       ///      _encoderNames: keys from this._permutations of only the encoder
       ///        variables.
       ///      _reportKeys:   The 'report' list from the permutations file.
       ///        This is a list of the items from each experiment's pickled
       ///        results file that should be included in the final report. The
       ///        format of each item is a string of key names separated by colons,
       ///        each key being one level deeper into the experiment results
       ///        dict. For example, 'key1:key2'.
       ///      _filterFunc: a user-supplied function that can be used to
       ///        filter out specific permutation combinations.
       ///      _optimizeKey: which report key to optimize for
       ///      _maximize: True if we should try and maximize the optimizeKey
       ///        metric. False if we should minimize it.
       ///      _dummyModelParamsFunc: a user-supplied function that can be used to
       ///        artificially generate CLA model results. When supplied,
       ///        the model is not actually run through the OPF, but instead is run
       ///        through a "Dummy Model" (nupic.swarming.ModelRunner.
       ///        OPFDummyModelRunner). This function returns the params dict used
       ///        to control various options in the dummy model (the returned metric,
       ///        the execution time, etc.). This is used for hypersearch algorithm
       ///        development.
       /// 
       ///  Parameters:
       ///  ---------------------------------------------------------
       ///  filename:     Name of permutations file
       ///  retval:       None
        """;
        // Open and execute the permutations file
        vars = {};

        permFile = execfile(filename, globals(), vars);


        // Read in misc info.
        this._reportKeys = vars.get('report', []);
        this._filterFunc = vars.get('permutationFilter', None);
        this._dummyModelParamsFunc = vars.get('dummyModelParams', None);
        this._predictedField = None;   // default
        this._predictedFieldEncoder = None;   // default
        this._fixedFields = None; // default

        // The fastSwarm variable, if present, contains the params from a best
        //  model from a previous swarm. If present, use info from that to seed
        //  a fast swarm
        this._fastSwarmModelParams = vars.get('fastSwarmModelParams', None);
        if( this._fastSwarmModelParams is not None)
        {
          encoders = this._fastSwarmModelParams["structuredParams"]["modelParams"]\
                      ["sensorParams"]["encoders"];
          this._fixedFields = [];
          for( fieldName in encoders)
          {
            if( encoders[fieldName] is not None)
            {
              this._fixedFields.append(fieldName);
            }
          }
        }

        if( 'fixedFields' in vars)
        {
          this._fixedFields = vars["fixedFields"];
        }

        // Get min number of particles per swarm from either permutations file or
        // config.
        this._minParticlesPerSwarm = vars.get('minParticlesPerSwarm');
        if( this._minParticlesPerSwarm  == None)
        {
          this._minParticlesPerSwarm = Configuration.get(
                                          'nupic.hypersearch.minParticlesPerSwarm');
        }
        this._minParticlesPerSwarm = int(this._minParticlesPerSwarm);

        // Enable logic to kill off speculative swarms when an earlier sprint
        //  has found that it contains poorly performing field combination?
        this._killUselessSwarms = vars.get('killUselessSwarms', True);

        // The caller can request that the predicted field ALWAYS be included ("yes")
        //  or optionally include ("auto"). The setting of "no" is N/A and ignored
        //  because in that case the encoder for the predicted field will not even
        //  be present in the permutations file.
        // When set to "yes", this will force the first sprint to try the predicted
        //  field only (the legacy mode of swarming).
        // When set to "auto", the first sprint tries all possible fields (one at a
        //  time) in the first sprint.
        this._inputPredictedField = vars.get("inputPredictedField", "yes");

        // Try all possible 3-field combinations? Normally, we start with the best
        //  2-field combination as a base. When this flag is set though, we try
        //  all possible 3-field combinations which takes longer but can find a
        //  better model.
        this._tryAll3FieldCombinations = vars.get('tryAll3FieldCombinations', False);

        // Always include timestamp fields in the 3-field swarms?
        // This is a less compute intensive version of tryAll3FieldCombinations.
        // Instead of trying ALL possible 3 field combinations, it just insures
        // that the timestamp fields (dayOfWeek, timeOfDay, weekend) are never left
        // out when generating the 3-field swarms.
        this._tryAll3FieldCombinationsWTimestamps = vars.get(
                                    'tryAll3FieldCombinationsWTimestamps', False);

        // Allow the permutations file to override minFieldContribution. This would
        //  be set to a negative number for large swarms so that you don't disqualify
        //  a field in an early sprint just because it did poorly there. Sometimes,
        //  a field that did poorly in an early sprint could help accuracy when
        //  added in a later sprint
        minFieldContribution = vars.get('minFieldContribution', None);
        if( minFieldContribution is not None)
        {
          this._minFieldContribution = minFieldContribution;
        }

        // Allow the permutations file to override maxBranching.
        maxBranching = vars.get('maxFieldBranching', None);
        if( maxBranching is not None)
        {
          this._maxBranching = maxBranching;
        }

        // Read in the optimization info.
        if( 'maximize' in vars)
        {
          this._optimizeKey = vars["maximize"];
          this._maximize = True;
        }
        else if( 'minimize' in vars)
        {
          this._optimizeKey = vars["minimize"];
          this._maximize = False;
        }
        else
        {
          raise RuntimeError("Permutations file '%s' does not include a maximize"
                             " or minimize metric.");
        }

        // The permutations file is the new location for maxModels. The old location,
        //  in the jobParams is deprecated.
        maxModels = vars.get('maxModels');
        if( maxModels is not None)
        {
          if( this._maxModels is None)
          {
            this._maxModels = maxModels;
          }
          else
          {
            raise RuntimeError('It is an error to specify maxModels both in the job'
                    ' params AND in the permutations file.');
          }
        }


        // Figure out if what kind of search this is:
        #
        //  If it's a temporal prediction search:
        //    the first sprint has 1 swarm, with just the predicted field
        //  elif it's a spatial prediction search:
        //    the first sprint has N swarms, each with predicted field + one
        //    other field.
        //  elif it's a classification search:
        //    the first sprint has N swarms, each with 1 field
        inferenceType = modelDescription["modelParams"]["inferenceType"];
        if( not InferenceType.validate(inferenceType))
        {
          raise ValueError("Invalid inference type %s" %inferenceType);
        }

        if( inferenceType in [InferenceType.TemporalMultiStep,
                             InferenceType.NontemporalMultiStep])
        {
          // If it does not have a separate encoder for the predicted field that
          //  goes to the classifier, it is a legacy multi-step network
          classifierOnlyEncoder = None;
          for( encoder in modelDescription["modelParams"]["sensorParams"]\
                        ["encoders"].values())
          {
            if( encoder.get("classifierOnly", False) \
                 and encoder["fieldname"] == vars.get('predictedField', None))
            {
              classifierOnlyEncoder = encoder;
              break;
            }
          }

          if( classifierOnlyEncoder is None or this._inputPredictedField=="yes")
          {
            // If we don't have a separate encoder for the classifier (legacy
            //  MultiStep) or the caller explicitly wants to include the predicted
            //  field, then use the legacy temporal search methodology.
            this._searchType = HsSearchType.legacyTemporal;
          }
          else
          {
            this._searchType = HsSearchType.temporal;
          }
        }


        else if( inferenceType in [InferenceType.TemporalNextStep,
                             InferenceType.TemporalAnomaly])
        {
          this._searchType = HsSearchType.legacyTemporal;
        }

        else if( inferenceType in (InferenceType.TemporalClassification,
                                InferenceType.NontemporalClassification))
        {
          this._searchType = HsSearchType.classification;
        }

        else
        {
          raise RuntimeError("Unsupported inference type: %s" % inferenceType);
        }

        // Get the predicted field. Note that even classification experiments
        //  have a "predicted" field - which is the field that contains the
        //  classification value.
        this._predictedField = vars.get('predictedField', None);
        if( this._predictedField is None)
        {
          raise RuntimeError("Permutations file '%s' does not have the required"
                             " 'predictedField' variable" % filename);
        }

        // Read in and validate the permutations dict
        if( 'permutations' not in vars)
        {
          raise RuntimeError("Permutations file '%s' does not define permutations" % filename);
        }

        if( not isinstance(vars["permutations"], dict))
        {
          raise RuntimeError("Permutations file '%s' defines a permutations variable "
                             "but it is not a dict");
        }

        this._encoderNames = [];
        this._permutations = vars["permutations"];
        this._flattenedPermutations = dict();
        def _flattenPermutations(value, keys)
        {
          if( ':' in keys[-1])
          {
            raise RuntimeError("The permutation variable '%s' contains a ':' "
                               "character, which is not allowed.");
          }
          flatKey = _flattenKeys(keys);
          if( isinstance(value, PermuteEncoder))
          {
            this._encoderNames.append(flatKey);

            // If this is the encoder for the predicted field, save its name.
            if( value.fieldName == this._predictedField)
            {
              this._predictedFieldEncoder = flatKey;
            }

            // Store the flattened representations of the variables within the
            // encoder.
            for( encKey, encValue in value.kwArgs.iteritems())
            {
              if( isinstance(encValue, PermuteVariable))
              {
                this._flattenedPermutations["%s:%s' % (flatKey, encKey)] = encValue;
              }
            }
          }
          else if( isinstance(value, PermuteVariable))
          {
            this._flattenedPermutations[flatKey] = value;
          }


          else
          {
            if( isinstance(value, PermuteVariable))
            {
              this._flattenedPermutations[key] = value;
            }
          }
        }
        rApply(this._permutations, _flattenPermutations);
      }


      def getExpectedNumModels(self)
      {
        """Computes the number of models that are expected to complete as part of
        this instances's HyperSearch.

        NOTE: This is compute-intensive for HyperSearches with a huge number of
        combinations.

        NOTE/TODO:  THIS ONLY WORKS FOR RONOMATIC: This method is exposed for the
                    benefit of perutations_runner.py for use in progress
                    reporting.

        Parameters:
        ---------------------------------------------------------
        retval:       The total number of expected models, if known; -1 if unknown
        """;
        return -1;
      }

      def getModelNames(self)
      {
        """Generates a list of model names that are expected to complete as part of
        this instances's HyperSearch.

        NOTE: This is compute-intensive for HyperSearches with a huge number of
        combinations.

        NOTE/TODO:  THIS ONLY WORKS FOR RONOMATIC: This method is exposed for the
                    benefit of perutations_runner.py.

        Parameters:
        ---------------------------------------------------------
        retval:       List of model names for this HypersearchV2 instance, or
                      None of not applicable
        """;
        return None;
      }

      def getPermutationVariables(self)
      {
        """Returns a dictionary of permutation variables.

        Parameters:
        ---------------------------------------------------------
        retval:       A dictionary of permutation variables; keys are
                      flat permutation variable names and each value is
                      a sub-class of PermuteVariable.
        """;
        return this._flattenedPermutations;
      }

      def getComplexVariableLabelLookupDict(self)
      {
        """Generates a lookup dictionary of permutation variables whose values
        are too complex for labels, so that artificial labels have to be generated
        for them.

        Parameters:
        ---------------------------------------------------------
        retval:       A look-up dictionary of permutation
                      variables whose values are too complex for labels, so
                      artificial labels were generated instead (e.g., "Choice0",
                      "Choice1", etc.); the key is the name of the complex variable
                      and the value is:
                        dict(labels=<list_of_labels>, values=<list_of_values>).
        """;
        raise NotImplementedError;
      }

      def getOptimizationMetricInfo(self)
      {
        """Retrieves the optimization key name and optimization function.

        Parameters:
        ---------------------------------------------------------
        retval:       (optimizationMetricKey, maximize)
                      optimizationMetricKey: which report key to optimize for
                      maximize: True if we should try and maximize the optimizeKey
                        metric. False if we should minimize it.
        """;
        return (this._optimizeKey, this._maximize);
      }

      def _checkForOrphanedModels (self)
      {
        """If there are any models that haven't been updated in a while, consider
        them dead, and mark them as hidden in our resultsDB. We also change the
        paramsHash and particleHash of orphaned models so that we can
        re-generate that particle and/or model again if we desire.

        Parameters:
        ----------------------------------------------------------------------
        retval:

        """;

        this.logger.debug("Checking for orphaned models older than %s" % \
                         (this._modelOrphanIntervalSecs));

        while( True)
        {
          orphanedModelId = this._cjDAO.modelAdoptNextOrphan(this._jobID,
                                                    this._modelOrphanIntervalSecs);
          if( orphanedModelId is None)
          {
            return;
          }

          this.logger.info("Removing orphaned model: %d" % (orphanedModelId));

          // Change the model hash and params hash as stored in the models table so
          //  that we can insert a new model with the same paramsHash
          for( attempt in range(100))
          {
            paramsHash = hashlib.md5("OrphanParams.%d.%d" % (orphanedModelId,
                                                             attempt)).digest();
            particleHash = hashlib.md5("OrphanParticle.%d.%d" % (orphanedModelId,
                                                              attempt)).digest();
            try
            {
              this._cjDAO.modelSetFields(orphanedModelId,
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

          // Mark this model as complete, with reason "orphaned"
          this._cjDAO.modelSetCompleted(modelID=orphanedModelId,
                        completionReason=ClientJobsDAO.CMPL_REASON_ORPHAN,
                        completionMsg="Orphaned");

          // Update our results DB immediately, rather than wait for the worker
          //  to inform us. This insures that the getParticleInfos() calls we make
          //  below don't include this particle. Setting the metricResult to None
          //  sets it to worst case
          this._resultsDB.update(modelID=orphanedModelId,
                                 modelParams=None,
                                 modelParamsHash=paramsHash,
                                 metricResult=None,
                                 completed = True,
                                 completionReason = ClientJobsDAO.CMPL_REASON_ORPHAN,
                                 matured = True,
                                 numRecords = 0);
        }
      }


      def _hsStatePeriodicUpdate(self, exhaustedSwarmId=None)
      {
        """
        Periodically, check to see if we should remove a certain field combination
        from evaluation (because it is doing so poorly) or move on to the next
        sprint (add in more fields).

        This method is called from _getCandidateParticleAndSwarm(), which is called
        right before we try and create a new model to run.

        Parameters:
        -----------------------------------------------------------------------
        removeSwarmId:     If not None, force a change to the current set of active
                          swarms by removing this swarm. This is used in situations
                          where we can't find any new unique models to create in
                          this swarm. In these situations, we update the hypersearch
                          state regardless of the timestamp of the last time another
                          worker updated it.

        """;
        if( this._hsState is None)
        {
          this._hsState =  HsState(self);
        }

        // Read in current state from the DB
        this._hsState.readStateFromDB();

        // This will hold the list of completed swarms that we find
        completedSwarms = set();

        // Mark the exhausted swarm as completing/completed, if any
        if( exhaustedSwarmId is not None)
        {
          this.logger.info("Removing swarm %s from the active set "
                           "because we can't find any new unique particle "
                           "positions" % (exhaustedSwarmId));
          // Is it completing or completed?
          (particles, _, _, _, _) = this._resultsDB.getParticleInfos(
                                          swarmId=exhaustedSwarmId, matured=False);
          if( len(particles) > 0)
          {
            exhaustedSwarmStatus = 'completing';
          }
          else
          {
            exhaustedSwarmStatus = 'completed';
          }
        }

        // Kill all swarms that don't need to be explored based on the most recent
        // information.
        if( this._killUselessSwarms)
        {
          this._hsState.killUselessSwarms();
        }

        // For all swarms that were in the 'completing' state, see if they have
        // completed yet.
        #
        // Note that we are not quite sure why this doesn't automatically get handled
        // when we receive notification that a model finally completed in a swarm.
        // But, we ARE running into a situation, when speculativeParticles is off,
        // where we have one or more swarms in the 'completing' state even though all
        // models have since finished. This logic will serve as a failsafe against
        // this situation.
        completingSwarms = this._hsState.getCompletingSwarms();
        for( swarmId in completingSwarms)
        {
          // Is it completed?
          (particles, _, _, _, _) = this._resultsDB.getParticleInfos(
                                          swarmId=swarmId, matured=False);
          if( len(particles) == 0)
          {
            completedSwarms.add(swarmId);
          }
        }

        // Are there any swarms we can remove (because they have matured)?
        completedSwarmGens = this._resultsDB.getMaturedSwarmGenerations();
        priorCompletedSwarms = this._hsState.getCompletedSwarms();
        for (swarmId, genIdx, errScore) in completedSwarmGens
        {

          // Don't need to report it if the swarm already completed
          if( swarmId in priorCompletedSwarms)
          {
            continue;
          }

          completedList = this._swarmTerminator.recordDataPoint(
              swarmId=swarmId, generation=genIdx, errScore=errScore);

          // Update status message
          statusMsg = "Completed generation #%d of swarm '%s' with a best" \
                      " errScore of %g" % (genIdx, swarmId, errScore);
          if( len(completedList) > 0)
          {
            statusMsg = "%s. Matured swarm(s): %s" % (statusMsg, completedList);
          }
          this.logger.info(statusMsg);
          this._cjDAO.jobSetFields (jobID=this._jobID,
                                    fields=dict(engStatus=statusMsg),
                                    useConnectionID=False,
                                    ignoreUnchanged=True);

          // Special test mode to check which swarms have terminated
          if( 'NTA_TEST_recordSwarmTerminations' in os.environ)
          {
            while( True)
            {
              resultsStr = this._cjDAO.jobGetFields(this._jobID, ["results"])[0];
              if( resultsStr is None)
              {
                results = {};
              }
              else
              {
                results = json.loads(resultsStr);
              }
              if( not 'terminatedSwarms' in results)
              {
                results["terminatedSwarms"] = {};
              }

              for( swarm in completedList)
              {
                if( swarm not in results["terminatedSwarms"])
                {
                  results["terminatedSwarms"][swarm] = (genIdx,
                                        this._swarmTerminator.swarmScores[swarm]);
                }
              }

              newResultsStr = json.dumps(results);
              if( newResultsStr == resultsStr)
              {
                break;
              }
              updated = this._cjDAO.jobSetFieldIfEqual(jobID=this._jobID,
                                                       fieldName='results',
                                                       curValue=resultsStr,
                                                       newValue = json.dumps(results));
              if( updated)
              {
                break;
              }
            }
          }

          if( len(completedList) > 0)
          {
            for( name in completedList)
            {
              this.logger.info("Swarm matured: %s. Score at generation %d: "
                               "%s" % (name, genIdx, errScore));
            }
            completedSwarms = completedSwarms.union(completedList);
          }
        }

        if( len(completedSwarms)==0 and (exhaustedSwarmId is None))
        {
          return;
        }

        // We need to mark one or more swarms as completed, keep trying until
        //  successful, or until some other worker does it for us.
        while( True)
        {

          if( exhaustedSwarmId is not None)
          {
            this._hsState.setSwarmState(exhaustedSwarmId, exhaustedSwarmStatus);
          }

          // Mark the completed swarms as completed
          for( swarmId in completedSwarms)
          {
            this._hsState.setSwarmState(swarmId, 'completed');
          }

          // If nothing changed, we're done
          if( not this._hsState.isDirty())
          {
            return;
          }

          // Update the shared Hypersearch state now
          // This will do nothing and return False if some other worker beat us to it
          success = this._hsState.writeStateToDB();

          if( success)
          {
            // Go through and cancel all models that are still running, except for
            // the best model. Once the best model changes, the one that used to be
            // best (and has  matured) will notice that and stop itself at that point.
            jobResultsStr = this._cjDAO.jobGetFields(this._jobID, ["results"])[0];
            if( jobResultsStr is not None)
            {
              jobResults = json.loads(jobResultsStr);
              bestModelId = jobResults.get('bestModel', None);
            }
            else
            {
              bestModelId = None;
            }

            for( swarmId in list(completedSwarms))
            {
              (_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(
                                              swarmId=swarmId, completed=False);
              if( bestModelId in modelIds)
              {
                modelIds.remove(bestModelId);
              }
              if( len(modelIds) == 0)
              {
                continue;
              }
              this.logger.info("Killing the following models in swarm '%s' because"
                               "the swarm is being terminated: %s" % (swarmId,
                               str(modelIds)));

              for( modelId in modelIds)
              {
                this._cjDAO.modelSetFields(modelId,
                        dict(engStop=ClientJobsDAO.STOP_REASON_KILLED),
                        ignoreUnchanged = True);
              }
            }
            return;
          }

          // We were not able to change the state because some other worker beat us
          // to it.
          // Get the new state, and try again to apply our changes.
          this._hsState.readStateFromDB();
          this.logger.debug("New hsState has been set by some other worker to: "
                           " \n%s" % (pprint.pformat(this._hsState._state, indent=4)));
        }
      }

      def _getCandidateParticleAndSwarm (self, exhaustedSwarmId=None)
      {
        """Find or create a candidate particle to produce a new model.

        At any one time, there is an active set of swarms in the current sprint, where
        each swarm in the sprint represents a particular combination of fields.
        Ideally, we should try to balance the number of models we have evaluated for
        each swarm at any time.

        This method will see how many models have been evaluated for each active
        swarm in the current active sprint(s) and then try and choose a particle
        from the least represented swarm in the first possible active sprint, with
        the following constraints/rules:

        for each active sprint:
          for each active swarm (preference to those with least// of models so far):
            1.) The particle will be created from new (generation #0) if there are not
            already this._minParticlesPerSwarm particles in the swarm.

            2.) Find the first gen that has a completed particle and evolve that
            particle to the next generation.

            3.) If we got to here, we know that we have satisfied the min// of
            particles for the swarm, and they are all currently running (probably at
            various generation indexes). Go onto the next swarm

          If we couldn't find a swarm to allocate a particle in, go onto the next
          sprint and start allocating particles there....


        Parameters:
        ----------------------------------------------------------------
        exhaustedSwarmId:   If not None, force a change to the current set of active
                            swarms by marking this swarm as either 'completing' or
                            'completed'. If there are still models being evaluaed in
                            it, mark it as 'completing', else 'completed. This is
                            used in situations where we can't find any new unique
                            models to create in this swarm. In these situations, we
                            force an update to the hypersearch state so no other
                            worker wastes time try to use this swarm.

        retval: (exit, particle, swarm)
                  exit: If true, this worker is ready to exit (particle and
                          swarm will be None)
                  particle: Which particle to run
                  swarm: which swarm the particle is in

                  NOTE: When particle and swarm are None and exit is False, it
                  means that we need to wait for one or more other worker(s) to
                  finish their respective models before we can pick a particle
                  to run. This will generally only happen when speculativeParticles
                  is set to False.
        """;
        // Cancel search?
        jobCancel = this._cjDAO.jobGetFields(this._jobID, ["cancel"])[0];
        if( jobCancel)
        {
          this._jobCancelled = True;
          // Did a worker cancel the job because of an error?
          (workerCmpReason, workerCmpMsg) = this._cjDAO.jobGetFields(this._jobID,
              ["workerCompletionReason', 'workerCompletionMsg"]);
          if( workerCmpReason == ClientJobsDAO.CMPL_REASON_SUCCESS)
          {
            this.logger.info("Exiting due to job being cancelled");
            this._cjDAO.jobSetFields(this._jobID,
                  dict(workerCompletionMsg="Job was cancelled"),
                  useConnectionID=False, ignoreUnchanged=True);
          }
          else
          {
            this.logger.error("Exiting because some worker set the "
                  "workerCompletionReason to %s. WorkerCompletionMsg: %s" %
                  (workerCmpReason, workerCmpMsg));
          }
          return (True, None, None);
        }

        // Perform periodic updates on the Hypersearch state.
        if( this._hsState is not None)
        {
          priorActiveSwarms = this._hsState.getActiveSwarms();
        }
        else
        {
          priorActiveSwarms = None;
        }

        // Update the HypersearchState, checking for matured swarms, and marking
        //  the passed in swarm as exhausted, if any
        this._hsStatePeriodicUpdate(exhaustedSwarmId=exhaustedSwarmId);

        // The above call may have modified this._hsState["activeSwarmIds"]
        // Log the current set of active swarms
        activeSwarms = this._hsState.getActiveSwarms();
        if( activeSwarms != priorActiveSwarms)
        {
          this.logger.info("Active swarms changed to %s (from %s)" % (activeSwarms,
                                                            priorActiveSwarms));
        }
        this.logger.debug("Active swarms: %s" % (activeSwarms));

        // If too many model errors were detected, exit
        totalCmpModels = this._resultsDB.getNumCompletedModels();
        if( totalCmpModels > 5)
        {
          numErrs = this._resultsDB.getNumErrModels();
          if (float(numErrs) / totalCmpModels) > this._maxPctErrModels
          {
            // Get one of the errors
            errModelIds = this._resultsDB.getErrModelIds();
            resInfo = this._cjDAO.modelsGetResultAndStatus([errModelIds[0]])[0];
            modelErrMsg = resInfo.completionMsg;
            cmpMsg = "%s: Exiting due to receiving too many models failing" \
                     " from exceptions (%d out of %d). \nModel Exception: %s" % \
                      (ErrorCodes.tooManyModelErrs, numErrs, totalCmpModels,
                       modelErrMsg);
            this.logger.error(cmpMsg);

            // Cancel the entire job now, if it has not already been cancelled
            workerCmpReason = this._cjDAO.jobGetFields(this._jobID,
                ["workerCompletionReason"])[0];
            if( workerCmpReason == ClientJobsDAO.CMPL_REASON_SUCCESS)
            {
              this._cjDAO.jobSetFields(
                  this._jobID,
                  fields=dict(
                          cancel=True,
                          workerCompletionReason = ClientJobsDAO.CMPL_REASON_ERROR,
                          workerCompletionMsg = cmpMsg),
                  useConnectionID=False,
                  ignoreUnchanged=True);
            }
            return (True, None, None);
          }
        }

        // If HsState thinks the search is over, exit. It is seeing if the results
        //  on the sprint we just completed are worse than a prior sprint.
        if( this._hsState.isSearchOver())
        {
          cmpMsg = "Exiting because results did not improve in most recently" \
                            " completed sprint.";
          this.logger.info(cmpMsg);
          this._cjDAO.jobSetFields(this._jobID,
                dict(workerCompletionMsg=cmpMsg),
                useConnectionID=False, ignoreUnchanged=True);
          return (True, None, None);
        }

        // Search successive active sprints, until we can find a candidate particle
        //   to work with
        sprintIdx = -1;
        while( True)
        {
          // Is this sprint active?
          sprintIdx += 1;
          (active, eos) = this._hsState.isSprintActive(sprintIdx);

          // If no more sprints to explore:
          if( eos)
          {
            // If any prior ones are still being explored, finish up exploring them
            if( this._hsState.anyGoodSprintsActive())
            {
              this.logger.info("No more sprints to explore, waiting for prior"
                             " sprints to complete");
              return (False, None, None);
            }

            // Else, we're done
            else
            {
              cmpMsg = "Exiting because we've evaluated all possible field " \
                               "combinations";
              this._cjDAO.jobSetFields(this._jobID,
                                       dict(workerCompletionMsg=cmpMsg),
                                       useConnectionID=False, ignoreUnchanged=True);
              this.logger.info(cmpMsg);
              return (True, None, None);
            }
          }

          if( not active)
          {
            if( not this._speculativeParticles)
            {
              if( not this._hsState.isSprintCompleted(sprintIdx))
              {
                this.logger.info("Waiting for all particles in sprint %d to complete"
                              "before evolving any more particles" % (sprintIdx));
                return (False, None, None);
              }
            }
            continue;
          }


          // ====================================================================
          // Look for swarms that have particle "holes" in their generations. That is,
          //  an earlier generation with less than minParticlesPerSwarm. This can
          //  happen if a model that was started eariler got orphaned. If we detect
          //  this, start a new particle in that generation.
          swarmIds = this._hsState.getActiveSwarms(sprintIdx);
          for( swarmId in swarmIds)
          {
            firstNonFullGenIdx = this._resultsDB.firstNonFullGeneration(
                                    swarmId=swarmId,
                                    minNumParticles=this._minParticlesPerSwarm);
            if( firstNonFullGenIdx is None)
            {
              continue;
            }

            if( firstNonFullGenIdx < this._resultsDB.highestGeneration(swarmId))
            {
              this.logger.info("Cloning an earlier model in generation %d of swarm "
                  "%s (sprintIdx=%s) to replace an orphaned model" % (
                    firstNonFullGenIdx, swarmId, sprintIdx));

              // Clone a random orphaned particle from the incomplete generation
              (allParticles, allModelIds, errScores, completed, matured) = \
                this._resultsDB.getOrphanParticleInfos(swarmId, firstNonFullGenIdx);

              if( len(allModelIds) > 0)
              {
                // We have seen instances where we get stuck in a loop incessantly
                //  trying to clone earlier models (NUP-1511). My best guess is that
                //  we've already successfully cloned each of the orphaned models at
                //  least once, but still need at least one more. If we don't create
                //  a new particleID, we will never be able to instantiate another
                //  model (since particleID hash is a unique key in the models table).
                //  So, on 1/8/2013 this logic was changed to create a new particleID
                //  whenever we clone an orphan.
                newParticleId = True;
                this.logger.info("Cloning an orphaned model");
              }

              // If there is no orphan, clone one of the other particles. We can
              //  have no orphan if this was a speculative generation that only
              //  continued particles completed in the prior generation.
              else
              {
                newParticleId = True;
                this.logger.info("No orphans found, so cloning a non-orphan");
                (allParticles, allModelIds, errScores, completed, matured) = \
                this._resultsDB.getParticleInfos(swarmId=swarmId,
                                                 genIdx=firstNonFullGenIdx);
              }

              // Clone that model
              modelId = random.choice(allModelIds);
              this.logger.info("Cloning model %r" % (modelId));
              (particleState, _, _, _, _) = this._resultsDB.getParticleInfo(modelId);
              particle = Particle(hsObj = self,
                                  resultsDB = this._resultsDB,
                                  flattenedPermuteVars=this._flattenedPermutations,
                                  newFromClone=particleState,
                                  newParticleId=newParticleId);
              return (False, particle, swarmId);
            }
          }


          // ====================================================================
          // Sort the swarms in priority order, trying the ones with the least
          //  number of models first
          swarmSizes = numpy.array([this._resultsDB.numModels(x) for x in swarmIds]);
          swarmSizeAndIdList = zip(swarmSizes, swarmIds);
          swarmSizeAndIdList.sort();
          for (_, swarmId) in swarmSizeAndIdList
          {

            // -------------------------------------------------------------------
            // 1.) The particle will be created from new (at generation #0) if there
            //   are not already this._minParticlesPerSwarm particles in the swarm.
            (allParticles, allModelIds, errScores, completed, matured) = (
                this._resultsDB.getParticleInfos(swarmId));
            if( len(allParticles) < this._minParticlesPerSwarm)
            {
              particle = Particle(hsObj=self,
                                  resultsDB=this._resultsDB,
                                  flattenedPermuteVars=this._flattenedPermutations,
                                  swarmId=swarmId,
                                  newFarFrom=allParticles);

              // Jam in the best encoder state found from the first sprint
              bestPriorModel = None;
              if( sprintIdx >= 1)
              {
                (bestPriorModel, errScore) = this._hsState.bestModelInSprint(0);
              }

              if( bestPriorModel is not None)
              {
                this.logger.info("Best model and errScore from previous sprint(%d):"
                                  " %s, %g" % (0, str(bestPriorModel), errScore));
                (baseState, modelId, errScore, completed, matured) \
                     = this._resultsDB.getParticleInfo(bestPriorModel);
                particle.copyEncoderStatesFrom(baseState);

                // Copy the best inference type from the earlier sprint
                particle.copyVarStatesFrom(baseState, ["modelParams|inferenceType"]);

                // It's best to jiggle the best settings from the prior sprint, so
                //  compute a new position starting from that previous best
                // Only jiggle the vars we copied from the prior model
                whichVars = [];
                for( varName in baseState["varStates"])
                {
                  if( ':' in varName)
                  {
                    whichVars.append(varName);
                  }
                }
                particle.newPosition(whichVars);

                this.logger.debug("Particle after incorporating encoder vars from best "
                                 "model in previous sprint: \n%s" % (str(particle)));
              }

              return (False, particle, swarmId);
            }

            // -------------------------------------------------------------------
            // 2.) Look for a completed particle to evolve
            // Note that we use lastDescendent. We only want to evolve particles that
            // are at their most recent generation index.
            (readyParticles, readyModelIds, readyErrScores, _, _) = (
                this._resultsDB.getParticleInfos(swarmId, genIdx=None,
                                                 matured=True, lastDescendent=True));

            // If we have at least 1 ready particle to evolve...
            if( len(readyParticles) > 0)
            {
              readyGenIdxs = [x["genIdx"] for x in readyParticles];
              sortedGenIdxs = sorted(set(readyGenIdxs));
              genIdx = sortedGenIdxs[0];

              // Now, genIdx has the generation of the particle we want to run,
              // Get a particle from that generation and evolve it.
              useParticle = None;
              for( particle in readyParticles)
              {
                if( particle["genIdx"] == genIdx)
                {
                  useParticle = particle;
                  break;
                }
              }

              // If speculativeParticles is off, we don't want to evolve a particle
              // into the next generation until all particles in the current
              // generation have completed.
              if( not this._speculativeParticles)
              {
                (particles, _, _, _, _) = this._resultsDB.getParticleInfos(
                    swarmId, genIdx=genIdx, matured=False);
                if( len(particles) > 0)
                {
                  continue;
                }
              }

              particle = Particle(hsObj=self,
                                  resultsDB=this._resultsDB,
                                  flattenedPermuteVars=this._flattenedPermutations,
                                  evolveFromState=useParticle);
              return (False, particle, swarmId);
            }

            // END: for (swarmSize, swarmId) in swarmSizeAndIdList:
            // No success in this swarm, onto next swarm
          }

          // ====================================================================
          // We couldn't find a particle in this sprint ready to evolve. If
          //  speculative particles is OFF, we have to wait for one or more other
          //  workers to finish up their particles before we can do anything.
          if( not this._speculativeParticles)
          {
            this.logger.info("Waiting for one or more of the %s swarms "
                "to complete a generation before evolving any more particles" \
                % (str(swarmIds)));
            return (False, None, None);
          }

          // END: while True:
          // No success in this sprint, into next sprint
        }
      }

      def _okToExit(self)
      {
        """Test if it's OK to exit this worker. This is only called when we run
        out of prospective new models to evaluate. This method sees if all models
        have matured yet. If not, it will sleep for a bit and return False. This
        will indicate to the hypersearch worker that we should keep running, and
        check again later. This gives this worker a chance to pick up and adopt any
        model which may become orphaned by another worker before it matures.

        If all models have matured, this method will send a STOP message to all
        matured, running models (presummably, there will be just one - the model
        which thinks it's the best) before returning True.
        """;
        // Send an update status periodically to the JobTracker so that it doesn't
        // think this worker is dead.
        print >> sys.stderr, "reporter:status:In hypersearchV2: _okToExit";

        // Any immature models still running?
        if( not this._jobCancelled)
        {
          (_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(matured=False);
          if( len(modelIds) > 0)
          {
            this.logger.info("Ready to end hyperseach, but not all models have " \
                             "matured yet. Sleeping a bit to wait for all models " \
                             "to mature.");
            // Sleep for a bit, no need to check for orphaned models very often
            time.sleep(5.0 * random.random());
            return False;
          }
        }

        // All particles have matured, send a STOP signal to any that are still
        // running.
        (_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(completed=False);
        for( modelId in modelIds)
        {
          this.logger.info("Stopping model %d because the search has ended" \
                              % (modelId));
          this._cjDAO.modelSetFields(modelId,
                          dict(engStop=ClientJobsDAO.STOP_REASON_STOPPED),
                          ignoreUnchanged = True);
        }

        // Update the HsState to get the accurate field contributions.
        this._hsStatePeriodicUpdate();
        pctFieldContributions, absFieldContributions = \
                                              this._hsState.getFieldContributions();


        // Update the results field with the new field contributions.
        jobResultsStr = this._cjDAO.jobGetFields(this._jobID, ["results"])[0];
        if( jobResultsStr is not None)
        {
          jobResults = json.loads(jobResultsStr);
        }
        else
        {
          jobResults = {};
        }

        // Update the fieldContributions field.
        if( pctFieldContributions != jobResults.get('fieldContributions', None))
        {
          jobResults["fieldContributions"] = pctFieldContributions;
          jobResults["absoluteFieldContributions"] = absFieldContributions;

          isUpdated = this._cjDAO.jobSetFieldIfEqual(this._jobID,
                                                       fieldName='results',
                                                       curValue=jobResultsStr,
                                                       newValue=json.dumps(jobResults));
          if( isUpdated)
          {
            this.logger.info('Successfully updated the field contributions:%s',
                                                                  pctFieldContributions);
          }
          else
          {
            this.logger.info('Failed updating the field contributions, ' \
                             'another hypersearch worker must have updated it');
          }
        }

        return True;
      }


      def killSwarmParticles(self, swarmID)
      {
        (_, modelIds, _, _, _) = this._resultsDB.getParticleInfos(
            swarmId=swarmID, completed=False);
        for( modelId in modelIds)
        {
          this.logger.info("Killing the following models in swarm '%s' because"
                           "the swarm is being terminated: %s" % (swarmID,
                                                                  str(modelIds)));
          this._cjDAO.modelSetFields(
              modelId, dict(engStop=ClientJobsDAO.STOP_REASON_KILLED),
              ignoreUnchanged=True);
        }
      }

      def createModels(self, numModels=1)
      {
       ///  Create one or more new models for evaluation. These should NOT be models
       ///  that we already know are in progress (i.e. those that have been sent to us
       ///  via recordModelProgress). We return a list of models to the caller
       ///  (HypersearchWorker) and if one can be successfully inserted into
       ///  the models table (i.e. it is not a duplicate) then HypersearchWorker will
       ///  turn around and call our runModel() method, passing in this model. If it
       ///  is a duplicate, HypersearchWorker will call this method again. A model
       ///  is a duplicate if either the  modelParamsHash or particleHash is
       ///  identical to another entry in the model table.
       /// 
       ///  The numModels is provided by HypersearchWorker as a suggestion as to how
       ///  many models to generate. This particular implementation only ever returns 1
       ///  model.
       /// 
       ///  Before choosing some new models, we first do a sweep for any models that
       ///  may have been abandonded by failed workers. If/when we detect an abandoned
       ///  model, we mark it as complete and orphaned and hide it from any subsequent
       ///  queries to our ResultsDB. This effectively considers it as if it never
       ///  existed. We also change the paramsHash and particleHash in the model record
       ///  of the models table so that we can create another model with the same
       ///  params and particle status and run it (which we then do immediately).
       /// 
       ///  The modelParamsHash returned for each model should be a hash (max allowed
       ///  size of ClientJobsDAO.hashMaxSize) that uniquely identifies this model by
       ///  it's params and the optional particleHash should be a hash of the particleId
       ///  and generation index. Every model that gets placed into the models database,
       ///  either by this worker or another worker, will have these hashes computed for
       ///  it. The recordModelProgress gets called for every model in the database and
       ///  the hash is used to tell which, if any, are the same as the ones this worker
       ///  generated.
       /// 
       ///  NOTE: We check first ourselves for possible duplicates using the paramsHash
       ///  before we return a model. If HypersearchWorker failed to insert it (because
       ///  some other worker beat us to it), it will turn around and call our
       ///  recordModelProgress with that other model so that we now know about it. It
       ///  will then call createModels() again.
       /// 
       ///  This methods returns an exit boolean and the model to evaluate. If there is
       ///  no model to evalulate, we may return False for exit because we want to stay
       ///  alive for a while, waiting for all other models to finish. This gives us
       ///  a chance to detect and pick up any possibly orphaned model by another
       ///  worker.
       /// 
       ///  Parameters:
       ///  ----------------------------------------------------------------------
       ///  numModels:   number of models to generate
       ///  retval:      (exit, models)
       ///                  exit: true if this worker should exit.
       ///                  models: list of tuples, one for each model. Each tuple contains:
       ///                    (modelParams, modelParamsHash, particleHash)
       /// 
       ///               modelParams is a dictionary containing the following elements:
       /// 
       ///                 structuredParams: dictionary containing all variables for
       ///                   this model, with encoders represented as a dict within
       ///                   this dict (or None if they are not included.
       /// 
       ///                 particleState: dictionary containing the state of this
       ///                   particle. This includes the position and velocity of
       ///                   each of it's variables, the particleId, and the particle
       ///                   generation index. It contains the following keys:
       /// 
       ///                   id: The particle Id of the particle we are using to
       ///                         generate/track this model. This is a string of the
       ///                         form <hypesearchWorkerId>.<particleIdx>
       ///                   genIdx: the particle's generation index. This starts at 0
       ///                         and increments every time we move the particle to a
       ///                         new position.
       ///                   swarmId: The swarmId, which is a string of the form
       ///                     <encoder>.<encoder>... that describes this swarm
       ///                   varStates: dict of the variable states. The key is the
       ///                       variable name, the value is a dict of the variable's
       ///                       position, velocity, bestPosition, bestResult, etc.
        """;

        // Check for and mark orphaned models
        this._checkForOrphanedModels();

        modelResults = [];
        for( _ in xrange(numModels))
        {
          candidateParticle = None;

          // If we've reached the max // of model to evaluate, we're done.
          if (this._maxModels is not None and
              (this._resultsDB.numModels() - this._resultsDB.getNumErrModels()) >=
              this._maxModels)
          {

            return (this._okToExit(), []);
          }

          // If we don't already have a particle to work on, get a candidate swarm and
          // particle to work with. If None is returned for the particle it means
          // either that the search is over (if exitNow is also True) or that we need
          // to wait for other workers to finish up their models before we can pick
          // another particle to run (if exitNow is False).
          if( candidateParticle is None)
          {
            (exitNow, candidateParticle, candidateSwarm) = (
                this._getCandidateParticleAndSwarm());
          }
          if( candidateParticle is None)
          {
            if( exitNow)
            {
              return (this._okToExit(), []);
            }
            else
            {
              // Send an update status periodically to the JobTracker so that it doesn't
              // think this worker is dead.
              print >> sys.stderr, "reporter:status:In hypersearchV2: speculativeWait";
              time.sleep(this._speculativeWaitSecondsMax * random.random());
              return (False, []);
            }
          }
          useEncoders = candidateSwarm.split('.');
          numAttempts = 0;

          // Loop until we can create a unique model that we haven't seen yet.
          while( True)
          {

            // If this is the Nth attempt with the same candidate, agitate it a bit
            // to find a new unique position for it.
            if( numAttempts >= 1)
            {
              this.logger.debug("Agitating particle to get unique position after %d "
                      "failed attempts in a row" % (numAttempts));
              candidateParticle.agitate();
            }

            // Create the hierarchical params expected by the base description. Note
            // that this is where we incorporate encoders that have no permuted
            // values in them.
            position = candidateParticle.getPosition();
            structuredParams = dict();
            def _buildStructuredParams(value, keys)
            {
              flatKey = _flattenKeys(keys);
              // If it's an encoder, either put in None if it's not used, or replace
              // all permuted constructor params with the actual position.
              if( flatKey in this._encoderNames)
              {
                if( flatKey in useEncoders)
                {
                  // Form encoder dict, substituting in chosen permutation values.
                  return value.getDict(flatKey, position);
                }
                // Encoder not used.
                else
                {
                  return None;
                }
              }
              // Regular top-level variable.
              else if( flatKey in position)
              {
                return position[flatKey];
              }
              // Fixed override of a parameter in the base description.
              else
              {
                return value;
              }
            }

            structuredParams = rCopy(this._permutations,
                                               _buildStructuredParams,
                                               discardNoneKeys=False);

            // Create the modelParams.
            modelParams = dict(
                       structuredParams=structuredParams,
                       particleState = candidateParticle.getState()
                       );

            // And the hashes.
            m = hashlib.md5();
            m.update(sortedJSONDumpS(structuredParams));
            m.update(this._baseDescriptionHash);
            paramsHash = m.digest();

            particleInst = "%s.%s" % (modelParams["particleState"]["id"],
                                      modelParams["particleState"]["genIdx"]);
            particleHash = hashlib.md5(particleInst).digest();

            // Increase attempt counter
            numAttempts += 1;

            // If this is a new one, and passes the filter test, exit with it.
            // TODO: There is currently a problem with this filters implementation as
            // it relates to this._maxUniqueModelAttempts. When there is a filter in
            // effect, we should try a lot more times before we decide we have
            // exhausted the parameter space for this swarm. The question is, how many
            // more times?
            if( this._filterFunc and not this._filterFunc(structuredParams))
            {
              valid = False;
            }
            else
            {
              valid = True;
            }
            if( valid and this._resultsDB.getModelIDFromParamsHash(paramsHash) is None)
            {
              break;
            }

            // If we've exceeded the max allowed number of attempts, mark this swarm
            //  as completing or completed, so we don't try and allocate any more new
            //  particles to it, and pick another.
            if( numAttempts >= this._maxUniqueModelAttempts)
            {
              (exitNow, candidateParticle, candidateSwarm) \
                    = this._getCandidateParticleAndSwarm(
                                                  exhaustedSwarmId=candidateSwarm);
              if( candidateParticle is None)
              {
                if( exitNow)
                {
                  return (this._okToExit(), []);
                }
                else
                {
                  time.sleep(this._speculativeWaitSecondsMax * random.random());
                  return (False, []);
                }
              }
              numAttempts = 0;
              useEncoders = candidateSwarm.split('.');
            }
          }

          // Log message
          if( this.logger.getEffectiveLevel() <= logging.DEBUG)
          {
            this.logger.debug("Submitting new potential model to HypersearchWorker: \n%s"
                           % (pprint.pformat(modelParams, indent=4)));
          }
          modelResults.append((modelParams, paramsHash, particleHash));
        }
        return (False, modelResults);
      }

      def recordModelProgress(self, modelID, modelParams, modelParamsHash, results,
                             completed, completionReason, matured, numRecords)
      {

        if( results is None)
        {
          metricResult = None;
        }
        else
        {
          metricResult = results[1].values()[0];
        }

        // Update our database.
        errScore = this._resultsDB.update(modelID=modelID,
                    modelParams=modelParams,modelParamsHash=modelParamsHash,
                    metricResult=metricResult, completed=completed,
                    completionReason=completionReason, matured=matured,
                    numRecords=numRecords);

        // Log message.
        this.logger.debug('Received progress on model %d: completed: %s, '
                          'cmpReason: %s, numRecords: %d, errScore: %s' ,
                          modelID, completed, completionReason, numRecords, errScore);

        // Log best so far.
        (bestModelID, bestResult) = this._resultsDB.bestModelIdAndErrScore();
        this.logger.debug('Best err score seen so far: %s on model %s' % \
                         (bestResult, bestModelID));
      }

*/
}