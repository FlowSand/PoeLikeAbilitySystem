using Combat.Editor.GraphAuthoring;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor.GraphBuild
{
    /// <summary>
    /// Unity menu items for baking EffectGraphAssets.
    /// </summary>
    public static class BakeMenuItem
    {
        /// <summary>
        /// Validate that the selected object is an EffectGraphAsset.
        /// </summary>
        [MenuItem("Assets/Combat/Bake Effect Graph", validate = true)]
        private static bool ValidateBake()
        {
            return Selection.activeObject is EffectGraphAsset;
        }

        /// <summary>
        /// Bake the selected EffectGraphAsset.
        /// </summary>
        [MenuItem("Assets/Combat/Bake Effect Graph")]
        private static void BakeSelected()
        {
            var graphAsset = Selection.activeObject as EffectGraphAsset;
            if (graphAsset == null)
            {
                Debug.LogError("[Bake] No EffectGraphAsset selected");
                return;
            }

            var execPlan = ExecPlanBaker.Bake(graphAsset);
            if (execPlan != null)
            {
                // Ping the created asset in the Project window
                EditorGUIUtility.PingObject(execPlan);
                Selection.activeObject = execPlan;
            }
        }
    }
}
