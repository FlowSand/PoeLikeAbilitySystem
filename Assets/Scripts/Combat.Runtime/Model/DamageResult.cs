namespace Combat.Runtime.Model
{
    public readonly struct DamageResult
    {
        public readonly int FinalValue;
        public readonly bool IsCrit;

        public DamageResult(int finalValue, bool isCrit)
        {
            FinalValue = finalValue;
            IsCrit = isCrit;
        }
    }
}

