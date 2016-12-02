using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.HyperSearch;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
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
        private Map<string, Tuple<double?, Map<string, double>>> _particleBest;
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
            this._particleBest = new Map<string, Tuple<double?, Map<string, double>>>();

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
                Tuple<double?, Map<string, double>> oldResultPos = this._particleBest.Get(particleId, new Tuple<double?, Map<string, double>>(double.PositiveInfinity, null));// TODO check!
                if (errScore < oldResultPos.Item1 /*oldResult*/)
                {
                    Map<string, double> pos1 = Particle.GetPositionFromState(modelParams.particleState);
                    this._particleBest[particleId] = new Tuple<double?, Map<string, double>>(errScore, pos1);
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
            var firstNonFull = ArrayUtils.Where(numPsPerGen, x => x < minNumParticles).ToArray();
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
        public Tuple<double?, Map<string, double>> getParticleBest(string particleId)
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
        public Map<int, Tuple<int, List<double>>> getResultsPerChoice(string swarmId, int? maxGenIdx, string varName)
        {
            var results = new Map<int, Tuple<int, List<double>>>();
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
                if (double.IsPositiveInfinity(resultErr))
                {
                    continue;
                }

                var position = Particle.GetPositionFromState(particleState);
                double varPosition = position[varName];
                //var varPositionStr = varPosition.ToString();

                if (results.ContainsKey((int)varPosition))
                {
                    results[(int)varPosition].Item2.Add(resultErr);
                    //results[varPositionStr][1].Add(resultErr);
                }
                else
                {
                    //results[varPositionStr] = (varPosition, [resultErr]);
                    results[(int)varPosition] = new Tuple<int, List<double>>((int)varPosition, new List<double> { resultErr });
                }
            }

            return results;
        }

    }

    public class SwarmEncoderState
    {
        public SwarmStatus status { get; set; }
        public ulong? bestModelId { get; set; }
        public double? bestErrScore { get; set; }
        public int sprintIdx { get; set; }
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
        /// <summary>
        /// REQUIRED.
        /// Persistent, globally-unique identifier for this job for use in constructing persistent model checkpoint
        /// keys.MUST be compatible with S3 key-naming rules, but MUST NOT contain forward slashes.This GUID is
        /// expected to retain its global uniqueness across clusters and cluster software updates(unlike the
        /// record IDs in the Engine's jobs table, which recycle upon table schema change and software update). In the
        /// future, this may also be instrumental for checkpoint garbage collection.
        /// </summary>
        public string persistentJobGUID { get; set; }
        /// <summary>
        /// path to permutations.py file
        /// </summary>
        public string permutationsPyFilename { get; set; }
        /// <summary>
        /// JSON encoded string with contents of permutations.py file
        /// </summary>
        public ExperimentPermutationParameters permutationsPyContents { get; set; }
        /// <summary>
        /// JSON encoded string with contents of base description.py file
        /// </summary>
        public ExperimentParameters descriptionPyContents { get; set; }
        /// <summary>
        /// JSON description of the search
        /// </summary>
        public string description { get; set; }
        /// <summary>
        /// OPTIONAL - Whether to create checkpoints
        /// </summary>
        public bool? createCheckpoints { get; set; }
        /// <summary>
        /// OPTIONAL - True of False (default config.xml). 
        /// When set to False, the model and swarm terminators are disabled.
        /// </summary>
        public bool? useTerminators { get; set; }
        /// <summary>
        /// OPTIONAL - max # of models to generate
        /// NOTE: This is a deprecated location for this
        /// setting.Now, it should be specified through the maxModels variable within the permutations
        /// file, or maxModels in the JSON description
        /// </summary>
        public int? maxModels { get; set; }
        /// <summary>
        /// OPTIONAL - Either (True/False) or a dict of parameters
        /// for a dummy model.If this key is absent, a real model is trained.
        /// See utils.py/OPFDummyModel runner for the schema of the dummy parameters
        /// </summary>
        public object dummyModel { get; set; }
        /// <summary>
        /// OPTIONAL - True or False (default obtained from
        /// nupic.hypersearch.speculative.particles.default
        /// configuration property). See note below.
        /// </summary>
        public bool? speculativeParticles { get; set; }
        public int? predictionCacheMaxRecords;

        public string hsVersion { get; set; } = "v2";

        public void Populate(Map<string, object> jobParamsMap)
        {
            persistentJobGUID = (string)jobParamsMap["persistentJobGUID"];
            descriptionPyContents = (ExperimentParameters)jobParamsMap["descriptionPyContents"];
            permutationsPyContents = (ExperimentPermutationParameters)jobParamsMap["permutationsPyContents"];
            maxModels = TypeConverter.Convert<int?>(jobParamsMap.Get("maxModels"));
            hsVersion = (string)jobParamsMap["hsVersion"];
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
        private ExperimentParameters _baseDescription;
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
        private Func<ExperimentPermutationParameters, bool> _filterFunc;
        private Func<ExperimentPermutationParameters, bool, DummyModelParameters> _dummyModelParamsFunc;
        private ExperimentPermutationParameters _fastSwarmModelParams;
        private ExperimentPermutationParameters _permutations;

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
                    //var searchParamObj = this._searchParams;
                    //JObject anomalyParams = (JObject)searchParamObj.description["anomalyParams"];//?? new Dictionary<string, string>();
                    //anomalyParams = searchParamObj.description.get("anomalyParams", dict());

                    // This is used in case searchParamObj["description"]["anomalyParams"]
                    // is set to None.
                    //if (anomalyParams == null)
                    //{
                    //    anomalyParams = new JObject();
                    //}

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
                    // string outDir = this._tempDir = @"C:\temp\" + Path.GetRandomFileName();//tempfile.mkdtemp();
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
                //ConfigModelDescription modelDescription = _baseDescription.modelConfig;

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
                this._readPermutationsFile(permutationsScript, _baseDescription);

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
                if (this._dummyModelParamsFunc != null)
                {
                    if (this._dummyModel == null)
                    {
                        this._dummyModel = new DummyModelParameters();
                    }
                }

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
        private void _readPermutationsFile(string permFileJson, ExperimentParameters modelDescription)
        {
            // Open and execute the permutations file
            //Dictionary<string, object> vars = new Dictionary<string, object>();

            //permFile = execfile(filename, globals(), vars);
            ExperimentPermutationParameters permFile = Json.Deserialize<ExperimentPermutationParameters>(permFileJson);

            // Read in misc info.
            this._reportKeys = permFile.Report; // vars.Get("report", []);
            this._filterFunc = permFile.PermutationFilter; //vars.Get("permutationFilter", null);
            this._dummyModelParamsFunc = permFile.DummyModelParams(permFile, true) != null ? permFile.DummyModelParams : (Func<ExperimentPermutationParameters, bool, DummyModelParameters>)null;// vars.Get("dummyModelParams", null);
            this._predictedField = null;   // default
            this._predictedFieldEncoder = null;   // default
            this._fixedFields = null; // default

            // The fastSwarm variable, if present, contains the params from a best
            //  model from a previous swarm. If present, use info from that to seed
            //  a fast swarm
            this._fastSwarmModelParams = permFile.FastSwarmModelParams; // vars.Get("fastSwarmModelParams", null);
            if (this._fastSwarmModelParams != null)
            {
                Map<string, object> encoders = this._fastSwarmModelParams.Encoders;
                this._fixedFields = new List<string>();
                foreach (var fieldName in encoders.Keys)
                {
                    if (encoders[fieldName] != null)
                    {
                        this._fixedFields.Add(fieldName);
                    }
                }
            }

            if (permFile.FixedFields != null)
            {
                this._fixedFields = permFile.FixedFields.ToList();
            }

            // Get min number of particles per swarm from either permutations file or
            // config.
            this._minParticlesPerSwarm = (int?)permFile.MinParticlesPerSwarm;
            if (this._minParticlesPerSwarm == null)
            {
                this._minParticlesPerSwarm = SwarmConfiguration.minParticlesPerSwarm;
            }
            //this._minParticlesPerSwarm = int(this._minParticlesPerSwarm);

            // Enable logic to kill off speculative swarms when an earlier sprint
            //  has found that it contains poorly performing field combination?
            this._killUselessSwarms = (bool)(permFile.KillUselessSwarms ?? true);// vars.Get("killUselessSwarms", true);

            // The caller can request that the predicted field ALWAYS be included ("yes")
            //  or optionally include ("auto"). The setting of "no" is N/A and ignored
            //  because in that case the encoder for the predicted field will not even
            //  be present in the permutations file.
            // When set to "yes", this will force the first sprint to try the predicted
            //  field only (the legacy mode of swarming).
            // When set to "auto", the first sprint tries all possible fields (one at a
            //  time) in the first sprint.
            this._inputPredictedField = permFile.InputPredictedField ?? InputPredictedField.Yes; //vars.Get("inputPredictedField", "yes");

            // Try all possible 3-field combinations? Normally, we start with the best
            //  2-field combination as a base. When this flag is set though, we try
            //  all possible 3-field combinations which takes longer but can find a
            //  better model.
            this._tryAll3FieldCombinations = (bool)(permFile.TryAll3FieldCombinations ?? false);//vars.Get("tryAll3FieldCombinations", false);

            // Always include timestamp fields in the 3-field swarms?
            // This is a less compute intensive version of tryAll3FieldCombinations.
            // Instead of trying ALL possible 3 field combinations, it just insures
            // that the timestamp fields (dayOfWeek, timeOfDay, weekend) are never left
            // out when generating the 3-field swarms.
            this._tryAll3FieldCombinationsWTimestamps = (bool)(permFile.TryAll3FieldCombinationsWTimestamps ?? false);//vars.Get("tryAll3FieldCombinationsWTimestamps", false);

            // Allow the permutations file to override minFieldContribution. This would
            //  be set to a negative number for large swarms so that you don't disqualify
            //  a field in an early sprint just because it did poorly there. Sometimes,
            //  a field that did poorly in an early sprint could help accuracy when
            //  added in a later sprint
            int? minFieldContribution = (int?)(permFile.MinFieldContribution ?? null);//vars.Get("minFieldContribution", null);
            if (minFieldContribution != null)
            {
                this._minFieldContribution = minFieldContribution.GetValueOrDefault();
            }

            // Allow the permutations file to override maxBranching.
            var maxBranching = permFile.MaxFieldBranching ?? null;//vars.Get("maxFieldBranching", null);
            if (maxBranching != null)
            {
                this._maxBranching = maxBranching.GetValueOrDefault();
            }

            // Read in the optimization info.
            if (permFile.Maximize != null)
            {
                this._optimizeKey = permFile.Maximize;
                this._maximize = true;
            }
            else if (permFile.Minimize != null)
            {
                this._optimizeKey = permFile.Minimize;
                this._maximize = false;
            }
            else
            {
                throw new InvalidOperationException($"Permutations file '{permFile}' does not include a maximize or minimize metric.");
            }

            // The permutations file is the new location for maxModels. The old location,
            //  in the jobParams is deprecated.
            int? maxModels = (int?)permFile.MaxModels;
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
            string sIinferenceType = modelDescription.InferenceType.ToString();
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

                foreach (var encoder in modelDescription.GetEncoderSettings().Values)
                {
                    if ((bool)encoder.classifierOnly.GetValueOrDefault() && encoder.fieldName == permFile.PredictedField)
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
            this._predictedField = permFile.PredictedField;
            if (this._predictedField == null)
            {
                throw new InvalidOperationException(string.Format(
                    "Permutations file '{0}' does not have the required 'predictedField' variable", permFile.GetType().Name));
            }

            // Read in and validate the permutations dict
            if (!permFile.HasPermutations())
            {
                throw new InvalidOperationException(string.Format(
                    "Permutations file '{0}' does not define permutations", permFile.GetType().Name));
            }

            //if (!(vars["permutations"] is IDictionary))
            //{
            //    throw new InvalidOperationException(string.Format(
            //        "Permutations file '{0}' defines a permutations variable but it is not a dict", typeof(IPermutionFilter).Name));
            //}

            this._encoderNames = new List<string>();
            this._permutations = permFile; //vars["permutations"];
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
                this.logger.Info($"Active swarms changed to {Arrays.ToString(activeSwarms)} (from {Arrays.ToString(priorActiveSwarms)})");
            }
            this.logger.Debug($"Active swarms: {Arrays.ToString(activeSwarms)}");

            // If too many model errors were detected, exit
            int totalCmpModels = this._resultsDB.getNumCompletedModels();
            if (totalCmpModels > 5)
            {
                int numErrs = this._resultsDB.getNumErrModels();
                if ((double)numErrs / totalCmpModels > this._maxPctErrModels)
                {
                    // Get one of the errors
                    List<ulong> errModelIds = this._resultsDB.getErrModelIds();
                    ResultAndStatusModel resInfo = this._cjDAO.modelsGetResultAndStatus(new[] { errModelIds[0] })[0];
                    string modelErrMsg = resInfo.completionMsg;
                    string cmpMsg = string.Format("{0}: Exiting due to receiving too many models failing" +
                                                  " from exceptions ({1} out of {2}). \nModel Exception: {3}",
                              "tooManyModelErrs", numErrs, totalCmpModels, modelErrMsg);
                    this.logger.Error(cmpMsg);

                    // Cancel the entire job now, if it has not already been cancelled
                    var workerCmpReason = this._cjDAO.jobGetFields(this._jobID, new[] { "workerCompletionReason" })[0] as string;
                    if (workerCmpReason == BaseClientJobDao.CMPL_REASON_SUCCESS)
                    {
                        this._cjDAO.jobSetFields(
                            this._jobID,
                            fields: new Dictionary<string, object>
                            {
                              {"cancel", true},
                              {"workerCompletionReason", BaseClientJobDao.CMPL_REASON_ERROR},
                              {"workerCompletionMsg", cmpMsg}
                            },
                            useConnectionID: false,
                            ignoreUnchanged: true);
                    }
                    return new CanidateParticleAndSwarm(true, null, null);
                }
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
                                            resultsDb: this._resultsDB,
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
                                            resultsDb: this._resultsDB,
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
                            particle.CopyEncoderStatesFrom(baseState);

                            // Copy the best inference type from the earlier sprint
                            particle.CopyVarStatesFrom(baseState, new List<string> { "modelParams|inferenceType" });

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
                            particle.NewPosition(whichVars);

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
                                            resultsDb: this._resultsDB,
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
                        candidateParticle.Agitate();
                    }

                    // Create the hierarchical params expected by the base description. Note
                    // that this is where we incorporate encoders that have no permuted
                    // values in them.
                    var position = candidateParticle.GetPosition();
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

                    var structuredParams = (ExperimentPermutationParameters)Utils.rCopy(this._permutations,
                        _buildStructuredParams,
                        discardNoneKeys: false);

                    // Create the modelParams.
                    modelParams = new ModelParams
                    {
                        structuredParams = structuredParams,
                        particleState = candidateParticle.GetState()
                    };

                    // And the hashes.
                    MD5 m = MD5.Create();
                    //m.update(sortedJSONDumpS(structuredParams));
                    //m.update(this._baseDescriptionHash);
                    //paramsHash = m.digest();
                    //paramsHash = GetMd5Hash(m, Json.Serialize(structuredParams) + Json.Serialize(_baseDescriptionHash));
                    paramsHash = GetMd5Hash(m, (structuredParams.GetHashCode().ToString() + _baseDescription.GetHashCode()).ToString());
                    string particleInst = $"{modelParams.particleState.id}.{modelParams.particleState.genIdx}";
                    particleHash = GetMd5Hash(m, particleInst);// hashlib.md5(particleInst).digest();

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
            ExperimentPermutationParameters structuredParams = modelParams.structuredParams;

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
                                modelCheckpointGuid: modelCheckpointGUID,
                                predictionCacheMaxRecords: this._predictionCacheMaxRecords);
                    cmpReason = pair.completionReason;
                    cmpMsg = pair.completionMsg;
                }
                else
                {
                    var dummyParams = (DummyModelParameters)_dummyModel;
                    dummyParams.permutationParams = structuredParams;
                    if (_dummyModelParamsFunc != null)
                    {
                        var permInfo = structuredParams.Copy();
                        permInfo.Generation = modelParams.particleState.genIdx;
                        dummyParams = _dummyModelParamsFunc(permInfo, false);
                        dummyParams.permutationParams = structuredParams;
                    }

                    var pair = Utils.runDummyModel(
                                modelID: modelID,
                                jobID: jobID,
                                @params: dummyParams,
                                predictedField: this._predictedField,
                                reportKeys: this._reportKeys,
                                optimizeKey: this._optimizeKey,
                                jobsDAO: jobsDAO,
                                modelCheckpointGuid: modelCheckpointGUID,
                                predictionCacheMaxRecords: this._predictionCacheMaxRecords);
                    cmpReason = pair.completionReason;
                    cmpMsg = pair.completionMsg;
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
}