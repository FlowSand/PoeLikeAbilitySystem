using System;
using System.Collections.Generic;
using System.IO;
using Combat.Runtime.Commands;
using Combat.Runtime.Events;
using Combat.Runtime.GraphRuntime;
using Combat.Runtime.Model;
using Combat.Runtime.Trace;

namespace Combat.Runtime
{
    /// <summary>
    /// BattleSimulator is the main execution loop for event-driven combat.
    /// It manages the EventQueue, dispatches events to ExecPlans, and applies
    /// Commands via two-phase commit. Enforces depth limits and execution budgets.
    /// </summary>
    public class BattleSimulator
    {
        private readonly BattleContext _context;
        private readonly EventQueue _eventQueue;
        private readonly CommandBuffer _commandBuffer;
        private readonly ExecutionBudget _globalBudget;

        // MVP: Hard-coded event type → ExecPlanAsset mapping
        // Future: Load from ScriptableObject or config table
        private readonly Dictionary<Type, ExecPlanAsset> _eventPlanMap;

        // Tracing configuration
        private readonly bool _enableTracing;
        private readonly string _traceOutputPath;

        private int _nextEventId = 1;

        public BattleSimulator(BattleContext context, bool enableTracing = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _eventQueue = new EventQueue(initialCapacity: 128);
            _commandBuffer = new CommandBuffer();
            _globalBudget = new ExecutionBudget(
                maxOps: BattleConfig.MAX_OPS_PER_EVENT,
                maxCommands: BattleConfig.MAX_COMMANDS_PER_EVENT
            );
            _eventPlanMap = new Dictionary<Type, ExecPlanAsset>(capacity: 16);

            _enableTracing = enableTracing;
            _traceOutputPath = Path.Combine(UnityEngine.Application.persistentDataPath, "Traces");

            if (_enableTracing && !Directory.Exists(_traceOutputPath))
            {
                Directory.CreateDirectory(_traceOutputPath);
            }
        }

        /// <summary>
        /// Register an ExecPlanAsset for a specific event type.
        /// When an event of this type is processed, this plan will be executed.
        /// </summary>
        public void RegisterEventPlan(Type eventType, ExecPlanAsset planAsset)
        {
            if (eventType == null) throw new ArgumentNullException(nameof(eventType));
            if (planAsset == null) throw new ArgumentNullException(nameof(planAsset));

            _eventPlanMap[eventType] = planAsset;
        }

        /// <summary>
        /// Register an ExecPlan for a specific event type (for testing).
        /// Creates a temporary ExecPlanAsset wrapper.
        /// </summary>
        public void RegisterEventPlan(Type eventType, ExecPlan plan)
        {
            if (eventType == null) throw new ArgumentNullException(nameof(eventType));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            // Create a temporary ExecPlanAsset for testing
            var tempAsset = UnityEngine.ScriptableObject.CreateInstance<ExecPlanAsset>();
            tempAsset.Initialize(plan, "test-graph", 1, new string[plan.operations.Length]);

            _eventPlanMap[eventType] = tempAsset;
        }

