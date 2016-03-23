namespace HTM.Net.Util
{
    public interface ICondition<T>
    {
        bool eval(int n);
        bool eval(double d);
        bool eval(T t);
    }
}