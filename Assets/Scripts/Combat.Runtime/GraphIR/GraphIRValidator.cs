using System;
using System.Collections.Generic;

namespace Combat.Runtime.GraphIR
{
    public static class GraphIRValidator
    {
        private const string GraphNodeId = "<graph>";

        public static ValidationResult Validate(GraphIR graph)
        {
            var result = new ValidationResult();

            if (graph == null)
            {
                result.AddError(GraphNodeId, "GraphIR is null.");
                return result;
            }

            var nodeById = new Dictionary<string, IRNode>(16);
            if (graph.nodes != null)
            {
                int nodeCount = graph.nodes.Count;
                nodeById = new Dictionary<string, IRNode>(nodeCount);
                for (int i = 0; i < nodeCount; i++)
                {
                    var node = graph.nodes[i];
                    if (node == null)
                    {
                        result.AddError(GraphNodeId, "Node is null at index " + i.ToString() + ".");
                        continue;
                    }

                    if (string.IsNullOrEmpty(node.nodeId))
                    {
                        result.AddError(GraphNodeId, "Node has empty nodeId at index " + i.ToString() + ".");
                        continue;
                    }

                    if (nodeById.ContainsKey(node.nodeId))
                    {
                        result.AddError(node.nodeId, "Duplicate nodeId.");
                        continue;
                    }

                    nodeById.Add(node.nodeId, node);
                }
            }

            if (string.IsNullOrEmpty(graph.entryNodeId))
            {
                result.AddError(GraphNodeId, "entryNodeId is null or empty.");
            }
            else if (!nodeById.ContainsKey(graph.entryNodeId))
            {
                result.AddError(graph.entryNodeId, "entryNodeId does not exist in nodes.");
            }

            ValidateEdges(graph.edges, nodeById, result);
            ValidateDag(graph.edges, nodeById, result);

            return result;
        }

        private static void ValidateEdges(List<IREdge> edges, Dictionary<string, IRNode> nodeById, ValidationResult result)
        {
            if (edges == null) return;

            int edgeCount = edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                var edge = edges[i];
                if (edge == null)
                {
                    result.AddError(GraphNodeId, "Edge is null at index " + i.ToString() + ".");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.fromNodeId))
                {
                    result.AddError(GraphNodeId, "Edge has empty fromNodeId at index " + i.ToString() + ".");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.toNodeId))
                {
                    result.AddError(GraphNodeId, "Edge has empty toNodeId at index " + i.ToString() + ".");
                    continue;
                }

                if (!nodeById.TryGetValue(edge.fromNodeId, out var fromNode))
                {
                    result.AddError(edge.fromNodeId, FormatEdge(edge) + " fromNodeId does not exist.");
                    continue;
                }

                if (!nodeById.TryGetValue(edge.toNodeId, out var toNode))
                {
                    result.AddError(edge.toNodeId, FormatEdge(edge) + " toNodeId does not exist.");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.fromPort))
                {
                    result.AddError(fromNode.nodeId, FormatEdge(edge) + " fromPort is null or empty.");
                    continue;
                }

                if (string.IsNullOrEmpty(edge.toPort))
                {
                    result.AddError(toNode.nodeId, FormatEdge(edge) + " toPort is null or empty.");
                    continue;
                }

                if (fromNode.ports == null)
                {
                    result.AddError(fromNode.nodeId, "Node ports dictionary is null.");
                    continue;
                }

                if (toNode.ports == null)
                {
                    result.AddError(toNode.nodeId, "Node ports dictionary is null.");
                    continue;
                }

                if (!fromNode.ports.TryGetValue(edge.fromPort, out var fromPort))
                {
                    result.AddError(fromNode.nodeId, FormatEdge(edge) + " fromPort does not exist.");
                    continue;
                }

                if (!toNode.ports.TryGetValue(edge.toPort, out var toPort))
                {
                    result.AddError(toNode.nodeId, FormatEdge(edge) + " toPort does not exist.");
                    continue;
                }

                if (fromPort == null)
                {
                    result.AddError(fromNode.nodeId, FormatEdge(edge) + " fromPort is null.");
                    continue;
                }

                if (toPort == null)
                {
                    result.AddError(toNode.nodeId, FormatEdge(edge) + " toPort is null.");
                    continue;
                }

                if (fromPort.direction != IRPortDirection.Out)
                {
                    result.AddError(fromNode.nodeId, FormatEdge(edge) + " fromPort direction must be Out.");
                }

                if (toPort.direction != IRPortDirection.In)
                {
                    result.AddError(toNode.nodeId, FormatEdge(edge) + " toPort direction must be In.");
                }

                if (fromPort.portType != toPort.portType)
                {
                    result.AddError(
                        toNode.nodeId,
                        FormatEdge(edge) + " portType mismatch (" + fromPort.portType.ToString() + " -> " + toPort.portType.ToString() + ").");
                }
            }
        }

        private static void ValidateDag(List<IREdge> edges, Dictionary<string, IRNode> nodeById, ValidationResult result)
        {
            int nodeCount = nodeById.Count;
            if (nodeCount <= 1) return;

            var nodeIds = new string[nodeCount];
            var indexByNodeId = new Dictionary<string, int>(nodeCount);

            int index = 0;
            foreach (var kvp in nodeById)
            {
                nodeIds[index] = kvp.Key;
                indexByNodeId.Add(kvp.Key, index);
                index++;
            }

            var indegree = new int[nodeCount];
            List<int>[] outgoing = new List<int>[nodeCount];

            if (edges != null)
            {
                int edgeCount = edges.Count;
                for (int i = 0; i < edgeCount; i++)
                {
                    var edge = edges[i];
                    if (edge == null) continue;

                    if (!indexByNodeId.TryGetValue(edge.fromNodeId, out var fromIndex)) continue;
                    if (!indexByNodeId.TryGetValue(edge.toNodeId, out var toIndex)) continue;

                    var list = outgoing[fromIndex];
                    if (list == null)
                    {
                        list = new List<int>(2);
                        outgoing[fromIndex] = list;
                    }

                    list.Add(toIndex);
                    indegree[toIndex]++;
                }
            }

            var queue = new int[nodeCount];
            int head = 0;
            int tail = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                if (indegree[i] == 0)
                {
                    queue[tail] = i;
                    tail++;
                }
            }

            int visited = 0;
            while (head < tail)
            {
                int n = queue[head];
                head++;
                visited++;

                var list = outgoing[n];
                if (list == null) continue;

                int outCount = list.Count;
                for (int i = 0; i < outCount; i++)
                {
                    int m = list[i];
                    indegree[m]--;
                    if (indegree[m] == 0)
                    {
                        queue[tail] = m;
                        tail++;
                    }
                }
            }

            if (visited == nodeCount) return;

            string cycleNodeId = nodeIds[0];
            for (int i = 0; i < nodeCount; i++)
            {
                if (indegree[i] > 0)
                {
                    cycleNodeId = nodeIds[i];
                    break;
                }
            }

            result.AddError(cycleNodeId, "Graph contains a cycle; DAG required.");
        }

        private static string FormatEdge(IREdge edge)
        {
            return "Edge(" +
                   (edge.fromNodeId ?? "<null>") + "." + (edge.fromPort ?? "<null>") +
                   " -> " +
                   (edge.toNodeId ?? "<null>") + "." + (edge.toPort ?? "<null>") +
                   ")";
        }
    }
}

