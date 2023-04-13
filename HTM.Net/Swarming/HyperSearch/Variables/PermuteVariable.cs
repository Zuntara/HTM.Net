using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Swarming.HyperSearch.Variables;

// TODO:  PermutationTemporalPoolerParams  >  let float, double, int be assignable to a permute-variable, lock the variable to the type then.
// now we have to put object on the props instead of a type.


/// <summary>
/// "The base class of all PermuteXXX classes that can be used from within a permutation script.
/// </summary>
[Serializable]
public abstract class PermuteVariable
{
    /// <summary>
    /// Choose a new position that is as far away as possible from all
    /// 'otherVars', where 'otherVars' is a list of PermuteVariable instances.
    /// </summary>
    /// <param name="otherVars">list of other PermuteVariables to push away from</param>
    /// <param name="rng">instance of random.Random() used for generating random numbers</param>
    public virtual void PushAwayFrom(List<object> otherVars, IRandom rng)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Return the current state of this particle. This is used for
    /// communicating our state into a model record entry so that it can be
    /// instantiated on another worker.
    /// </summary>
    /// <returns></returns>
    public abstract VarState GetState();

    /// <summary>
    /// Set the current state of this particle. This is counterpart to <see cref="GetState"/>.
    /// </summary>
    /// <param name="varState"></param>
    public abstract void SetState(VarState varState);

    /// <summary>
    /// Reset the velocity to be some fraction of the total distance. This
    /// is called usually when we start a new swarm and want to start at the
    /// previous best position found in the previous swarm but with a
    /// velocity which is a known fraction of the total distance between min
    /// and max.
    /// </summary>
    /// <param name="rng">instance of random.Random() used for generating random numbers</param>
    public virtual void ResetVelocity(IRandom rng)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// for int vars, returns position to nearest int
    /// </summary>
    /// <returns>current position</returns>
    public virtual object GetPosition()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// This causes the variable to jiggle away from its current position.
    /// It does this by increasing its velocity by a multiplicative factor.
    /// Every time agitate() is called, the velocity will increase.In this way,
    /// you can call agitate over and over again until the variable reaches a
    /// new position.
    /// </summary>
    public virtual void Agitate()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Choose a new position based on results obtained so far from other
    /// particles and the passed in globalBestPosition.
    /// </summary>
    /// <param name="globalBestPosition">global best position for this colony</param>
    /// <param name="rng">instance of random.Random() used for generating random numbers</param>
    public virtual object NewPosition(object globalBestPosition, IRandom rng)
    {
        throw new NotImplementedException();
    }
}