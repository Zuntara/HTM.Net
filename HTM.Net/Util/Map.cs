using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HTM.Net.Util;

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

    #region Overrides of Object

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (!(obj is Map<TKey, TValue>))
        {
            return false;
        }

        return Equals((Map<TKey, TValue>)obj);
    }

    #region Equality members

    protected bool Equals(Map<TKey, TValue> other)
    {
        if (this.Count != other.Count) return false;

        string keyList1 = Arrays.ToString(Keys);
        string keyList2 = Arrays.ToString(other.Keys);

        if (keyList1 != keyList2) return false;

        string valueList1 = Arrays.ToString(Values);
        string valueList2 = Arrays.ToString(other.Values);
        if (valueList1 != valueList2) return false;

        return true;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    #endregion

    #endregion

    public override string ToString()
    {
        return Arrays.ToString(Keys);
    }
}