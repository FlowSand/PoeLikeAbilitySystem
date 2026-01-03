using System;
using Combat.Runtime.Model;

namespace Combat.Runtime.Commands
{
    public sealed class ApplyDamageCommand : ICombatCommand
    {
        private readonly DamageSpec _spec;

        public ApplyDamageCommand(DamageSpec spec)
        {
            _spec = spec;
        }

        public void Execute(BattleContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            int finalDamage = _spec.BaseValue;
            if (finalDamage <= 0) return;

            context.ApplyDamage(_spec.TargetUnitId, finalDamage);
        }
    }
}

