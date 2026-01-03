namespace Combat.Runtime.GraphIR
{
    public enum IRNodeType : byte
    {
        Unknown = 0,

        OnCastEntry = 1,
        OnHitEntry = 2,

        ConstNumber = 10,
        GetStat = 11,
        Add = 12,
        Mul = 13,

        RollChance = 20,
        Branch = 21,

        GetCaster = 30,
        GetTarget = 31,
        FindTargetsInRadius = 32,

        MakeDamageSpec = 40,

        EmitApplyDamageCommand = 50,
        EmitApplyModifierCommand = 51,
    }
}

