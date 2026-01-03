using System.Diagnostics;
using Combat.Runtime.Commands;
using Combat.Runtime.GraphRuntime;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Concrete implementation of ITraceRecorder.
    /// Records execution traces using high-resolution Stopwatch timing.
    /// Designed for minimal GC allocations and performance overhead.
    /// </summary>
    public class TraceRecorder : ITraceRecorder
    {
        private readonly bool _captureSlots;
        private ExecutionTrace _trace;
        private Stopwatch _stopwatch;
        private OpCode _currentOpCode;
        private int _currentOpIndex;

        /// <summary>
        /// Create a new TraceRecorder.
        /// </summary>
        /// <param name="captureSlots">If true, captures slot values at each step (expensive). Default: false for MVP.</param>
        public TraceRecorder(bool captureSlots = false)
        {
            _captureSlots = captureSlots;
            _trace = new ExecutionTrace();
            _stopwatch = new Stopwatch();
        }

        public void BeginTrace(ExecutionContext ctx, ulong planHash, string sourceGraphId)
        {
            _trace = new ExecutionTrace
            {
                eventType = ctx.eventType,
                rootEventId = ctx.rootEventId,
                triggerDepth = ctx.triggerDepth,
                randomSeed = ctx.randomSeed,
                sourceGraphId = sourceGraphId,
                planHash = planHash,
                casterUnitId = ctx.casterUnitId,
                targetUnitId = ctx.targetUnitId
            };

            _stopwatch.Restart();
        }

        public void RecordOpBegin(int opIndex, OpCode opCode)
        {
            _currentOpIndex = opIndex;
            _currentOpCode = opCode;
            // For MVP, we only record timing in RecordOpEnd.
            // This method exists for future enhancements (e.g., slot snapshots).
        }

        public void RecordOpEnd(int opIndex, long microseconds)
        {
            var record = new OpExecutionRecord(
                opIndex: opIndex,
                opCode: _currentOpCode.ToString(),
                microseconds: microseconds
            );

            _trace.opExecutions.Add(record);
            _trace.totalOpsExecuted++;
        }

        public void RecordCommand(ICombatCommand command, int opIndex)
        {
            if (command == null)
                return;

            string commandType = command.GetType().Name;
            string commandData = SerializeCommand(command);

            var record = new CommandRecord(
                commandType: commandType,
                commandData: commandData,
                emittedAtOpIndex: opIndex
            );

            _trace.commands.Add(record);
            _trace.totalCommandsEmitted++;
        }

        public void EndTrace(long totalMicroseconds)
        {
            _trace.totalExecutionMicroseconds = totalMicroseconds;
            _stopwatch.Stop();
        }

        public ExecutionTrace GetTrace()
        {
            return _trace;
        }

        /// <summary>
        /// Serialize a command to string for trace storage.
        /// Uses simple ToString() for MVP; can be enhanced with JSON serialization.
        /// </summary>
        private string SerializeCommand(ICombatCommand command)
        {
            // For MVP, use ToString()
            // For better debugging, could use JsonUtility or custom serialization
            return command.ToString();
        }
    }
}
