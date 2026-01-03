using Combat.Runtime.GraphIR;
using System;
using System.Collections.Generic;

namespace Combat.Editor.Build
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    /// <summary>
    /// Graph transformation utilities (static methods).
    /// All Transformers must use these utilities to manipulate GraphIR.
    /// </summary>
    public static class GraphTransformUtils
    {
        // ========================================
        // Deep Copy
        // ========================================

        /// <summary>
        /// Deep clone an entire GraphIR.
        /// </summary>
        public static GraphIRModel CloneGraph(GraphIRModel source)
        {
            if (source == null)
                return null;

            GraphIRModel clone = new GraphIRModel();
            clone.graphId = source.graphId;
            clone.version = source.version;
            clone.entryNodeId = source.entryNodeId;

            // Clone nodes
            clone.nodes = new List<IRNode>(source.nodes.Count);
            for (int i = 0; i < source.nodes.Count; i++)
            {
                clone.nodes.Add(CloneNode(source.nodes[i]));
            }

            // Clone edges
            clone.edges = new List<IREdge>(source.edges.Count);
            for (int i = 0; i < source.edges.Count; i++)
            {
                clone.edges.Add(CloneEdge(source.edges[i]));
            }

            return clone;
        }

        /// <summary>
        /// Deep clone a single IRNode.
        /// </summary>
        public static IRNode CloneNode(IRNode source)
        {
            if (source == null)
                return null;

            IRNode clone = new IRNode
            {
                nodeId = source.nodeId,
                nodeType = source.nodeType
            };

            // Clone ports
            if (source.ports != null)
            {
                foreach (var kvp in source.ports)
                {
                    clone.ports[kvp.Key] = ClonePort(kvp.Value);
                }
            }

            // Clone intParams
            if (source.intParams != null)
            {
                foreach (var kvp in source.intParams)
                {
                    clone.intParams[kvp.Key] = kvp.Value;
                }
            }

            // Clone tags
            if (source.tags != null)
            {
                for (int i = 0; i < source.tags.Count; i++)
                {
                    clone.tags.Add(source.tags[i]);
                }
            }

            return clone;
        }

        /// <summary>
        /// Clone an IRPort.
        /// </summary>
        private static IRPort ClonePort(IRPort source)
        {
            if (source == null)
                return null;

            return new IRPort
            {
                portName = source.portName,
                portType = source.portType,
                direction = source.direction
            };
        }

        /// <summary>
        /// Clone an IREdge.
        /// </summary>
        private static IREdge CloneEdge(IREdge source)
        {
            if (source == null)
                return null;

            return new IREdge
            {
                fromNodeId = source.fromNodeId,
                fromPort = source.fromPort,
                toNodeId = source.toNodeId,
                toPort = source.toPort
            };
        }

        // ========================================
        // Find Nodes
        // ========================================

        /// <summary>
        /// Find a node by its ID.
        /// Returns null if not found.
        /// </summary>
        public static IRNode FindNodeById(GraphIRModel graph, string nodeId)
        {
            if (graph == null || graph.nodes == null || string.IsNullOrEmpty(nodeId))
                return null;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i].nodeId == nodeId)
                    return graph.nodes[i];
            }

            return null;
        }

        /// <summary>
        /// Find all nodes of a specific type.
        /// </summary>
        public static List<IRNode> FindNodesByType(GraphIRModel graph, IRNodeType nodeType)
        {
            List<IRNode> result = new List<IRNode>();

            if (graph == null || graph.nodes == null)
                return result;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i].nodeType == nodeType)
                {
                    result.Add(graph.nodes[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Find all nodes that contain a specific tag.
        /// </summary>
        public static List<IRNode> FindNodesByTag(GraphIRModel graph, string tag)
        {
            List<IRNode> result = new List<IRNode>();

            if (graph == null || graph.nodes == null || string.IsNullOrEmpty(tag))
                return result;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                IRNode node = graph.nodes[i];
                if (node.tags != null && node.tags.Contains(tag))
                {
                    result.Add(node);
                }
            }

            return result;
        }

        // ========================================
        // Find Edges
        // ========================================

        /// <summary>
        /// Find all edges targeting a specific node (incoming edges).
        /// </summary>
        public static List<IREdge> FindIncomingEdges(GraphIRModel graph, string nodeId)
        {
            List<IREdge> result = new List<IREdge>();

            if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeId))
                return result;

            for (int i = 0; i < graph.edges.Count; i++)
            {
                if (graph.edges[i].toNodeId == nodeId)
                {
                    result.Add(graph.edges[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Find all edges originating from a specific node (outgoing edges).
        /// </summary>
        public static List<IREdge> FindOutgoingEdges(GraphIRModel graph, string nodeId)
        {
            List<IREdge> result = new List<IREdge>();

            if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeId))
                return result;

            for (int i = 0; i < graph.edges.Count; i++)
            {
                if (graph.edges[i].fromNodeId == nodeId)
                {
                    result.Add(graph.edges[i]);
                }
            }

            return result;
        }

        // ========================================
        // Modify Parameters
        // ========================================

        /// <summary>
        /// Modify a single int parameter.
        /// Creates the parameter if it doesn't exist.
        /// </summary>
        public static void ModifyIntParam(IRNode node, string paramKey, int newValue)
        {
            if (node == null || string.IsNullOrEmpty(paramKey))
                return;

            if (node.intParams == null)
            {
                node.intParams = new Dictionary<string, int>();
            }

            node.intParams[paramKey] = newValue;
        }

        /// <summary>
        /// Modify multiple int parameters at once.
        /// </summary>
        public static void ModifyIntParams(IRNode node, Dictionary<string, int> newParams)
        {
            if (node == null || newParams == null)
                return;

            if (node.intParams == null)
            {
                node.intParams = new Dictionary<string, int>();
            }

            foreach (var kvp in newParams)
            {
                node.intParams[kvp.Key] = kvp.Value;
            }
        }

        // ========================================
        // Tag Operations
        // ========================================

        /// <summary>
        /// Add a tag to a node (if not already present).
        /// </summary>
        public static void AddTag(IRNode node, string tag)
        {
            if (node == null || string.IsNullOrEmpty(tag))
                return;

            if (node.tags == null)
            {
                node.tags = new List<string>();
            }

            if (!node.tags.Contains(tag))
            {
                node.tags.Add(tag);
            }
        }

        /// <summary>
        /// Remove a tag from a node.
        /// </summary>
        public static void RemoveTag(IRNode node, string tag)
        {
            if (node == null || node.tags == null || string.IsNullOrEmpty(tag))
                return;

            node.tags.Remove(tag);
        }

        /// <summary>
        /// Check if a node has a specific tag.
        /// </summary>
        public static bool HasTag(IRNode node, string tag)
        {
            if (node == null || node.tags == null || string.IsNullOrEmpty(tag))
                return false;

            return node.tags.Contains(tag);
        }

        /// <summary>
        /// Check if the graph contains any node with a specific tag.
        /// </summary>
        public static bool GraphContainsTag(GraphIRModel graph, string tag)
        {
            if (graph == null || graph.nodes == null || string.IsNullOrEmpty(tag))
                return false;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (HasTag(graph.nodes[i], tag))
                {
                    return true;
                }
            }

            return false;
        }

        // ========================================
        // Node Management
        // ========================================

        /// <summary>
        /// Add a node to the graph.
        /// </summary>
        public static void AddNode(GraphIRModel graph, IRNode node)
        {
            if (graph == null || node == null)
                return;

            if (graph.nodes == null)
            {
                graph.nodes = new List<IRNode>();
            }

            graph.nodes.Add(node);
        }

        /// <summary>
        /// Remove a node from the graph (does not remove connected edges).
        /// Returns true if node was found and removed.
        /// </summary>
        public static bool RemoveNode(GraphIRModel graph, string nodeId)
        {
            if (graph == null || graph.nodes == null || string.IsNullOrEmpty(nodeId))
                return false;

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i].nodeId == nodeId)
                {
                    graph.nodes.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        // ========================================
        // Edge Management
        // ========================================

        /// <summary>
        /// Add an edge to the graph.
        /// </summary>
        public static void AddEdge(GraphIRModel graph, IREdge edge)
        {
            if (graph == null || edge == null)
                return;

            if (graph.edges == null)
            {
                graph.edges = new List<IREdge>();
            }

            graph.edges.Add(edge);
        }

        /// <summary>
        /// Remove an edge from the graph.
        /// </summary>
        public static void RemoveEdge(GraphIRModel graph, IREdge edge)
        {
            if (graph == null || graph.edges == null || edge == null)
                return;

            graph.edges.Remove(edge);
        }

        /// <summary>
        /// Remove all edges connected to a specific node.
        /// </summary>
        public static void RemoveAllEdgesForNode(GraphIRModel graph, string nodeId)
        {
            if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeId))
                return;

            // Iterate backwards to safely remove during iteration
            for (int i = graph.edges.Count - 1; i >= 0; i--)
            {
                IREdge edge = graph.edges[i];
                if (edge.fromNodeId == nodeId || edge.toNodeId == nodeId)
                {
                    graph.edges.RemoveAt(i);
                }
            }
        }

        // ========================================
        // ID Generation
        // ========================================

        /// <summary>
        /// Generate a unique node ID.
        /// Format: prefix_guid (e.g., "node_a1b2c3d4")
        /// </summary>
        public static string GenerateNodeId(GraphIRModel graph, string prefix = "node")
        {
            if (string.IsNullOrEmpty(prefix))
                prefix = "node";

            // Generate GUID-based unique ID
            string uniqueId = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            // Ensure uniqueness (extremely unlikely to collide, but check anyway)
            while (FindNodeById(graph, uniqueId) != null)
            {
                uniqueId = prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            return uniqueId;
        }
    }
}
