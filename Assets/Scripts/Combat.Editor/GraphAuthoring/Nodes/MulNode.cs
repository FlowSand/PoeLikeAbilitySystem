using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Multiply node.
    /// Multiplies two numbers together.
    /// Port names "A" and "B" are alphabetically sorted for compiler compatibility.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Math/Mul")]
    public class MulNode : BaseNode
    {
        [Input("A")] public PortTypeNumber inputA;
        [Input("B")] public PortTypeNumber inputB;
        [Output("Result")] public PortTypeNumber output;

        public override string name => "Multiply";

        static MulNode()
        {
            NodeTypeRegistry.RegisterNodeType<MulNode>(IRNodeType.Mul);
        }
    }
}
