using System;
using System.Reactive.Linq;

namespace HTM.Net.Network
{
    public abstract class Transformer<TSource, TTarget>
    {
        public virtual IObservable<TTarget> TransformFiltered(IObservable<object> source, Func<object, bool> predicate)
        {
            return source.Where(predicate).Cast<TSource>().Select(DoMapping).AsObservable();
        }

        protected abstract TTarget DoMapping(TSource source);
    }
}