using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Add node.
    /// Adds two numbers together.
    /// Port names "A" and "B" are alphabetically sorted for compiler compatibility.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Math/Add")]
    public class AddNode : BaseNode
    {
        [Input("A")] public PortTypeNumber inputA;
        [Input("B")] public PortTypeNumber inputB;
        [Output("Result")] public PortTypeNumber output;

        public override string name => "Add";

        static AddNode()
        {
            NodeTypeRegistry.RegisterNodeType<AddNode>(IRNodeType.Add);
        }
    }
}
