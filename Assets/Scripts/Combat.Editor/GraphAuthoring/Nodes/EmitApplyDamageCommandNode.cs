using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Emit Apply Damage Command node.
    /// Generates a command to apply damage to a target.
    /// This is a terminal node that produces side effects.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Command/EmitApplyDamage")]
    public class EmitApplyDamageCommandNode : BaseNode
    {
        [Input("DamageSpec")] public PortTypeDamageSpec spec;

        public override string name => "Emit Apply Damage";

        static EmitApplyDamageCommandNode()
        {
            NodeTypeRegistry.RegisterNodeType<EmitApplyDamageCommandNode>(IRNodeType.EmitApplyDamageCommand);
        }
    }
}
