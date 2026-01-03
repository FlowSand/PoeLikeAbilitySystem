using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Entry node for OnCast events.
    /// Provides Caster entity ID from the event context.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entry/OnCast")]
    public class OnCastEntryNode : BaseNode
    {
        [Output("Caster")] public PortTypeEntityId caster;

        public override string name => "OnCast Entry";

        static OnCastEntryNode()
        {
            NodeTypeRegistry.RegisterNodeType<OnCastEntryNode>(IRNodeType.OnCastEntry);
        }
    }
}
