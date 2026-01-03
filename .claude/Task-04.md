你正在一个 Unity 工程中实现 PoE-like 技能系统的 Runtime 执行核心。

在此前 Task-01 ～ Task-03 中，以下模块已经存在：
- Combat.Runtime 战斗模型（Unit / Stats / Damage）
- EventBus + ICombatEvent
- CommandBuffer（两阶段提交）
- GraphIR
- ExecPlan（编译后的技能执行计划）

【本任务的目标】
实现一个“可控、可复现、不会指数爆炸”的技能执行 Runtime，包括：
- EventQueue（事件调度）
- ExecPlanRunner（执行 ExecPlan）
- Trigger Depth 控制
- Execution Budget 控制

这是系统是否能支撑 PoE-like 的**生死线**。

---

## 一、强制架构约束（ABSOLUTE）

1. 所有技能执行 **必须从 EventQueue 触发**
2. ExecPlanRunner **只能执行 ExecPlan**
3. ExecPlanRunner：
    - ❌ 不得解释 GraphIR
    - ❌ 不得直接修改战斗状态
    - ✅ 只能生成 Command
4. Command 必须通过 CommandBuffer 两阶段提交
5. 禁止在 Update 中执行技能逻辑
6. 禁止递归调用 ExecPlanRunner（必须通过 EventQueue）

---

## 二、需要实现的核心模块

### 1️⃣ EventQueue

实现一个战斗内事件队列。

#### 接口建议
- Enqueue(ICombatEvent evt)
- Dequeue() → ICombatEvent
- HasPendingEvents

#### 约束
- FIFO 或 BFS 均可
- 每个事件必须携带：
    - rootEventId
    - triggerDepth
    - randomSeed

---

### 2️⃣ ExecutionContext

为每次 ExecPlan 执行构建 Context。

必须包含：
- 当前 Event
- CasterUnitId
- TargetUnitId（如有）
- RandomSeed
- TriggerDepth
- ExecutionBudget（引用）

---

### 3️⃣ ExecPlanRunner

ExecPlanRunner 是 Runtime 的“虚拟机”。

#### 职责
- 顺序执行 ExecPlan.operations
- 维护 slot 数据（数组，不允许 Dictionary）
- 根据 OpCode 调用对应 handler
- 生成 ICombatCommand，写入 CommandBuffer

#### 约束
- 不允许反射
- 不允许 LINQ
- 不允许 new 大对象（避免 GC）

---

### 4️⃣ Trigger Depth Limiter

实现触发链深度限制。

#### 规则
- ExecutionContext.triggerDepth + 1
- 若超过 MAX_TRIGGER_DEPTH：
    - 中断执行
    - 记录错误日志或 trace
    - 不影响战斗主循环

---

### 5️⃣ Execution Budget

为每条“事件链”限制执行规模。

#### 至少包含
- MaxOpCount（最大 Op 执行数）
- MaxCommandCount（最大命令数）

#### 行为
- 超过预算 → 立即中断当前 ExecPlan
- 已生成的 Command 仍可提交（可选，但需一致）

---

## 三、ExecPlan 执行流程（必须严格遵循）

1. EventQueue 出队一个 ICombatEvent
2. 查找该 Event 对应的 ExecPlan
3. 构建 ExecutionContext
4. ExecPlanRunner.Execute(plan, context, commandBuffer)
5. 将 CommandBuffer 提交：
    - Phase 1：ApplyAll → 修改状态
    - Phase 2：生成新事件 → Enqueue

---

## 四、必须支持的最小 OpCode（与 Task-03 对齐）

- CONST_NUMBER
- GET_STAT
- ADD
- MUL
- MAKE_DAMAGE
- EMIT_APPLY_DAMAGE

（可以为未来扩展预留 switch default）

---

## 五、必须提供的测试（不可省略）

### 测试 1：基本执行
- 构造一个 ExecPlan（火球）
- 触发 OnCastEvent
- 执行后：
    - CommandBuffer 里有 ApplyDamageCommand
    - 提交后目标 HP 减少

### 测试 2：触发深度限制
- 构造 OnHit → 再触发 OnHit 的事件链
- 超过 MAX_TRIGGER_DEPTH 后：
    - 执行被中断
    - 程序不崩溃

### 测试 3：预算限制
- 构造一个 Op 数量超预算的 ExecPlan
- 执行中断
- 不出现死循环或卡死

---

## 六、交付要求（DELIVERABLES）

1. 新增 / 修改的文件路径列表
2. 核心类说明：
    - EventQueue
    - ExecutionContext
    - ExecPlanRunner
    - ExecutionBudget
3. 简要说明：
    - 为什么必须使用 EventQueue 而不是递归
    - TriggerDepth 与 Budget 的区别
4. 所有测试可运行、通过

---

## 七、重要提醒（FAIL 条件）

以下任一情况，视为任务失败：
- ExecPlanRunner 直接修改 Unit 状态
- 在执行中递归触发 ExecPlanRunner
- 没有 TriggerDepth 限制
- 没有 ExecutionBudget
- 技能逻辑出现在 Update

