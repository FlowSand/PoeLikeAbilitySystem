using Combat.Runtime.Model;

namespace Combat.Runtime.Combat
{
    /// <summary>
    /// Pipeline Step 上下文，携带当前结算状态
    /// </summary>
    public class PipelineContext
    {
        /// <summary>
        /// 命中实例（包含所有输入数据）
        /// </summary>
        public HitInstance Hit { get; }

        /// <summary>
        /// 当前伤害包（每个 Step 可修改此值）
        /// </summary>
        public DamagePacket CurrentDamage { get; set; }

        /// <summary>
        /// 是否已暴击（RollCritStep 设置）
        /// </summary>
        public bool IsCrit { get; set; }

        /// <summary>
        /// 是否命中（预留，当前恒为 true）
        /// </summary>
        public bool IsHit { get; set; }

        /// <summary>
        /// 是否格挡（预留，当前恒为 false）
        /// </summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// Trace 数据收集器（可选，用于调试与性能分析）
        /// </summary>
        public PipelineTraceCollector TraceCollector { get; set; }

        public PipelineContext(HitInstance hit)
        {
            Hit = hit;
            CurrentDamage = hit.BaseDamage;
            IsCrit = false;
            IsHit = true;
            IsBlocked = false;
        }
    }

    /// <summary>
    /// Pipeline Trace 数据收集器
    /// </summary>
    public class PipelineTraceCollector
    {
        private System.Collections.Generic.List<PipelineTracePoint> _tracePoints =
            new System.Collections.Generic.List<PipelineTracePoint>();

        /// <summary>
        /// 添加一个 Trace 点
        /// </summary>
        public void RecordStep(string stepName, string inputSummary, string outputSummary)
        {
            _tracePoints.Add(new PipelineTracePoint
            {
                StepName = stepName,
                InputSummary = inputSummary,
                OutputSummary = outputSummary
            });
        }

        /// <summary>
        /// 获取所有 Trace 点
        /// </summary>
        public PipelineTracePoint[] GetTracePoints()
        {
            return _tracePoints.ToArray();
        }
    }

    /// <summary>
    /// Pipeline Trace 点（一个 Step 的执行记录）
    /// </summary>
    public struct PipelineTracePoint
    {
        public string StepName;
        public string InputSummary;
        public string OutputSummary;
    }
}
