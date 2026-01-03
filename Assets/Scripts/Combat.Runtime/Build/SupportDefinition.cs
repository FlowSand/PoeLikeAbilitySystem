using System;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Runtime.Build
{
    /// <summary>
    /// Support definition (ScriptableObject asset).
    /// Defines a Support Gem that transforms GraphIR at build time.
    /// Path: Assets/GraphAssets/Supports/
    /// </summary>
    [CreateAssetMenu(fileName = "Support_", menuName = "Combat/Support Definition")]
    public class SupportDefinition : ScriptableObject
    {
        [Header("Basic Info")]
        public string supportId;           // e.g., "support_damage_scale"
        public string displayName;         // e.g., "Damage Scale Support"

        [Header("Application Conditions")]
        public List<string> requiredTags;  // GraphIR must contain these tags
        public List<string> forbiddenTags; // GraphIR must NOT contain these tags

        [Header("Execution")]
        [Range(0, 100)]
        public int priority;               // Lower priority executes first

        [Tooltip("Full type name for reflection, e.g., 'Combat.Editor.Build.Transformers.DamageScaleTransformer'")]
        public string transformerTypeName;

        [Header("Parameters")]
        public List<SupportParam> parameters; // Configurable parameters

        // Cached transformer instance (created at editor time)
        [NonSerialized]
        private IGraphTransformer cachedTransformer;

        public SupportDefinition()
        {
            requiredTags = new List<string>();
            forbiddenTags = new List<string>();
            parameters = new List<SupportParam>();
            priority = 50;
        }

        /// <summary>
        /// Get or create the transformer instance.
        /// Uses reflection to instantiate the transformer class.
        /// </summary>
        public IGraphTransformer GetTransformer()
        {
            if (cachedTransformer == null && !string.IsNullOrEmpty(transformerTypeName))
            {
                Type type = Type.GetType(transformerTypeName);
                if (type != null && typeof(IGraphTransformer).IsAssignableFrom(type))
                {
                    cachedTransformer = (IGraphTransformer)Activator.CreateInstance(type);

                    // Inject parameters if transformer supports it
                    if (cachedTransformer is IParameterizedTransformer parameterized)
                    {
                        parameterized.SetParameters(parameters);
                    }
                }
                else
                {
                    Debug.LogError($"[SupportDefinition] Invalid transformer type: {transformerTypeName}");
                }
            }
            return cachedTransformer;
        }

        /// <summary>
        /// Clear cached transformer (useful when parameters change in editor).
        /// </summary>
        public void ClearCache()
        {
            cachedTransformer = null;
        }
    }

    /// <summary>
    /// Serializable parameter for Support configuration.
    /// </summary>
    [Serializable]
    public class SupportParam
    {
        public string key;
        public ParamType type;
        public string stringValue;
        public float floatValue;
        public int intValue;
        public bool boolValue;
    }

    /// <summary>
    /// Parameter type enum for SupportParam.
    /// </summary>
    public enum ParamType
    {
        String,
        Float,
        Int,
        Bool
    }
}
