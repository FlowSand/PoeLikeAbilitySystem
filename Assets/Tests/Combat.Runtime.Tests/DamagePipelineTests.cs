using Combat.Runtime.Combat;
using Combat.Runtime.Events;
using Combat.Runtime.Model;
using NUnit.Framework;

namespace Combat.Runtime.Tests
{
    /// <summary>
    /// DamagePipeline 单元测试
    /// 验证伤害结算管线的各项功能：暴击、抗性、多分量伤害、确定性随机等
    /// </summary>
    public sealed class DamagePipelineTests
    {
        private BattleContext _battleContext;
        private StandardDamagePipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            EventBus eventBus = new EventBus();
            _battleContext = new BattleContext(eventBus);
            _pipeline = new StandardDamagePipeline(_battleContext);
        }

        [Test]
        public void Pipeline_CritMultiplier_AppliesCorrectly()
        {
            // 创建施法者（100% 暴击率，200% 暴击倍率）
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.CritChance, 100);      // 100% 暴击率
            casterStats.SetStat(StatType.CritMultiplier, 200);  // 200% 暴击倍率（2.0 倍）
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标（0 抗性）
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 创建命中实例（100 点火焰伤害）
            HitInstance hit = new HitInstance(casterId, targetId, 12345u, HitFlags.IsSpell);
            hit.BaseDamage = new DamagePacket(fire: 100f);

            // 执行结算
            DamageResult result = _pipeline.Resolve(hit);

            // 验证：暴击倍率应用（100 × 2.0 = 200）
            Assert.IsTrue(result.IsCrit, "Should be crit with 100% crit chance");
            Assert.AreEqual(200f, result.FinalDamage.Fire, 0.01f, "Fire damage should be 200 (100 * 2.0)");
        }

        [Test]
        public void Pipeline_Resist75Percent_MitigatesCorrectly()
        {
            // 创建施法者（0% 暴击率）
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.CritChance, 0);  // 0% 暴击率
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标（75% 火焰抗性）
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.FireResist, 75);  // 75% 火焰抗性
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 创建命中实例（100 点火焰伤害）
            HitInstance hit = new HitInstance(casterId, targetId, 12345u, HitFlags.IsSpell);
            hit.BaseDamage = new DamagePacket(fire: 100f);

            // 执行结算
            DamageResult result = _pipeline.Resolve(hit);

            // 验证：75% 抗性减免（100 × (1 - 0.75) = 25）
            Assert.IsFalse(result.IsCrit, "Should not crit with 0% crit chance");
            Assert.AreEqual(25f, result.FinalDamage.Fire, 0.01f, "Fire damage should be 25 (100 * 0.25)");
        }

        [Test]
        public void Pipeline_NegativeResist_IncreaseDamage()
        {
            // 创建施法者（0% 暴击率）
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.CritChance, 0);
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标（-50% 火焰抗性，即 150% 承伤）
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.FireResist, -50);  // -50% 抗性
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 创建命中实例（100 点火焰伤害）
            HitInstance hit = new HitInstance(casterId, targetId, 12345u, HitFlags.IsSpell);
            hit.BaseDamage = new DamagePacket(fire: 100f);

            // 执行结算
            DamageResult result = _pipeline.Resolve(hit);

            // 验证：负抗性增伤（100 × (1 - (-0.5)) = 150）
            Assert.AreEqual(150f, result.FinalDamage.Fire, 0.01f, "Fire damage should be 150 (100 * 1.5)");
        }

        [Test]
        public void Pipeline_ZeroDamage_ProducesZeroResult()
        {
            // 创建施法者
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 创建命中实例（0 伤害）
            HitInstance hit = new HitInstance(casterId, targetId, 12345u, HitFlags.IsSpell);
            hit.BaseDamage = new DamagePacket();  // 全 0

            // 执行结算
            DamageResult result = _pipeline.Resolve(hit);

            // 验证：总伤害为 0
            Assert.AreEqual(0f, result.GetTotalDamage(), 0.01f, "Total damage should be 0");
        }

        [Test]
        public void Pipeline_MultiComponentDamage_AccumulatesCorrectly()
        {
            // 创建施法者（0% 暴击率）
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.CritChance, 0);
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标（所有抗性 0）
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 创建命中实例（多分量伤害：100 物理 + 50 火焰 + 30 冰霜）
            HitInstance hit = new HitInstance(casterId, targetId, 12345u, HitFlags.IsAttack);
            hit.BaseDamage = new DamagePacket(physical: 100f, fire: 50f, cold: 30f);

            // 执行结算
            DamageResult result = _pipeline.Resolve(hit);

            // 验证：各分量独立，总伤害为和
            Assert.AreEqual(100f, result.FinalDamage.Physical, 0.01f, "Physical damage should be 100");
            Assert.AreEqual(50f, result.FinalDamage.Fire, 0.01f, "Fire damage should be 50");
            Assert.AreEqual(30f, result.FinalDamage.Cold, 0.01f, "Cold damage should be 30");
            Assert.AreEqual(180f, result.GetTotalDamage(), 0.01f, "Total damage should be 180 (100+50+30)");
        }

        [Test]
        public void Pipeline_DeterministicRandomness_SameSeedSameResult()
        {
            // 创建施法者（50% 暴击率）
            UnitId casterId = new UnitId(1);
            StatCollection casterStats = new StatCollection();
            casterStats.SetStat(StatType.CritChance, 50);       // 50% 暴击率
            casterStats.SetStat(StatType.CritMultiplier, 150);  // 150% 暴击倍率
            casterStats.SetStat(StatType.MaxHealth, 1000);
            casterStats.SetStat(StatType.Health, 1000);
            Unit caster = new Unit(casterId, casterStats);
            _battleContext.AddUnit(caster);

            // 创建目标
            UnitId targetId = new UnitId(2);
            StatCollection targetStats = new StatCollection();
            targetStats.SetStat(StatType.MaxHealth, 1000);
            targetStats.SetStat(StatType.Health, 1000);
            Unit target = new Unit(targetId, targetStats);
            _battleContext.AddUnit(target);

            // 相同 seed 执行两次
            uint seed = 99999u;

            HitInstance hit1 = new HitInstance(casterId, targetId, seed, HitFlags.IsSpell);
            hit1.BaseDamage = new DamagePacket(fire: 100f);
            DamageResult result1 = _pipeline.Resolve(hit1);

            HitInstance hit2 = new HitInstance(casterId, targetId, seed, HitFlags.IsSpell);
            hit2.BaseDamage = new DamagePacket(fire: 100f);
            DamageResult result2 = _pipeline.Resolve(hit2);

            // 验证：相同 seed，结果完全一致
            Assert.AreEqual(result1.IsCrit, result2.IsCrit, "Crit result should be identical with same seed");
            Assert.AreEqual(result1.FinalDamage.Fire, result2.FinalDamage.Fire, 0.01f, "Fire damage should be identical with same seed");

            // 验证：不同 seed，结果可能不同
            HitInstance hit3 = new HitInstance(casterId, targetId, 88888u, HitFlags.IsSpell);
            hit3.BaseDamage = new DamagePacket(fire: 100f);
            DamageResult result3 = _pipeline.Resolve(hit3);

            // 注意：这里不能强制断言不同，因为可能偶然相同，但概率很低
            // 我们只验证前两次相同即可
        }
    }
}
