using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace HTM.Net.Util
{
    public static class DictionaryExtentions
    {
        /// <summary>
        /// Adjusts the primitive value mapped to the key if the key is present in the map. Otherwise, the initial_value is put in the map.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="dict"></param>
        /// <param name="key">the key of the value to increment</param>
        /// <param name="adjustVal"> the amount to adjust the value by</param>
        /// <param name="putValue">the value put into the map if the key is not initial present</param>
        public static void AdjustOrPutValue<TKey>(this IDictionary<TKey, int> dict, TKey key, int adjustVal, int putValue)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] += adjustVal;
            }
            else
            {
                dict.Add(key, putValue);
            }
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            return defaultValue;
        }

        public static void AddAll(this Parameters.ParametersMap dict, Parameters.ParametersMap otherDict, bool throwErrorOnDouble = false)
        {
            foreach (var valuePair in otherDict)
            {
                if (dict.ContainsKey(valuePair.Key))
                {
                    if (throwErrorOnDouble)
                        throw new InvalidOperationException("Key already present: " + valuePair.Key);
                }
                else
                {
                    dict.Add(valuePair.Key, valuePair.Value);
                }
            }
        }

        public static void AddAll<TKey, TValue>(this IDictionary<TKey, TValue> dict, IDictionary<TKey, TValue> otherDict, bool throwErrorOnDouble = false)
        {
            foreach (var valuePair in otherDict)
            {
                if (dict.ContainsKey(valuePair.Key))
                {
                    if (throwErrorOnDouble)
                        throw new InvalidOperationException("Key already present: " + valuePair.Key);
                }
                else
                {
                    dict.Add(valuePair.Key, valuePair.Value);
                }
            }
        }

        public static bool DictEquals<TKey, TValue>(this IDictionary<TKey, TValue> dict, IDictionary<TKey, TValue> other)
        {
            // compare two dictionaries
            if (dict.Count != other.Count) return false;

            for (int i = 0; i < dict.Count; i++)
            {
                var left = dict.ElementAt(i);
                var right = dict.ElementAt(i);
                if (!left.Equals(right))
                {
                    return false;
                }
            }
            return true;
        }

        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
        {
            if (dict.ContainsKey(key))
                return dict[key];
            return defaultValue;
        }

        public static object Get(this IDictionary dict, object key, object defaultValue = default(object))
        {
            if (dict.Contains(key))
                return dict[key];
            return defaultValue;
        }
    }

    //public class NonUniqueDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
    //{
    //    private List<KeyValuePair<TKey, TValue>> _items;
    //    private object _syncRoot = new object();

    //    public NonUniqueDictionary()
    //    {
    //        _items = new List<KeyValuePair<TKey, TValue>>();
    //    }

    //    public TValue Get(TKey key)
    //    {
    //        if (_items.Any(i => i.Key.Equals(key)))
    //        {
    //            return _items.First(i => i.Key.Equals(key)).Value;
    //        }
    //        return default(TValue);
    //    }

    //    #region Implementation of IEnumerable

    //    void IDictionary.Clear()
    //    {
    //        _items.Clear();
    //    }

    //    IDictionaryEnumerator IDictionary.GetEnumerator()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void Remove(object key)
    //    {
    //        var item = _items.FirstOrDefault(kvp => kvp.Key.Equals(key));
    //    }

    //    object IDictionary.this[object key]
    //    {
    //        get { throw new NotImplementedException(); }
    //        set { throw new NotImplementedException(); }
    //    }

    //    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
    //    {
    //        return _items.GetEnumerator();
    //    }

    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return _items.GetEnumerator();
    //    }

    //    #endregion

    //    #region Implementation of ICollection<KeyValuePair<TKey,TValue>>

    //    public void Add(KeyValuePair<TKey, TValue> item)
    //    {
    //        _items.Add(item);
    //    }

    //    public bool Contains(object key)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void Add(object key, object value)
    //    {
    //        _items.Add(new KeyValuePair<TKey, TValue>((TKey)key, (TValue)value));
    //    }

    //    void ICollection<KeyValuePair<TKey, TValue>>.Clear()
    //    {
    //        _items.Clear();
    //    }

    //    public bool Contains(KeyValuePair<TKey, TValue> item)
    //    {
    //        return _items.Contains(item);
    //    }

    //    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool Remove(KeyValuePair<TKey, TValue> item)
    //    {
    //        return _items.Remove(item);
    //    }

    //    public void CopyTo(Array array, int index)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    int ICollection.Count { get { return _items.Count; } }
    //    public object SyncRoot { get { return _syncRoot; } }
    //    public bool IsSynchronized { get { return false; } }
    //    int ICollection<KeyValuePair<TKey, TValue>>.Count { get { return _items.Count; } }
    //    ICollection IDictionary.Values { get { return _items.Select(i => i.Value).ToList(); } }
    //    bool IDictionary.IsReadOnly { get { return false; } }
    //    public bool IsFixedSize { get { return false; } }
    //    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly { get { return false; } }

    //    #endregion

    //    #region Implementation of IDictionary<TKey,TValue>

    //    public bool ContainsKey(TKey key)
    //    {
    //        if (_items.Any(kvp => kvp.Key.Equals(key)))
    //        {
    //            return true;
    //        }
    //        return false;
    //    }

    //    public void Add(TKey key, TValue value)
    //    {
    //        if (ContainsKey(key))
    //        {
    //            var item = _items.First(i => i.Key.Equals(key));
    //            int index = _items.IndexOf(item);
    //            _items[index] = new KeyValuePair<TKey, TValue>(key, value);
    //        }
    //        else
    //        {
    //            _items.Add(new KeyValuePair<TKey, TValue>(key, value));
    //        }
    //    }

    //    public bool Remove(TKey key)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool TryGetValue(TKey key, out TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    TValue IDictionary<TKey, TValue>.this[TKey key]
    //    {
    //        get { return Get(key); }
    //        set { throw new NotImplementedException(); }
    //    }

    //    ICollection<TKey> IDictionary<TKey, TValue>.Keys
    //    {
    //        get { return _items.Select(i => i.Key).ToList(); }
    //    }

    //    ICollection IDictionary.Keys
    //    {
    //        get { return _items.Select(i => i.Key).ToList(); }
    //    }

    //    ICollection<TValue> IDictionary<TKey, TValue>.Values
    //    {
    //        get { return _items.Select(i => i.Value).ToList(); }
    //    }

    //    #endregion
    //}

    [Serializable]
    public class Map<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private object _syncRoot = new object();

        public Map()
        {

        }

        protected Map(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public Map(IDictionary<TKey, TValue> classifierInput)
            : base(classifierInput)
        {
        }

        public new void Add(TKey key, TValue value)
        {
            lock (_syncRoot)
            {
                if (base.ContainsKey(key))
                {
                    base[key] = value;
                }
                else
                {
                    base.Add(key, value);
                }
            }
        }

        public void Update(IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) return;
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                Add(pair.Key, pair.Value);  // replace where needed
            }
        }

        public override string ToString()
        {
            return Arrays.ToString(Keys);
        }
    }
}