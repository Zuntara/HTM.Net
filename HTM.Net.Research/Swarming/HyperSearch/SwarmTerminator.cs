using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Research.Swarming.HyperSearch
{
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
}