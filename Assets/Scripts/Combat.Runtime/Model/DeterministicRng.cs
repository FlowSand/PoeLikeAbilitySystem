namespace Combat.Runtime.Model
{
    /// <summary>
    /// 确定性随机数生成器（基于 Xorshift32 算法）
    /// 相同的 seed 保证相同的随机数序列，确保战斗可复现
    /// </summary>
    public class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(uint seed)
        {
            // 如果 seed 为 0，使用默认值（Xorshift 不能为 0）
            _state = seed == 0 ? 12345u : seed;
        }

        /// <summary>
        /// 生成下一个 32 位无符号整数
        /// </summary>
        public uint NextUInt()
        {
            // Xorshift32 算法
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>
        /// 生成 [0.0, 1.0) 范围的浮点数
        /// </summary>
        public float NextFloat()
        {
            // 将 uint 映射到 [0.0, 1.0) 范围
            return (float)(NextUInt() / (double)uint.MaxValue);
        }

        /// <summary>
        /// 生成 [min, max) 范围的浮点数
        /// </summary>
        public float NextFloat(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        /// <summary>
        /// Roll 概率判定（返回是否成功）
        /// </summary>
        /// <param name="chance">成功概率（0.0 ~ 1.0）</param>
        public bool Roll(float chance)
        {
            return NextFloat() < chance;
        }
    }
}
