using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Find Targets In Radius node.
    /// Finds all entities within a specified radius of a center entity.
    /// Returns a list of entity IDs.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entity/FindTargetsInRadius")]
    public class FindTargetsInRadiusNode : BaseNode
    {
        [Input("Center")] public PortTypeEntityId center;
        [Input("Radius")] public PortTypeNumber radius;
        [Output("Targets")] public PortTypeEntityList targets;

        public override string name => "Find Targets In Radius";

        static FindTargetsInRadiusNode()
        {
            NodeTypeRegistry.RegisterNodeType<FindTargetsInRadiusNode>(IRNodeType.FindTargetsInRadius);
        }
    }
}
