using Combat.Runtime.Commands;
using Combat.Runtime.Events;
using Combat.Runtime.Model;
using NUnit.Framework;

namespace Combat.Runtime.Tests
{
    public sealed class OnHitDamageSystemTests
    {
        [Test]
        public void PublishOnHitEvent_EnqueuesCommand_And_ApplyAll_ReducesTargetHp()
        {
            var eventBus = new EventBus();
            var commandBuffer = new CommandBuffer();
            var battle = new BattleContext(eventBus);

            var attackerId = new UnitId(1);
            var targetId = new UnitId(2);

            var attackerStats = new StatCollection();
            attackerStats.SetStat(StatType.Health, 100);
            battle.AddUnit(new Unit(attackerId, attackerStats));

            var targetStats = new StatCollection();
            targetStats.SetStat(StatType.Health, 100);
            var target = new Unit(targetId, targetStats);
            battle.AddUnit(target);

            _ = new OnHitDamageSystem(eventBus, commandBuffer);

            eventBus.Publish(new OnHitEvent(attackerId, targetId, 10, DamageType.Physical));

            Assert.AreEqual(1, commandBuffer.Count);
            Assert.AreEqual(100, target.Stats.GetStat(StatType.Health));

            commandBuffer.ApplyAll(battle);

            Assert.AreEqual(0, commandBuffer.Count);
            Assert.AreEqual(90, target.Stats.GetStat(StatType.Health));
        }
    }
}

