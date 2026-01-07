namespace Combat.Runtime.Model
{
    /// <summary>
    /// 命中实例，表示一次完整的命中事件上下文
    /// 包含来源、目标、技能信息、随机数生成器等所有结算所需数据
    /// </summary>
    public class HitInstance
    {
        /// <summary>
        /// 来源单位 ID
        /// </summary>
        public UnitId SourceUnitId { get; }

        /// <summary>
        /// 目标单位 ID
        /// </summary>
        public UnitId TargetUnitId { get; }

        /// <summary>
        /// 技能实例 ID（预留字段，后续可扩展为 SkillInstance 类）
        /// </summary>
        public int SkillInstanceId { get; }

        /// <summary>
        /// 命中时间（预留字段，用于时序相关的逻辑）
        /// </summary>
        public float HitTime { get; }

        /// <summary>
        /// 确定性随机数生成器
        /// 所有随机判定（暴击、异常状态等）必须使用此 Rng，确保可复现
        /// </summary>
        public DeterministicRng Rng { get; }

        /// <summary>
        /// 命中标志位（法术、攻击、投射物、范围等）
        /// </summary>
        public HitFlags Flags { get; }

        /// <summary>
        /// 初始伤害包（结算前的原始伤害）
        /// </summary>
        public DamagePacket BaseDamage { get; set; }

        /// <summary>
        /// 目标防御快照（结算时捕获的目标防御数据）
        /// </summary>
        public DefenseSnapshot DefenseSnapshot { get; set; }

        public HitInstance(
            UnitId sourceUnitId,
            UnitId targetUnitId,
            uint randomSeed,
            HitFlags flags,
            int skillInstanceId = 0,
            float hitTime = 0f)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            SkillInstanceId = skillInstanceId;
            HitTime = hitTime;
            Rng = new DeterministicRng(randomSeed);
            Flags = flags;
            BaseDamage = new DamagePacket();
            DefenseSnapshot = new DefenseSnapshot();
        }
    }
}
