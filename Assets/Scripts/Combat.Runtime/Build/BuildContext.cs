using System.Collections.Generic;

namespace Combat.Runtime.Build
{
    /// <summary>
    /// Build context (build-time only information).
    /// Contains all information needed to apply Support transformations.
    /// </summary>
    public class BuildContext
    {
        // === Skill Base Info ===
        public string skillId;
        public List<string> skillTags;

        // === Support Gems ===
        public List<SupportDefinition> supports;

        // === Future Extensions ===
        // Equipment affixes
        public List<string> equipmentAffixes;

        // Passive tree modifiers
        public List<string> passiveModifiers;

        // === Build Options ===
        public BuildOptions options;

        public BuildContext()
        {
            skillTags = new List<string>();
            supports = new List<SupportDefinition>();
            equipmentAffixes = new List<string>();
            passiveModifiers = new List<string>();
            options = new BuildOptions();
        }

        /// <summary>
        /// Generate a hash for caching (deterministic based on content).
        /// Used to determine if a cached ExecPlan can be reused.
        /// </summary>
        public int GetHash()
        {
            int hash = skillId != null ? skillId.GetHashCode() : 0;

            // Hash skill tags
            for (int i = 0; i < skillTags.Count; i++)
            {
                hash = hash * 31 + (skillTags[i] != null ? skillTags[i].GetHashCode() : 0);
            }

            // Hash support IDs
            for (int i = 0; i < supports.Count; i++)
            {
                if (supports[i] != null && supports[i].supportId != null)
                {
                    hash = hash * 31 + supports[i].supportId.GetHashCode();
                }
            }

            // Future: hash equipment affixes and passive modifiers

            return hash;
        }
    }

    /// <summary>
    /// Build options for controlling transform behavior.
    /// </summary>
    public class BuildOptions
    {
        /// <summary>
        /// Validate GraphIR after each transform.
        /// </summary>
        public bool enableValidation = true;

        /// <summary>
        /// Log each transform application.
        /// </summary>
        public bool logTransforms = true;

        /// <summary>
        /// Maximum number of transform passes (prevents infinite loops).
        /// </summary>
        public int maxTransformPasses = 10;
    }
}
