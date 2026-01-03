using System;
using GraphProcessor;
using Combat.Runtime.GraphIR;
using Combat.Runtime.Model;

namespace Combat.Editor.GraphAuthoring.Nodes
{
    /// <summary>
    /// Make Damage Spec node.
    /// Creates a DamageSpec from base damage amount and damage type.
    /// Caster and Target are automatically provided by the execution context.
    /// </summary>
    [Serializable, NodeMenuItem("Effect/Damage/MakeDamageSpec")]
    public class MakeDamageSpecNode : BaseNode
    {
        [Input("BaseDamage")] public PortTypeNumber baseDamage;
        [Output("DamageSpec")] public PortTypeDamageSpec output;

        /// <summary>
        /// The type of damage (Physical, Fire, Cold, Lightning, Chaos).
        /// </summary>
        public DamageType damageType = DamageType.Fire;

        public override string name => "Make Damage Spec";

        static MakeDamageSpecNode()
        {
            NodeTypeRegistry.RegisterNodeType<MakeDamageSpecNode>(IRNodeType.MakeDamageSpec);
        }
    }
}
