namespace Combat.Runtime.Combat.Steps
{
    /// <summary>
    /// 最终结算 Step
    /// 对伤害进行最终处理（例如取整、最小值限制等）
    /// </summary>
    public class FinalizeStep : IDamageStep
    {
        public string StepName => "Finalize";

        public void Execute(PipelineContext context)
        {
            // 确保伤害不为负数（最小值为 0）
            var damage = context.CurrentDamage;

            if (damage.Physical < 0) damage.Physical = 0;
            if (damage.Fire < 0) damage.Fire = 0;
            if (damage.Cold < 0) damage.Cold = 0;
            if (damage.Lightning < 0) damage.Lightning = 0;
            if (damage.Chaos < 0) damage.Chaos = 0;

            context.CurrentDamage = damage;
        }
    }
}
