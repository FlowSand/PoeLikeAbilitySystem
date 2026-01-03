using System.Collections.Generic;
using Combat.Runtime.Trace;

namespace Combat.Editor.Trace
{
    /// <summary>
    /// Data package for highlighting executed nodes in the graph editor.
    /// </summary>
    public class TraceHighlightData
    {
        public List<string> executedNodeIds;           // IRNode.nodeId (GUID) values
        public List<OpExecutionRecord> opExecutions;   // Full execution records
        public string[] opToNodeIdMapping;             // Op index → IRNode ID

        /// <summary>
        /// Find execution record for a given IRNode ID.
        /// </summary>
        public OpExecutionRecord? GetExecutionForNode(string nodeId)
        {
            if (opToNodeIdMapping == null || opExecutions == null)
                return null;

            for (int i = 0; i < opToNodeIdMapping.Length; i++)
            {
                if (opToNodeIdMapping[i] == nodeId)
                {
                    // Find corresponding OpExecutionRecord
                    foreach (var opExec in opExecutions)
                    {
                        if (opExec.opIndex == i)
                            return opExec;
                    }
                }
            }
            return null;
        }
    }
}
