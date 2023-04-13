using System;

namespace HTM.Net.Swarming.HyperSearch.Variables;

[Serializable]
public class RangeVariable : PermuteVariable
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Step { get; set; }

    public double Value { get; set; }
    private bool MoveUp { get; set; }

    public RangeVariable(double min, double max, double step)
    {
        Min = min;
        Max = max;
        Step = step;

        Value = (max - min) / 2;
        MoveUp = true;
    }

    public double GetValue()
    {
        double value = Value;

        if (Value + Step >= Max)
        {
            // we would reach the end
            MoveUp = false;
        }
        if (Value - Step <= Min)
        {
            // we would reach the end
            MoveUp = true;
        }
        if (MoveUp) Value += Step;
        else Value -= Step;

        if (Value > Max) Value = Max;
        if (Value < Min) Value = Min;

        return value;
    }

    public bool AtEnd()
    {
        return Value == Max;
    }

    public override void SetState(VarState varState)
    {
        throw new NotSupportedException();
    }

    public override VarState GetState()
    {
        throw new NotSupportedException();
    }
}