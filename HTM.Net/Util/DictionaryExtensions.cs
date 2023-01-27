using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Util
{
    public static class DictionaryExtensions
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
}