namespace Combat.Runtime.Model
{
    /// <summary>
    /// 目标防御快照，用于伤害结算时的防御数据
    /// </summary>
    public struct DefenseSnapshot
    {
        /// <summary>
        /// 物理抗性（百分比，0.0 ~ 1.0，可为负）
        /// </summary>
        public float PhysicalResist;

        /// <summary>
        /// 火焰抗性（百分比，0.0 ~ 1.0，可为负）
        /// </summary>
        public float FireResist;

        /// <summary>
        /// 冰霜抗性（百分比，0.0 ~ 1.0，可为负）
        /// </summary>
        public float ColdResist;

        /// <summary>
        /// 闪电抗性（百分比，0.0 ~ 1.0，可为负）
        /// </summary>
        public float LightningResist;

        /// <summary>
        /// 混沌抗性（百分比，0.0 ~ 1.0，可为负）
        /// </summary>
        public float ChaosResist;

        /// <summary>
        /// 护甲（预留字段，暂未使用）
        /// </summary>
        public float Armor;

        /// <summary>
        /// 闪避（预留字段，暂未使用）
        /// </summary>
        public float Evasion;

        public DefenseSnapshot(
            float physicalResist = 0f,
            float fireResist = 0f,
            float coldResist = 0f,
            float lightningResist = 0f,
            float chaosResist = 0f,
            float armor = 0f,
            float evasion = 0f)
        {
            PhysicalResist = physicalResist;
            FireResist = fireResist;
            ColdResist = coldResist;
            LightningResist = lightningResist;
            ChaosResist = chaosResist;
            Armor = armor;
            Evasion = evasion;
        }

        /// <summary>
        /// 获取指定伤害类型的抗性
        /// </summary>
        public float GetResist(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical: return PhysicalResist;
                case DamageType.Fire: return FireResist;
                case DamageType.Cold: return ColdResist;
                case DamageType.Lightning: return LightningResist;
                case DamageType.Chaos: return ChaosResist;
                default: return 0f;
            }
        }
    }
}
