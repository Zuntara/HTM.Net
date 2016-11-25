using System;

namespace HTM.Net.Model
{
    public interface IPersistable
    {
        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called prior to this object being serialized. Any
        /// preparation required for serialization should be done
        /// in this method.
        /// </summary>
        object PreSerialize();
        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called following deserialization to execute logic required
        /// to "fix up" any inconsistencies within the object being
        /// reified.
        /// </summary>
        object PostDeSerialize();
        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called to implement a full or partial copy of an object 
        /// upon de-serialization.
        /// </summary>
        /// <param name="t">the instance of type &lt;T&gt;</param>
        /// <returns>a post serialized custom form of T</returns>
        object PostDeSerialize(object t);
    }

    [Serializable]
    public abstract class Persistable : IPersistable
    {
        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called prior to this object being serialized. Any
        /// preparation required for serialization should be done
        /// in this method.
        /// </summary>
        public virtual object PreSerialize()
        {
            return this;
        }

        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called following deserialization to execute logic required
        /// to "fix up" any inconsistencies within the object being
        /// reified.
        /// </summary>
        public virtual object PostDeSerialize()
        {
            return PostDeSerialize(this);
        }

        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called to implement a full or partial copy of an object 
        /// upon de-serialization.
        /// </summary>
        /// <param name="t">the instance of type &lt;T&gt;</param>
        /// <returns>a post serialized custom form of T</returns>
        public virtual object PostDeSerialize(object t)
        {
            return t;
        }
    }
}