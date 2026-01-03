namespace Combat.Runtime.Events
{
    /// <summary>
    /// Wrapper for ICombatEvent that carries trigger chain metadata.
    /// Avoids modifying existing event types (OnHitEvent, OnCastEvent).
    /// </summary>
    public struct EventEnvelope
    {
        /// <summary>
        /// Root event ID for this trigger chain. All events spawned from the same
        /// initial action share the same rootEventId for correlation.
        /// </summary>
        public int rootEventId;

        /// <summary>
        /// Current trigger depth. Incremented when one event causes another.
        /// Used to prevent infinite trigger chains (e.g. OnHit -> OnHit -> ...).
        /// </summary>
        public int triggerDepth;

        /// <summary>
        /// Deterministic random seed for this event's execution.
        /// Ensures reproducibility: same seed + same inputs = same results.
        /// </summary>
        public uint randomSeed;

        /// <summary>
        /// The actual event payload (OnHitEvent, OnCastEvent, etc.).
        /// Note: ICombatEvent is an interface, so this will box struct events once.
        /// </summary>
        public ICombatEvent payload;

        public EventEnvelope(int rootEventId, int triggerDepth, uint randomSeed, ICombatEvent payload)
        {
            this.rootEventId = rootEventId;
            this.triggerDepth = triggerDepth;
            this.randomSeed = randomSeed;
            this.payload = payload;
        }
    }
}
