using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Research.Swarming
{
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
    /// is a string formed as '&lt;workerConnectionId&gt;.&lt;particleIdx&gt;', where particleIdx
    /// starts at 0 for each worker and increments by 1 every time a new particle
    /// is created by that worker.
    /// </summary>
    public class Particle
    {
        private static int _nextParticleId;
        private readonly HypersearchV2 _hsObj;
        private readonly ILog _logger;
        private readonly ResultsDB _resultsDb;
        private readonly IRandom _rng;

        private Dictionary<string, PermuteVariable> _permuteVars;
        private readonly int _genIdx;
        private readonly string _swarmId;
        private readonly string _particleId;

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
        /// <param name="resultsDb">the ResultsDB instance that holds all the model results</param>
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
        public Particle(HypersearchV2 hsObj, ResultsDB resultsDb, Dictionary<string, PermuteVariable> flattenedPermuteVars,
            string swarmId = null, List<ParticleStateModel> newFarFrom = null, ParticleStateModel evolveFromState = null,
            ParticleStateModel newFromClone = null, bool newParticleId = false)
        {
            // Save constructor arguments
            _hsObj = hsObj;
            _logger = hsObj.logger;
            _resultsDb = resultsDb;

            // See the random number generator used for all the variables in this
            // particle. We will seed it differently based on the construction method,
            // below.
            _rng = new XorshiftRandom(42);
            //this._rng.seed(42);

            Action<Dictionary<string, PermuteVariable>> setupVars = flattenedPermVars =>
            {
                var allowedEncoderNames = (swarmId ?? "").Split('.');
                _permuteVars = new Dictionary<string, PermuteVariable>(flattenedPermVars); // copy.deepcopy(flattenedPermuteVars);

                // Remove fields we don't want.
                var varNames = flattenedPermVars.Keys;
                foreach (string varName in varNames)
                {
                    // Remove encoders we're not using
                    if (varName.Contains(":"))    // if an encoder
                    {
                        if (!allowedEncoderNames.Contains(varName.Split(':')[0]))
                        {
                            _permuteVars.Remove(varName);
                            continue;
                        }
                    }

                    // All PermuteChoice variables need to know all prior results obtained
                    // with each choice.
                    if (_permuteVars[varName] is PermuteChoices)
                    {
                        int? maxGenIdx;
                        if (_hsObj._speculativeParticles)
                        {
                            maxGenIdx = null;
                        }
                        else
                        {
                            maxGenIdx = _genIdx - 1;
                        }

                        var resultsPerChoice = _resultsDb.getResultsPerChoice(
                            swarmId: swarmId, maxGenIdx: maxGenIdx, varName: varName);
                        ((PermuteChoices)_permuteVars[varName]).SetResultsPerChoice(resultsPerChoice.Values.ToList());
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
                _swarmId = swarmId;

                // Assign a new unique ID to this particle
                _particleId = $"{_hsObj._workerID}.{_nextParticleId}";
                _nextParticleId += 1;

                // Init the generation index
                _genIdx = 0;

                // Setup the variables to initial locations.
                setupVars(flattenedPermuteVars);

                // Push away from other particles?
                if (newFarFrom != null)
                {
                    foreach (var varName in _permuteVars.Keys)
                    {
                        var otherPositions = new List<double>();
                        foreach (var particleState in newFarFrom)
                        {
                            otherPositions.Add(particleState.varStates[varName].position.GetValueOrDefault());
                        }
                        _permuteVars[varName].PushAwayFrom(otherPositions, _rng);

                        // Give this particle a unique seed.
                        _rng = new MersenneTwister(otherPositions.GetHashCode());
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
                _particleId = evolveFromState.id;
                _genIdx = evolveFromState.genIdx + 1;
                _swarmId = evolveFromState.swarmId;

                // Setup the variables to initial locations.
                setupVars(flattenedPermuteVars);

                // Override the position and velocity of each variable from
                //  saved state
                InitStateFrom(_particleId, evolveFromState, newBest: true);

                // Move it to the next position. We need the swarm best for this.
                NewPosition();
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
                _particleId = newFromClone.id;
                if (newParticleId)
                {
                    _particleId = $"{_hsObj._workerID}.{_nextParticleId}";
                    _nextParticleId += 1;
                }

                _genIdx = newFromClone.genIdx;
                _swarmId = newFromClone.swarmId;

                // Setup the variables to initial locations.
                setupVars(flattenedPermuteVars);

                // Override the position and velocity of each variable from
                //  the clone
                InitStateFrom(_particleId, newFromClone, newBest: false);
            }

            #endregion

            else
            {
                Debug.Assert(false, "invalid creation parameters");
            }

            // Log it
            _logger.Debug($"Created particle: {this}");
        }

        /// <summary>
        /// Get the particle state as a dict. This is enough information to instantiate this particle on another worker.
        /// </summary>
        /// <returns></returns>
        public ParticleStateModel GetState()
        {
            Dictionary<string, VarState> varStates = new Dictionary<string, VarState>();
            foreach (KeyValuePair<string, PermuteVariable> permuteVar in _permuteVars)
            {
                varStates[permuteVar.Key] = permuteVar.Value.GetState();
            }
            //for (varName, var in this.permuteVars.iteritems())
            //{
            //    varStates[varName] = var.getState();
            //}

            return new ParticleStateModel
            {
                id = _particleId,
                genIdx = _genIdx,
                swarmId = _swarmId,
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
        private void InitStateFrom(string particleId, ParticleStateModel particleState, bool newBest)
        {
            // Get the update best position and result?
            double? bestResult;
            Dictionary<string, double> bestPosition;
            if (newBest)
            {
                var tuplePositions = _resultsDb.getParticleBest(particleId);
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
                _permuteVars[varName].SetState(varState);
            }
        }
        /// <summary>
        /// Copy all encoder variables from particleState into this particle.
        /// </summary>
        /// <param name="particleState">dict produced by a particle's getState() method</param>
        public void CopyEncoderStatesFrom(ParticleStateModel particleState)
        {
            // Set this to false if you don't want the variable to move anymore
            //  after we set the state
            bool allowedToMove = true;

            foreach (var varName in particleState.varStates.Keys)
            {
                if (varName.Contains(":"))    // if an encoder
                {

                    // If this particle doesn't include this field, don't copy it
                    if (!_permuteVars.ContainsKey(varName))
                    {
                        continue;
                    }

                    // Set the best position to the copied position
                    VarState state = particleState.varStates[varName].Clone();
                    state._position = state.position.GetValueOrDefault();
                    state.bestPosition = state.position;

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (!allowedToMove)
                    {
                        state.velocity = 0;
                    }

                    // Set the state now
                    _permuteVars[varName].SetState(state);

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (allowedToMove)
                    {
                        // Let the particle move in both directions from the best position
                        //  it found previously and set it's initial velocity to a known
                        //  fraction of the total distance.
                        _permuteVars[varName].ResetVelocity(_rng);
                    }
                }
            }
        }

        /// <summary>
        /// Copy specific variables from particleState into this particle.
        /// </summary>
        /// <param name="particleState">dict produced by a particle's getState() method</param>
        /// <param name="varNames">which variables to copy</param>
        public void CopyVarStatesFrom(ParticleStateModel particleState, List<string> varNames)
        {
            // Set this to false if you don't want the variable to move anymore
            //  after we set the state
            bool allowedToMove = true;

            foreach (var varName in particleState.varStates.Keys)
            {
                if (varNames.Contains(varName))
                {
                    // If this particle doesn't include this field, don't copy it
                    if (!_permuteVars.ContainsKey(varName))
                    {
                        continue;
                    }

                    // Set the best position to the copied position
                    var state = particleState.varStates[varName].Clone();
                    state._position = state.position.GetValueOrDefault();
                    state.bestPosition = state.position;

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (!allowedToMove)
                    {
                        state.velocity = 0;
                    }

                    // Set the state now
                    _permuteVars[varName].SetState(state);

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (allowedToMove)
                    {
                        // Let the particle move in both directions from the best position
                        //  it found previously and set it's initial velocity to a known
                        //  fraction of the total distance.
                        _permuteVars[varName].ResetVelocity(_rng);
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
        public Map<string, object> GetPosition()
        {
            Map<string, object> result = new Map<string, object>();
            foreach (var pair in _permuteVars)
            //for (varName, value) in this.permuteVars.iteritems()
            {
                result[pair.Key] = pair.Value.GetPosition();
            }

            return result;
        }
        /// <summary>
        /// Agitate this particle so that it is likely to go to a new position.
        /// Every time agitate is called, the particle is jiggled an even greater
        /// amount.
        /// </summary>
        public void Agitate()
        {
            foreach (var pair in _permuteVars)
            //for (varName, var) in this.permuteVars.iteritems()
            {
                pair.Value.Agitate();
            }

            NewPosition();
        }
        /// <summary>
        /// Choose a new position based on results obtained so far from all other particles.
        /// </summary>
        /// <param name="whichVars">If not None, only move these variables</param>
        /// <returns>new position</returns>
        public Dictionary<string, object> NewPosition(List<string> whichVars = null)
        {
            // TODO: incorporate data from choice variables....
            // TODO: make sure we're calling this when appropriate.

            // Get the global best position for this swarm generation
            Dictionary<string, double> globalBestPosition = null;
            // If speculative particles are enabled, use the global best considering
            //  even particles in the current generation. This gives better results
            //  but does not provide repeatable results because it depends on
            //  worker timing
            int genIdx;
            if (_hsObj._speculativeParticles)
            {
                genIdx = _genIdx;
            }
            else
            {
                genIdx = _genIdx - 1;
            }

            if (genIdx >= 0)
            {
                //(bestModelId, _) = this._resultsDB.bestModelIdAndErrScore(this.swarmId, genIdx);
                var tuple = _resultsDb.bestModelIdAndErrScore(_swarmId, genIdx);
                var bestModelId = tuple.Item1;
                if (bestModelId != null)
                {
                    var particleInfo = _resultsDb.getParticleInfo(bestModelId.Value);
                    globalBestPosition = GetPositionFromState(particleInfo.particleState);
                }
            }

            // Update each variable
            foreach (var pair in _permuteVars)
            {
                string varName = pair.Key;
                PermuteVariable var = pair.Value;
                if (whichVars != null && !whichVars.Contains(varName))
                {
                    continue;
                }
                if (globalBestPosition == null)
                {
                    var.NewPosition(null, _rng);
                }
                else
                {
                    var.NewPosition(globalBestPosition[varName], _rng);
                }
            }

            // get the new position
            Dictionary<string, object> position = GetPosition();

            // Log the new position
            if (_logger.IsDebugEnabled)
            {
                var msg = new StringBuilder();
                //msg = StringIO.StringIO();
                msg.AppendFormat("New particle position: {0}", Arrays.ToString(position));
                //print >> msg, "New particle position: \n%s" % (pprint.pformat(position,
                //                                                indent = 4));
                msg.Append("Particle variables:");
                //print >> msg, "Particle variables:";
                foreach (var pair in _permuteVars)
                {
                    msg.AppendFormat("  {0}: {1}", pair.Key, pair.Value);
                }
                //for (varName, var) in this.permuteVars.iteritems()
                //{
                //    print >> msg, "  %s: %s" % (varName, str(var));
                //}
                _logger.Debug(msg.ToString());
                //msg.close();
            }

            return position;
        }
        /// <summary>
        /// Return the position of a particle given its state.
        /// </summary>
        /// <param name="pState"></param>
        /// <returns>dict() of particle position, keys are the variable names, values are their positions</returns>
        public static Map<string, double> GetPositionFromState(ParticleStateModel pState)
        {
            Map<string, double> result = new Map<string, double>();
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
            return $"Particle(swarmId={_swarmId}) " +
                   $"[particleId={_particleId}, genIdx={_genIdx}d, permuteVars=\n{Arrays.ToString(_permuteVars)}]";
        }

        #endregion
    }
}