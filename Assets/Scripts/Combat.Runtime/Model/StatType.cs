namespace Combat.Runtime.Model
{
    public enum StatType : byte
    {
        // 生命相关
        Health = 0,
        MaxHealth = 1,

        // 暴击相关
        CritChance = 2,         // 暴击率（百分比，0 ~ 100）
        CritMultiplier = 3,     // 暴击倍率（百分比，默认 150 表示 1.5 倍）

        // 元素抗性（百分比，0 ~ 100，可为负）
        PhysicalResist = 4,
        FireResist = 5,
        ColdResist = 6,
        LightningResist = 7,
        ChaosResist = 8,

        // 预留：护甲与闪避
        Armor = 9,              // 护甲
        Evasion = 10,           // 闪避

        Count = 11,
    }
}

