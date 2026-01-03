using System;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Model;

namespace Combat.Runtime
{
    /// <summary>
    /// Execution context for a single ExecPlan run. Lightweight struct that references
    /// heap-allocated slot storage and budget tracker.
    /// </summary>
    public struct ExecutionContext
    {
        /// <summary>Root event ID for correlation and debugging.</summary>
        public int rootEventId;

        /// <summary>Current trigger depth for chain safety.</summary>
        public int triggerDepth;

        /// <summary>Deterministic random seed for this execution.</summary>
        public uint randomSeed;

        /// <summary>Event type name for trace identification (e.g., "OnHitEvent", "OnCastEvent").</summary>
        public string eventType;

        /// <summary>Source GraphIR GUID for trace → graph mapping.</summary>
        public string sourceGraphId;

        /// <summary>The caster/source unit (e.g. who cast the spell).</summary>
        public UnitId casterUnitId;

        /// <summary>The target unit (if applicable, may be default/invalid for AOE skills).</summary>
        public UnitId targetUnitId;

        /// <summary>Slot arrays for storing intermediate values during execution.</summary>
        public SlotStorage slots;

        /// <summary>Budget tracker shared across execution (reference type).</summary>
        public ExecutionBudget budget;

    }

    /// <summary>
    /// Storage for slot arrays used during ExecPlan execution.
    /// Separated from ExecutionContext to enable pooling/reuse (future optimization).
    /// </summary>
    public class SlotStorage
    {
        /// <summary>Number slots (floats).</summary>
        public float[] numbers;

        /// <summary>Entity ID slots (unit references).</summary>
        public UnitId[] entities;

        /// <summary>DamageSpec slots (structured damage data).</summary>
        public DamageSpec[] damageSpecs;

        /// <summary>
        /// Allocate slot arrays based on the ExecPlan's SlotLayout.
        /// MVP: Direct allocation. Future: Use ArrayPool for zero-GC.
        /// </summary>
        public static SlotStorage Rent(SlotLayout layout)
        {
            return new SlotStorage
            {
                numbers = new float[layout.numberSlotCount],
                entities = new UnitId[layout.entitySlotCount],
                damageSpecs = new DamageSpec[layout.damageSpecSlotCount]
            };
        }

        /// <summary>
        /// Clear all slots to default values. Call after execution completes.
        /// </summary>
        public void Clear()
        {
            Array.Clear(numbers, 0, numbers.Length);
            Array.Clear(entities, 0, entities.Length);
            Array.Clear(damageSpecs, 0, damageSpecs.Length);
        }
    }

    /// <summary>
    /// Tracks execution budget for a single event (or entire trigger chain).
    /// Class (reference type) so modifications are visible across ExecPlanRunner and caller.
    /// </summary>
    public class ExecutionBudget
    {
        /// <summary>Maximum ops allowed per event.</summary>
        public int maxOpsPerEvent;

        /// <summary>Maximum commands allowed per event.</summary>
        public int maxCommandsPerEvent;

        /// <summary>Current number of ops executed.</summary>
        public int opsExecuted;

        /// <summary>Current number of commands emitted.</summary>
        public int commandsEmitted;

        public ExecutionBudget(int maxOps, int maxCommands)
        {
            maxOpsPerEvent = maxOps;
            maxCommandsPerEvent = maxCommands;
            Reset();
        }

        /// <summary>Reset counters for a new event.</summary>
        public void Reset()
        {
            opsExecuted = 0;
            commandsEmitted = 0;
        }

        /// <summary>Check if we can execute another op without exceeding budget.</summary>
        public bool CanExecuteOp()
        {
            return opsExecuted < maxOpsPerEvent;
        }

        /// <summary>Check if we can emit another command without exceeding budget.</summary>
        public bool CanEmitCommand()
        {
            return commandsEmitted < maxCommandsPerEvent;
        }
    }
}
