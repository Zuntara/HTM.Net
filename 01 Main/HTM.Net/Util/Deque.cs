using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace HTM.Net.Util
{
    /**
 * Double ended queue implementation which has a restricted capacity.
 * Operations may be conducted on both ends and when capacity is reached,
 * the next addition to either end will result in a removal on the opposite
 * end, thus always maintaining a size &lt;= initial size.
 * 
 * This behavior differs from the {@link LinkedBlockingDeque} implementation
 * of the Java Collections Framework, and is the reason for the development of this
 * "alternative" - by allowing constant mutation of this list without an exception
 * being thrown and forcing the client to handle capacity management logic. 
 * 
 * @author David Ray
 *
 * @param <E>
 */
    public class Deque<E> : IEnumerator<E>
    {
        /** Backing array list */
        private List<E> backingList = new List<E>();
        /** Originating size of this {@code Deque} */
        private int capacity;
        /** The internal size monitor */
        private int currentSize;

        /**
         * Constructs a new {@code Deque} with the specified capacity.
         * @param capacity
         */
        public Deque(int capacity)
        {
            this.capacity = capacity;
        }

        /**
         * Appends the specified item to the end of this {@code Deque}
         * 
         * @param t		the object of type &lt;T&gt; to add
         * @return		flag indicating whether capacity had been reached 
         * 				<em><b>prior</b></em> to this call.
         */
        public bool Append(E t)
        {
            bool ret = currentSize == capacity;
            if (ret)
            {
                backingList.RemoveAt(0);
                backingList.Add(t);
            }
            else {
                backingList.Add(t);
                currentSize++;
            }
            return ret;
        }

        /**
         * Inserts the specified item at the head of this {@code Deque}
         * 
         * @param t		the object of type &lt;T&gt; to add
         * @return		flag indicating whether capacity had been reached 
         * 				<em><b>prior</b></em> to this call.
         */
        public bool Insert(E t)
        {
            bool ret = currentSize == capacity;
            if (ret)
            {
                backingList.RemoveAt(backingList.Count - 1);
                backingList.Insert(0,t);
            }
            else {
                backingList.Insert(0, t);
                currentSize++;
            }
            return ret;
        }

        /**
         * Appends the specified item to the end of this {@code Deque},
         * and if this deque was at capacity prior to this call, the object
         * residing at the head of this queue is returned, otherwise null
         * is returned
         * 
         * @param t		the object of type &lt;T&gt; to add
         * @return		the object residing at the head of this queue is 
         * 				returned if previously at capacity, otherwise null 
         * 				is returned
         */
        public E PushLast(E t)
        {
            E retVal = default(E);
            bool ret = currentSize == capacity;
            if (ret)
            {
                retVal = backingList[0];
                backingList.RemoveAt(0);
                backingList.Add(t);
            }
            else {
                backingList.Add(t);
                currentSize++;
            }
            return retVal;
        }

        /**
         * Inserts the specified item at the head of this {@code Deque},
         * and if this deque was at capacity prior to this call, the object
         * residing at the tail of this queue is returned, otherwise null
         * is returned
         * 
         * @param t		the object of type &lt;T&gt; to add
         * @return		the object residing at the tail of this queue is 
         * 				returned if previously at capacity, otherwise null 
         * 				is returned
         */
        public E PushFirst(E t)
        {
            E retVal = default(E);
            bool ret = currentSize == capacity;
            if (ret)
            {
                retVal = backingList[backingList.Count - 1];
                backingList.RemoveAt(backingList.Count - 1);
                backingList.Insert(0, t);
            }
            else
            {
                backingList.Insert(0, t);
                currentSize++;
            }
            return retVal;
        }

        /**
         * Clears this {@code Deque} of all contents
         */
        public void Clear()
        {
            backingList.Clear();
            currentSize = 0;
        }

        /**
         * Returns the item at the head of this {@code Deque} or null
         * if it is empty. This call does not block if empty.
         * 
         * @return	item at the head of this {@code Deque} or null
         * 			if it is empty.
         */
        public E TakeFirst()
        {
            if (currentSize == 0) return default(E);

            E val = default(E);
            try
            {
                val = backingList[0];
                backingList.RemoveAt(0);
                currentSize--;
            }
            catch (Exception e) { Console.WriteLine(e); }

            return val;
        }

        /**
         * Returns the item at the tail of this {@code Deque} or null
         * if it is empty. This call does not block if empty.
         * 
         * @return	item at the tail of this {@code Deque} or null
         * 			if it is empty.
         */
        public E TakeLast()
        {
            if (currentSize == 0) return default(E);

            E val = default(E);
            try
            {
                val = backingList[backingList.Count - 1];
                backingList.RemoveAt(backingList.Count-1);
                currentSize--;
            }
            catch (Exception e) { Console.WriteLine(e); }

            return val;
        }

        /**
         * Returns the item at the head of this {@code Deque}, blocking
         * until an item is available.
         * 
         * @return	item at the tail of this {@code Deque}
         */
        public E Head()
        {
            E val = default(E);
            try
            {
                val = backingList[0];
                backingList.RemoveAt(0);
                currentSize--;
            }
            catch (Exception e) { Console.WriteLine(e); }

            return val;
        }

        /**
         * Returns the item at the tail of this {@code Deque} or null
         * if it is empty. This call does not block if empty.
         * 
         * @return	item at the tail of this {@code Deque} or null
         * 			if it is empty.
         */
        public E Tail()
        {
            E val = default(E);
            try
            {
                val = backingList[backingList.Count - 1];
                backingList.RemoveAt(backingList.Count - 1);
                currentSize--;
            }
            catch (Exception e) { Console.WriteLine(e); }

            return val;
        }

        /**
         * Returns an array containing all of the elements in this deque, 
         * in proper sequence; the runtime type of the returned array is 
         * that of the specified array.
         *  
         * @param a		array indicating return type
         * @return		the contents of this {@code Deque} in an array of
         * 				type &lt;T&gt;
         */
        public T[] ToArray<T>(T[] a)
        {
            return backingList.Cast<T>().ToArray();
        }

        /**
         * Returns the number of elements in this {@code Deque}
         * @return
         */
        public int Size()
        {
            return currentSize;
        }

        /**
         * Returns the capacity this {@code Deque} was last configured with
         * @return
         */
        public int Capacity()
        {
            return capacity;
        }

        /**
         * Resizes the capacity of this {@code Deque} to the capacity
         * specified. 
         * 
         * @param newCapacity
         * @throws IllegalArgumentException if the specified new capacity is less than
         * the previous capacity
         */
        public void Resize(int newCapacity)
        {
            if (capacity == newCapacity) return;
            if (capacity > newCapacity)
            {
                throw new ArgumentException("Cannot resize to less than " + "the original capacity: " + capacity + " > " + newCapacity);
            }

            this.capacity = newCapacity;
        }

        /**
         * Retrieves, but does not remove, the first element of this deque, or 
         * returns null if this deque is empty.
         * 
         * @return
         */
        public E PeekFirst()
        {
            return backingList.FirstOrDefault();
        }

        /**
         * Retrieves, but does not remove, the last element of this deque, or 
         * returns null if this deque is empty.
         * 
         * @return
         */
        public E PeekLast()
        {
            return backingList.LastOrDefault();
        }

        /**
         * Returns an {@link Iterator} over the contents of this {@code Deque}
         * @return
         */
        public IEnumerator<E> GetEnumerator()
        {
            return backingList.AsEnumerable().GetEnumerator();
        }

        /* (non-Javadoc)
         * @see java.lang.Object#hashCode()
         */
        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result
                    + ((backingList == null) ? 0 : backingList.GetHashCode());
            result = prime * result + capacity;
            result = prime * result + currentSize;
            return result;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /* (non-Javadoc)
         * @see java.lang.Object#equals(java.lang.Object)
         */
        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Deque<E> other = (Deque<E>)obj;
            if (capacity != other.capacity)
                return false;
            if (currentSize != other.currentSize)
                return false;
            if (backingList == null)
            {
                if (other.backingList != null)
                    return false;
            }
            else if (!DeepEquals(other))
                return false;

            return true;
        }

        private bool DeepEquals(Deque<E> other)
        {
            IEnumerator<E> otherIt = other.GetEnumerator();

            foreach (var currentItem in this)
            {
                if (!otherIt.MoveNext().Equals(currentItem))
                {
                    return false;
                }
            }

            //for (IEnumerator<E> it = GetEnumerator(); it.HasNext();)
            //{
            //    if (!otherIt.HasNext() || !it.Next().equals(otherIt.Next()))
            //    {
            //        return false;
            //    }
            //}
            return true;
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return backingList.ToString() + " capacity: " + capacity;
        }

        #region Implementation of IEnumerator

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public E Current { get; internal set; }

        [JsonIgnore]
        object IEnumerator.Current
        {
            get { return Current; }
        }

        #endregion
    }
}