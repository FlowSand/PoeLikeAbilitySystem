using Combat.Runtime.GraphIR;
using Combat.Runtime.Build;
using Combat.Runtime.Model;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Editor.Build.Transformers
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;
    /// <summary>
    /// Elemental Conversion Transformer (Fire → Cold).
    /// Converts all Fire damage to Cold damage.
    ///
    /// Conditions:
    /// - GraphIR must contain "Fire" tag (via skillTags or node tags)
    ///
    /// Transformation:
    /// - Finds all MakeDamageSpec nodes
    /// - Changes damageType parameter from Fire (1) to Cold (2)
    /// - Updates node tags (removes "Fire", adds "Cold")
    /// </summary>
    public class ElementalConversionTransformer : IGraphTransformer, IParameterizedTransformer
    {
        private string fromElement = SkillTags.Fire;
        private string toElement = SkillTags.Cold;

        public void SetParameters(List<SupportParam> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].key == "fromElement")
                {
                    fromElement = parameters[i].stringValue;
                }
                else if (parameters[i].key == "toElement")
                {
                    toElement = parameters[i].stringValue;
                }
            }
        }

        public bool CanApply(GraphIRModel graph, BuildContext context)
        {
            // Check if skill has the source element tag
            bool hasSourceTag = false;

            // Check context skill tags
            if (context != null && context.skillTags != null)
            {
                hasSourceTag = context.skillTags.Contains(fromElement);
            }

            // Also check if any node in the graph has the tag
            if (!hasSourceTag)
            {
                hasSourceTag = GraphTransformUtils.GraphContainsTag(graph, fromElement);
            }

            return hasSourceTag;
        }

        public GraphIRModel Apply(GraphIRModel sourceGraph, BuildContext context)
        {
            // Clone the graph
            GraphIRModel graph = GraphTransformUtils.CloneGraph(sourceGraph);

            // Find all MakeDamageSpec nodes
            List<IRNode> damageNodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.MakeDamageSpec);

            if (damageNodes.Count == 0)
            {
                Debug.LogWarning("[ElementalConversionTransformer] No MakeDamageSpec nodes found");
                return graph;
            }

            int convertedCount = 0;

            for (int i = 0; i < damageNodes.Count; i++)
            {
                IRNode node = damageNodes[i];

                if (node.intParams != null && node.intParams.ContainsKey("damageType"))
                {
                    int currentType = node.intParams["damageType"];
                    DamageType sourceDamageType = GetDamageTypeFromElement(fromElement);

                    // Only convert if current type matches source element
                    if (currentType == (int)sourceDamageType)
                    {
                        DamageType targetDamageType = GetDamageTypeFromElement(toElement);
                        GraphTransformUtils.ModifyIntParam(node, "damageType", (int)targetDamageType);

                        // Update node tags
                        GraphTransformUtils.RemoveTag(node, fromElement);
                        GraphTransformUtils.AddTag(node, toElement);

                        convertedCount++;
                        Debug.Log($"[ElementalConversionTransformer] Converted {node.nodeId}: {fromElement} → {toElement}");
                    }
                }
            }

            if (convertedCount > 0)
            {
                Debug.Log($"[ElementalConversionTransformer] Converted {convertedCount} damage node(s) from {fromElement} to {toElement}");
            }
            else
            {
                Debug.LogWarning($"[ElementalConversionTransformer] No {fromElement} damage nodes found to convert");
            }

            return graph;
        }

        /// <summary>
        /// Map element tag to DamageType enum.
        /// </summary>
        private DamageType GetDamageTypeFromElement(string elementTag)
        {
            switch (elementTag)
            {
                case SkillTags.Physical:
                    return DamageType.Physical;
                case SkillTags.Fire:
                    return DamageType.Fire;
                case SkillTags.Cold:
                    return DamageType.Cold;
                case SkillTags.Lightning:
                    return DamageType.Lightning;
                case SkillTags.Chaos:
                    return DamageType.Chaos;
                default:
                    Debug.LogWarning($"[ElementalConversionTransformer] Unknown element tag: {elementTag}, defaulting to Physical");
                    return DamageType.Physical;
            }
        }
    }
}
