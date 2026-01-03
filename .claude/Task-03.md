你正在实现 Effect Graph Runtime 的核心：ExecPlan 编译器。

ExecPlan 是运行时唯一允许执行的技能逻辑形式。

【目标】
把 GraphIR 编译为线性、可缓存、可高效执行的 ExecPlan。

【强制约束】
- 禁止在运行时解释 GraphIR
- 编译期完成：
    - 拓扑排序
    - slot 分配
    - Op 生成
- ExecPlan 必须是不可变数据

【需要实现的结构】

1. ExecPlan
- planHash
- Op[] operations
- SlotLayout

2. Op
- opCode（enum）
- int a
- int b
- int out

3. OpCode（最小集）
- CONST_NUMBER
- GET_STAT
- ADD
- MUL
- MAKE_DAMAGE
- EMIT_APPLY_DAMAGE

4. SlotLayout
- 数值槽 / Entity 槽 / DamageSpec 槽
- 使用 int index，不使用 Dictionary

【编译步骤（必须实现）】
1. GraphIR 校验（复用 Task-02）
2. 拓扑排序
3. 为每个端口分配 slot
4. 生成 Op 列表
5. 生成 planHash（稳定、顺序无关）

【必须提供的测试】
- 同一 GraphIR → hash 一致
- 不同 GraphIR → hash 不同
- 编译出的 Op 顺序满足依赖

【交付要求】
- 清晰说明 slot 分配策略
- 示例：火球 GraphIR → ExecPlan Ops 列表
