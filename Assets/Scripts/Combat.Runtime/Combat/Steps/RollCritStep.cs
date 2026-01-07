using Combat.Runtime.Model;

namespace Combat.Runtime.Combat.Steps
{
    /// <summary>
    /// 暴击判定 Step
    /// 根据施法者的暴击率进行 roll，如果成功则设置 IsCrit 标志
    /// 暴击后会应用暴击倍率到伤害上
    /// </summary>
    public class RollCritStep : IDamageStep
    {
        public string StepName => "RollCrit";

        private readonly BattleContext _battleContext;

        public RollCritStep(BattleContext battleContext)
        {
            _battleContext = battleContext;
        }

        public void Execute(PipelineContext context)
        {
            // 获取施法者 Unit
            if (!_battleContext.TryGetUnit(context.Hit.SourceUnitId, out Unit caster))
            {
                // 施法者不存在，跳过暴击判定
                context.IsCrit = false;
                return;
            }

            // 读取暴击率（百分比，0 ~ 100）
            float critChance = caster.Stats.GetStat(StatType.CritChance);

            // 暴击率转换为 0.0 ~ 1.0 概率
            float critProbability = critChance / 100f;

            // 使用确定性 Rng 进行 Roll
            bool isCrit = context.Hit.Rng.Roll(critProbability);
            context.IsCrit = isCrit;

            // 如果暴击，应用暴击倍率
            if (isCrit)
            {
                // 读取暴击倍率（百分比，例如 150 表示 1.5 倍）
                float critMultiplier = caster.Stats.GetStat(StatType.CritMultiplier);
                if (critMultiplier == 0)
                {
                    // 默认暴击倍率 150%（1.5 倍）
                    critMultiplier = 150f;
                }

                float multiplier = critMultiplier / 100f;
                context.CurrentDamage = context.CurrentDamage.MultiplyAll(multiplier);
            }
        }
    }
}
