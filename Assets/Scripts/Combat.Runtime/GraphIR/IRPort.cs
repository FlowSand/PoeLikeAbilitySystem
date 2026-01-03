using System;

namespace Combat.Runtime.GraphIR
{
    [Serializable]
    public sealed class IRPort
    {
        public string portName;
        public IRPortType portType;
        public IRPortDirection direction;
    }
}

