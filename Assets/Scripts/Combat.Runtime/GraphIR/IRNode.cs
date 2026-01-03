using System;
using System.Collections.Generic;

namespace Combat.Runtime.GraphIR
{
    [Serializable]
    public sealed class IRNode
    {
        public string nodeId;
        public IRNodeType nodeType;
        public Dictionary<string, IRPort> ports;
        public Dictionary<string, int> intParams;
        public List<string> tags;

        public IRNode()
        {
            ports = new Dictionary<string, IRPort>();
            intParams = new Dictionary<string, int>();
            tags = new List<string>();
        }
    }
}
