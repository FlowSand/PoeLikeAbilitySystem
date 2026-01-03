你正在一个 Unity 工程中实现 PoE-like 技能系统的 Runtime 基础模块。

【目标】
实现 Combat.Runtime 层的最小可运行战斗模型，包括：
- Unit / Stats / Damage 结算
- EventBus（事件驱动）
- CommandBuffer（两阶段提交）

这是整个 Effect Graph Runtime 的地基，必须稳定、可测试。

【强制约束】
- 代码必须放在 Assets/Scripts/Combat.Runtime/
- 必须创建 asmdef：Combat.Runtime
- 禁止引用 UnityEditor、GraphProcessor、NodeGraphProcessor
- 禁止使用 LINQ（避免 GC）
- 禁止在 Update 中跑逻辑
- 所有战斗逻辑必须由 Event 驱动

【需要实现的核心模块】

1. Unit & Stats
- UnitId（struct / int）
- Unit
    - UnitId Id
    - StatCollection Stats
    - bool IsAlive
- StatCollection
    - GetStat(StatType)
    - ApplyModifier(StatModifier)

2. Damage 模型
- DamageSpec
    - sourceUnitId
    - targetUnitId
    - baseValue
    - damageType
- DamageResult
    - finalValue
    - isCrit

3. Event 系统
- ICombatEvent（接口）
- OnHitEvent
- OnCastEvent
- EventBus
    - Subscribe<T>()
    - Publish<T>()
    - 不允许反射

4. Command 系统（两阶段）
- ICombatCommand
- ApplyDamageCommand
- CommandBuffer
    - Enqueue(command)
    - ApplyAll(BattleContext)

【必须提供的测试】
- 单元测试或 PlayMode Test：
    - 创建两个 Unit
    - 发送 OnHitEvent
    - 生成 ApplyDamageCommand
    - 在 ApplyAll 后，目标 Unit 的 HP 减少

【交付要求】
- 给出新增/修改的文件路径列表
- 所有代码可编译
- 测试可运行
- 简要说明：两阶段提交为何必要
