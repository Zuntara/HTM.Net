using System;

namespace HTM.Net.Encoders;

/// <summary>
/// An encoder that can be used to permute the encodings through different spaces
/// These include absolute value, delta, log space, etc.
/// </summary>
[Serializable]
public class ScalarSpaceEncoder : ScalarEncoder
{
    public enum SpaceEnum
    {
        None,
        Absolute,
        Delta
    }
    
    public static IEncoder Create(
        int w, double minVal, double maxVal, bool periodic = false, int n = 0, double radius = 0, 
        double resolution = 0, string name = null, bool clipInput = false,
        SpaceEnum space = SpaceEnum.Absolute, bool forced = false)
    {
        if (space == SpaceEnum.Absolute)
        {
            return AdaptiveScalarEncoder.GetAdaptiveBuilder()
                .W(w)
                .MinVal(minVal)
                .MaxVal(maxVal)
                .Periodic(periodic)
                .N(n)
                .Radius(radius)
                .Resolution(resolution)
                .Name(name)
                .ClipInput(clipInput)
                .Forced(forced)
                .Build();
        }

        if (space == SpaceEnum.Delta)
        {
            return DeltaEncoder.GetDeltaBuilder()
                .W(w)
                .MinVal(minVal)
                .MaxVal(maxVal)
                .Periodic(periodic)
                .N(n)
                .Radius(radius)
                .Resolution(resolution)
                .Name(name)
                .ClipInput(clipInput)
                .Forced(forced)
                .Build();
        }

        throw new ArgumentException("Unknown space: " + space);
    }

    /**
         * Returns a builder for building AdaptiveScalarEncoder. This builder may be
         * reused to produce multiple builders
         *
         * @return a {@code AdaptiveScalarEncoder.Builder}
         */
    public static ScalarSpaceEncoder.Builder GetSpaceBuilder()
    {
        return new Builder();
    }

    /**
     * Constructs a new {@link Builder} suitable for constructing
     * {@code AdaptiveScalarEncoder}s.
     */
    public new class Builder : BuilderBase
    {
        protected SpaceEnum space;

        internal Builder() { }

        public virtual IBuilder Space(SpaceEnum space)
        {
            this.space = space;
            return this;
        }

        public override IEncoder Build()
        {
            encoder = Create(
                w, minVal, maxVal, periodic, n, radius, resolution, name, clipInput, space, forced);

            base.Build();

            ((ScalarEncoder)encoder).Init();
            return encoder;
        }
    }
}