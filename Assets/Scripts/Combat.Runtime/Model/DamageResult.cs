namespace Combat.Runtime.Model
{
    /// <summary>
    /// 伤害结算结果，包含最终伤害值、暴击信息、命中信息等
    /// </summary>
    public struct DamageResult
    {
        /// <summary>
        /// 最终伤害包（各分量经过所有结算步骤后的值）
        /// </summary>
        public DamagePacket FinalDamage;

        /// <summary>
        /// 是否暴击
        /// </summary>
        public bool IsCrit;

        /// <summary>
        /// 是否命中（预留字段，当前恒为 true）
        /// </summary>
        public bool IsHit;

        /// <summary>
        /// 是否格挡（预留字段，当前恒为 false）
        /// </summary>
        public bool IsBlocked;

        /// <summary>
        /// 异常状态触发列表（预留字段，用于后续 Ailment 系统）
        /// 当前为空，暂不使用
        /// </summary>
        // public List<AilmentInstance> TriggeredAilments; // 后续实现

        public DamageResult(DamagePacket finalDamage, bool isCrit, bool isHit = true, bool isBlocked = false)
        {
            FinalDamage = finalDamage;
            IsCrit = isCrit;
            IsHit = isHit;
            IsBlocked = isBlocked;
        }

        /// <summary>
        /// 获取总伤害值
        /// </summary>
        public float GetTotalDamage()
        {
            return FinalDamage.GetTotal();
        }
    }
}
