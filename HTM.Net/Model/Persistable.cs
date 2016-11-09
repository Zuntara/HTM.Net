using System;

namespace HTM.Net.Model
{
    public interface IPersistable
    {
        object PreSerialize();
        object PostDeSerialize();
        object PostDeSerialize(object t);
    }

    [Serializable]
    public abstract class Persistable : IPersistable
    {
        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called prior to this object being serialized. Any
         * preparation required for serialization should be done
         * in this method.
         */
        public virtual object PreSerialize()
        {
            return this;
        }

        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called following deserialization to execute logic required
         * to "fix up" any inconsistencies within the object being
         * reified.
         */
        public virtual object PostDeSerialize()
        {
            return PostDeSerialize(this);
        }

        /**
         * <em>FOR INTERNAL USE ONLY</em><p>
         * Called to implement a full or partial copy of an object 
         * upon de-serialization.
         * 
         * @param t     the instance of type &lt;T&gt;
         * @return  a post serialized custom form of T
         */

        public virtual object PostDeSerialize(object t)
        {
            return t;
        }
    }
}