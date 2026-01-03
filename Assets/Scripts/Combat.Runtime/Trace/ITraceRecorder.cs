using Combat.Runtime.Commands;
using Combat.Runtime.GraphRuntime;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Interface for recording execution traces.
    /// Implemented by TraceRecorder for production use.
    /// Can be mocked for testing or disabled by passing null to ExecPlanRunner.
    /// </summary>
    public interface ITraceRecorder
    {
        /// <summary>
        /// Begin recording a new trace.
        /// Called before ExecPlan execution starts.
        /// </summary>
        void BeginTrace(ExecutionContext ctx, ulong planHash, string sourceGraphId);

        /// <summary>
        /// Record the start of an Op execution.
        /// Called immediately before executing an Op.
        /// </summary>
        void RecordOpBegin(int opIndex, OpCode opCode);

        /// <summary>
        /// Record the completion of an Op execution.
        /// Called immediately after executing an Op.
        /// </summary>
        /// <param name="opIndex">Index of the Op that completed</param>
        /// <param name="microseconds">Execution time in microseconds</param>
        void RecordOpEnd(int opIndex, long microseconds);

        /// <summary>
        /// Record a command emission.
        /// Called when an Op emits a command (e.g., EmitApplyDamage).
        /// </summary>
        /// <param name="command">The command that was emitted</param>
        /// <param name="opIndex">Index of the Op that emitted the command</param>
        void RecordCommand(ICombatCommand command, int opIndex);

        /// <summary>
        /// End the current trace recording.
        /// Called after all Ops have been executed.
        /// </summary>
        /// <param name="totalMicroseconds">Total execution time in microseconds</param>
        void EndTrace(long totalMicroseconds);

        /// <summary>
        /// Get the recorded execution trace.
        /// Should be called after EndTrace().
        /// </summary>
        ExecutionTrace GetTrace();
    }
}
