using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Roll Chance node.
    /// Performs a random roll based on a chance value (0-1).
    /// Outputs true if the roll succeeds, false otherwise.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Random/RollChance")]
    public class RollChanceNode : BaseNode
    {
        [Input("Chance")] public PortTypeNumber chance;
        [Output("Success")] public PortTypeBool success;

        public override string name => "Roll Chance";

        static RollChanceNode()
        {
            NodeTypeRegistry.RegisterNodeType<RollChanceNode>(IRNodeType.RollChance);
        }
    }
}
