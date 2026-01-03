using System;

namespace Combat.Runtime.GraphRuntime
{
    public sealed class ExecPlan
    {
        public readonly ulong planHash;
        public readonly Op[] operations;
        public readonly SlotLayout slotLayout;

        public ExecPlan(ulong planHash, Op[] operations, SlotLayout slotLayout)
        {
            this.planHash = planHash;
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.slotLayout = slotLayout;
        }
    }
}

