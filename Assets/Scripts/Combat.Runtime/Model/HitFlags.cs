using System;

namespace Combat.Runtime.Model
{
    /// <summary>
    /// 命中标志位，用于标识技能/攻击的类型特征
    /// </summary>
    [Flags]
    public enum HitFlags : byte
    {
        None = 0,

        /// <summary>
        /// 法术
        /// </summary>
        IsSpell = 1 << 0,

        /// <summary>
        /// 攻击
        /// </summary>
        IsAttack = 1 << 1,

        /// <summary>
        /// 投射物
        /// </summary>
        IsProjectile = 1 << 2,

        /// <summary>
        /// 范围伤害
        /// </summary>
        IsAoE = 1 << 3,
    }
}
