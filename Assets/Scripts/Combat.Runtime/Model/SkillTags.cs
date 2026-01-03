namespace Combat.Runtime.Model
{
    /// <summary>
    /// Skill tag constants for categorizing nodes and abilities.
    /// Used by Support system for conditional transformations.
    /// </summary>
    public static class SkillTags
    {
        // === Projectile Types ===
        public const string Projectile = "Projectile";
        public const string AOE = "AOE";
        public const string Melee = "Melee";
        public const string Channeling = "Channeling";

        // === Element Types ===
        public const string Fire = "Fire";
        public const string Cold = "Cold";
        public const string Lightning = "Lightning";
        public const string Physical = "Physical";
        public const string Chaos = "Chaos";

        // === Target Types ===
        public const string SingleTarget = "SingleTarget";
        public const string MultiTarget = "MultiTarget";
        public const string TargetSelf = "TargetSelf";

        // === Effect Types ===
        public const string DOT = "DOT";                   // Damage Over Time
        public const string Modifier = "Modifier";         // Applies buff/debuff
        public const string Conversion = "Conversion";     // Converts damage type
        public const string Trigger = "Trigger";           // Can trigger other skills

        // === Damage Properties ===
        public const string Crit = "Crit";                 // Can critically strike
        public const string CannotCrit = "CannotCrit";     // Cannot crit
        public const string AlwaysHit = "AlwaysHit";       // Never misses

        // === Special ===
        public const string Vaal = "Vaal";                 // Vaal skill
        public const string Curse = "Curse";               // Curse effect
        public const string Aura = "Aura";                 // Aura effect
    }
}
