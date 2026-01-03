using System.Collections.Generic;
using Combat.Runtime.GraphIR;
using Combat.Runtime.GraphRuntime;
using NUnit.Framework;

namespace Combat.Runtime.Tests
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    public sealed class ExecPlanCompilerTests
    {
        [Test]
        public void Compile_SameGraphDifferentOrder_HashMatches()
        {
            var graph1 = CreateAddGraphInOrder();
            var (plan1, _) = ExecPlanCompiler.Compile(graph1);

            var graph2 = CreateAddGraphReordered();
            var (plan2, _) = ExecPlanCompiler.Compile(graph2);

            Assert.AreEqual(plan1.planHash, plan2.planHash);
        }

        [Test]
        public void Compile_DifferentGraph_HashDiffers()
        {
            var graph1 = CreateAddGraphInOrder();
            var (plan1, _) = ExecPlanCompiler.Compile(graph1);

            var graph2 = CreateAddGraphInOrder();
            graph2.nodes[0].intParams["value"] = 11;
            var (plan2, _) = ExecPlanCompiler.Compile(graph2);

            Assert.AreNotEqual(plan1.planHash, plan2.planHash);
        }

        [Test]
        public void Compile_OpsOrder_InputsDefinedBeforeUse()
        {
            var graph = CreateArithmeticGraphWithDependencies();
            var (plan, _) = ExecPlanCompiler.Compile(graph);

            AssertOpsHaveDefinedInputs(plan);
        }

        [Test]
        public void Compile_FireballExample_ProducesExpectedOps()
        {
            var plan = ExecPlanExamples.CompileFireballExecPlan();

            Assert.AreEqual(3, plan.operations.Length);
            Assert.AreEqual(OpCode.ConstNumber, plan.operations[0].opCode);
            Assert.AreEqual(OpCode.MakeDamage, plan.operations[1].opCode);
            Assert.AreEqual(OpCode.EmitApplyDamage, plan.operations[2].opCode);
        }

        private static GraphIRModel CreateAddGraphInOrder()
        {
            var c1 = new IRNode
            {
                nodeId = "c1",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            c1.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });
            c1.intParams.Add("value", 10);

            var c2 = new IRNode
            {
                nodeId = "c2",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            c2.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });
            c2.intParams.Add("value", 20);

            var add = new IRNode
            {
                nodeId = "add",
                nodeType = IRNodeType.Add,
                ports = new Dictionary<string, IRPort>(3),
            };
            add.ports.Add("a", new IRPort { portName = "a", portType = IRPortType.Number, direction = IRPortDirection.In });
            add.ports.Add("b", new IRPort { portName = "b", portType = IRPortType.Number, direction = IRPortDirection.In });
            add.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });

            var graph = new GraphIRModel
            {
                graphId = "add_graph",
                version = 1,
                entryNodeId = "c1",
                nodes = new List<IRNode>(3) { c1, c2, add },
                edges = new List<IREdge>(2)
                {
                    new IREdge { fromNodeId = "c1", fromPort = "out", toNodeId = "add", toPort = "a" },
                    new IREdge { fromNodeId = "c2", fromPort = "out", toNodeId = "add", toPort = "b" },
                }
            };

            return graph;
        }

        private static GraphIRModel CreateAddGraphReordered()
        {
            var graph = CreateAddGraphInOrder();

            graph.nodes = new List<IRNode>(3) { graph.nodes[2], graph.nodes[0], graph.nodes[1] };

            graph.edges = new List<IREdge>(2)
            {
                new IREdge { fromNodeId = "c2", fromPort = "out", toNodeId = "add", toPort = "b" },
                new IREdge { fromNodeId = "c1", fromPort = "out", toNodeId = "add", toPort = "a" },
            };

            return graph;
        }

        private static GraphIRModel CreateArithmeticGraphWithDependencies()
        {
            var c1 = new IRNode
            {
                nodeId = "c1",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            c1.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });
            c1.intParams.Add("value", 1);

            var c2 = new IRNode
            {
                nodeId = "c2",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            c2.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });
            c2.intParams.Add("value", 2);

            var c3 = new IRNode
            {
                nodeId = "c3",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            c3.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });
            c3.intParams.Add("value", 3);

            var add = new IRNode
            {
                nodeId = "add",
                nodeType = IRNodeType.Add,
                ports = new Dictionary<string, IRPort>(3),
            };
            add.ports.Add("a", new IRPort { portName = "a", portType = IRPortType.Number, direction = IRPortDirection.In });
            add.ports.Add("b", new IRPort { portName = "b", portType = IRPortType.Number, direction = IRPortDirection.In });
            add.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });

            var mul = new IRNode
            {
                nodeId = "mul",
                nodeType = IRNodeType.Mul,
                ports = new Dictionary<string, IRPort>(3),
            };
            mul.ports.Add("a", new IRPort { portName = "a", portType = IRPortType.Number, direction = IRPortDirection.In });
            mul.ports.Add("b", new IRPort { portName = "b", portType = IRPortType.Number, direction = IRPortDirection.In });
            mul.ports.Add("out", new IRPort { portName = "out", portType = IRPortType.Number, direction = IRPortDirection.Out });

            var graph = new GraphIRModel
            {
                graphId = "arithmetic_graph",
                version = 1,
                entryNodeId = "c1",
                nodes = new List<IRNode>(5) { c1, c2, c3, add, mul },
                edges = new List<IREdge>(4)
                {
                    new IREdge { fromNodeId = "c1", fromPort = "out", toNodeId = "add", toPort = "a" },
                    new IREdge { fromNodeId = "c2", fromPort = "out", toNodeId = "add", toPort = "b" },
                    new IREdge { fromNodeId = "add", fromPort = "out", toNodeId = "mul", toPort = "a" },
                    new IREdge { fromNodeId = "c3", fromPort = "out", toNodeId = "mul", toPort = "b" },
                }
            };

            return graph;
        }

        private static void AssertOpsHaveDefinedInputs(ExecPlan plan)
        {
            var definedNumbers = new bool[plan.slotLayout.numberSlotCount];
            var definedDamageSpecs = new bool[plan.slotLayout.damageSpecSlotCount];

            for (int i = 0; i < plan.operations.Length; i++)
            {
                var op = plan.operations[i];
                switch (op.opCode)
                {
                    case OpCode.ConstNumber:
                        Assert.IsTrue(op.output >= 0 && op.output < definedNumbers.Length);
                        definedNumbers[op.output] = true;
                        break;

                    case OpCode.Add:
                    case OpCode.Mul:
                        Assert.IsTrue(definedNumbers[op.a], "Op[" + i.ToString() + "] a not defined.");
                        Assert.IsTrue(definedNumbers[op.b], "Op[" + i.ToString() + "] b not defined.");
                        Assert.IsTrue(op.output >= 0 && op.output < definedNumbers.Length);
                        definedNumbers[op.output] = true;
                        break;

                    case OpCode.MakeDamage:
                        Assert.IsTrue(definedNumbers[op.a], "Op[" + i.ToString() + "] baseDamage not defined.");
                        Assert.IsTrue(op.output >= 0 && op.output < definedDamageSpecs.Length);
                        definedDamageSpecs[op.output] = true;
                        break;

                    case OpCode.EmitApplyDamage:
                        Assert.IsTrue(definedDamageSpecs[op.a], "Op[" + i.ToString() + "] spec not defined.");
                        break;
                }
            }
        }
    }
}