        /// <summary>
        /// Enqueue an event for processing.
        /// If rootEventId is -1, a new trigger chain is started.
        /// If depth exceeds MAX_TRIGGER_DEPTH, the event is rejected.
        /// </summary>
        public void EnqueueEvent(
            ICombatEvent evt,
            int rootEventId = -1,
            int depth = 0,
            uint seed = 0)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            // Depth limit check
            if (depth >= BattleConfig.MAX_TRIGGER_DEPTH)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BattleSimulator] Trigger depth limit exceeded: {depth} >= {BattleConfig.MAX_TRIGGER_DEPTH}. Event rejected."
                );
                return;
            }

            // New trigger chain: generate rootEventId and seed
            if (rootEventId < 0)
            {
                rootEventId = GenerateEventId();
                seed = GenerateRandomSeed();
            }

            _eventQueue.Enqueue(evt, rootEventId, depth, seed);
        }

        /// <summary>
        /// Process all pending events in the queue (up to MAX_EVENTS_PER_FRAME).
        /// Call this once per frame/tick from your game loop.
        /// </summary>
        public void ProcessEvents()
        {
            int eventsProcessed = 0;

            while (_eventQueue.HasPendingEvents && eventsProcessed < BattleConfig.MAX_EVENTS_PER_FRAME)
            {
                if (!_eventQueue.TryDequeue(out EventEnvelope envelope))
                    break;

                ProcessSingleEvent(ref envelope);
                eventsProcessed++;
            }

            if (_eventQueue.HasPendingEvents)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BattleSimulator] {_eventQueue.Count} events still pending after processing {eventsProcessed} events this frame"
                );
            }
        }

        /// <summary>
        /// Process a single event: find its ExecPlan, execute it, apply commands.
        /// </summary>
        private void ProcessSingleEvent(ref EventEnvelope envelope)
        {
            Type eventType = envelope.payload.GetType();

            // Lookup ExecPlanAsset for this event type
            if (!_eventPlanMap.TryGetValue(eventType, out ExecPlanAsset planAsset))
            {
                UnityEngine.Debug.LogWarning(
                    $"[BattleSimulator] No ExecPlan registered for event type: {eventType.Name}"
                );
                return;
            }

            ExecPlan plan = planAsset.GetPlan();

            // Create trace recorder if tracing is enabled
            ITraceRecorder traceRecorder = null;
            if (_enableTracing)
            {
                traceRecorder = new TraceRecorder(captureSlots: false);
            }

            // Create runner with trace recorder
            var runner = new ExecPlanRunner(_context, traceRecorder);

            // Build execution context
            ExecutionContext ctx = CreateExecutionContext(ref envelope, plan.slotLayout, planAsset.SourceGraphId);

            // Begin trace recording
            traceRecorder?.BeginTrace(ctx, plan.planHash, planAsset.SourceGraphId);

            // Reset budget for this event
            _globalBudget.Reset();

            // Execute the plan
            bool success = runner.Execute(plan, ref ctx, _commandBuffer);

            if (!success)
            {
                UnityEngine.Debug.LogError(
                    $"[BattleSimulator] ExecPlan execution failed for event {eventType.Name} (rootId={envelope.rootEventId}, depth={envelope.triggerDepth})"
                );
            }

            // Export trace if enabled
            if (_enableTracing && traceRecorder != null)
            {
                var trace = traceRecorder.GetTrace();
                string fileName = $"trace_{envelope.rootEventId}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                TraceExporter.ExportToJson(trace, fileName);
                UnityEngine.Debug.Log($"[BattleSimulator] Trace exported: {fileName}");
            }

            // Two-phase commit: apply all commands
            ApplyCommands(ref envelope);

            // Cleanup: clear slots
            ctx.slots.Clear();
        }

        /// <summary>
        /// Create an ExecutionContext from the event envelope.
        /// Extracts caster/target based on event type.
        /// </summary>
        private ExecutionContext CreateExecutionContext(ref EventEnvelope envelope, SlotLayout layout, string sourceGraphId)
        {
            ICombatEvent payload = envelope.payload;

            // Extract caster/target based on event type
            UnitId caster = default;
            UnitId target = default;

            if (payload is OnCastEvent castEvt)
            {
                caster = castEvt.CasterUnitId;
                target = default; // OnCastEvent has no target field currently
            }
            else if (payload is OnHitEvent hitEvt)
            {
                caster = hitEvt.SourceUnitId;
                target = hitEvt.TargetUnitId;
            }

            return new ExecutionContext
            {
                rootEventId = envelope.rootEventId,
                triggerDepth = envelope.triggerDepth,
                randomSeed = envelope.randomSeed,
                eventType = payload.GetType().Name,
                sourceGraphId = sourceGraphId,
                casterUnitId = caster,
                targetUnitId = target,
                slots = SlotStorage.Rent(layout),
                budget = _globalBudget
            };
        }

        /// <summary>
        /// Apply all commands in the CommandBuffer.
        /// Phase 1: ApplyAll modifies state.
        /// Phase 2: Commands can generate new events (future enhancement).
        /// </summary>
        private void ApplyCommands(ref EventEnvelope envelope)
        {
            // Phase 1: Apply all commands
            _commandBuffer.ApplyAll(_context);

            // Phase 2: Generate new events (future)
            // e.g. if target HP <= 0, emit OnKillEvent
            // For MVP, we skip this

            // Clear command buffer for next event
            // (ApplyAll already clears, but explicit for clarity)
        }

        /// <summary>
        /// Generate a unique event ID for a new trigger chain.
        /// </summary>
        private int GenerateEventId()
        {
            return _nextEventId++;
        }

        /// <summary>
        /// Generate a deterministic random seed.
        /// MVP: Use Unity's Random. Future: Use a seeded RNG for replay.
        /// </summary>
        private uint GenerateRandomSeed()
        {
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }
    }
}
