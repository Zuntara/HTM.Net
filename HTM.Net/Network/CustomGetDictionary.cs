using System;
using System.Collections;
using System.Collections.Generic;

namespace HTM.Net.Network
{
    public class CustomGetDictionary<K, V> : IDictionary<K, V>
    {
        private readonly Func<K, V> _getFunc;

        public CustomGetDictionary(Func<K, V> getFunc)
        {
            _getFunc = getFunc;
        }


        public bool ContainsKey(K key)
        {
            return true;
        }

        public void Add(K key, V value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(K key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out V value)
        {
            throw new NotImplementedException();
        }

        V IDictionary<K, V>.this[K key]
        {
            get { return _getFunc(key); }
            set { throw new NotImplementedException(); }
        }

        public ICollection<K> Keys { get; }
        public ICollection<V> Values { get; }

        public V this[K key]
        {
            get { return _getFunc(key); }
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public int Count { get; }
        public bool IsReadOnly { get; }
    }
}