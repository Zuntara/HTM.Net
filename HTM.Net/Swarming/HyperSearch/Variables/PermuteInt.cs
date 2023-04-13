using System;
using System.Globalization;

namespace HTM.Net.Swarming.HyperSearch.Variables;

/// <summary>
/// Define a permutation variable which can take on integer values.
/// </summary>
[Serializable]
public class PermuteInt : PermuteFloat
{
    [Obsolete("Don' use")]
    public PermuteInt()
    {

    }

    public PermuteInt(int min, int max, int? stepSize = 1, double? inertia = null, double? cogRate = null,
        double? socRate = null)
        : base(min, max, stepSize, inertia, cogRate, socRate)
    {

    }

    #region Overrides of PermuteFloat

    public override object GetPosition()
    {
        double position = (double)base.GetPosition();
        position = (int)Math.Round(position);
        return position;
    }

    #endregion

    #region Overrides of Object

    public override string ToString()
    {
        return
            $"PermuteInt(min={Min.ToString("0.00", CultureInfo.InvariantCulture)}, max={Max.ToString("0.00", CultureInfo.InvariantCulture)}, stepSize={StepSize?.ToString("0.00", CultureInfo.InvariantCulture)}) [position={GetPosition()}({Position.ToString("0.00", CultureInfo.InvariantCulture)}), " +
            $"velocity={Velocity?.ToString("0.00", CultureInfo.InvariantCulture)}, _bestPosition={BestPosition?.ToString("0.00", CultureInfo.InvariantCulture)}, _bestResult={BestResult?.ToString("0.00", CultureInfo.InvariantCulture)}]";
    }

    #endregion
}