namespace HTM.Net.Swarming.HyperSearch
{
    public class VarState
    {
        public object position;
        public object _position;
        public double? velocity;
        public object bestPosition;
        public double? bestResult;

        public VarState Clone()
        {
            return new VarState
            {
                position = position,
                _position = _position,
                velocity = velocity,
                bestPosition = bestPosition,
                bestResult = bestResult
            };
        }
    }
}