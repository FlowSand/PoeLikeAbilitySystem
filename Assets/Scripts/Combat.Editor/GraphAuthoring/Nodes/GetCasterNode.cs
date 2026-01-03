using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Get Caster node.
    /// Retrieves the caster entity from the execution context.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entity/GetCaster")]
    public class GetCasterNode : BaseNode
    {
        [Output("Caster")] public PortTypeEntityId caster;

        public override string name => "Get Caster";

        static GetCasterNode()
        {
            NodeTypeRegistry.RegisterNodeType<GetCasterNode>(IRNodeType.GetCaster);
        }
    }
}
