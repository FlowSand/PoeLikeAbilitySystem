namespace Combat.Runtime.Model
{
    public readonly struct DamageSpec
    {
        public readonly UnitId SourceUnitId;
        public readonly UnitId TargetUnitId;
        public readonly int BaseValue;
        public readonly DamageType DamageType;

        public DamageSpec(UnitId sourceUnitId, UnitId targetUnitId, int baseValue, DamageType damageType)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            BaseValue = baseValue;
            DamageType = damageType;
        }
    }
}

