namespace Combat.Runtime.GraphRuntime
{
    public enum OpCode : byte
    {
        ConstNumber = 0,
        GetStat = 1,
        Add = 2,
        Mul = 3,
        MakeDamage = 4,
        EmitApplyDamage = 5,
        GetCaster = 6,
        GetTarget = 7,
    }
}

