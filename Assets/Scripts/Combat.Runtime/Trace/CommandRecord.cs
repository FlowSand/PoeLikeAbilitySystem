using System;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Record of a command emitted during execution.
    /// Captures command type, serialized data, and the Op that emitted it.
    /// </summary>
    [Serializable]
    public struct CommandRecord
    {
        public string commandType;        // Command type name (e.g., "ApplyDamageCommand")
        public string commandData;        // Serialized command data (JSON or string representation)
        public int emittedAtOpIndex;      // Op index that emitted this command

        public CommandRecord(string commandType, string commandData, int emittedAtOpIndex)
        {
            this.commandType = commandType;
            this.commandData = commandData;
            this.emittedAtOpIndex = emittedAtOpIndex;
        }
    }
}
