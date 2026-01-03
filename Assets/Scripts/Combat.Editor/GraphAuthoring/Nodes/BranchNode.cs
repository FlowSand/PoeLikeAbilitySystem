using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Branch node.
    /// Conditional branching based on a boolean condition.
    /// Outputs the condition value to both True and False ports for flow control.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Control/Branch")]
    public class BranchNode : BaseNode
    {
        [Input("Condition")] public PortTypeBool condition;
        [Output("True")] public PortTypeBool trueOutput;
        [Output("False")] public PortTypeBool falseOutput;

        public override string name => "Branch";

        static BranchNode()
        {
            NodeTypeRegistry.RegisterNodeType<BranchNode>(IRNodeType.Branch);
        }
    }
}
