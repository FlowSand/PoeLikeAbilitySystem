using System.Collections.Generic;
using Combat.Runtime.Model;

namespace Combat.Runtime.Combat
{
    /// <summary>
    /// 伤害结算管线抽象基类
    /// 定义标准的伤害结算流程：命中判定 → 暴击判定 → 抗性减免 → 最终化
    /// </summary>
    public abstract class DamagePipeline
    {
        /// <summary>
        /// 结算步骤列表（按顺序执行）
        /// </summary>
        protected readonly List<IDamageStep> _steps = new List<IDamageStep>();

        /// <summary>
        /// 添加一个结算步骤
        /// </summary>
        protected void AddStep(IDamageStep step)
        {
            _steps.Add(step);
        }

        /// <summary>
        /// 执行整个伤害结算管线，返回最终结果
        /// </summary>
        /// <param name="hit">命中实例</param>
        /// <param name="enableTrace">是否启用 Trace</param>
        /// <returns>伤害结算结果</returns>
        public DamageResult Resolve(HitInstance hit, bool enableTrace = false)
        {
            // 创建上下文
            PipelineContext context = new PipelineContext(hit);

            // 可选启用 Trace
            if (enableTrace)
            {
                context.TraceCollector = new PipelineTraceCollector();
            }

            // 依次执行所有 Step
            foreach (var step in _steps)
            {
                string inputSummary = null;
                string outputSummary = null;

                // 记录输入（如果启用 Trace）
                if (enableTrace)
                {
                    inputSummary = CreateStepInputSummary(context);
                }

                // 执行 Step
                step.Execute(context);

                // 记录输出（如果启用 Trace）
                if (enableTrace)
                {
                    outputSummary = CreateStepOutputSummary(context);
                    context.TraceCollector.RecordStep(step.StepName, inputSummary, outputSummary);
                }
            }

            // 返回最终结果
            return new DamageResult(context.CurrentDamage, context.IsCrit, context.IsHit, context.IsBlocked);
        }

        /// <summary>
        /// 创建 Step 输入摘要（子类可重写）
        /// </summary>
        protected virtual string CreateStepInputSummary(PipelineContext context)
        {
            return $"Damage: {context.CurrentDamage.GetTotal():F1}, IsCrit: {context.IsCrit}";
        }

        /// <summary>
        /// 创建 Step 输出摘要（子类可重写）
        /// </summary>
        protected virtual string CreateStepOutputSummary(PipelineContext context)
        {
            return $"Damage: {context.CurrentDamage.GetTotal():F1}, IsCrit: {context.IsCrit}";
        }

        /// <summary>
        /// 获取最近一次执行的 Trace 数据（如果启用了 Trace）
        /// 注意：此方法需要在 Resolve 时传入 enableTrace: true，否则返回 null
        /// </summary>
        public PipelineTracePoint[] GetLastTrace(PipelineContext context)
        {
            return context?.TraceCollector?.GetTracePoints();
        }
    }
}
