using Combat.Runtime.Model;

namespace Combat.Runtime.Events
{
    public readonly struct OnCastEvent : ICombatEvent
    {
        public readonly UnitId CasterUnitId;

        public OnCastEvent(UnitId casterUnitId)
        {
            CasterUnitId = casterUnitId;
        }
    }
}

