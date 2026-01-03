namespace Combat.Runtime.GraphRuntime
{
    public static class StableHash64
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong AddByte(ulong hash, byte value)
        {
            hash ^= value;
            hash *= Prime;
            return hash;
        }

        public static ulong AddInt(ulong hash, int value)
        {
            unchecked
            {
                hash = AddByte(hash, (byte)value);
                hash = AddByte(hash, (byte)(value >> 8));
                hash = AddByte(hash, (byte)(value >> 16));
                hash = AddByte(hash, (byte)(value >> 24));
                return hash;
            }
        }

        public static ulong AddString(ulong hash, string value)
        {
            if (value == null) value = string.Empty;

            int length = value.Length;
            hash = AddInt(hash, length);

            for (int i = 0; i < length; i++)
            {
                char c = value[i];
                hash = AddByte(hash, (byte)c);
                hash = AddByte(hash, (byte)(c >> 8));
            }

            return hash;
        }
    }
}

