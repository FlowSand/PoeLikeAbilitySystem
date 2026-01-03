namespace Combat.Runtime
{
    /// <summary>
    /// Global constants for battle execution limits and safety thresholds.
    /// </summary>
    public static class BattleConfig
    {
        /// <summary>
        /// Maximum trigger depth to prevent infinite trigger chains.
        /// OnHit -> OnKill -> OnHit -> ... will be cut off at this depth.
        /// </summary>
        public const int MAX_TRIGGER_DEPTH = 10;

        /// <summary>
        /// Maximum number of Ops that can be executed in a single event.
        /// Protects against malformed or malicious ExecPlans.
        /// </summary>
        public const int MAX_OPS_PER_EVENT = 1000;

        /// <summary>
        /// Maximum number of Commands that can be emitted in a single event.
        /// Prevents excessive state modifications.
        /// </summary>
        public const int MAX_COMMANDS_PER_EVENT = 100;

        /// <summary>
        /// Maximum number of events that can be processed in a single frame/tick.
        /// Prevents event queue exhaustion stalling the game loop.
        /// </summary>
        public const int MAX_EVENTS_PER_FRAME = 100;
    }
}
