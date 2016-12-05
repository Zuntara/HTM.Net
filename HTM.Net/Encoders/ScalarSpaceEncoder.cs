using System;

namespace HTM.Net.Encoders
{
    [Serializable]
    public class ScalarSpaceEncoder : AdaptiveScalarEncoder
    {
        public static Builder GetScalarSpaceBuilder()
        {
            return new Builder();
        }

        public new class Builder : BuilderBase
        {
            internal Builder()
            {
                space = "absolute";    // default
            }

            public override IEncoder Build()
            {
                if (space == "absolute")
                {
                    encoder = new AdaptiveScalarEncoder();
                    base.Build();
                    ((AdaptiveScalarEncoder)encoder).Init();
                    return (AdaptiveScalarEncoder)encoder;
                }
                // delta
                encoder = new DeltaEncoder();
                base.Build();
                ((DeltaEncoder)encoder).Init();
                return (DeltaEncoder)encoder;
            }
        }
    }
}