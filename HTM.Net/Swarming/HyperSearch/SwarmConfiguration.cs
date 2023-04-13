namespace HTM.Net.Swarming.HyperSearch;

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
    /// <summary>
    /// The max # of attempts we will make to create a unique model before
    /// giving up and assuming all the possible positions in a swarm have been
    /// evaluated.
    /// </summary>
    public static int maxUniqueModelAttempts = 10;
    /// <summary>
    /// The max amount of time (in seconds) allowed before a model is
    /// considered orphaned.At this point, it is available for being taken over
    /// by another worker.
    /// </summary>
    public static int modelOrphanIntervalSecs = 180;
    /// <summary>
    /// If this percent of the models in a hyperseach completed with an error, abort the search.
    /// </summary>
    public static double maxPctErrModels = 0.20;
    public static int minParticlesPerSwarm = 5;
    /// <summary>
    /// Weight given to the previous velocity of a particle in PSO.
    /// </summary>
    public static double inertia = 0.25;
    /// <summary>
    /// This parameter controls how much the particle is affected by its distance from it's local best position.
    /// </summary>
    public static double cogRate = 0.25;
    /// <summary>
    /// This parameter controls how much the particle is affected by its distance from the global best position.
    /// </summary>
    public static double socRate = 1.0;
    /// <summary>
    /// Lower bound for sampling a random number in the PSO.
    /// </summary>
    public static double randomLowerBound = 0.8;
    /// <summary>
    /// Upper bound for sampling a random number in the PSO.
    /// </summary>
    public static double randomUpperBound = 1.2;
    public static int? bestModelMinRecords = 1000;
    public static double? maturityPctChange = 0.005;
    public static int? maturityNumPoints = 10;
    public static double? opf_metricWindow = 1000;
}