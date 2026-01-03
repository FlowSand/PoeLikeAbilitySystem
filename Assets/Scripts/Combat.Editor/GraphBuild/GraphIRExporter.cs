using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Combat.Editor.GraphAuthoring;
using Combat.Runtime.GraphIR;
using Combat.Editor.GraphAuthoring.Nodes;
using GraphProcessor;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor.GraphBuild
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    /// <summary>
    /// Exports EffectGraphAsset (NGP graph) to GraphIR (intermediate representation).
    /// Validates the exported GraphIR and returns validation results.
    /// </summary>
    public static class GraphIRExporter
    {
        /// <summary>
        /// Export EffectGraphAsset to GraphIR and validate.
        /// </summary>
        public static (GraphIRModel ir, ValidationResult validation) Export(EffectGraphAsset graphAsset)
        {
            if (graphAsset == null)
                throw new ArgumentNullException(nameof(graphAsset));

            var ir = new GraphIRModel
            {
                graphId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(graphAsset)),
                version = graphAsset.graphVersion,
                nodes = new List<IRNode>(),
                edges = new List<IREdge>()
            };

            // Convert nodes
            foreach (BaseNode node in graphAsset.nodes)
            {
                try
                {
                    var irNode = ConvertNode(node);
                    ir.nodes.Add(irNode);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to convert node '{node.name}' (GUID: {node.GUID}): {ex.Message}");
                    throw;
                }
            }

            // Convert edges
            foreach (var edge in graphAsset.edges)
            {
                ir.edges.Add(ConvertEdge(edge));
            }

            // Find entry node
            ir.entryNodeId = FindEntryNode(graphAsset);

            // Validate
            var validation = GraphIRValidator.Validate(ir);

            return (ir, validation);
        }

        private static IRNode ConvertNode(BaseNode node)
        {
            var irNode = new IRNode
            {
                nodeId = node.GUID,
                nodeType = NodeTypeRegistry.GetIRNodeType(node.GetType()),
                ports = ConvertPorts(node),
                intParams = ExtractParameters(node)
            };

            return irNode;
        }

        private static Dictionary<string, IRPort> ConvertPorts(BaseNode node)
        {
            var ports = new Dictionary<string, IRPort>();

            // Use reflection to find fields with [Input] or [Output] attributes
            var type = node.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Check for Input attribute
                var inputAttr = field.GetCustomAttribute<InputAttribute>();
                if (inputAttr != null)
                {
                    var portType = NodeTypeRegistry.GetIRPortType(field.FieldType);
                    // Use field name as key (NGP uses fieldName for edge identification)
                    ports[field.Name] = new IRPort
                    {
                        portName = field.Name, // Use C# field name
                        portType = portType,
                        direction = IRPortDirection.In
                    };
                }

                // Check for Output attribute
                var outputAttr = field.GetCustomAttribute<OutputAttribute>();
                if (outputAttr != null)
                {
                    var portType = NodeTypeRegistry.GetIRPortType(field.FieldType);
                    // Use field name as key (NGP uses fieldName for edge identification)
                    ports[field.Name] = new IRPort
                    {
                        portName = field.Name, // Use C# field name
                        portType = portType,
                        direction = IRPortDirection.Out
                    };
                }
            }

            return ports;
        }

        private static Dictionary<string, int> ExtractParameters(BaseNode node)
        {
            var parameters = new Dictionary<string, int>();

            // Extract parameters based on node type
            // Use pattern matching to avoid reflection overhead
            if (node is ConstNumberNode constNode)
            {
                parameters["value"] = BitConverter.SingleToInt32Bits(constNode.value);
            }
            else if (node is GetStatNode getStatNode)
            {
                parameters["statType"] = (int)getStatNode.statType;
            }
            else if (node is MakeDamageSpecNode damageNode)
            {
                parameters["damageType"] = (int)damageNode.damageType;
            }
            // Add more node types as needed

            return parameters;
        }

        private static IREdge ConvertEdge(SerializableEdge edge)
        {
            // Use fieldName instead of portIdentifier
            // NGP uses fieldName to identify ports, which corresponds to our [Input]/[Output] attribute names
            string fromPortName = edge.outputFieldName;
            string toPortName = edge.inputFieldName;

            // If portIdentifier is available and not empty, use it (for multi-port fields)
            if (!string.IsNullOrEmpty(edge.outputPortIdentifier))
                fromPortName = edge.outputPortIdentifier;
            if (!string.IsNullOrEmpty(edge.inputPortIdentifier))
                toPortName = edge.inputPortIdentifier;

            return new IREdge
            {
                fromNodeId = edge.outputNode.GUID,
                fromPort = fromPortName,
                toNodeId = edge.inputNode.GUID,
                toPort = toPortName
            };
        }

        private static string FindEntryNode(EffectGraphAsset graphAsset)
        {
            // Find the entry node based on entryEventType
            // For now, we look for OnHitEntryNode or OnCastEntryNode
            BaseNode entryNode = null;

            if (graphAsset.entryEventType == "OnHit")
            {
                entryNode = graphAsset.nodes.FirstOrDefault(n => n is OnHitEntryNode);
            }
            else if (graphAsset.entryEventType == "OnCast")
            {
                entryNode = graphAsset.nodes.FirstOrDefault(n => n is OnCastEntryNode);
            }

            if (entryNode == null)
            {
                throw new InvalidOperationException(
                    $"No entry node found for event type '{graphAsset.entryEventType}'. " +
                    $"Please add an appropriate entry node to the graph.");
            }

            return entryNode.GUID;
        }
    }
}
