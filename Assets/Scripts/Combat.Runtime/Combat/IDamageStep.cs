namespace Combat.Runtime.Combat
{
    /// <summary>
    /// 伤害结算管线的单个步骤接口
    /// </summary>
    public interface IDamageStep
    {
        /// <summary>
        /// Step 名称（用于 Trace）
        /// </summary>
        string StepName { get; }

        /// <summary>
        /// 执行此 Step，修改 PipelineContext 中的状态
        /// </summary>
        /// <param name="context">Pipeline 上下文</param>
        void Execute(PipelineContext context);
    }
}
