using System.Linq;
using Combat.Editor.GraphAuthoring;
using Combat.Runtime.Trace;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor.Trace
{
    /// <summary>
    /// Applies trace-based highlighting to EffectGraphWindow.
    /// MVP: Console logging. Future: Visual node highlighting via NGP extension.
    /// </summary>
    public static class NodeHighlighter
    {
        private static TraceHighlightData _currentHighlight;

        public static void ApplyHighlightToGraph(EffectGraphAsset graphAsset, TraceHighlightData highlightData)
        {
            if (graphAsset == null || highlightData == null)
            {
                Debug.LogWarning("[NodeHighlighter] Invalid arguments");
                return;
            }

            _currentHighlight = highlightData;

            // Open the graph window
            var window = EditorWindow.GetWindow<EffectGraphWindow>();
            window.InitializeGraph(graphAsset);
            window.Show();

            // MVP: Log executed nodes to console
            Debug.Log($"[NodeHighlighter] Highlighting {highlightData.executedNodeIds.Count} executed nodes:");

            foreach (var nodeId in highlightData.executedNodeIds)
            {
                // Find node in graph
                var node = graphAsset.nodes.FirstOrDefault(n => n.GUID == nodeId);

                if (node != null)
                {
                    // Get execution record for this node
                    var execRecord = highlightData.GetExecutionForNode(nodeId);

                    if (execRecord.HasValue)
                    {
                        Debug.Log($"  • {node.name} ({node.GetType().Name}) - " +
                                  $"OpCode: {execRecord.Value.opCode}, " +
                                  $"Time: {execRecord.Value.microseconds} μs");
                    }
                    else
                    {
                        Debug.Log($"  • {node.name} ({node.GetType().Name})");
                    }
                }
                else
                {
                    Debug.LogWarning($"  • Node with ID {nodeId} not found in graph");
                }
            }

            Debug.Log($"[NodeHighlighter] EffectGraphWindow opened for: {graphAsset.name}");

            // Future enhancement: Visual highlighting
            // Requires extending NGP's BaseNodeView to support custom rendering
            // See FUTURE_ENHANCEMENTS section in plan
        }

        public static OpExecutionRecord? GetHighlightForNode(string nodeGuid)
        {
            return _currentHighlight?.GetExecutionForNode(nodeGuid);
        }

        public static void ClearHighlight()
        {
            _currentHighlight = null;
            Debug.Log("[NodeHighlighter] Highlight cleared");
        }
    }
}
