using System;

namespace Combat.Runtime.GraphIR
{
    [Serializable]
    public sealed class IREdge
    {
        public string fromNodeId;
        public string fromPort;
        public string toNodeId;
        public string toPort;
    }
}

