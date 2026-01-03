using System;

namespace Combat.Runtime.GraphIR
{
    public readonly struct ValidationError
    {
        public readonly string nodeId;
        public readonly string message;

        public ValidationError(string nodeId, string message)
        {
            this.nodeId = nodeId;
            this.message = message;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(nodeId)) return message ?? string.Empty;
            return nodeId + ": " + (message ?? string.Empty);
        }
    }
}

