using System.Collections.Generic;

namespace Combat.Runtime.GraphIR
{
    public static class GraphIRExamples
    {
        public static GraphIR CreateMinimalValidExample()
        {
            var entryNode = new IRNode
            {
                nodeId = "entry",
                nodeType = IRNodeType.OnHitEntry,
                ports = new Dictionary<string, IRPort>(1)
            };
            entryNode.ports.Add(
                "out",
                new IRPort
                {
                    portName = "out",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.Out,
                });

            var sinkNode = new IRNode
            {
                nodeId = "sink",
                nodeType = IRNodeType.Add,
                ports = new Dictionary<string, IRPort>(1)
            };
            sinkNode.ports.Add(
                "in",
                new IRPort
                {
                    portName = "in",
                    portType = IRPortType.Number,
                    direction = IRPortDirection.In,
                });

            var graph = new GraphIR
            {
                graphId = "example_minimal_valid",
                version = 1,
                entryNodeId = "entry",
                nodes = new List<IRNode>(2) { entryNode, sinkNode },
                edges = new List<IREdge>(1)
                {
                    new IREdge
                    {
                        fromNodeId = "entry",
                        fromPort = "out",
                        toNodeId = "sink",
                        toPort = "in",
                    }
                }
            };

            return graph;
        }

        public const string MinimalValidExampleJson = @"{
  ""graphId"": ""example_minimal_valid"",
  ""version"": 1,
  ""entryNodeId"": ""entry"",
  ""nodes"": [
    {
      ""nodeId"": ""entry"",
      ""nodeType"": ""OnHitEntry"",
      ""ports"": {
        ""out"": { ""portName"": ""out"", ""portType"": ""Number"", ""direction"": ""Out"" }
      }
    },
    {
      ""nodeId"": ""sink"",
      ""nodeType"": ""Add"",
      ""ports"": {
        ""in"": { ""portName"": ""in"", ""portType"": ""Number"", ""direction"": ""In"" }
      }
    }
  ],
  ""edges"": [
    { ""fromNodeId"": ""entry"", ""fromPort"": ""out"", ""toNodeId"": ""sink"", ""toPort"": ""in"" }
  ]
}";
    }
}

