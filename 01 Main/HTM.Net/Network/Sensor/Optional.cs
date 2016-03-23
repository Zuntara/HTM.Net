using System.Collections.Generic;

namespace HTM.Net.Network.Sensor
{
    public struct Optional<T>
        where T : class
    {
       
        public bool IsPresent { get; internal set; }
        private readonly T _value;
        public T Value
        {
            get
            {
                if (IsPresent)
                    return _value;
                else
                    return default(T);
            }
        }

        public Optional(T value)
        {
            this._value = value;
            if (value != default(T))
            {
                IsPresent = true;
            }
            else
            {
                IsPresent = false;
            }
        }

        public static explicit operator T(Optional<T> optional)
        {
            return optional.Value;
        }
        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>(value);
        }

        public static Optional<T> Empty()
        {
            return new Optional<T>(default(T));
        }



        #region Equality members

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Optional<T> && Equals((Optional<T>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(_value) * 397) ^ IsPresent.GetHashCode();
            }
        }

        #endregion

        public bool Equals(Optional<T> other)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value) && IsPresent == other.IsPresent;
        }
    }
}