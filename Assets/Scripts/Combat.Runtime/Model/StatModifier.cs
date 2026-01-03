namespace Combat.Runtime.Model
{
    public readonly struct StatModifier
    {
        public readonly StatType StatType;
        public readonly int Delta;

        public StatModifier(StatType statType, int delta)
        {
            StatType = statType;
            Delta = delta;
        }
    }
}

