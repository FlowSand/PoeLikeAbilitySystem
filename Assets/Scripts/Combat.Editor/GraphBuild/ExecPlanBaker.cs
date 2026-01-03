using System;
using Combat.Editor.GraphAuthoring;
using Combat.Runtime.GraphIR;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Build;
using Combat.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor.GraphBuild
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    /// <summary>
    /// Bakes EffectGraphAsset into ExecPlanAsset.
    /// Pipeline: EffectGraphAsset → GraphIR → [BuildAssembler] → ExecPlan → ExecPlanAsset
    /// </summary>
    public static class ExecPlanBaker
    {
        /// <summary>
        /// Bake an EffectGraphAsset into an ExecPlanAsset.
        /// Returns null if baking fails.
        /// </summary>
        public static ExecPlanAsset Bake(EffectGraphAsset graphAsset)
        {
            if (graphAsset == null)
            {
                Debug.LogError("[Bake] GraphAsset is null");
                return null;
            }

            Debug.Log($"[Bake] Starting bake for '{graphAsset.name}'...");

            // Step 1: Export to GraphIR
            GraphIRModel ir;
            ValidationResult validation;
            try
            {
                (ir, validation) = GraphIRExporter.Export(graphAsset);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bake] Export failed: {ex.Message}\n{ex.StackTrace}");
                return null;
            }

            // Step 2: Validate GraphIR
            if (!validation.isValid)
            {
                Debug.LogError($"[Bake] GraphIR validation failed:");
                foreach (var error in validation.errors)
                {
                    Debug.LogError($"  - {error}");
                }
                return null;
            }

            Debug.Log($"[Bake] GraphIR validation passed (nodes: {ir.nodes.Count}, edges: {ir.edges.Count})");

            // Step 2.5: Apply Build Transformations (Support System)
            if (graphAsset.supports != null && graphAsset.supports.Count > 0)
            {
                BuildContext buildContext = new BuildContext
                {
                    skillId = graphAsset.name,
                    skillTags = new System.Collections.Generic.List<string>(graphAsset.skillTags),
                    supports = new System.Collections.Generic.List<SupportDefinition>(graphAsset.supports)
                };

                ir = BuildAssembler.Assemble(ir, buildContext);

                // Re-validate after transformations
                validation = GraphIRValidator.Validate(ir);
                if (!validation.isValid)
                {
                    Debug.LogError($"[Bake] Post-transform validation failed:");
                    foreach (var error in validation.errors)
                    {
                        Debug.LogError($"  [{error.nodeId}] {error.message}");
                    }
                    return null;
                }

                Debug.Log($"[Bake] Applied {buildContext.supports.Count} support(s), validation passed");
            }

            // Step 3: Compile to ExecPlan
            ExecPlan plan;
            string[] opToNodeId;
            try
            {
                (plan, opToNodeId) = ExecPlanCompiler.Compile(ir);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bake] ExecPlan compilation failed: {ex.Message}\n{ex.StackTrace}");
                return null;
            }

            Debug.Log($"[Bake] Compilation succeeded (hash: {plan.planHash:X16}, ops: {plan.operations.Length}, nodeMapping: {opToNodeId.Length})");

            // Step 4: Create or update ExecPlanAsset
            string assetName = $"{graphAsset.name}_ExecPlan";
            string path = $"Assets/Generated/ExecPlans/{assetName}.asset";

            // Incremental build: check if existing asset has same hash
            var existing = AssetDatabase.LoadAssetAtPath<ExecPlanAsset>(path);
            if (existing != null && existing.PlanHash == plan.planHash)
            {
                Debug.Log($"[Bake] ExecPlan unchanged (hash: {plan.planHash:X16}), skipping asset creation");
                return existing;
            }

            // Create new asset
            var asset = ScriptableObject.CreateInstance<ExecPlanAsset>();
            asset.Initialize(plan, ir.graphId, ir.version, opToNodeId);

            // Save asset
            if (existing != null)
            {
                // Update existing asset
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Bake] Updated ExecPlan asset at '{path}' (hash: {plan.planHash:X16})");
                return existing;
            }
            else
            {
                // Create new asset
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Bake] Created ExecPlan asset at '{path}' (hash: {plan.planHash:X16})");
                return asset;
            }
        }
    }
}
