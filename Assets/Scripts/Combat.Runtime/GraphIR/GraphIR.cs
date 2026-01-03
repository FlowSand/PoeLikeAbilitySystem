using System;
using System.Collections.Generic;

namespace Combat.Runtime.GraphIR
{
    [Serializable]
    public sealed class GraphIR
    {
        public string graphId;
        public int version;
        public List<IRNode> nodes;
        public List<IREdge> edges;
        public string entryNodeId;
    }
}

