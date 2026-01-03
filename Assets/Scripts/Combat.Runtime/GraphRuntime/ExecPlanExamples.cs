using System.Collections.Generic;
using Combat.Runtime.GraphIR;

namespace Combat.Runtime.GraphRuntime
{
    using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;

    public static class ExecPlanExamples
    {
        public static GraphIRModel CreateFireballGraphIR()
        {
            var constDamage = new IRNode
            {
                nodeId = "const_damage",
                nodeType = IRNodeType.ConstNumber,
                ports = new Dictionary<string, IRPort>(1),
                intParams = new Dictionary<string, int>(1),
            };
            constDamage.ports.Add(
                "out",
                new IRPort
                {
                    portName = "out",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.Out,
                });
            constDamage.intParams.Add("value", 100);

            var makeDamage = new IRNode
            {
                nodeId = "make_damage",
                nodeType = IRNodeType.MakeDamageSpec,
                ports = new Dictionary<string, IRPort>(2),
                intParams = new Dictionary<string, int>(1),
            };
            makeDamage.ports.Add(
                "base",
                new IRPort
                {
                    portName = "base",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.In,
                });
            makeDamage.ports.Add(
                "out",
                new IRPort
                {
                    portName = "out",
                    portType = IRPortType.DamageSpec,
                    direction = IRPortDirection.Out,
                });
            makeDamage.intParams.Add("damageType", 1); // Fire

            var emitDamage = new IRNode
            {
                nodeId = "emit_damage",
                nodeType = IRNodeType.EmitApplyDamageCommand,
                ports = new Dictionary<string, IRPort>(1),
            };
            emitDamage.ports.Add(
                "spec",
                new IRPort
                {
                    portName = "spec",
                    portType = IRPortType.DamageSpec,
                    direction = IRPortDirection.In,
                });

            var graph = new GraphIRModel
            {
                graphId = "example_fireball",
                version = 1,
                entryNodeId = "const_damage",
                nodes = new List<IRNode>(3) { constDamage, makeDamage, emitDamage },
                edges = new List<IREdge>(2)
                {
                    new IREdge
                    {
                        fromNodeId = "const_damage",
                        fromPort = "out",
                        toNodeId = "make_damage",
                        toPort = "base",
                    },
                    new IREdge
                    {
                        fromNodeId = "make_damage",
                        fromPort = "out",
                        toNodeId = "emit_damage",
                        toPort = "spec",
                    }
                },
            };

            return graph;
        }

        public static ExecPlan CompileFireballExecPlan()
        {
            var (plan, opToNodeId) = ExecPlanCompiler.Compile(CreateFireballGraphIR());
            return plan;
        }
    }
}

