using System;

namespace HTM.Net.Research.Tests.Examples.Random
{
    [Flags]
    public enum PickThreeHit
    {
        None = 0,
        CorrectOrder = 1,
        CorrectNumbers = 2,
        CorrectNumbersWithDoubles = 4,
        CorrectFirstTwo = 8,
        CorrectLastTwo = 16
    }
}