using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Constant number node.
    /// Outputs a fixed numeric value configured in the inspector.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Number/Const")]
    public class ConstNumberNode : BaseNode
    {
        [Output("Value")] public PortTypeNumber output;

        /// <summary>
        /// The constant value to output.
        /// This will be compiled into the ExecPlan as an immediate operand.
        /// </summary>
        public float value = 0f;

        public override string name => "Const Number";

        static ConstNumberNode()
        {
            NodeTypeRegistry.RegisterNodeType<ConstNumberNode>(IRNodeType.ConstNumber);
        }
    }
}
