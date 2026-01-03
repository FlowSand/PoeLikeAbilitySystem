using Combat.Runtime.Model;

namespace Combat.Runtime.Events
{
    public readonly struct OnHitEvent : ICombatEvent
    {
        public readonly UnitId SourceUnitId;
        public readonly UnitId TargetUnitId;
        public readonly int BaseDamage;
        public readonly DamageType DamageType;

        public OnHitEvent(UnitId sourceUnitId, UnitId targetUnitId, int baseDamage, DamageType damageType)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            BaseDamage = baseDamage;
            DamageType = damageType;
        }
    }
}

