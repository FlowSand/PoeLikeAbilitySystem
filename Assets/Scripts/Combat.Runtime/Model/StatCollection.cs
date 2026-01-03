namespace Combat.Runtime.Model
{
    public sealed class StatCollection
    {
        private readonly int[] _values;

        public StatCollection()
        {
            _values = new int[(int)StatType.Count];
        }

        public int GetStat(StatType statType)
        {
            return _values[(int)statType];
        }

        public void SetStat(StatType statType, int value)
        {
            _values[(int)statType] = value;
        }

        public void ApplyModifier(StatModifier modifier)
        {
            int index = (int)modifier.StatType;
            _values[index] += modifier.Delta;
        }
    }
}

