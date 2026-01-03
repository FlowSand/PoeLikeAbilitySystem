using GraphProcessor;
using UnityEngine;
using System.Collections.Generic;
using Combat.Runtime.Build;

namespace Combat.Editor.GraphAuthoring
{
    /// <summary>
    /// Effect Graph Asset - NGP graph for skill/effect authoring.
    /// This is the designer-facing entry point for creating combat effects.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Effect Graph", fileName = "EffectGraph")]
    public class EffectGraphAsset : BaseGraph
    {
        /// <summary>
        /// Graph version for migration support.
        /// </summary>
        public int graphVersion = 1;

        /// <summary>
        /// Entry event type: OnCast, OnHit, OnKill, etc.
        /// Determines when this effect graph is executed.
        /// </summary>
        public string entryEventType = "OnHit";

        [Header("Build Configuration")]
        /// <summary>
        /// Skill tags for conditional Support application.
        /// </summary>
        [Tooltip("Tags that describe this skill (e.g., 'Projectile', 'Fire', 'AOE')")]
        public List<string> skillTags = new List<string>();

        /// <summary>
        /// Support definitions to apply during build.
        /// </summary>
        [Tooltip("Support Gems that modify this skill's behavior")]
        public List<SupportDefinition> supports = new List<SupportDefinition>();
    }
}
