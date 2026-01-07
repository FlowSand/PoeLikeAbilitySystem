using System;

namespace Combat.Runtime.Model
{
    /// <summary>
    /// 多分量伤害包，支持 5 种伤害类型的独立值
    /// </summary>
    public struct DamagePacket
    {
        public float Physical;
        public float Fire;
        public float Cold;
        public float Lightning;
        public float Chaos;

        public DamagePacket(float physical = 0f, float fire = 0f, float cold = 0f, float lightning = 0f, float chaos = 0f)
        {
            Physical = physical;
            Fire = fire;
            Cold = cold;
            Lightning = lightning;
            Chaos = chaos;
        }

        /// <summary>
        /// 获取指定伤害类型的值
        /// </summary>
        public float GetValue(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical: return Physical;
                case DamageType.Fire: return Fire;
                case DamageType.Cold: return Cold;
                case DamageType.Lightning: return Lightning;
                case DamageType.Chaos: return Chaos;
                default: throw new ArgumentOutOfRangeException(nameof(damageType));
            }
        }

        /// <summary>
        /// 设置指定伤害类型的值
        /// </summary>
        public void SetValue(DamageType damageType, float value)
        {
            switch (damageType)
            {
                case DamageType.Physical: Physical = value; break;
                case DamageType.Fire: Fire = value; break;
                case DamageType.Cold: Cold = value; break;
                case DamageType.Lightning: Lightning = value; break;
                case DamageType.Chaos: Chaos = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(damageType));
            }
        }

        /// <summary>
        /// 计算总伤害（所有分量之和）
        /// </summary>
        public float GetTotal()
        {
            return Physical + Fire + Cold + Lightning + Chaos;
        }

        /// <summary>
        /// 将所有分量乘以一个系数
        /// </summary>
        public DamagePacket MultiplyAll(float multiplier)
        {
            return new DamagePacket(
                Physical * multiplier,
                Fire * multiplier,
                Cold * multiplier,
                Lightning * multiplier,
                Chaos * multiplier
            );
        }

        /// <summary>
        /// 将另一个伤害包加到当前包上
        /// </summary>
        public DamagePacket Add(DamagePacket other)
        {
            return new DamagePacket(
                Physical + other.Physical,
                Fire + other.Fire,
                Cold + other.Cold,
                Lightning + other.Lightning,
                Chaos + other.Chaos
            );
        }
    }
}
