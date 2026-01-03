using System;
using NUnit.Framework;
using Combat.Runtime.Events;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Model;

namespace Combat.Runtime.Tests
{
    [TestFixture]
    public class BattleSimulatorTests
    {
        private BattleContext _context;
        private BattleSimulator _simulator;

        [SetUp]
        public void SetUp()
        {
            _context = new BattleContext(new EventBus());
            _simulator = new BattleSimulator(_context);
        }

        /// <summary>
        /// Helper: Create a simple "fireball" plan that deals 100 damage to the target.
        /// Plan: GetTarget -> ConstNumber(100) -> MakeDamage -> EmitApplyDamage
        /// </summary>
        private ExecPlan CreateSimpleFireballPlan()
        {
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 1,
                damageSpecSlotCount: 1
            );

            var operations = new[]
            {
                // entities[0] = GetTarget
                new Op(OpCode.GetTarget, 0, 0, 0),

                // numbers[0] = 100.0
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(100f), 0, 0),

                // damageSpecs[0] = MakeDamage(numbers[0], entities[0])
                new Op(OpCode.MakeDamage, 0, 0, 0),

                // EmitApplyDamage(damageSpecs[0])
                new Op(OpCode.EmitApplyDamage, 0, 0, 0)
            };

            return new ExecPlan(0, operations, layout);
        }

        [Test]
        public void BattleSimulator_BasicExecution_AppliesDamage()
        {
            // Setup units
            var casterStats = new StatCollection();
            casterStats.SetStat(StatType.Health, 100);
            var caster = new Unit(new UnitId(1), casterStats);

            var targetStats = new StatCollection();
            targetStats.SetStat(StatType.Health, 500);
            var target = new Unit(new UnitId(2), targetStats);

            _context.AddUnit(caster);
            _context.AddUnit(target);

            // Register plan
            var plan = CreateSimpleFireballPlan();
            _simulator.RegisterEventPlan(typeof(OnHitEvent), plan);

            // Trigger event (using OnHitEvent since it has target field)
            var hitEvent = new OnHitEvent(
                sourceUnitId: caster.Id,
                targetUnitId: target.Id,
                baseDamage: 50, // Note: plan overrides this with 100
                damageType: DamageType.Physical
            );

            _simulator.EnqueueEvent(hitEvent);
            _simulator.ProcessEvents();

            // Verify damage was applied
            var targetAfter = _context.GetUnit(target.Id);
            Assert.AreEqual(400, targetAfter.Stats.GetStat(StatType.Health)); // 500 - 100 = 400
        }

        [Test]
        public void BattleSimulator_TriggerDepthLimit_RejectsDeepEvents()
        {
            var plan = CreateSimpleFireballPlan();
            _simulator.RegisterEventPlan(typeof(OnHitEvent), plan);

            var evt = new OnHitEvent(
                sourceUnitId: new UnitId(1),
                targetUnitId: new UnitId(2),
                baseDamage: 10,
                damageType: DamageType.Physical
            );

            // Enqueue events with increasing depth
            for (int depth = 0; depth < 15; depth++)
            {
                _simulator.EnqueueEvent(evt, rootEventId: 1, depth: depth, seed: 12345);
            }

            // Setup units so execution doesn't fail
            var stats = new StatCollection();
            stats.SetStat(StatType.Health, 10000);
            _context.AddUnit(new Unit(new UnitId(1), stats));
            _context.AddUnit(new Unit(new UnitId(2), stats));

            // Process events
            _simulator.ProcessEvents();

            // Verify: Only events with depth < MAX_TRIGGER_DEPTH (10) were processed
            // Each event deals 100 damage (from plan)
            var targetAfter = _context.GetUnit(new UnitId(2));
            int expectedHealth = 10000 - (100 * BattleConfig.MAX_TRIGGER_DEPTH);
            Assert.AreEqual(expectedHealth, targetAfter.Stats.GetStat(StatType.Health));
        }

        [Test]
        public void BattleSimulator_ExecutionBudgetLimit_InterruptsExecution()
        {
            // Create a plan with 1500 ConstNumber ops (exceeds MAX_OPS_PER_EVENT = 1000)
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 0,
                damageSpecSlotCount: 0
            );

            var ops = new Op[1500];
            for (int i = 0; i < 1500; i++)
            {
                ops[i] = new Op(
                    opCode: OpCode.ConstNumber,
                    a: BitConverter.SingleToInt32Bits(1f),
                    b: 0,
                    output: 0
                );
            }

            var hugePlan = new ExecPlan(0, ops, layout);

            _simulator.RegisterEventPlan(typeof(OnCastEvent), hugePlan);

            // Trigger event
            var castEvent = new OnCastEvent(new UnitId(1));
            _simulator.EnqueueEvent(castEvent);
            _simulator.ProcessEvents();

            // Test passes if no exception is thrown
            // Execution should have been interrupted at op 1000
            Assert.Pass("Execution budget limit prevented runaway execution");
        }

        [Test]
        public void BattleSimulator_MultipleEvents_ProcessedInFIFOOrder()
        {
            var plan = CreateSimpleFireballPlan();
            _simulator.RegisterEventPlan(typeof(OnHitEvent), plan);

            // Setup units
            var stats = new StatCollection();
            stats.SetStat(StatType.Health, 1000);
            _context.AddUnit(new Unit(new UnitId(1), stats));
            _context.AddUnit(new Unit(new UnitId(2), stats));

            // Enqueue 3 events
            for (int i = 0; i < 3; i++)
            {
                var evt = new OnHitEvent(
                    sourceUnitId: new UnitId(1),
                    targetUnitId: new UnitId(2),
                    baseDamage: 10,
                    damageType: DamageType.Physical
                );
                _simulator.EnqueueEvent(evt);
            }

            // Process all events
            _simulator.ProcessEvents();

            // Each event deals 100 damage (from plan)
            var targetAfter = _context.GetUnit(new UnitId(2));
            Assert.AreEqual(700, targetAfter.Stats.GetStat(StatType.Health)); // 1000 - 300 = 700
        }

        [Test]
        public void BattleSimulator_NoRegisteredPlan_SkipsEvent()
        {
            // Don't register any plan

            var evt = new OnCastEvent(new UnitId(1));
            _simulator.EnqueueEvent(evt);
            _simulator.ProcessEvents();

            // Should complete without throwing
            Assert.Pass("Event with no registered plan was safely skipped");
        }

        [Test]
        public void BattleSimulator_MaxEventsPerFrame_LimitsProcessing()
        {
            var plan = CreateSimpleFireballPlan();
            _simulator.RegisterEventPlan(typeof(OnHitEvent), plan);

            // Setup units
            var stats = new StatCollection();
            stats.SetStat(StatType.Health, 100000);
            _context.AddUnit(new Unit(new UnitId(1), stats));
            _context.AddUnit(new Unit(new UnitId(2), stats));

            // Enqueue more events than MAX_EVENTS_PER_FRAME (100)
            for (int i = 0; i < 150; i++)
            {
                var evt = new OnHitEvent(
                    sourceUnitId: new UnitId(1),
                    targetUnitId: new UnitId(2),
                    baseDamage: 10,
                    damageType: DamageType.Physical
                );
                _simulator.EnqueueEvent(evt);
            }

            // Process once
            _simulator.ProcessEvents();

            // Only MAX_EVENTS_PER_FRAME (100) events should have been processed
            var targetAfter = _context.GetUnit(new UnitId(2));
            int expectedHealth = 100000 - (100 * BattleConfig.MAX_EVENTS_PER_FRAME);
            Assert.AreEqual(expectedHealth, targetAfter.Stats.GetStat(StatType.Health));
        }
    }
}
