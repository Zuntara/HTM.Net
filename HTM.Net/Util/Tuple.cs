using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Model;
using Newtonsoft.Json;

namespace HTM.Net.Util
{
    [Serializable]
    public class Tuple : Persistable
    {
        /** The internal container array */
        [JsonProperty]
        private object[] container;

        private int _hashcode;

        [JsonConstructor]
        internal Tuple()
        {

        }

        /**
         * Instantiates a new {@code Tuple}
         * @param objects
         */
        public Tuple(params object[] objects)
        {
            container = new object[objects.Length];
            for (int i = 0; i < container.Length; i++)
            {
                container[i] = objects[i];
            }
            _hashcode = GetHashCode();
        }

        public Tuple(IEnumerable<object> objects)
        {
            if (objects != null)
            {
                var list = objects.ToList();
                container = new object[list.Count];
                int i = 0;
                foreach (object o in list)
                {
                    container[i++] = o;
                }
                //container = new object[objects.Count()];
                //for (int i = 0; i < container.Length; i++)
                //{
                //    container[i] = objects[i];
                //}
            }
            _hashcode = GetHashCode();
        }

        public Tuple(Array objects)
        {
            container = new object[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                container[i] = objects.GetValue(i);
            }
            _hashcode = GetHashCode();
        }
        /**
         * Returns the object previously inserted into the
         * specified index.
         * 
         * @param index    the index representing the insertion order.
         * @return
         */
        public virtual object Get(int index)
        {
            if (container.Length > index)
                return container[index];
            return null;
        }

        public void Set(int index, object value)
        {
            if (container.Length > index)
                container[index] = value;
        }

        /**
         * Returns the number of items in this {@code Tuple}
         * 
         * @return
         */
        [JsonIgnore]
        public virtual int Count
        {
            get { return container.Length; }
        }

        [JsonIgnore]
        public object Item1 { get { return Get(0); } set { Set(0, value); } }
        [JsonIgnore]
        public object Item2 { get { return Get(1); } set { Set(1, value); } }
        [JsonIgnore]
        public object Item3 { get { return Get(2); } set { Set(2, value); } }
        [JsonIgnore]
        public object Item4 { get { return Get(3); } set { Set(3, value); } }

        /// <summary>
        /// Returns an <em>unmodifiable</em> view of the underlying data.
        /// </summary>
        public List<object> All()
        {
            return container.ToList();
        }

        /**
         * {@inheritDoc}
         */

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < container.Length; i++)
            {
                try
                {
                    //new Double((double)container[i]);
                    double number;
                    if (double.TryParse(container[i].ToString(), out number))
                    {
                        sb.Append(container[i]);
                    }
                }
                catch (Exception) { sb.Append("'").Append(container[i]).Append("'"); }
                sb.Append(":");
            }
            sb.Length = (sb.Length - 1);

            return sb.ToString();
        }

        /**
         * {@inheritDoc}
         */
        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + container.GetArrayHashCode();

            return result;
        }

        /**
         * {@inheritDoc}
         */

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Tuple other = (Tuple)obj;
            if (this._hashcode != other._hashcode)
                return false;
            return true;
        }

        /**
	     * Returns builder for building immutable {@code Tuple}s
	     * @return
	     */
        public static Builder GetBuilder()
        {
            return new Builder();
        }

        /**
	     * Allows the creation of an immutable {@link Tuple}
	     * using a "Fluent" style construction.
	     */
        public class Builder
        {
            List<object> accumulator = new List<object>();
            public Builder Add(Object o)
            {
                accumulator.Add(o);
                return this;
            }

            /**
             * Creates and returns the {@link Tuple}
             * @return
             */
            public Tuple Build()
            {
                return new Tuple(accumulator);
            }

            /**
             * So that this builder can be used as a custom {@link Collector}
             * @param b
             */
            public void AddAll(Builder b)
            {
                accumulator.AddRange(b.accumulator);
            }
        }
    }

    public class BitsTuple : Tuple
    {
        public BitsTuple(int bitsToUse, double radius)
            : base(bitsToUse, radius)
        {

        }

        public int BitsToUse => (int)Get(0);
        public double Radius => (double)Get(1);
    }
}