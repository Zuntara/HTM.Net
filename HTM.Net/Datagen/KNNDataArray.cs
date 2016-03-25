namespace HTM.Net.Datagen
{
    /// <summary>
    /// Container holding a pairing of data and its class
    /// </summary>
    public class KNNDataArray
    {
        private readonly double[][] _data;
        private readonly int[] _class;

        public KNNDataArray(double[][] data, int[] @class)
        {
            _data = data;
            _class = @class;
        }

        public double[][] GetDataArray()
        {
            return _data;
        }

        public int[] GetClassArray()
        {
            return _class;
        }
    }
}