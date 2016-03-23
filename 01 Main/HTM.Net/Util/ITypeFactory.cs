using System;

namespace HTM.Net.Util
{
    public interface ITypeFactory<T>
    {
        T Make(params int[] args);
        Type TypeClass(); // Class<T>
    }
}