using System;
using System.Collections.Generic;
using Combat.Runtime.GraphIR;

namespace Combat.Runtime.GraphRuntime
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    public static class ExecPlanCompiler
    {
        private const string ParamValue = "value";
        private const string ParamStatType = "statType";
        private const string ParamDamageType = "damageType";

        public static (ExecPlan, string[]) Compile(GraphIRModel graph)
        {
            var validation = GraphIRValidator.Validate(graph);
            if (!validation.isValid)
                throw new ArgumentException("GraphIR validation failed: " + validation.errors[0].ToString());

            if (graph.nodes == null)
                throw new ArgumentException("GraphIR nodes is null.");

            int nodeCount = graph.nodes.Count;
            var nodesById = new Dictionary<string, IRNode>(nodeCount);
            var nodeIdsByIndex = new string[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                var node = graph.nodes[i];
                if (node == null)
                    throw new ArgumentException("GraphIR has null node at index " + i.ToString() + ".");

                nodeIdsByIndex[i] = node.nodeId;
                nodesById.Add(node.nodeId, node);
            }

            ulong planHash = ComputePlanHash(graph, nodeIdsByIndex, nodesById);

            int[] topoOrder = TopologicalSort(nodeIdsByIndex, graph.edges);

            var numberSlotByOutPort = new Dictionary<PortKey, int>(64);
            var entitySlotByOutPort = new Dictionary<PortKey, int>(16);
            var damageSlotByOutPort = new Dictionary<PortKey, int>(16);

            int numberSlotCount = 0;
            int entitySlotCount = 0;
            int damageSlotCount = 0;

            AllocateOutPortSlots(
                graph.nodes,
                topoOrder,
                numberSlotByOutPort,
                entitySlotByOutPort,
                damageSlotByOutPort,
                ref numberSlotCount,
                ref entitySlotCount,
                ref damageSlotCount);

            var numberSlotByInPort = new Dictionary<PortKey, int>(64);
            var entitySlotByInPort = new Dictionary<PortKey, int>(16);
            var damageSlotByInPort = new Dictionary<PortKey, int>(16);

            BindInPorts(
                graph.edges,
                nodesById,
                numberSlotByOutPort,
                entitySlotByOutPort,
                damageSlotByOutPort,
                numberSlotByInPort,
                entitySlotByInPort,
                damageSlotByInPort);

            var ops = new List<Op>(nodeCount);
            var opToNodeIdList = new List<string>(nodeCount);

            for (int i = 0; i < topoOrder.Length; i++)
            {
                var node = graph.nodes[topoOrder[i]];

                switch (node.nodeType)
                {
                    case IRNodeType.OnCastEntry:
                    case IRNodeType.OnHitEntry:
                        continue;

                    case IRNodeType.ConstNumber:
                        ops.Add(CompileConstNumber(node, numberSlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.GetStat:
                        ops.Add(CompileGetStat(node, entitySlotByInPort, numberSlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.Add:
                        ops.Add(CompileBinaryNumberOp(OpCode.Add, node, numberSlotByInPort, numberSlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.Mul:
                        ops.Add(CompileBinaryNumberOp(OpCode.Mul, node, numberSlotByInPort, numberSlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.GetCaster:
                        ops.Add(CompileGetCaster(node, entitySlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.GetTarget:
                        ops.Add(CompileGetTarget(node, entitySlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.MakeDamageSpec:
                        ops.Add(CompileMakeDamage(node, numberSlotByInPort, damageSlotByOutPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    case IRNodeType.EmitApplyDamageCommand:
                        ops.Add(CompileEmitApplyDamage(node, damageSlotByInPort));
                        opToNodeIdList.Add(node.nodeId);
                        break;

                    default:
                        throw new NotSupportedException("Unsupported nodeType '" + node.nodeType.ToString() + "' at nodeId '" +
                                                        (node.nodeId ?? "<null>") + "'.");
                }
            }

            var layout = new SlotLayout(numberSlotCount, entitySlotCount, damageSlotCount);
            var execPlan = new ExecPlan(planHash, ops.ToArray(), layout);
            return (execPlan, opToNodeIdList.ToArray());
        }

        private static Op CompileConstNumber(IRNode node, Dictionary<PortKey, int> numberSlotByOutPort)
        {
            int value = GetRequiredIntParam(node, ParamValue);
            int outputSlot = GetSingleOutSlot(node, IRPortType.Number, numberSlotByOutPort, null, null);
            return new Op(OpCode.ConstNumber, value, 0, outputSlot);
        }

        private static Op CompileGetStat(
            IRNode node,
            Dictionary<PortKey, int> entitySlotByInPort,
            Dictionary<PortKey, int> numberSlotByOutPort)
        {
            int statType = GetRequiredIntParam(node, ParamStatType);
            int entitySlot = GetSingleInSlot(node, IRPortType.EntityId, entitySlotByInPort, null, null);
            int outputSlot = GetSingleOutSlot(node, IRPortType.Number, numberSlotByOutPort, null, null);
            return new Op(OpCode.GetStat, entitySlot, statType, outputSlot);
        }

        private static Op CompileGetCaster(IRNode node, Dictionary<PortKey, int> entitySlotByOutPort)
        {
            int outputSlot = GetSingleOutSlot(node, IRPortType.EntityId, null, entitySlotByOutPort, null);
            return new Op(OpCode.GetCaster, 0, 0, outputSlot);
        }

        private static Op CompileGetTarget(IRNode node, Dictionary<PortKey, int> entitySlotByOutPort)
        {
            int outputSlot = GetSingleOutSlot(node, IRPortType.EntityId, null, entitySlotByOutPort, null);
            return new Op(OpCode.GetTarget, 0, 0, outputSlot);
        }

        private static Op CompileBinaryNumberOp(
            OpCode opCode,
            IRNode node,
            Dictionary<PortKey, int> numberSlotByInPort,
            Dictionary<PortKey, int> numberSlotByOutPort)
        {
            int outputSlot = GetSingleOutSlot(node, IRPortType.Number, numberSlotByOutPort, null, null);

            string firstInPort;
            string secondInPort;
            GetTwoInPortsSorted(node, IRPortType.Number, out firstInPort, out secondInPort);

            int a = GetInSlot(node.nodeId, firstInPort, numberSlotByInPort);
            int b = GetInSlot(node.nodeId, secondInPort, numberSlotByInPort);

            return new Op(opCode, a, b, outputSlot);
        }

        private static Op CompileMakeDamage(
            IRNode node,
            Dictionary<PortKey, int> numberSlotByInPort,
            Dictionary<PortKey, int> damageSlotByOutPort)
        {
            int damageType = GetRequiredIntParam(node, ParamDamageType);
            int outputSlot = GetSingleOutSlot(node, IRPortType.DamageSpec, null, null, damageSlotByOutPort);
            int baseDamageSlot = GetSingleInSlot(node, IRPortType.Number, null, numberSlotByInPort, null);
            return new Op(OpCode.MakeDamage, baseDamageSlot, damageType, outputSlot);
        }

        private static Op CompileEmitApplyDamage(IRNode node, Dictionary<PortKey, int> damageSlotByInPort)
        {
            int specSlot = GetSingleInSlot(node, IRPortType.DamageSpec, null, null, damageSlotByInPort);
            return new Op(OpCode.EmitApplyDamage, specSlot, 0, 0);
        }

        private static int GetRequiredIntParam(IRNode node, string key)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            if (node.intParams == null || !node.intParams.TryGetValue(key, out var value))
                throw new InvalidOperationException("Missing intParams['" + key + "'] at nodeId '" + (node.nodeId ?? "<null>") + "'.");

            return value;
        }

        private static void GetTwoInPortsSorted(IRNode node, IRPortType requiredType, out string firstPortName, out string secondPortName)
        {
            if (node.ports == null)
                throw new InvalidOperationException("Node ports dictionary is null at nodeId '" + (node.nodeId ?? "<null>") + "'.");

            string[] names = new string[2];
            int count = 0;
            foreach (var kvp in node.ports)
            {
                var port = kvp.Value;
                if (port == null) continue;
                if (port.direction != IRPortDirection.In) continue;
                if (port.portType != requiredType) continue;

                if (count >= 2)
                    throw new InvalidOperationException("Expected exactly two In ports of type '" + requiredType.ToString() +
                                                        "' at nodeId '" + node.nodeId + "'.");

                names[count] = kvp.Key;
                count++;
            }

            if (count != 2)
                throw new InvalidOperationException("Expected exactly two In ports of type '" + requiredType.ToString() +
                                                    "' at nodeId '" + node.nodeId + "', found " + count.ToString() + ".");

            if (string.CompareOrdinal(names[0], names[1]) <= 0)
            {
                firstPortName = names[0];
                secondPortName = names[1];
            }
            else
            {
                firstPortName = names[1];
                secondPortName = names[0];
            }
        }

        private static int GetSingleOutSlot(
            IRNode node,
            IRPortType requiredType,
            Dictionary<PortKey, int> numberSlotByOutPort,
            Dictionary<PortKey, int> entitySlotByOutPort,
            Dictionary<PortKey, int> damageSlotByOutPort)
        {
            string singlePortName = GetSinglePortName(node, requiredType, IRPortDirection.Out);
            return GetOutSlot(node.nodeId, singlePortName, requiredType, numberSlotByOutPort, entitySlotByOutPort, damageSlotByOutPort);
        }

        private static int GetSingleInSlot(
            IRNode node,
            IRPortType requiredType,
            Dictionary<PortKey, int> entitySlotByInPort,
            Dictionary<PortKey, int> numberSlotByInPort,
            Dictionary<PortKey, int> damageSlotByInPort)
        {
            string singlePortName = GetSinglePortName(node, requiredType, IRPortDirection.In);

            switch (requiredType)
            {
                case IRPortType.Number:
                    if (numberSlotByInPort == null) break;
                    return GetInSlot(node.nodeId, singlePortName, numberSlotByInPort);
                case IRPortType.EntityId:
                case IRPortType.EntityList:
                    if (entitySlotByInPort == null) break;
                    return GetInSlot(node.nodeId, singlePortName, entitySlotByInPort);
                case IRPortType.DamageSpec:
                    if (damageSlotByInPort == null) break;
                    return GetInSlot(node.nodeId, singlePortName, damageSlotByInPort);
            }

            throw new NotSupportedException("Unsupported In portType '" + requiredType.ToString() + "' at nodeId '" + node.nodeId + "'.");
        }

        private static string GetSinglePortName(IRNode node, IRPortType requiredType, IRPortDirection direction)
        {
            if (node.ports == null)
                throw new InvalidOperationException("Node ports dictionary is null at nodeId '" + (node.nodeId ?? "<null>") + "'.");

            string singlePortName = null;
            foreach (var kvp in node.ports)
            {
                var port = kvp.Value;
                if (port == null) continue;
                if (port.direction != direction) continue;
                if (port.portType != requiredType) continue;

                if (singlePortName != null)
                    throw new InvalidOperationException("Expected a single " + direction.ToString() + " port of type '" + requiredType.ToString() +
                                                        "' at nodeId '" + node.nodeId + "'.");

                singlePortName = kvp.Key;
            }

            if (singlePortName == null)
                throw new InvalidOperationException("Missing " + direction.ToString() + " port of type '" + requiredType.ToString() +
                                                    "' at nodeId '" + node.nodeId + "'.");

            return singlePortName;
        }

        private static int GetOutSlot(
            string nodeId,
            string portName,
            IRPortType portType,
            Dictionary<PortKey, int> numberSlotByOutPort,
            Dictionary<PortKey, int> entitySlotByOutPort,
            Dictionary<PortKey, int> damageSlotByOutPort)
        {
            var key = new PortKey(nodeId, portName);

            switch (portType)
            {
                case IRPortType.Number:
                    if (numberSlotByOutPort != null && numberSlotByOutPort.TryGetValue(key, out var n)) return n;
                    break;
                case IRPortType.EntityId:
                case IRPortType.EntityList:
                    if (entitySlotByOutPort != null && entitySlotByOutPort.TryGetValue(key, out var e)) return e;
                    break;
                case IRPortType.DamageSpec:
                    if (damageSlotByOutPort != null && damageSlotByOutPort.TryGetValue(key, out var d)) return d;
                    break;
            }

            throw new InvalidOperationException("Missing slot for Out port '" + nodeId + "." + portName + "'.");
        }

        private static int GetInSlot(string nodeId, string portName, Dictionary<PortKey, int> slotByInPort)
        {
            if (slotByInPort.TryGetValue(new PortKey(nodeId, portName), out var slot))
                return slot;

            throw new InvalidOperationException("Missing edge binding for In port '" + nodeId + "." + portName + "'.");
        }

        private static void AllocateOutPortSlots(
            List<IRNode> nodes,
            int[] topoOrder,
            Dictionary<PortKey, int> numberSlotByOutPort,
            Dictionary<PortKey, int> entitySlotByOutPort,
            Dictionary<PortKey, int> damageSlotByOutPort,
            ref int numberSlotCount,
            ref int entitySlotCount,
            ref int damageSlotCount)
        {
            for (int t = 0; t < topoOrder.Length; t++)
            {
                var node = nodes[topoOrder[t]];
                if (node.ports == null) continue;

                int portCount = node.ports.Count;
                if (portCount == 0) continue;

                var outPortNames = new string[portCount];
                int outCount = 0;

                foreach (var kvp in node.ports)
                {
                    var port = kvp.Value;
                    if (port == null) continue;
                    if (port.direction != IRPortDirection.Out) continue;

                    outPortNames[outCount] = kvp.Key;
                    outCount++;
                }

                if (outCount == 0) continue;
                Array.Sort(outPortNames, 0, outCount, StringComparer.Ordinal);

                for (int i = 0; i < outCount; i++)
                {
                    var portName = outPortNames[i];
                    var port = node.ports[portName];

                    var key = new PortKey(node.nodeId, portName);
                    switch (port.portType)
                    {
                        case IRPortType.Number:
                            numberSlotByOutPort.Add(key, numberSlotCount);
                            numberSlotCount++;
                            break;
                        case IRPortType.EntityId:
                        case IRPortType.EntityList:
                            entitySlotByOutPort.Add(key, entitySlotCount);
                            entitySlotCount++;
                            break;
                        case IRPortType.DamageSpec:
                            damageSlotByOutPort.Add(key, damageSlotCount);
                            damageSlotCount++;
                            break;
                        default:
                            throw new NotSupportedException("Unsupported Out portType '" + port.portType.ToString() + "' at nodeId '" +
                                                            (node.nodeId ?? "<null>") + "'.");
                    }
                }
            }
        }

        private static void BindInPorts(
            List<IREdge> edges,
            Dictionary<string, IRNode> nodesById,
            Dictionary<PortKey, int> numberSlotByOutPort,
            Dictionary<PortKey, int> entitySlotByOutPort,
            Dictionary<PortKey, int> damageSlotByOutPort,
            Dictionary<PortKey, int> numberSlotByInPort,
            Dictionary<PortKey, int> entitySlotByInPort,
            Dictionary<PortKey, int> damageSlotByInPort)
        {
            if (edges == null) return;

            int edgeCount = edges.Count;
            for (int i = 0; i < edgeCount; i++)
            {
                var edge = edges[i];
                if (edge == null)
                    throw new ArgumentException("Edge is null at index " + i.ToString() + ".");

                var fromNode = nodesById[edge.fromNodeId];
                var toNode = nodesById[edge.toNodeId];

                var fromPort = fromNode.ports[edge.fromPort];
                var toPort = toNode.ports[edge.toPort];

                if (fromPort.portType != toPort.portType)
                    throw new InvalidOperationException("PortType mismatch at " + FormatEdge(edge) + ".");

                var toKey = new PortKey(toNode.nodeId, edge.toPort);

                switch (fromPort.portType)
                {
                    case IRPortType.Number:
                    {
                        if (numberSlotByInPort.ContainsKey(toKey))
                            throw new InvalidOperationException("Multiple edges connected to In port '" + toNode.nodeId + "." + edge.toPort + "'.");

                        int slot = GetOutSlot(fromNode.nodeId, edge.fromPort, IRPortType.Number, numberSlotByOutPort, null, null);
                        numberSlotByInPort.Add(toKey, slot);
                        break;
                    }
                    case IRPortType.EntityId:
                    case IRPortType.EntityList:
                    {
                        if (entitySlotByInPort.ContainsKey(toKey))
                            throw new InvalidOperationException("Multiple edges connected to In port '" + toNode.nodeId + "." + edge.toPort + "'.");

                        int slot = GetOutSlot(fromNode.nodeId, edge.fromPort, fromPort.portType, null, entitySlotByOutPort, null);
                        entitySlotByInPort.Add(toKey, slot);
                        break;
                    }
                    case IRPortType.DamageSpec:
                    {
                        if (damageSlotByInPort.ContainsKey(toKey))
                            throw new InvalidOperationException("Multiple edges connected to In port '" + toNode.nodeId + "." + edge.toPort + "'.");

                        int slot = GetOutSlot(fromNode.nodeId, edge.fromPort, IRPortType.DamageSpec, null, null, damageSlotByOutPort);
                        damageSlotByInPort.Add(toKey, slot);
                        break;
                    }
                    default:
                        throw new NotSupportedException(
                            "Unsupported edge portType '" + fromPort.portType.ToString() + "' at " + FormatEdge(edge) + ".");
                }
            }
        }

        private static int[] TopologicalSort(string[] nodeIdsByIndex, List<IREdge> edges)
        {
            int nodeCount = nodeIdsByIndex.Length;
            var indexByNodeId = new Dictionary<string, int>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
                indexByNodeId.Add(nodeIdsByIndex[i], i);

            var indegree = new int[nodeCount];
            List<int>[] outgoing = new List<int>[nodeCount];

            if (edges != null)
            {
                int edgeCount = edges.Count;
                for (int i = 0; i < edgeCount; i++)
                {
                    var edge = edges[i];
                    if (edge == null) continue;

                    int fromIndex = indexByNodeId[edge.fromNodeId];
                    int toIndex = indexByNodeId[edge.toNodeId];

                    var list = outgoing[fromIndex];
                    if (list == null)
                    {
                        list = new List<int>(2);
                        outgoing[fromIndex] = list;
                    }

                    list.Add(toIndex);
                    indegree[toIndex]++;
                }
            }

            var order = new int[nodeCount];
            var used = new bool[nodeCount];

            for (int k = 0; k < nodeCount; k++)
            {
                int bestIndex = -1;
                string bestNodeId = null;

                for (int i = 0; i < nodeCount; i++)
                {
                    if (used[i]) continue;
                    if (indegree[i] != 0) continue;

                    string nodeId = nodeIdsByIndex[i];
                    if (bestIndex < 0 || string.CompareOrdinal(nodeId, bestNodeId) < 0)
                    {
                        bestIndex = i;
                        bestNodeId = nodeId;
                    }
                }

                if (bestIndex < 0)
                    throw new InvalidOperationException("Graph contains a cycle; cannot compile.");

                used[bestIndex] = true;
                order[k] = bestIndex;

                var list = outgoing[bestIndex];
                if (list == null) continue;

                int outCount = list.Count;
                for (int i = 0; i < outCount; i++)
                    indegree[list[i]]--;
            }

            return order;
        }

        private static ulong ComputePlanHash(GraphIRModel graph, string[] nodeIdsByIndex, Dictionary<string, IRNode> nodesById)
        {
            ulong hash = StableHash64.OffsetBasis;
            hash = StableHash64.AddString(hash, graph.graphId ?? string.Empty);
            hash = StableHash64.AddInt(hash, graph.version);
            hash = StableHash64.AddString(hash, graph.entryNodeId ?? string.Empty);

            string[] sortedNodeIds = (string[])nodeIdsByIndex.Clone();
            Array.Sort(sortedNodeIds, StringComparer.Ordinal);

            int nodeCount = sortedNodeIds.Length;
            hash = StableHash64.AddInt(hash, nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                if (!nodesById.TryGetValue(sortedNodeIds[i], out var node) || node == null)
                    continue;

                hash = StableHash64.AddString(hash, node.nodeId ?? string.Empty);
                hash = StableHash64.AddByte(hash, (byte)node.nodeType);

                hash = HashPorts(hash, node.ports);
                hash = HashIntParams(hash, node.intParams);
            }

            hash = HashEdges(hash, graph.edges);
            return hash;
        }

        private static ulong HashPorts(ulong hash, Dictionary<string, IRPort> ports)
        {
            if (ports == null)
            {
                hash = StableHash64.AddInt(hash, 0);
                return hash;
            }

            int portCount = ports.Count;
            var portNames = new string[portCount];
            int index = 0;
            foreach (var kvp in ports)
            {
                portNames[index] = kvp.Key ?? string.Empty;
                index++;
            }

            Array.Sort(portNames, StringComparer.Ordinal);

            hash = StableHash64.AddInt(hash, portCount);
            for (int i = 0; i < portCount; i++)
            {
                string name = portNames[i];
                hash = StableHash64.AddString(hash, name);

                if (!ports.TryGetValue(name, out var port) || port == null)
                {
                    hash = StableHash64.AddByte(hash, 0xFF);
                    continue;
                }

                hash = StableHash64.AddByte(hash, (byte)port.portType);
                hash = StableHash64.AddByte(hash, (byte)port.direction);
            }

            return hash;
        }

        private static ulong HashIntParams(ulong hash, Dictionary<string, int> intParams)
        {
            if (intParams == null)
            {
                hash = StableHash64.AddInt(hash, 0);
                return hash;
            }

            int paramCount = intParams.Count;
            var keys = new string[paramCount];
            int index = 0;
            foreach (var kvp in intParams)
            {
                keys[index] = kvp.Key ?? string.Empty;
                index++;
            }

            Array.Sort(keys, StringComparer.Ordinal);

            hash = StableHash64.AddInt(hash, paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                string key = keys[i];
                hash = StableHash64.AddString(hash, key);
                hash = StableHash64.AddInt(hash, intParams[key]);
            }

            return hash;
        }

        private static ulong HashEdges(ulong hash, List<IREdge> edges)
        {
            if (edges == null)
            {
                hash = StableHash64.AddInt(hash, 0);
                return hash;
            }

            int edgeCount = edges.Count;
            var edgeKeys = new EdgeKey[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                var e = edges[i];
                edgeKeys[i] = new EdgeKey(
                    e != null ? e.fromNodeId : string.Empty,
                    e != null ? e.fromPort : string.Empty,
                    e != null ? e.toNodeId : string.Empty,
                    e != null ? e.toPort : string.Empty);
            }

            Array.Sort(edgeKeys, EdgeKeyComparer.Instance);

            hash = StableHash64.AddInt(hash, edgeCount);
            for (int i = 0; i < edgeCount; i++)
            {
                var e = edgeKeys[i];
                hash = StableHash64.AddString(hash, e.fromNodeId);
                hash = StableHash64.AddString(hash, e.fromPort);
                hash = StableHash64.AddString(hash, e.toNodeId);
                hash = StableHash64.AddString(hash, e.toPort);
            }

            return hash;
        }

        private static string FormatEdge(IREdge edge)
        {
            return "Edge(" +
                   (edge.fromNodeId ?? "<null>") + "." + (edge.fromPort ?? "<null>") +
                   " -> " +
                   (edge.toNodeId ?? "<null>") + "." + (edge.toPort ?? "<null>") +
                   ")";
        }

        private readonly struct PortKey : IEquatable<PortKey>
        {
            private readonly string _nodeId;
            private readonly string _portName;

            public PortKey(string nodeId, string portName)
            {
                _nodeId = nodeId;
                _portName = portName;
            }

            public bool Equals(PortKey other)
            {
                return _nodeId == other._nodeId && _portName == other._portName;
            }

            public override bool Equals(object obj)
            {
                return obj is PortKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = _nodeId != null ? _nodeId.GetHashCode() : 0;
                    hashCode = (hashCode * 397) ^ (_portName != null ? _portName.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private readonly struct EdgeKey
        {
            public readonly string fromNodeId;
            public readonly string fromPort;
            public readonly string toNodeId;
            public readonly string toPort;

            public EdgeKey(string fromNodeId, string fromPort, string toNodeId, string toPort)
            {
                this.fromNodeId = fromNodeId ?? string.Empty;
                this.fromPort = fromPort ?? string.Empty;
                this.toNodeId = toNodeId ?? string.Empty;
                this.toPort = toPort ?? string.Empty;
            }
        }

        private sealed class EdgeKeyComparer : IComparer<EdgeKey>
        {
            public static readonly EdgeKeyComparer Instance = new EdgeKeyComparer();

            public int Compare(EdgeKey x, EdgeKey y)
            {
                int c = string.CompareOrdinal(x.fromNodeId, y.fromNodeId);
                if (c != 0) return c;

                c = string.CompareOrdinal(x.fromPort, y.fromPort);
                if (c != 0) return c;

                c = string.CompareOrdinal(x.toNodeId, y.toNodeId);
                if (c != 0) return c;

                return string.CompareOrdinal(x.toPort, y.toPort);
            }
        }
    }
}
