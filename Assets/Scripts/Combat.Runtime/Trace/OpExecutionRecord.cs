using System;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Record of a single Op execution.
    /// Captures Op index, type, and timing information.
    /// </summary>
    [Serializable]
    public struct OpExecutionRecord
    {
        public int opIndex;              // Index in ExecPlan.operations array
        public string opCode;            // OpCode name (e.g., "ConstNumber", "Add")
        public long microseconds;        // Execution time in microseconds

        // Future enhancements: slot values, parameters
        // public SlotSnapshot slotsBefore;
        // public SlotSnapshot slotsAfter;

        public OpExecutionRecord(int opIndex, string opCode, long microseconds)
        {
            this.opIndex = opIndex;
            this.opCode = opCode;
            this.microseconds = microseconds;
        }
    }
}
