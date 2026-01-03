namespace Combat.Runtime.GraphRuntime
{
    public readonly struct Op
    {
        public readonly OpCode opCode;
        public readonly int a;
        public readonly int b;
        public readonly int output;

        public Op(OpCode opCode, int a, int b, int output)
        {
            this.opCode = opCode;
            this.a = a;
            this.b = b;
            this.output = output;
        }

        public override string ToString()
        {
            return opCode.ToString() + "(" + a.ToString() + ", " + b.ToString() + ") -> " + output.ToString();
        }
    }
}

