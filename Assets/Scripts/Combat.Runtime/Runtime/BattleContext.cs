using System;
using System.Collections.Generic;
using Combat.Runtime.Events;
using Combat.Runtime.Model;

namespace Combat.Runtime
{
    public sealed class BattleContext
    {
        private readonly Dictionary<UnitId, Unit> _units = new Dictionary<UnitId, Unit>(16);

        public EventBus Events { get; }

        public BattleContext(EventBus events)
        {
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void AddUnit(Unit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            _units[unit.Id] = unit;
        }

        public bool TryGetUnit(UnitId unitId, out Unit unit)
        {
            return _units.TryGetValue(unitId, out unit);
        }

        public Unit GetUnit(UnitId unitId)
        {
            if (!_units.TryGetValue(unitId, out var unit))
                throw new KeyNotFoundException("Unknown unitId: " + unitId.ToString());

            return unit;
        }

        public void ApplyDamage(UnitId targetUnitId, int damage)
        {
            if (damage <= 0) return;
            if (!_units.TryGetValue(targetUnitId, out var target)) return;

            int health = target.Stats.GetStat(StatType.Health);
            int newHealth = health - damage;
            if (newHealth < 0) newHealth = 0;
            target.Stats.SetStat(StatType.Health, newHealth);
        }
    }
}

