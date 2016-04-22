using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HTM.Net.Util
{
    /**
 * Immutable tuple which adds associative lookup functionality.
 * 
 * @author David Ray
 */
    public class NamedTuple : Tuple
    {
        private readonly Bucket[] _entries;
        private string[] _keys;
        private int _hash;
        private readonly int _thisHashcode;
        private readonly string[] _emptyKeys = new string[0];

        [Obsolete("This may not be used in code - here for deserialisation", true)]
        public NamedTuple()
        {
            
        }

        /**
         * Constructs and new {@code NamedTuple}
         * 
         * @param keys      
         * @param objects
         */
        public NamedTuple(string[] keys, params object[] objects)
                    : base(Interleave(keys, objects))
        {
            if (keys.Length != objects.Length)
            {
                throw new ArgumentException("Keys and values must be same length.");
            }
            
            _keys = keys;

            _entries = new Bucket[keys.Length * 2];
            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i] = new Bucket(i);
            }

            for (int i = 0; i < keys.Length; i++)
            {
                AddEntry(keys[i], objects[i]);
            }

            _thisHashcode = GetHashCode();
        }

        /**
         * Returns a array copy of this {@code NamedTuple}'s keys.
         * @return
         */
        public string[] GetKeys()
        {
            if (_keys == null || _keys.Length < 1) return _emptyKeys;

            return Arrays.CopyOf(_keys, _keys.Length);
        }

        /**
         * Returns a Collection view of the values of this {@code NamedTuple}
         * @return
         */
        public List<object> Values()
        {
            List<object> retVal = new List<object>();
            for (int i = 1; i < All().Count; i += 2)
            {
                retVal.Add(All()[i]);
            }
            return retVal;
        }

        public object this[string key]
        {
            get { return Get(key); }
            set
            {
                if (key == null) throw new ArgumentNullException("key");
                int hash = HashIndex(key);
                Entry e = _entries[hash].Find(key, hash);
                if (e == null)
                {
                    AddEntry(key, null);
                    e = _entries[hash].Find(key, hash);
                }
                e.Value = value;
            }
        }

        /**
         * Returns the Object corresponding with the specified
         * key.
         * 
         * @param key   the identifier with the same corresponding index as 
         *              its value during this {@code NamedTuple}'s construction.
         * @return
         */
        public object Get(string key)
        {
            if (key == null) return null;
            int hash = HashIndex(key);
            Entry e = _entries[hash].Find(key, hash);
            return e?.Value;
        }

        public string GetAsString(string key)
        {
            object value = Get(key);
            if (value is double)
            {
                return ((double)value).ToString(NumberFormatInfo.InvariantInfo);
            }
            return value?.ToString();
        }

        /**
         * Returns a flag indicating whether the specified key
         * exists within this {@code NamedTuple}
         * 
         * @param key
         * @return
         */
        public bool HasKey(string key)
        {
            int hash = HashIndex(key);
            Entry e = _entries[hash].Find(key, hash);
            return e != null;
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _entries.Length; i++)
            {
                sb.Append(_entries[i]);
            }
            return sb.ToString();
        }

        /**
         * Creates an {@link Entry} with the hashed key value, checking 
         * for duplicates (which aren't allowed during construction).
         * 
         * @param key       the unique String identifier
         * @param value     the Object corresponding to the specified key
         */
        private void AddEntry(string key, object value)
        {
            int hash = HashIndex(key);
            Entry e;
            if ((e = _entries[hash].Find(key, hash)) != null && e.Key.Equals(key))
            {
                throw new InvalidOperationException("Duplicates Not Allowed - Key: " + key + ", reinserted.");
            }

            Entry entry = new Entry(this, key, value, hash);
            _entries[hash].Add(entry);

            if (!_keys.Contains(key))
            {
                var extraKeys = new List<string>(_keys);
                extraKeys.Add(key);
                _keys = extraKeys.ToArray();
            }
        }

        /**
         * Creates and returns a hash code conforming to a number
         * between 0 - n-1, where n = #Buckets
         * 
         * @param key   String to be hashed.
         * @return
         */
        private int HashIndex(string key)
        {
            return Math.Abs(key.GetHashCode()) % _entries.Length;
        }

        /**
         * {@inheritDoc}
         */
        public override sealed int GetHashCode()
        {
            if (_hash == 0)
            {
                const int prime = 31;
                int result = base.GetHashCode();
                result = prime * result + (_entries != null ? _entries.GetHashCode() : 0);
                _hash = result;
            }
            return _hash;
        }

        /**
         * {@inheritDoc}
         */
        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (GetType() != obj.GetType())
                return false;
            if (!base.Equals(obj))
                return false;
            NamedTuple other = (NamedTuple)obj;
            if (_thisHashcode != other._thisHashcode)
                return false;
            return true;
        }

        /**
         * Encapsulates the hashed key/value pair in a linked node.
         */
        private sealed class Entry
        {
            internal readonly string Key;
            internal object Value;
            private readonly int _hash;
            internal Entry Prev;

            /**
             * Constructs a new {@code Entry}
             * 
             * @param key
             * @param value
             * @param hash
             */
            public Entry(NamedTuple parent, string key, object value, int hash)
            {
                Key = key;
                Value = value;
                _hash = parent.HashIndex(key);
            }

            /**
             * {@inheritDoc}
             */
            public override string ToString()
            {
                return new StringBuilder("key=").Append(Key)
                    .Append(", value=").Append(Value)
                        .Append(", hash=").Append(_hash).ToString();
            }

            /**
             * {@inheritDoc}
             */
            public override int GetHashCode()
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + _hash;
                result = prime * result + (Key?.GetHashCode() ?? 0);
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
                Entry other = (Entry)obj;
                if (_hash != other._hash)
                    return false;
                if (Key == null)
                {
                    if (other.Key != null)
                        return false;
                }
                else if (!Key.Equals(other.Key))
                    return false;
                if (Value == null)
                {
                    if (other.Value != null)
                        return false;
                }
                else if (!Value.Equals(other.Value))
                    return false;
                return true;
            }
        }

        /**
         * Rudimentary (light-weight) Linked List implementation for storing
         * hash {@link Entry} collisions.
         */
        private sealed class Bucket
        {
            Entry _last;
            readonly int _idx;

            /**
             * Constructs a new {@code Bucket}
             * @param idx   the identifier of this bucket for debug purposes.
             */
            public Bucket(int idx)
            {
                _idx = idx;
            }

            /**
             * Adds the specified {@link Entry} to this Bucket.
             * @param e
             */
            internal void Add(Entry e)
            {
                if (_last == null)
                {
                    _last = e;
                }
                else {
                    e.Prev = _last;
                    _last = e;
                }
            }

            /**
             * Searches for an {@link Entry} with the specified key,
             * and returns it if found and otherwise returns null.
             * 
             * @param key       the String identifier corresponding to the
             *                  hashed value
             * @param hash      the hash code.
             * @return
             */
            internal Entry Find(string key, int hash)
            {
                if (_last == null) return null;

                Entry found = _last;
                while (found.Prev != null && !found.Key.Equals(key))
                {
                    found = found.Prev;
                    if (found.Key.Equals(key))
                    {
                        return found;
                    }
                }
                return found.Key.Equals(key) ? found : null;
            }

            /**
             * {@inheritDoc}
             */
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder("Bucket: ").Append(_idx).Append("\n");
                Entry l = _last;
                while (l != null)
                {
                    sb.Append("\t").Append(l).Append("\n");
                    l = l.Prev;
                }

                return sb.ToString();
            }

            /**
             * {@inheritDoc}
             */
            public override int GetHashCode()
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + _idx;
                result = prime * result + (_last?.GetHashCode() ?? 0);
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
                Bucket other = (Bucket)obj;
                if (_idx != other._idx)
                    return false;
                if (_last == null)
                {
                    if (other._last != null)
                        return false;
                }
                else if (!_last.Equals(other._last))
                    return false;
                return true;
            }
        }

        /**
         * Returns an array containing the successive elements of each
         * argument array as in [ first[0], second[0], first[1], second[1], ... ].
         * 
         * Arrays may be of zero length, and may be of different sizes, but may not be null.
         * 
         * @param first     the first array
         * @param second    the second array
         * @return
         */
        internal static object[] Interleave<TF, TS>(TF[] first, TS[] second)
        {
            if(first == null || second == null || first.Length == 0 || second.Length == 0)
                throw new ArgumentException("There must be at least one key given to interleave!");
            int flen = first.Length, slen = second.Length;
            object[] retVal = new object[flen + slen];
            for (int i = 0, j = 0, k = 0; i < flen || j < slen;)
            {
                if (i < flen)
                {
                    retVal[k++] = first[i++];// Array.get(first, i++);
                }
                if (j < slen)
                {
                    retVal[k++] = second[j++]; //Array.get(second, j++);
                }
            }

            return retVal;
        }
    }
}