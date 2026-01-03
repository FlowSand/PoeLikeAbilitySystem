using System;

namespace Combat.Runtime.Model
{
    public readonly struct UnitId : IEquatable<UnitId>
    {
        public readonly int Value;

        public UnitId(int value)
        {
            Value = value;
        }

        public bool Equals(UnitId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is UnitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(UnitId left, UnitId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(UnitId left, UnitId right)
        {
            return left.Value != right.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}

