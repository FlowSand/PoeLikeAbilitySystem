using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Get Target node.
    /// Retrieves the target entity from the execution context.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entity/GetTarget")]
    public class GetTargetNode : BaseNode
    {
        [Output("Target")] public PortTypeEntityId target;

        public override string name => "Get Target";

        static GetTargetNode()
        {
            NodeTypeRegistry.RegisterNodeType<GetTargetNode>(IRNodeType.GetTarget);
        }
    }
}
