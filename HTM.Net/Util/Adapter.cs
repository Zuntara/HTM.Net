namespace HTM.Net.Util
{
    public class Adapter<T> : ICondition<T>
    {
        public bool eval(int n) { return false; }
        public bool eval(double d) { return false; }
        public bool eval(T t) { return false; }
    }
}