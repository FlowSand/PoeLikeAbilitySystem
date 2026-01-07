using Combat.Runtime.Model;

namespace Combat.Runtime.Combat.Steps
{
    /// <summary>
    /// 抗性减免 Step
    /// 根据目标的元素抗性，对各分量伤害进行减免
    /// 公式：最终伤害 = 原始伤害 × (1 - 抗性)
    /// 抗性为负数时，实际增加伤害
    /// </summary>
    public class ApplyResistStep : IDamageStep
    {
        public string StepName => "ApplyResist";

        private readonly BattleContext _battleContext;

        public ApplyResistStep(BattleContext battleContext)
        {
            _battleContext = battleContext;
        }

        public void Execute(PipelineContext context)
        {
            // 获取目标 Unit
            if (!_battleContext.TryGetUnit(context.Hit.TargetUnitId, out Unit target))
            {
                // 目标不存在，跳过抗性减免
                return;
            }

            // 读取目标的各项抗性（从 DefenseSnapshot 或 Unit.Stats）
            // 优先使用 DefenseSnapshot（如果已经设置）
            DefenseSnapshot defense = context.Hit.DefenseSnapshot;

            // 如果 DefenseSnapshot 未设置，从 Unit.Stats 读取
            if (defense.PhysicalResist == 0 && defense.FireResist == 0 &&
                defense.ColdResist == 0 && defense.LightningResist == 0 && defense.ChaosResist == 0)
            {
                // 从 Unit.Stats 读取抗性（百分比，0 ~ 100，可为负）
                defense = new DefenseSnapshot(
                    physicalResist: target.Stats.GetStat(StatType.PhysicalResist) / 100f,
                    fireResist: target.Stats.GetStat(StatType.FireResist) / 100f,
                    coldResist: target.Stats.GetStat(StatType.ColdResist) / 100f,
                    lightningResist: target.Stats.GetStat(StatType.LightningResist) / 100f,
                    chaosResist: target.Stats.GetStat(StatType.ChaosResist) / 100f
                );
            }

            // 对各分量伤害应用抗性
            DamagePacket damage = context.CurrentDamage;

            damage.Physical *= (1f - defense.PhysicalResist);
            damage.Fire *= (1f - defense.FireResist);
            damage.Cold *= (1f - defense.ColdResist);
            damage.Lightning *= (1f - defense.LightningResist);
            damage.Chaos *= (1f - defense.ChaosResist);

            context.CurrentDamage = damage;
        }
    }
}
