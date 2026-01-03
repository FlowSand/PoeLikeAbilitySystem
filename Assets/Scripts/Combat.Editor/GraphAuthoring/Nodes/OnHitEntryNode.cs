using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Entry node for OnHit events.
    /// Provides Caster and Target entity IDs from the event context.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entry/OnHit")]
    public class OnHitEntryNode : BaseNode
    {
        [Output("Caster")] public PortTypeEntityId caster;
        [Output("Target")] public PortTypeEntityId target;

        public override string name => "OnHit Entry";

        static OnHitEntryNode()
        {
            NodeTypeRegistry.RegisterNodeType<OnHitEntryNode>(IRNodeType.OnHitEntry);
        }
    }
}
