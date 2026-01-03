using System;
using UnityEngine;

namespace Combat.Runtime.GraphRuntime
{
    /// <summary>
    /// ScriptableObject wrapper for ExecPlan, enabling Unity asset serialization.
    /// Stores compiled execution plan with metadata for tracing and debugging.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/ExecPlan Asset", fileName = "ExecPlan")]
    public class ExecPlanAsset : ScriptableObject
    {
        [SerializeField] private string sourceGraphId;
        [SerializeField] private int graphVersion;
        [SerializeField] private ulong planHash;

        [SerializeField] private SerializedOp[] serializedOps;
        [SerializeField] private int numberSlotCount;
        [SerializeField] private int entitySlotCount;
        [SerializeField] private int damageSpecSlotCount;

        // NEW: Op index to IRNode.nodeId mapping for trace visualization
        [SerializeField] private string[] opToNodeId;

        private ExecPlan _cachedPlan;

        public string SourceGraphId => sourceGraphId;
        public int GraphVersion => graphVersion;
        public ulong PlanHash => planHash;
        public string[] OpToNodeId => opToNodeId;

        /// <summary>
        /// Get the compiled ExecPlan. Lazy-deserializes on first access.
        /// </summary>
        public ExecPlan GetPlan()
        {
            if (_cachedPlan == null)
            {
                _cachedPlan = Deserialize();
            }
            return _cachedPlan;
        }

        /// <summary>
        /// Initialize the asset from a compiled ExecPlan.
        /// Called by the bake pipeline.
        /// </summary>
        public void Initialize(ExecPlan plan, string sourceGraphId, int graphVersion, string[] opToNodeId)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            this.sourceGraphId = sourceGraphId;
            this.graphVersion = graphVersion;
            this.planHash = plan.planHash;
            this.opToNodeId = opToNodeId;

            // Serialize operations
            this.serializedOps = new SerializedOp[plan.operations.Length];
            for (int i = 0; i < plan.operations.Length; i++)
            {
                var op = plan.operations[i];
                this.serializedOps[i] = new SerializedOp
                {
                    opCode = (int)op.opCode,
                    a = op.a,
                    b = op.b,
                    output = op.output
                };
            }

            // Serialize slot layout
            this.numberSlotCount = plan.slotLayout.numberSlotCount;
            this.entitySlotCount = plan.slotLayout.entitySlotCount;
            this.damageSpecSlotCount = plan.slotLayout.damageSpecSlotCount;

            // Cache the plan
            _cachedPlan = plan;
        }

        private ExecPlan Deserialize()
        {
            // Deserialize operations
            var ops = new Op[serializedOps.Length];
            for (int i = 0; i < serializedOps.Length; i++)
            {
                var sop = serializedOps[i];
                ops[i] = new Op((OpCode)sop.opCode, sop.a, sop.b, sop.output);
            }

            // Deserialize slot layout
            var layout = new SlotLayout(numberSlotCount, entitySlotCount, damageSpecSlotCount);

            return new ExecPlan(planHash, ops, layout);
        }

        /// <summary>
        /// Serializable representation of Op struct.
        /// </summary>
        [Serializable]
        private struct SerializedOp
        {
            public int opCode;
            public int a;
            public int b;
            public int output;
        }
    }
}
