using System;
using Combat.Runtime.Commands;
using Combat.Runtime.Events;
using Combat.Runtime.Model;

namespace Combat.Runtime
{
    public sealed class OnHitDamageSystem
    {
        private readonly CommandBuffer _commandBuffer;

        public OnHitDamageSystem(EventBus eventBus, CommandBuffer commandBuffer)
        {
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            _commandBuffer = commandBuffer ?? throw new ArgumentNullException(nameof(commandBuffer));

            eventBus.Subscribe<OnHitEvent>(OnHit);
        }

        private void OnHit(OnHitEvent evt)
        {
            var spec = new DamageSpec(evt.SourceUnitId, evt.TargetUnitId, evt.BaseDamage, evt.DamageType);
            _commandBuffer.Enqueue(new ApplyDamageCommand(spec));
        }
    }
}

