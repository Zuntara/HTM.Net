using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;
using Newtonsoft.Json;

namespace HTM.Net.Swarming.HyperSearch.Variables;

/// <summary>
/// Define a permutation variable which can take on floating point values.
/// </summary>
[Serializable]
public class PermuteFloat : PermuteVariable
{
    [JsonProperty]
    public double Min { get; private set; }
    [JsonProperty]
    public double Max { get; private set; }
    [JsonProperty]
    public double? StepSize { get; private set; }
    [JsonProperty]
    public double Position { get; private set; }
    [JsonProperty]
    public double? Velocity { get; private set; }
    [JsonProperty]
    public double Inertia { get; private set; }
    [JsonProperty]
    public double CogRate { get; private set; }
    [JsonProperty]
    public double SocRate { get; private set; }
    [JsonProperty]
    public double? BestPosition { get; private set; }
    [JsonProperty]
    public double? BestResult { get; private set; }

    [Obsolete("Don' use")]
    [JsonConstructor]
    public PermuteFloat()
    {
        // For deserialization
    }

    /// <summary>
    /// Construct a variable that permutes over floating point values using
    /// the Particle Swarm Optimization(PSO) algorithm.See descriptions of
    /// PSO(i.e.http://en.wikipedia.org/wiki/Particle_swarm_optimization)
    /// for references to the inertia, cogRate, and socRate parameters.
    /// </summary>
    /// <param name="min">min allowed value of position</param>
    /// <param name="max">max allowed value of position</param>
    /// <param name="stepSize">if not None, the position must be at min + N * stepSize, where N is an integer</param>
    /// <param name="inertia">The inertia for the particle.</param>
    /// <param name="cogRate">This parameter controls how much the particle is affected by its distance from it's local best position</param>
    /// <param name="socRate">This parameter controls how much the particle is affected by its distance from it's global best position</param>
    public PermuteFloat(double min, double max, double? stepSize = null, double? inertia = null, double? cogRate = null,
        double? socRate = null)
    {
        Min = min;
        Max = max;
        StepSize = stepSize;

        // The particle's initial position and velocity.
        Position = (Max + Min) / 2.0;
        Velocity = (Max - Min) / 5.0;


        // The inertia, cognitive, and social components of the particle
        Inertia = inertia ?? SwarmConfiguration.inertia;
        CogRate = cogRate ?? SwarmConfiguration.cogRate;
        SocRate = socRate ?? SwarmConfiguration.socRate;

        // The particle's local best position and the best global position.
        BestPosition = (double)GetPosition();
        BestResult = null;
    }

    #region Overrides of PermuteVariable

    public override VarState GetState()
    {
        return new VarState
        {
            _position = Position,
            position = (double)GetPosition(),
            velocity = Velocity,
            bestPosition = BestPosition,
            bestResult = BestResult
        };
    }

    public override void SetState(VarState varState)
    {
        Position = (double)varState._position;
        Velocity = varState.velocity;
        BestPosition = (double?)varState.bestPosition;
        BestResult = varState.bestResult;
    }

    public override object GetPosition()
    {
        if (!StepSize.HasValue)
        {
            return Position;
        }

        // Find nearest step
        double numSteps = (Position - Min) / StepSize.Value;
        numSteps = (int)Math.Round(numSteps);
        double position = Min + numSteps * StepSize.Value;
        position = Math.Max(Min, position);
        position = Math.Min(Max, position);
        return position;
    }

    public override void Agitate()
    {
        // Increase velocity enough that it will be higher the next time
        // NewPosition() is called. We know that newPosition multiplies by inertia,
        // so take that into account.
        Velocity *= 1.5 / Inertia;

        // Clip velocity
        double maxV = (Max - Min) / 2.0;
        if (Velocity > maxV)
        {
            Velocity = maxV;
        }
        else if (Velocity < -maxV)
        {
            Velocity = -maxV;
        }

        // if we at the max or min, reverse direction
        if (Math.Abs(Position - Max) < double.Epsilon && Velocity > 0)
        {
            Velocity *= -1;
        }
        if (Math.Abs(Position - Min) < double.Epsilon && Velocity < 0)
        {
            Velocity *= -1;
        }
    }

    public override object NewPosition(object globalBestPosition, IRandom rng)
    {
        // First, update the velocity. The new velocity is given as:
        // v = (inertia * v)  + (cogRate * r1 * (localBest-pos))
        //                    + (socRate * r2 * (globalBest-pos))
        //
        // where r1 and r2 are random numbers between 0 and 1.0
        double lb = SwarmConfiguration.randomLowerBound;
        double ub = SwarmConfiguration.randomUpperBound;

        Velocity = Velocity * Inertia + rng.NextDouble(lb, ub) *
            CogRate * (BestPosition - (double)GetPosition());
        if (globalBestPosition != null)
        {
            Velocity += rng.NextDouble(lb, ub) * SocRate * (
                (double)globalBestPosition - (double)GetPosition());
        }

        // update position based on velocity
        Position += Velocity.GetValueOrDefault();

        // Clip it
        Position = Math.Max(Min, Position);
        Position = Math.Min(Max, Position);

        // Return it
        return GetPosition();
    }

    public override void PushAwayFrom(List<object> otherPositions, IRandom rng)
    {
        // If min and max are the same, nothing to do
        if (Math.Abs(Max - Min) < double.Epsilon)
        {
            return;
        }

        // How many potential other positions to evaluate?
        int numPositions = otherPositions.Count * 4;
        if (numPositions == 0)
        {
            return;
        }

        // Assign a weight to each potential position based on how close it is
        // to other particles.
        StepSize = (Max - Min) / numPositions;
        double[] positions = ArrayUtils.Arrange(Min, Max + StepSize.Value, StepSize.Value);

        // Get rid of duplicates.
        numPositions = positions.Length;
        double[] weights = new double[numPositions];

        // Assign a weight to each potential position, based on a gaussian falloff
        // from each existing variable. The weight of a variable to each potential
        // position is given as:
        //    e ^ -(dist^2/stepSize^2)
        double maxDistanceSq = -1 * Math.Pow(StepSize.Value, 2);
        foreach (double pos in otherPositions)
        {
            var distances = ArrayUtils.Sub(pos, positions);// pos - positions;

            var varWeights = ArrayUtils.Exp(ArrayUtils.Divide(ArrayUtils.Power(distances, 2), maxDistanceSq)).ToArray();
            // varWeights = numpy.exp(numpy.power(distances, 2) / maxDistanceSq)

            weights = ArrayUtils.Add(weights, varWeights);
        }


        // Put this particle at the position with smallest weight.
        int positionIdx = ArrayUtils.Argmin(weights);
        Position = positions[positionIdx];

        // Set its best position to this.
        BestPosition = (double)GetPosition();

        // Give it a random direction.
        Velocity *= rng.Choice(new[] { 1, -1 });
    }

    public override void ResetVelocity(IRandom rng)
    {
        double maxVelocity = (Max - Min) / 5.0;
        Velocity = maxVelocity; //#min(abs(this._velocity), maxVelocity)
        Velocity *= rng.Choice(new[] { 1, -1 });
    }

    #endregion

    #region Overrides of Object

    public override string ToString()
    {
        return $"PermuteFloat(min={Min}, max={Max}, stepSize={StepSize}) [position={GetPosition()}({Position}), " +
               $"velocity={Velocity}, _bestPosition={BestPosition}, _bestResult={BestResult}]";
    }

    #endregion
}