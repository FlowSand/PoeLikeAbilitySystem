using System;

namespace Combat.Runtime.Model
{
    public sealed class Unit
    {
        public UnitId Id { get; }
        public StatCollection Stats { get; }

        public bool IsAlive => Stats.GetStat(StatType.Health) > 0;

        public Unit(UnitId id, StatCollection stats)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Id = id;
        }
    }
}

