namespace HTM.Net.Swarming.HyperSearch
{
    public class VarState
    {
        public double? position;
        public object _position;
        public double? velocity;
        public double? bestPosition;
        public double? bestResult;

        public VarState Clone()
        {
            return new VarState
            {
                position = this.position,
                _position = this._position,
                velocity = this.velocity,
                bestPosition = this.bestPosition,
                bestResult = this.bestResult
            };
        }
    }
}