using Combat.Runtime.GraphIR;
using Combat.Runtime.Build;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Editor.Build
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    /// <summary>
    /// Build assembler: orchestrates application of all Support transformations.
    /// This is the main entry point for the build-time transformation pipeline.
    /// </summary>
    public static class BuildAssembler
    {
        /// <summary>
        /// Main entry point: apply build transformations to a GraphIR.
        /// </summary>
        /// <param name="sourceGraph">Source GraphIR (will not be modified)</param>
        /// <param name="context">Build context with supports and configuration</param>
        /// <returns>Transformed GraphIR, or clone of source if no supports</returns>
        public static GraphIRModel Assemble(GraphIRModel sourceGraph, BuildContext context)
        {
            if (sourceGraph == null)
            {
                Debug.LogError("[BuildAssembler] Source graph is null");
                return null;
            }

            if (context == null)
            {
                Debug.LogWarning("[BuildAssembler] Build context is null, returning cloned graph");
                return GraphTransformUtils.CloneGraph(sourceGraph);
            }

            if (context.supports == null || context.supports.Count == 0)
            {
                // No supports, return cloned graph
                if (context.options.logTransforms)
                {
                    Debug.Log("[BuildAssembler] No supports to apply");
                }
                return GraphTransformUtils.CloneGraph(sourceGraph);
            }

            // Step 1: Sort supports by priority (lower priority = execute first)
            List<SupportDefinition> sortedSupports = SortSupportsByPriority(context.supports);

            if (context.options.logTransforms)
            {
                Debug.Log($"[BuildAssembler] Applying {sortedSupports.Count} support(s):");
                for (int i = 0; i < sortedSupports.Count; i++)
                {
                    Debug.Log($"  {i + 1}. {sortedSupports[i].displayName} (priority: {sortedSupports[i].priority})");
                }
            }

            // Step 2: Clone source graph (first transform works on clone)
            GraphIRModel currentGraph = GraphTransformUtils.CloneGraph(sourceGraph);

            // Step 3: Apply each transformer in sequence
            int appliedCount = 0;
            for (int i = 0; i < sortedSupports.Count; i++)
            {
                SupportDefinition support = sortedSupports[i];
                IGraphTransformer transformer = support.GetTransformer();

                if (transformer == null)
                {
                    Debug.LogWarning($"[BuildAssembler] Support '{support.displayName}' has no valid transformer");
                    continue;
                }

                // Check if transformer can be applied
                if (!transformer.CanApply(currentGraph, context))
                {
                    if (context.options.logTransforms)
                    {
                        Debug.Log($"[BuildAssembler] Support '{support.displayName}' not applicable (CanApply returned false)");
                    }
                    continue;
                }

                // Apply transformation
                GraphIRModel transformedGraph = null;
                try
                {
                    transformedGraph = transformer.Apply(currentGraph, context);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BuildAssembler] Support '{support.displayName}' threw exception:\n{ex.Message}\n{ex.StackTrace}");
                    continue; // Skip this support
                }

                if (transformedGraph == null)
                {
                    Debug.LogError($"[BuildAssembler] Support '{support.displayName}' returned null graph!");
                    continue;
                }

                // Validate transformed graph (if validation enabled)
                if (context.options.enableValidation)
                {
                    ValidationResult validation = GraphIRValidator.Validate(transformedGraph);
                    if (!validation.isValid)
                    {
                        Debug.LogError($"[BuildAssembler] Support '{support.displayName}' produced invalid graph:");
                        foreach (var error in validation.errors)
                        {
                            Debug.LogError($"  [{error.nodeId}] {error.message}");
                        }
                        continue; // Skip this support, keep current graph
                    }
                }

                // Transformation successful, update current graph
                currentGraph = transformedGraph;
                appliedCount++;

                if (context.options.logTransforms)
                {
                    Debug.Log($"[BuildAssembler] ✓ Applied support '{support.displayName}' (nodes: {currentGraph.nodes.Count}, edges: {currentGraph.edges.Count})");
                }
            }

            if (context.options.logTransforms)
            {
                Debug.Log($"[BuildAssembler] Assembly complete: {appliedCount}/{sortedSupports.Count} supports applied");
            }

            return currentGraph;
        }

        /// <summary>
        /// Sort supports by priority (ascending order).
        /// Uses simple bubble sort to avoid LINQ (GC-friendly).
        /// </summary>
        private static List<SupportDefinition> SortSupportsByPriority(List<SupportDefinition> supports)
        {
            List<SupportDefinition> sorted = new List<SupportDefinition>(supports);

            // Bubble sort (ascending priority)
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                for (int j = 0; j < sorted.Count - i - 1; j++)
                {
                    if (sorted[j].priority > sorted[j + 1].priority)
                    {
                        // Swap
                        SupportDefinition temp = sorted[j];
                        sorted[j] = sorted[j + 1];
                        sorted[j + 1] = temp;
                    }
                }
            }

            return sorted;
        }
    }
}
