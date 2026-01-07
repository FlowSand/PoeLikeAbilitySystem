using Combat.Runtime.Combat.Steps;

namespace Combat.Runtime.Combat
{
    /// <summary>
    /// 标准伤害结算管线
    /// 包含完整的结算流程：暴击判定 → 抗性减免 → 最终化
    /// </summary>
    public class StandardDamagePipeline : DamagePipeline
    {
        public StandardDamagePipeline(BattleContext battleContext)
        {
            // 按顺序添加 Steps
            AddStep(new RollCritStep(battleContext));
            AddStep(new ApplyResistStep(battleContext));
            AddStep(new FinalizeStep());
        }
    }
}
