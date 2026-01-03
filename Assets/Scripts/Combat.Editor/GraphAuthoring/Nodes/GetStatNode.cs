using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;
using Combat.Runtime.Model;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Get Stat node.
    /// Retrieves a stat value from an entity.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Entity/GetStat")]
    public class GetStatNode : BaseNode
    {
        [Input("Entity")] public PortTypeEntityId entity;
        [Output("Value")] public PortTypeNumber output;

        /// <summary>
        /// The stat type to retrieve (Health, MaxHealth, etc.).
        /// </summary>
        public StatType statType = StatType.Health;

        public override string name => "Get Stat";

        static GetStatNode()
        {
            NodeTypeRegistry.RegisterNodeType<GetStatNode>(IRNodeType.GetStat);
        }
    }
}
