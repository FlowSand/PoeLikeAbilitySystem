using System;
using System.Collections.Generic;
using Combat.Runtime.Model;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Complete execution trace of a single event processing.
    /// Captures event metadata, Op execution sequence, timing data, and commands emitted.
    /// Serializable to JSON for debugging and visualization.
    /// </summary>
    [Serializable]
    public class ExecutionTrace
    {
        // Event metadata
        public string eventType;           // e.g., "OnHitEvent", "OnCastEvent"
        public int rootEventId;            // Trigger chain root ID
        public int triggerDepth;           // Depth in trigger chain
        public uint randomSeed;            // Random seed for determinism

        // Graph/Plan identification
        public string sourceGraphId;       // GraphIR GUID (for mapping back to NGP graph)
        public ulong planHash;             // ExecPlan hash (for cache identification)
        public int graphVersion;           // Graph version number

        // Execution context
        public UnitId casterUnitId;        // Caster unit ID
        public UnitId targetUnitId;        // Target unit ID

        // Execution records
        public List<OpExecutionRecord> opExecutions;     // Sequential Op execution records
        public List<CommandRecord> commands;             // Commands emitted during execution

        // Performance metrics
        public long totalExecutionMicroseconds;   // Total execution time in microseconds
        public int totalOpsExecuted;              // Number of Ops executed
        public int totalCommandsEmitted;          // Number of commands emitted

        // Execution result
        public bool success;                      // Whether execution completed successfully
        public string errorMessage;               // Error message if failed

        public ExecutionTrace()
        {
            opExecutions = new List<OpExecutionRecord>();
            commands = new List<CommandRecord>();
            success = true;
            errorMessage = string.Empty;
        }
    }
}
