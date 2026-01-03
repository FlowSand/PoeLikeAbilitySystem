using System;
using NUnit.Framework;
using Combat.Runtime.Commands;
using Combat.Runtime.Events;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Model;

namespace Combat.Runtime.Tests
{
    [TestFixture]
    public class ExecPlanRunnerTests
    {
        private BattleContext _context;
        private ExecPlanRunner _runner;
        private ExecutionContext _execCtx;
        private CommandBuffer _commandBuffer;

        [SetUp]
        public void SetUp()
        {
            _context = new BattleContext(new EventBus());
            _runner = new ExecPlanRunner(_context);
            _commandBuffer = new CommandBuffer();
        }

        private ExecutionContext CreateContext(SlotLayout layout)
        {
            return new ExecutionContext
            {
                rootEventId = 1,
                triggerDepth = 0,
                randomSeed = 12345,
                casterUnitId = new UnitId(1),
                targetUnitId = new UnitId(2),
                slots = SlotStorage.Rent(layout),
                budget = new ExecutionBudget(
                    maxOps: BattleConfig.MAX_OPS_PER_EVENT,
                    maxCommands: BattleConfig.MAX_COMMANDS_PER_EVENT
                )
            };
        }

        [Test]
        public void ExecuteConstNumber_StoresFloatInSlot()
        {
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 0,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            var operations = new[]
            {
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(42.5f), 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(42.5f, _execCtx.slots.numbers[0], 0.001f);
        }

        [Test]
        public void ExecuteGetCaster_LoadsCasterUnitId()
        {
            var layout = new SlotLayout(
                numberSlotCount: 0,
                entitySlotCount: 1,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            var operations = new[]
            {
                new Op(OpCode.GetCaster, 0, 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(new UnitId(1), _execCtx.slots.entities[0]);
        }

        [Test]
        public void ExecuteGetTarget_LoadsTargetUnitId()
        {
            var layout = new SlotLayout(
                numberSlotCount: 0,
                entitySlotCount: 1,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            var operations = new[]
            {
                new Op(OpCode.GetTarget, 0, 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(new UnitId(2), _execCtx.slots.entities[0]);
        }

        [Test]
        public void ExecuteGetStat_ReadsUnitStat()
        {
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 1,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            // Setup unit with stats
            var stats = new StatCollection();
            stats.SetStat(StatType.Health, 100);
            var unit = new Unit(new UnitId(1), stats);
            _context.AddUnit(unit);

            // Manually set entity slot to unit ID
            _execCtx.slots.entities[0] = new UnitId(1);

            var operations = new[]
            {
                new Op(OpCode.GetStat, (int)StatType.Health, 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(100f, _execCtx.slots.numbers[0], 0.001f);
        }

        [Test]
        public void ExecuteAdd_AddsNumbers()
        {
            var layout = new SlotLayout(
                numberSlotCount: 3,
                entitySlotCount: 0,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            _execCtx.slots.numbers[0] = 10f;
            _execCtx.slots.numbers[1] = 20f;

            var operations = new[]
            {
                new Op(OpCode.Add, 0, 1, 2)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(30f, _execCtx.slots.numbers[2], 0.001f);
        }

        [Test]
        public void ExecuteMul_MultipliesNumbers()
        {
            var layout = new SlotLayout(
                numberSlotCount: 3,
                entitySlotCount: 0,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);

            _execCtx.slots.numbers[0] = 5f;
            _execCtx.slots.numbers[1] = 3f;

            var operations = new[]
            {
                new Op(OpCode.Mul, 0, 1, 2)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(15f, _execCtx.slots.numbers[2], 0.001f);
        }

        [Test]
        public void ExecuteMakeDamage_CreatesDamageSpec()
        {
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 1,
                damageSpecSlotCount: 1
            );
            _execCtx = CreateContext(layout);

            _execCtx.slots.numbers[0] = 100f; // damage amount
            _execCtx.slots.entities[0] = new UnitId(99); // target

            var operations = new[]
            {
                new Op(OpCode.MakeDamage, 0, 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            var spec = _execCtx.slots.damageSpecs[0];
            Assert.AreEqual(new UnitId(1), spec.SourceUnitId); // caster
            Assert.AreEqual(new UnitId(99), spec.TargetUnitId);
            Assert.AreEqual(100, spec.BaseValue);
            Assert.AreEqual(DamageType.Physical, spec.DamageType);
        }

        [Test]
        public void ExecuteEmitApplyDamage_EnqueuesCommand()
        {
            var layout = new SlotLayout(
                numberSlotCount: 0,
                entitySlotCount: 0,
                damageSpecSlotCount: 1
            );
            _execCtx = CreateContext(layout);

            _execCtx.slots.damageSpecs[0] = new DamageSpec(
                sourceUnitId: new UnitId(1),
                targetUnitId: new UnitId(2),
                baseValue: 50,
                damageType: DamageType.Physical
            );

            var operations = new[]
            {
                new Op(OpCode.EmitApplyDamage, 0, 0, 0)
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsTrue(success);
            Assert.AreEqual(1, _commandBuffer.Count);
        }

        [Test]
        public void Execute_OpBudgetExceeded_ReturnsFalse()
        {
            var layout = new SlotLayout(
                numberSlotCount: 1,
                entitySlotCount: 0,
                damageSpecSlotCount: 0
            );
            _execCtx = CreateContext(layout);
            _execCtx.budget = new ExecutionBudget(maxOps: 2, maxCommands: 100);

            // Create plan with 5 ops
            var operations = new[]
            {
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(1f), 0, 0),
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(2f), 0, 0),
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(3f), 0, 0),
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(4f), 0, 0),
                new Op(OpCode.ConstNumber, BitConverter.SingleToInt32Bits(5f), 0, 0),
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsFalse(success);
            Assert.AreEqual(2, _execCtx.budget.opsExecuted); // Only 2 ops executed
        }

        [Test]
        public void Execute_CommandBudgetExceeded_ReturnsFalse()
        {
            var layout = new SlotLayout(
                numberSlotCount: 0,
                entitySlotCount: 0,
                damageSpecSlotCount: 1
            );
            _execCtx = CreateContext(layout);
            _execCtx.budget = new ExecutionBudget(maxOps: 100, maxCommands: 1);

            _execCtx.slots.damageSpecs[0] = new DamageSpec(
                sourceUnitId: new UnitId(1),
                targetUnitId: new UnitId(2),
                baseValue: 50,
                damageType: DamageType.Physical
            );

            // Try to emit 2 commands (exceeds budget of 1)
            var operations = new[]
            {
                new Op(OpCode.EmitApplyDamage, 0, 0, 0),
                new Op(OpCode.EmitApplyDamage, 0, 0, 0),
            };

            var plan = new ExecPlan(0, operations, layout);

            bool success = _runner.Execute(plan, ref _execCtx, _commandBuffer);

            Assert.IsFalse(success);
            Assert.AreEqual(1, _commandBuffer.Count); // Only 1 command emitted
            Assert.AreEqual(1, _execCtx.budget.commandsEmitted);
        }
    }
}
