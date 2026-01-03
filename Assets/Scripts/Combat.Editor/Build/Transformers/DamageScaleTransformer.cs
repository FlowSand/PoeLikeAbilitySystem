using Combat.Runtime.GraphIR;
using Combat.Runtime.Build;
using Combat.Runtime.Model;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Editor.Build.Transformers
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;
    /// <summary>
    /// Damage Scale Transformer.
    /// Scales all ConstNumber nodes' values by a multiplier.
    ///
    /// Example: With multiplier=0.7, a 100 damage skill becomes 70 damage.
    /// </summary>
    public class DamageScaleTransformer : IGraphTransformer, IParameterizedTransformer
    {
        private float damageMultiplier = 0.7f;

        public void SetParameters(List<SupportParam> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].key == "damageMultiplier")
                {
                    damageMultiplier = parameters[i].floatValue;
                }
            }
        }

        public bool CanApply(GraphIRModel graph, BuildContext context)
        {
            // Always applicable (no tag requirements)
            return true;
        }

        public GraphIRModel Apply(GraphIRModel sourceGraph, BuildContext context)
        {
            // Clone the graph (never modify source)
            GraphIRModel graph = GraphTransformUtils.CloneGraph(sourceGraph);

            // Find all ConstNumber nodes
            List<IRNode> constNodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.ConstNumber);

            if (constNodes.Count == 0)
            {
                Debug.LogWarning("[DamageScaleTransformer] No ConstNumber nodes found");
                return graph;
            }

            // Scale each ConstNumber node's value
            for (int i = 0; i < constNodes.Count; i++)
            {
                IRNode node = constNodes[i];

                if (node.intParams != null && node.intParams.ContainsKey("value"))
                {
                    // intParams stores floats as int bits, need to convert
                    int valueBits = node.intParams["value"];
                    float originalValue = System.BitConverter.Int32BitsToSingle(valueBits);
                    float scaledValue = originalValue * damageMultiplier;
                    int scaledBits = System.BitConverter.SingleToInt32Bits(scaledValue);

                    GraphTransformUtils.ModifyIntParam(node, "value", scaledBits);

                    Debug.Log($"[DamageScaleTransformer] Scaled {node.nodeId}: {originalValue} → {scaledValue}");
                }
            }

            Debug.Log($"[DamageScaleTransformer] Scaled {constNodes.Count} ConstNumber node(s) by {damageMultiplier}x");

            return graph;
        }
    }
}
