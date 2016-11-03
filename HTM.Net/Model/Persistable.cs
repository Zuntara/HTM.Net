using System;

namespace HTM.Net.Model
{
    public interface IPersistable<T>
            where T : IPersistable<T>
    {
        T PreSerialize();
        T PostDeSerialize();
        T PostDeSerialize(T t);
    }

    [Serializable]
    public abstract class Persistable<T> : IPersistable<T>
            where T : Persistable<T>
    {
        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called prior to this object being serialized. Any
         * preparation required for serialization should be done
         * in this method.
         */
        public virtual T PreSerialize()
        {
            return (T)this;
        }

        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called following deserialization to execute logic required
         * to "fix up" any inconsistencies within the object being
         * reified.
         */
        public virtual T PostDeSerialize()
        {
            return PostDeSerialize((T)this);
        }

        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called to implement a full or partial copy of an object 
         * upon de-serialization.
         * 
         * @param t     the instance of type &lt;T&gt;
         * @return  a post serialized custom form of T
         */

        public virtual T PostDeSerialize(T t)
        {
            return t;
        }
    }
}