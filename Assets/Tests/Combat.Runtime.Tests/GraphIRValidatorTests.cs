using System.Collections.Generic;
using Combat.Runtime.GraphIR;
using NUnit.Framework;

namespace Combat.Runtime.Tests
{
    public sealed class GraphIRValidatorTests
    {
        [Test]
        public void Validate_MinimalExample_IsValid()
        {
            var graph = GraphIRExamples.CreateMinimalValidExample();

            var result = GraphIRValidator.Validate(graph);

            Assert.IsTrue(result.isValid, FirstErrorOrDefault(result));
        }

        [Test]
        public void Validate_InvalidEdge_ReturnsError()
        {
            var graph = GraphIRExamples.CreateMinimalValidExample();
            graph.edges[0].toNodeId = "missing";

            var result = GraphIRValidator.Validate(graph);

            Assert.IsFalse(result.isValid);
            Assert.IsTrue(ContainsError(result, "missing", "toNodeId does not exist"), FirstErrorOrDefault(result));
        }

        [Test]
        public void Validate_TypeMismatch_Fails()
        {
            var graph = GraphIRExamples.CreateMinimalValidExample();
            graph.nodes[1].ports["in"].portType = IRPortType.Bool;

            var result = GraphIRValidator.Validate(graph);

            Assert.IsFalse(result.isValid);
            Assert.IsTrue(ContainsError(result, "sink", "portType mismatch"), FirstErrorOrDefault(result));
        }

        [Test]
        public void Validate_Cycle_Fails()
        {
            var graph = new GraphIR.GraphIR
            {
                graphId = "example_cycle",
                version = 1,
                entryNodeId = "a",
                nodes = new List<IRNode>(2),
                edges = new List<IREdge>(2),
            };

            var a = new IRNode
            {
                nodeId = "a",
                nodeType = IRNodeType.Unknown,
                ports = new Dictionary<string, IRPort>(2)
            };
            a.ports.Add(
                "out",
                new IRPort
                {
                    portName = "out",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.Out,
                });
            a.ports.Add(
                "in",
                new IRPort
                {
                    portName = "in",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.In,
                });

            var b = new IRNode
            {
                nodeId = "b",
                nodeType = IRNodeType.Unknown,
                ports = new Dictionary<string, IRPort>(2)
            };
            b.ports.Add(
                "out",
                new IRPort
                {
                    portName = "out",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.Out,
                });
            b.ports.Add(
                "in",
                new IRPort
                {
                    portName = "in",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.In,
                });

            graph.nodes.Add(a);
            graph.nodes.Add(b);
            graph.edges.Add(new IREdge { fromNodeId = "a", fromPort = "out", toNodeId = "b", toPort = "in" });
            graph.edges.Add(new IREdge { fromNodeId = "b", fromPort = "out", toNodeId = "a", toPort = "in" });

            var result = GraphIRValidator.Validate(graph);

            Assert.IsFalse(result.isValid);
            Assert.IsTrue(ContainsErrorMessage(result, "cycle"), FirstErrorOrDefault(result));
        }

        private static bool ContainsError(ValidationResult result, string expectedNodeId, string expectedMessageFragment)
        {
            int count = result.errors.Count;
            for (int i = 0; i < count; i++)
            {
                var error = result.errors[i];
                if (error.nodeId != expectedNodeId) continue;

                if (error.message != null &&
                    error.message.IndexOf(expectedMessageFragment, System.StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsErrorMessage(ValidationResult result, string expectedMessageFragment)
        {
            int count = result.errors.Count;
            for (int i = 0; i < count; i++)
            {
                var error = result.errors[i];
                if (error.message != null &&
                    error.message.IndexOf(expectedMessageFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string FirstErrorOrDefault(ValidationResult result)
        {
            if (result.errors.Count == 0) return string.Empty;
            return result.errors[0].ToString();
        }
    }
}
