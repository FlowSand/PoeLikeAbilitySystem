using System;
using System.Collections.Generic;
using Combat.Runtime.GraphIR;
using Combat.Editor.GraphAuthoring.Nodes;

namespace Combat.Editor.GraphAuthoring
{
    /// <summary>
    /// Centralized registry for mapping between NGP node types and GraphIR types.
    /// Single source of truth for type conversions during export.
    /// </summary>
    public static class NodeTypeRegistry
    {
        private static readonly Dictionary<Type, IRNodeType> _nodeTypeMap;
        private static readonly Dictionary<IRPortType, Type> _portTypeMap;

        static NodeTypeRegistry()
        {
            // Initialize node type mappings
            _nodeTypeMap = new Dictionary<Type, IRNodeType>();

            // Initialize port type mappings
            _portTypeMap = new Dictionary<IRPortType, Type>
            {
                { IRPortType.Number, typeof(PortTypeNumber) },
                { IRPortType.Bool, typeof(PortTypeBool) },
                { IRPortType.EntityId, typeof(PortTypeEntityId) },
                { IRPortType.EntityList, typeof(PortTypeEntityList) },
                { IRPortType.DamageSpec, typeof(PortTypeDamageSpec) }
            };

            // Force initialization of all node types to trigger their static constructors
            // This ensures all nodes are registered before first use
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(OnHitEntryNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(OnCastEntryNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ConstNumberNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(GetStatNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AddNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(MulNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(GetCasterNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(GetTargetNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(MakeDamageSpecNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(EmitApplyDamageCommandNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RollChanceNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(BranchNode).TypeHandle);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(FindTargetsInRadiusNode).TypeHandle);
        }

        /// <summary>
        /// Register a node type mapping.
        /// Called during static initialization of node classes.
        /// </summary>
        public static void RegisterNodeType<TNode>(IRNodeType irNodeType)
        {
            _nodeTypeMap[typeof(TNode)] = irNodeType;
        }

        /// <summary>
        /// Get IRNodeType for a given NGP node type.
        /// Throws if the node type is not registered.
        /// </summary>
        public static IRNodeType GetIRNodeType(Type nodeType)
        {
            if (_nodeTypeMap.TryGetValue(nodeType, out var irNodeType))
            {
                return irNodeType;
            }

            throw new InvalidOperationException(
                $"Node type '{nodeType.Name}' is not registered in NodeTypeRegistry. " +
                $"Did you forget to call RegisterNodeType in the node's static constructor?");
        }

        /// <summary>
        /// Get NGP port type (sentinel type) for a given IRPortType.
        /// </summary>
        public static Type GetPortTypeForIR(IRPortType irPortType)
        {
            if (_portTypeMap.TryGetValue(irPortType, out var portType))
            {
                return portType;
            }

            throw new InvalidOperationException($"Unknown IRPortType: {irPortType}");
        }

        /// <summary>
        /// Get IRPortType from NGP port type (sentinel type).
        /// </summary>
        public static IRPortType GetIRPortType(Type portType)
        {
            foreach (var kvp in _portTypeMap)
            {
                if (kvp.Value == portType)
                {
                    return kvp.Key;
                }
            }

            throw new InvalidOperationException($"Unknown port type: {portType.Name}");
        }
    }
}
