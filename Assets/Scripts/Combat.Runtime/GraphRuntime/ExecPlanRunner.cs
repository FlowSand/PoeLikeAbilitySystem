using System;
using System.Diagnostics;
using Combat.Runtime.Commands;
using Combat.Runtime.Model;
using Combat.Runtime.Trace;

namespace Combat.Runtime.GraphRuntime
{
    /// <summary>
    /// ExecPlanRunner is the "virtual machine" that executes ExecPlan operations.
    /// It processes Ops in sequence, reads/writes slot arrays, and generates Commands.
    /// Enforces execution budgets and depth limits to prevent runaway execution.
    /// </summary>
    public class ExecPlanRunner
    {
        private readonly BattleContext _battleContext;
        private readonly ITraceRecorder _traceRecorder;

        public ExecPlanRunner(BattleContext battleContext, ITraceRecorder traceRecorder = null)
        {
            _battleContext = battleContext ?? throw new ArgumentNullException(nameof(battleContext));
            _traceRecorder = traceRecorder;
        }

        /// <summary>
        /// Execute an ExecPlan in the given context.
        /// Returns true on success, false if budget was exceeded or an error occurred.
        /// </summary>
        public bool Execute(
            ExecPlan plan,
            ref ExecutionContext ctx,
            CommandBuffer commandBuffer)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (ctx.slots == null) throw new ArgumentNullException("ctx.slots");
            if (ctx.budget == null) throw new ArgumentNullException("ctx.budget");
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));

            long startTimestamp = Stopwatch.GetTimestamp();

            for (int i = 0; i < plan.operations.Length; i++)
            {
                // Check op budget before executing
                if (!ctx.budget.CanExecuteOp())
                {
                    UnityEngine.Debug.LogWarning($"[ExecPlanRunner] Op budget exceeded at op {i}/{plan.operations.Length}");
                    return false;
                }
                ctx.budget.opsExecuted++;

                ref Op op = ref plan.operations[i];

                // Trace: Record op begin
                long opStartTime = Stopwatch.GetTimestamp();
                _traceRecorder?.RecordOpBegin(i, op.opCode);

                // Dispatch based on OpCode
                switch (op.opCode)
                {
                    case OpCode.ConstNumber:
                        ExecuteConstNumber(ref op, ctx.slots);
                        break;

                    case OpCode.GetStat:
                        ExecuteGetStat(ref op, ctx.slots);
                        break;

                    case OpCode.Add:
                        ExecuteAdd(ref op, ctx.slots);
                        break;

                    case OpCode.Mul:
                        ExecuteMul(ref op, ctx.slots);
                        break;

                    case OpCode.MakeDamage:
                        ExecuteMakeDamage(ref op, ctx.slots, ctx.casterUnitId);
                        break;

                    case OpCode.EmitApplyDamage:
                        if (!ExecuteEmitApplyDamage(ref op, i, ctx, commandBuffer))
                            return false;
                        break;

                    case OpCode.GetCaster:
                        ExecuteGetCaster(ref op, ctx);
                        break;

                    case OpCode.GetTarget:
                        ExecuteGetTarget(ref op, ctx);
                        break;

                    default:
                        UnityEngine.Debug.LogError($"[ExecPlanRunner] Unknown OpCode: {op.opCode}");
                        return false;
                }

                // Trace: Record op end
                long opEndTime = Stopwatch.GetTimestamp();
                long opMicroseconds = (opEndTime - opStartTime) * 1_000_000 / Stopwatch.Frequency;
                _traceRecorder?.RecordOpEnd(i, opMicroseconds);
            }

            // Trace: Record total execution time
            long endTimestamp = Stopwatch.GetTimestamp();
            long totalMicroseconds = (endTimestamp - startTimestamp) * 1_000_000 / Stopwatch.Frequency;
            _traceRecorder?.EndTrace(totalMicroseconds);

            return true;
        }

        /// <summary>
        /// ConstNumber: Load a constant float into a number slot.
        /// Op: { a = floatAsInt, output = slotId }
        /// </summary>
        private void ExecuteConstNumber(ref Op op, SlotStorage slots)
        {
            float value = BitConverter.Int32BitsToSingle(op.a);
            slots.numbers[op.output] = value;
        }

        /// <summary>
        /// GetStat: Read a stat from a unit and store in a number slot.
        /// Op: { a = statType, b = entitySlot, output = numberSlot }
        /// </summary>
        private void ExecuteGetStat(ref Op op, SlotStorage slots)
        {
            UnitId entityId = slots.entities[op.b];
            StatType statType = (StatType)op.a;

            if (_battleContext.TryGetUnit(entityId, out Unit unit))
            {
                int statValue = unit.Stats.GetStat(statType);
                slots.numbers[op.output] = statValue;
            }
            else
            {
                // Unit not found, return 0
                UnityEngine.Debug.LogWarning($"[ExecPlanRunner] GetStat: Unit {entityId.Value} not found");
                slots.numbers[op.output] = 0f;
            }
        }

        /// <summary>
        /// Add: Add two numbers.
        /// Op: { a = slotA, b = slotB, output = slotOut }
        /// </summary>
        private void ExecuteAdd(ref Op op, SlotStorage slots)
        {
            slots.numbers[op.output] = slots.numbers[op.a] + slots.numbers[op.b];
        }

        /// <summary>
        /// Mul: Multiply two numbers.
        /// Op: { a = slotA, b = slotB, output = slotOut }
        /// </summary>
        private void ExecuteMul(ref Op op, SlotStorage slots)
        {
            slots.numbers[op.output] = slots.numbers[op.a] * slots.numbers[op.b];
        }

        /// <summary>
        /// MakeDamage: Construct a DamageSpec from amount and target.
        /// Op: { a = amountSlot, b = targetSlot, output = damageSpecSlot }
        /// Note: Currently uses DamageType.Physical by default. Future: add op.c for type.
        /// </summary>
        private void ExecuteMakeDamage(ref Op op, SlotStorage slots, UnitId casterUnitId)
        {
            int amount = (int)slots.numbers[op.a];
            UnitId targetId = slots.entities[op.b];

            slots.damageSpecs[op.output] = new DamageSpec(
                sourceUnitId: casterUnitId,
                targetUnitId: targetId,
                baseValue: amount,
                damageType: DamageType.Physical
            );
        }

        /// <summary>
        /// EmitApplyDamage: Generate an ApplyDamageCommand from a DamageSpec.
        /// Op: { a = damageSpecSlot }
        /// </summary>
        private bool ExecuteEmitApplyDamage(ref Op op, int opIndex, ExecutionContext ctx, CommandBuffer commandBuffer)
        {
            if (!ctx.budget.CanEmitCommand())
            {
                UnityEngine.Debug.LogWarning("[ExecPlanRunner] Command budget exceeded");
                return false;
            }

            DamageSpec spec = ctx.slots.damageSpecs[op.a];
            var command = new ApplyDamageCommand(spec);
            commandBuffer.Enqueue(command);
            ctx.budget.commandsEmitted++;

            // Trace: Record command emission
            _traceRecorder?.RecordCommand(command, opIndex);

            return true;
        }

        /// <summary>
        /// GetCaster: Load the caster UnitId into an entity slot.
        /// Op: { output = entitySlot }
        /// </summary>
        private void ExecuteGetCaster(ref Op op, ExecutionContext ctx)
        {
            ctx.slots.entities[op.output] = ctx.casterUnitId;
        }

        /// <summary>
        /// GetTarget: Load the target UnitId into an entity slot.
        /// Op: { output = entitySlot }
        /// </summary>
        private void ExecuteGetTarget(ref Op op, ExecutionContext ctx)
        {
            ctx.slots.entities[op.output] = ctx.targetUnitId;
        }
    }
}
