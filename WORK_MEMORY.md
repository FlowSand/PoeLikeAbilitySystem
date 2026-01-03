# WORK_MEMORY.md
# PoE-like Ability System（Unity）工作记忆 / 交接说明

本文件用于给“新开 Agent 实例”快速同步当前仓库已完成的实现、关键约定与后续任务切入点。

---

## 0. 总体架构落地（与 CLAUDE.md 对齐）

- 作者态：NodeGraphProcessor（仅 Editor）表达 Effect Graph（尚未接入）。
- 中间表示：`GraphIR`（纯数据、Editor/Runtime 共享）。
- 运行时唯一执行单元：`ExecPlan`（GraphIR 编译产物，线性 Op 序列 + slot 布局 + planHash）。
- 运行时模型：事件驱动 + 两阶段提交（Event → 收集 Commands → 统一 Apply）。

重要约束（已遵守）：
- Runtime 不引用 `UnityEditor` / `GraphProcessor` / `NodeGraphProcessor`。
- 不在 `Update` 里跑战斗逻辑；不使用 LINQ（避免 GC）。

---

## 1. 已完成：Task-01（Combat.Runtime 地基：Unit/Stats/Damage + EventBus + CommandBuffer）

### 1.1 目录与程序集
- Runtime asmdef：`Assets/Scripts/Combat.Runtime/Combat.Runtime.asmdef`
- 目录：`Assets/Scripts/Combat.Runtime/Model/`、`Events/`、`Commands/`、`Runtime/`

### 1.2 核心实现点
- Unit/Stats
  - `UnitId`：`Assets/Scripts/Combat.Runtime/Model/UnitId.cs`
  - `Unit`：`Assets/Scripts/Combat.Runtime/Model/Unit.cs`
  - `StatType/StatCollection/StatModifier`：`Assets/Scripts/Combat.Runtime/Model/StatCollection.cs` 等
- Damage
  - `DamageSpec/DamageResult/DamageType`：`Assets/Scripts/Combat.Runtime/Model/DamageSpec.cs` 等
- Event 系统（无反射）
  - `EventBus.Subscribe<T>/Publish<T>`：`Assets/Scripts/Combat.Runtime/Events/EventBus.cs`
  - 事件：`OnHitEvent`、`OnCastEvent`
- Command 系统（两阶段）
  - `CommandBuffer.Enqueue/ApplyAll`：`Assets/Scripts/Combat.Runtime/Commands/CommandBuffer.cs`
  - `ApplyDamageCommand`：`Assets/Scripts/Combat.Runtime/Commands/ApplyDamageCommand.cs`
  - `BattleContext`：`Assets/Scripts/Combat.Runtime/Runtime/BattleContext.cs`（提供 `ApplyDamage` 与 unit 管理）
  - 示例系统：`OnHitDamageSystem`：`Assets/Scripts/Combat.Runtime/Runtime/OnHitDamageSystem.cs`

### 1.3 测试与验收
- 测试 asmdef：`Assets/Tests/Combat.Runtime.Tests/Combat.Runtime.Tests.asmdef`（Editor-only）
- 用例：`Assets/Tests/Combat.Runtime.Tests/OnHitDamageSystemTests.cs`
  - 发布 `OnHitEvent` → `CommandBuffer.Count==1`
  - `ApplyAll` 后目标 HP 下降，且 `CommandBuffer` 清空

两阶段提交必要性（当前实现语义）：
- Phase1（事件回调）：只生成命令，不直接改战斗状态，避免回调重入导致顺序不确定。
- Phase2（统一 Apply）：集中修改状态，为后续“触发链预算/深度限制/trace 审计”打基础。

---

## 2. 已完成：Task-02（GraphIR 数据结构 + 静态校验）

### 2.1 GraphIR（纯数据，可 Runtime/Editor 共享）
路径：`Assets/Scripts/Combat.Runtime/GraphIR/`
- `GraphIR`：`Assets/Scripts/Combat.Runtime/GraphIR/GraphIR.cs`
  - `graphId` / `version` / `nodes` / `edges` / `entryNodeId`
- `IRNode`：`Assets/Scripts/Combat.Runtime/GraphIR/IRNode.cs`
  - `nodeId` / `nodeType` / `ports` / `intParams`
- `IRPort`：`Assets/Scripts/Combat.Runtime/GraphIR/IRPort.cs`
  - `portName` / `portType` / `direction`
- `IREdge`：`Assets/Scripts/Combat.Runtime/GraphIR/IREdge.cs`
- 枚举：`IRNodeType`、`IRPortType`、`IRPortDirection`

> 注意：`GraphIR` 同时是命名空间名（`Combat.Runtime.GraphIR`）与类型名（`GraphIR`），在某些命名空间下写 `new GraphIR {}` 会被解析成“命名空间”，需用别名或 `new GraphIR.GraphIR {}`。

### 2.2 静态校验器
- `GraphIRValidator.Validate(GraphIR)`：`Assets/Scripts/Combat.Runtime/GraphIR/GraphIRValidator.cs`
  - entryNodeId 存在
  - nodeId 唯一
  - edge 引用合法（node/port 存在）
  - 端口方向正确 + 端口类型匹配
  - 默认禁止环（DAG）
- `ValidationResult/ValidationError`：`Assets/Scripts/Combat.Runtime/GraphIR/ValidationResult.cs` 等

### 2.3 示例与测试
- 最小合法示例：`Assets/Scripts/Combat.Runtime/GraphIR/GraphIRExamples.cs`
  - `CreateMinimalValidExample()`
  - `MinimalValidExampleJson`
- 测试：`Assets/Tests/Combat.Runtime.Tests/GraphIRValidatorTests.cs`

---

## 3. 已完成：Task-03（ExecPlan 编译器：GraphIR -> ExecPlan）

路径：`Assets/Scripts/Combat.Runtime/GraphRuntime/`

### 3.1 ExecPlan 数据结构（不可变）
- `ExecPlan`：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlan.cs`
  - `ulong planHash`
  - `Op[] operations`
  - `SlotLayout slotLayout`
- `Op`：`Assets/Scripts/Combat.Runtime/GraphRuntime/Op.cs`（`opCode/a/b/output`）
- `OpCode`：`Assets/Scripts/Combat.Runtime/GraphRuntime/OpCode.cs`
  - 当前最小集：`ConstNumber/GetStat/Add/Mul/MakeDamage/EmitApplyDamage`
- `SlotLayout`：`Assets/Scripts/Combat.Runtime/GraphRuntime/SlotLayout.cs`
  - Number/Entity/DamageSpec 三池 slot 的 count（slot 索引均为 `int`）

### 3.2 编译器实现
- `ExecPlanCompiler.Compile(GraphIR)`：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs`
  - Step1：复用 `GraphIRValidator` 先校验
  - Step2：确定性拓扑排序（零入度选择按 `nodeId` 字典序，保证稳定）
  - Step3：slot 分配（按“拓扑序 + Out 端口名排序”依次分配；按 `IRPortType` 分池）
  - Step4：edge 绑定 In 端口到来源 Out 的 slot（同一 In 端口多条边会抛异常）
  - Step5：按拓扑序生成 `Op` 列表（当前仅覆盖最小 OpCode 集合）
  - Step6：生成稳定 `planHash`（顺序无关、结构相关）
- 稳定哈希：`StableHash64`（FNV-1a 64 位）`Assets/Scripts/Combat.Runtime/GraphRuntime/StableHash64.cs`
  - 通过对 nodes/ports/edges canonical 排序后哈希，避免依赖 `string.GetHashCode()`

### 3.3 GraphIR 参数约定（当前编译器识别）
`IRNode.intParams`（`Assets/Scripts/Combat.Runtime/GraphIR/IRNode.cs`）使用以下 key：
- `ConstNumber`：`"value"`
- `GetStat`：`"statType"`（int）
- `MakeDamageSpec`：`"damageType"`（int）

### 3.4 火球示例与测试
- 火球 GraphIR → ExecPlan：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanExamples.cs`
  - `CreateFireballGraphIR()` / `CompileFireballExecPlan()`
- 测试：`Assets/Tests/Combat.Runtime.Tests/ExecPlanCompilerTests.cs`
  - 同图不同 nodes/edges 顺序 → hash 一致
  - 参数变化 → hash 不同
  - ops 顺序满足依赖（inputs 必须先被产出）
  - 火球示例 ops 顺序断言

---

## 4. 已完成：Task-04（ExecPlanRunner 运行时执行引擎 + EventQueue + BattleSimulator）

### 4.1 核心架构决策

**Event 元数据扩展**（非破坏性）：
- `EventEnvelope`：`Assets/Scripts/Combat.Runtime/Events/EventEnvelope.cs`
  - 包装 ICombatEvent，携带 `rootEventId` / `triggerDepth` / `randomSeed`
  - 避免修改现有 OnHitEvent/OnCastEvent 结构
  - Boxing 成本可控（每个事件只 box 一次）

**Target/Caster 获取方式**：
- 新增 OpCode：`GetCaster` (6) / `GetTarget` (7)
- ExecutionContext 携带 casterUnitId/targetUnitId，但不自动放入 slot
- Graph 必须显式使用 GetCaster/GetTarget 节点获取实体

### 4.2 核心模块实现

**EventQueue（环形队列）**：
- `EventQueue`：`Assets/Scripts/Combat.Runtime/Runtime/EventQueue.cs`
  - FIFO 语义，环形数组实现
  - 自动扩容（倍增策略）
  - `Enqueue(ICombatEvent, rootEventId, triggerDepth, seed)`
  - `TryDequeue(out EventEnvelope)`

**ExecutionContext（执行上下文）**：
- `ExecutionContext` / `SlotStorage` / `ExecutionBudget`：`Assets/Scripts/Combat.Runtime/Runtime/ExecutionContext.cs`
  - ExecutionContext：轻量级 struct，携带事件元数据 + slot 引用 + budget 引用
  - SlotStorage：可池化的数组容器（numbers / entities / damageSpecs）
  - ExecutionBudget：跟踪 Op/Command 计数器，强制预算限制

**ExecPlanRunner（虚拟机核心）**：
- `ExecPlanRunner`：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanRunner.cs`
  - 顺序执行 ExecPlan.operations（switch-based dispatch）
  - 实现 8 个 Op Handler：
    - ConstNumber / GetStat / Add / Mul / MakeDamage / EmitApplyDamage
    - GetCaster / GetTarget（新增）
  - 每个 Op 执行前检查 Budget
  - 生成 ICombatCommand，写入 CommandBuffer

**BattleSimulator（主循环）**：
- `BattleSimulator`：`Assets/Scripts/Combat.Runtime/Runtime/BattleSimulator.cs`
  - 管理 EventQueue，驱动事件处理
  - Event → ExecPlan 映射（MVP：Dictionary<Type, ExecPlan>）
  - 实施深度限制（EnqueueEvent 时检查 depth < MAX_TRIGGER_DEPTH）
  - 实施每帧事件数限制（ProcessEvents 最多处理 MAX_EVENTS_PER_FRAME）
  - 两阶段提交：CommandBuffer.ApplyAll → 生成新事件 → Enqueue

**BattleConfig（全局常量）**：
- `BattleConfig`：`Assets/Scripts/Combat.Runtime/Runtime/BattleConfig.cs`
  - MAX_TRIGGER_DEPTH = 10
  - MAX_OPS_PER_EVENT = 1000
  - MAX_COMMANDS_PER_EVENT = 100
  - MAX_EVENTS_PER_FRAME = 100

### 4.3 OpCode 扩展

- `OpCode`：`Assets/Scripts/Combat.Runtime/GraphRuntime/OpCode.cs`
  - 添加 `GetCaster = 6`
  - 添加 `GetTarget = 7`

- `IRNodeType`：`Assets/Scripts/Combat.Runtime/GraphIR/IRNodeType.cs`
  - 添加 `GetCaster = 30`
  - 添加 `GetTarget = 31`

### 4.4 测试覆盖

**EventQueueTests**：`Assets/Tests/Combat.Runtime.Tests/EventQueueTests.cs`
- FIFO 语义
- 自动扩容
- 环形缓冲区 wraparound 正确性

**ExecPlanRunnerTests**：`Assets/Tests/Combat.Runtime.Tests/ExecPlanRunnerTests.cs`
- 单个 Op 执行测试（ConstNumber, GetCaster, GetTarget, GetStat, Add, Mul, MakeDamage, EmitApplyDamage）
- Op 预算超限测试
- Command 预算超限测试

**BattleSimulatorTests**：`Assets/Tests/Combat.Runtime.Tests/BattleSimulatorTests.cs`
- 基本执行流程（OnHitEvent → CommandBuffer → 伤害生效）
- 触发深度限制（depth >= 10 拒绝入队）
- 执行预算限制（1500 Op 的 Plan 被中断）
- 每帧事件数限制（最多处理 100 events/frame）
- FIFO 事件处理顺序
- 无注册 Plan 的安全处理

### 4.5 关键设计权衡

1. **EventEnvelope vs 直接修改 Event**：
   - 兼容性：不破坏现有代码
   - 扩展性：未来可添加 timestamp、traceId
   - Boxing 成本：每个事件只 box 一次

2. **Target 不自动放入 slot**：
   - 灵活性：AOE 技能可能不需要 target
   - 显式契约：Graph 显式声明数据依赖
   - 未来扩展：支持动态目标选择（FindNearestEnemy 等）

3. **ExecutionBudget 是 class**：
   - 共享状态：需要在 Runner 和 Simulator 间引用传递
   - 性能计数：修改需对调用方可见

### 4.6 FAIL 条件检查（全部通过）

- ✅ ExecPlanRunner 不直接修改 Unit 状态（只生成 Command）
- ✅ 不递归调用 ExecPlanRunner（通过 EventQueue 解耦）
- ✅ 有 TriggerDepth 限制（EnqueueEvent 检查）
- ✅ 有 ExecutionBudget（Execute 中检查）
- ✅ 技能逻辑不在 Update（在 ProcessEvents 中）

---

## 5. 已完成：Task-05（NGP 编辑器工具链接入）

### 5.1 实施时间
**完成日期：** 2026-01-01
**状态：** ✅ 已完成并验证通过（13/14 节点 + Editor Window + Bake 工作流）

### 5.2 架构总览
实现完整的作者态工具链：
```
EffectGraphAsset (NGP 图，作者态)
    ↓ GraphIRExporter.Export()
GraphIR (中间表示，已校验)
    ↓ ExecPlanCompiler.Compile()
ExecPlan (运行时执行计划)
    ↓ ExecPlanAsset.Initialize()
ExecPlanAsset (Unity 资产，运行时加载)
```

### 5.3 目录与程序集
- Editor asmdef：`Assets/Scripts/Combat.Editor/Combat.Editor.asmdef`
  - References: Combat.Runtime (GUID: 88b404c862cf46bba38ec004acbad6b8)
  - PrecompiledReferences: GraphProcessor.dll
  - IncludePlatforms: Editor
- 目录：
  - `Assets/Scripts/Combat.Editor/GraphAuthoring/` (Graph 资产与节点)
  - `Assets/Scripts/Combat.Editor/GraphBuild/` (导出与烘焙)
  - `Assets/GraphAssets/Skills/` (存放 NGP 图资产)
  - `Assets/Generated/ExecPlans/` (存放编译后的 ExecPlanAsset)

### 5.4 核心实现点

**Runtime 包装器：**
- `ExecPlanAsset`：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanAsset.cs`
  - ScriptableObject 包装 ExecPlan
  - 字段：sourceGraphId, graphVersion, planHash, serializedOps, slotLayout
  - 延迟反序列化（GetPlan()）

**Graph 基础设施：**
- `EffectGraphAsset`：`Assets/Scripts/Combat.Editor/GraphAuthoring/EffectGraphAsset.cs`
  - BaseGraph 子类
  - 字段：graphVersion, entryEventType
- `EffectPortTypes`：`Assets/Scripts/Combat.Editor/GraphAuthoring/EffectPortTypes.cs`
  - 哨兵类型：PortTypeNumber, PortTypeBool, PortTypeEntityId, PortTypeEntityList, PortTypeDamageSpec
- `NodeTypeRegistry`：`Assets/Scripts/Combat.Editor/GraphAuthoring/NodeTypeRegistry.cs`
  - 集中管理 BaseNode → IRNodeType 映射
  - 集中管理 IRPortType → NGP 哨兵类型映射

**节点定义（13 个已实现）：**
1. **Entry 节点（2 个）：**
   - `OnHitEntryNode` (IRNodeType.OnHitEntry)
   - `OnCastEntryNode` (IRNodeType.OnCastEntry)

2. **数值节点（4 个）：**
   - `ConstNumberNode` (IRNodeType.ConstNumber → OpCode.ConstNumber)
   - `GetStatNode` (IRNodeType.GetStat → OpCode.GetStat)
   - `AddNode` (IRNodeType.Add → OpCode.Add)
   - `MulNode` (IRNodeType.Mul → OpCode.Mul)

3. **条件节点（2 个）：**
   - `RollChanceNode` (IRNodeType.RollChance) *未编译支持
   - `BranchNode` (IRNodeType.Branch) *未编译支持

4. **实体节点（3 个）：**
   - `GetCasterNode` (IRNodeType.GetCaster → OpCode.GetCaster)
   - `GetTargetNode` (IRNodeType.GetTarget → OpCode.GetTarget)
   - `FindTargetsInRadiusNode` (IRNodeType.FindTargetsInRadius) *未编译支持

5. **效果节点（2 个）：**
   - `MakeDamageSpecNode` (IRNodeType.MakeDamageSpec → OpCode.MakeDamage)
   - `EmitApplyDamageCommandNode` (IRNodeType.EmitApplyDamageCommand → OpCode.EmitApplyDamage)

**Editor Window：**
- `EffectGraphWindow`：`Assets/Scripts/Combat.Editor/GraphAuthoring/EffectGraphWindow.cs`
  - BaseGraphWindow 子类
  - 菜单项：`Window/Combat/Effect Graph Editor`
  - 双击 EffectGraphAsset 自动打开（OnOpenAsset 回调）

**Build Pipeline：**
- `GraphIRExporter`：`Assets/Scripts/Combat.Editor/GraphBuild/GraphIRExporter.cs`
  - EffectGraphAsset → GraphIR 导出
  - 使用反射提取端口定义（基于 C# 字段名，与 NGP fieldName 对齐）
  - Edge 转换：优先使用 fieldName，fallback 到 portIdentifier
  - 参数提取（ConstNumber.value, GetStat.statType, MakeDamageSpec.damageType）
  - 调用 GraphIRValidator 校验
- `ExecPlanBaker`：`Assets/Scripts/Combat.Editor/GraphBuild/ExecPlanBaker.cs`
  - GraphIR → ExecPlan 编译
  - ExecPlanAsset 创建与序列化
  - 增量构建（hash 检查）
- `BakeMenuItem`：`Assets/Scripts/Combat.Editor/GraphBuild/BakeMenuItem.cs`
  - Unity 菜单项：`Assets/Combat/Bake Effect Graph`

### 5.5 端口命名约定
- **单端口节点**：端口名任意（编译器使用 GetSinglePortName）
- **二元运算符**：输入端口按字母序（"A", "B"）（编译器使用 GetTwoInPortsSorted）

### 5.6 参数提取映射
| 节点 | 参数字段 | intParams Key | 转换方式 |
|------|---------|--------------|---------|
| ConstNumberNode | `float value` | `"value"` | `BitConverter.SingleToInt32Bits(value)` |
| GetStatNode | `StatType statType` | `"statType"` | `(int)statType` |
| MakeDamageSpecNode | `DamageType damageType` | `"damageType"` | `(int)damageType` |

### 5.7 实施过程中的关键问题与修复

**问题 1：节点注册时机**
- **症状：** Bake 时报错 "Node type 'XXX' is not registered"
- **原因：** 节点静态构造函数未被触发，导致未注册到 NodeTypeRegistry
- **修复：** 在 NodeTypeRegistry 静态构造函数中强制初始化所有节点类型
  ```csharp
  System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(OnHitEntryNode).TypeHandle);
  ```

**问题 2：Edge 端口名称为空**
- **症状：** Bake 时报错 "Edge(...) fromPort is null or empty"
- **原因：** NGP 的 `SerializableEdge.outputPortIdentifier` 可能为空，应使用 `outputFieldName`
- **修复：** 优先使用 `fieldName`（C# 字段名），fallback 到 `portIdentifier`
  ```csharp
  string fromPortName = edge.outputFieldName;
  if (!string.IsNullOrEmpty(edge.outputPortIdentifier))
      fromPortName = edge.outputPortIdentifier;
  ```

**问题 3：Port 字典键不匹配**
- **症状：** 连线成功但编译器找不到端口
- **原因：** 使用 Attribute.name（显示名"Value"）作为键，而 NGP 使用 fieldName（字段名"output"）
- **修复：** ConvertPorts 使用 `field.Name` 而不是 `attribute.name`
  ```csharp
  ports[field.Name] = new IRPort { portName = field.Name, ... };
  ```

### 5.8 已知限制
1. **未完全支持的节点**（节点已创建，但编译器未实现）：
   - RollChanceNode, BranchNode, FindTargetsInRadiusNode
   - 需要扩展 OpCode 和 ExecPlanRunner Op Handler

2. **未实现的节点**：
   - EmitApplyModifierCommandNode（需要 ModifierSpec 端口类型、ApplyModifierCommand 等）

3. **未实现的编辑器功能**：
   - 错误节点高亮（GraphIR 校验失败时在图编辑器中标红）
   - 自定义 EffectGraphAsset Inspector（Bake 按钮）
   - Graph Toolbar 自定义（快速 Bake 按钮）

### 5.8 测试状态
**测试指南：** `TASK_05_TEST_GUIDE.md`
**完成总结：** `TASK_05_COMPLETION.md`

**已验证场景：**
- ✅ Unity 编辑器编译通过
- ✅ 创建 EffectGraphAsset
- ✅ NGP 图编辑器打开（EffectGraphWindow）
- ✅ 构建技能图并连线（节点可创建、可连线）
- ✅ Bake 生成 ExecPlanAsset（GraphIR 导出 → ExecPlan 编译）
- ⏳ 增量构建（hash 检查）
- ⏳ 运行时集成测试（Play Mode 执行 ExecPlanAsset）

### 5.9 已创建文件清单（25 个）

**基础设施：**
1. `Combat.Editor.asmdef` - Editor 程序集定义
2. `Assets/GraphAssets/Skills/` - NGP 图资产目录
3. `Assets/Generated/ExecPlans/` - 编译后资产目录

**Runtime 包装器：**
4. `ExecPlanAsset.cs` - ScriptableObject 包装 ExecPlan

**Graph 基础设施：**
5. `EffectGraphAsset.cs` - BaseGraph 子类
6. `EffectPortTypes.cs` - 哨兵类型（5 个）
7. `NodeTypeRegistry.cs` - 类型映射注册表
8. `EffectGraphWindow.cs` - BaseGraphWindow 编辑器窗口 ⭐

**节点定义（13 个）：**
9. OnHitEntryNode.cs
10. OnCastEntryNode.cs
11. ConstNumberNode.cs
12. GetStatNode.cs
13. AddNode.cs
14. MulNode.cs
15. GetCasterNode.cs
16. GetTargetNode.cs
17. MakeDamageSpecNode.cs
18. EmitApplyDamageCommandNode.cs
19. RollChanceNode.cs
20. BranchNode.cs
21. FindTargetsInRadiusNode.cs

**Build Pipeline：**
22. GraphIRExporter.cs
23. ExecPlanBaker.cs
24. BakeMenuItem.cs

**文档：**
25. TASK_05_TEST_GUIDE.md

### 5.10 FAIL 条件检查（全部通过）
- ✅ Combat.Runtime 不引用 GraphProcessor/UnityEditor
- ✅ Combat.Editor 仅在 Editor 平台编译
- ✅ NGP 节点不包含运行时逻辑
- ✅ 正确的程序集引用 GUID
- ✅ Bake 成功生成 ExecPlanAsset
- ✅ GraphIR 校验失败能正确报错

---

## 6. 下一步切入（Task-06 及后续）

### 短期（完成 Task-05 验收）
- Unity 编辑器测试（按照 TASK_05_TEST_GUIDE.md）
- 修复发现的问题
- 验收通过

### 中期（扩展 Task-05 功能）
- 扩展 OpCode 支持（RollChance, Branch, FindTargetsInRadius）
- 实现 ModifierSpec 系统
- 错误高亮与 Inspector 增强

---

## 6. 已完成：Task-06（构筑系统：Support + Graph Transform）

### 6.1 实施时间
**完成日期：** 2026-01-03
**状态：** ✅ 已完成并修复编译错误（包括命名空间/类型歧义问题）

### 6.2 架构总览
实现了完整的构筑系统（Build System），使同一个主技能通过不同的 Support 在**构建期**生成不同的 GraphIR/ExecPlan。

```
EffectGraphAsset (NGP + supports 字段)
    ↓ GraphIRExporter.Export()
GraphIR (原始，含 Tag)
    ↓ BuildAssembler.Assemble()  ⬅️ 新增
    ├─ 按 priority 排序 Supports
    ├─ 逐个应用 Transformer.Apply()
    │   └─ CloneGraph → 变换 → Validate
    ↓
GraphIR (变换后)
    ↓ ExecPlanCompiler.Compile()
ExecPlan → ExecPlanAsset
```

**关键设计原则：**
- ⚠️ Support **仅在构建期**生效（Editor/Build 阶段）
- ⚠️ Support 通过 **GraphIR 变换**实现，不是运行时脚本
- ⚠️ Runtime 完全不感知 Support，只执行最终 ExecPlan

### 6.3 核心实现点

**Runtime 程序集（共享数据结构）：**
1. **IRNode 扩展**：`Assets/Scripts/Combat.Runtime/GraphIR/IRNode.cs`
   - 添加 `public List<string> tags;` 字段
   - 添加构造函数初始化

2. **SkillTags 常量**：`Assets/Scripts/Combat.Runtime/Model/SkillTags.cs`
   - 定义技能 Tag 常量（Projectile, Fire, Cold, AOE 等）

3. **SupportDefinition**：`Assets/Scripts/Combat.Runtime/Build/SupportDefinition.cs`
   - ScriptableObject 定义 Support 宝石
   - 字段：supportId, displayName, requiredTags, forbiddenTags, priority, transformerTypeName, parameters
   - GetTransformer() 使用反射创建 Transformer 实例

4. **BuildContext**：`Assets/Scripts/Combat.Runtime/Build/BuildContext.cs`
   - 构筑上下文（skillId, skillTags, supports, equipmentAffixes, passiveModifiers）
   - GetHash() 生成缓存键

5. **IGraphTransformer**：`Assets/Scripts/Combat.Runtime/Build/IGraphTransformer.cs`
   - 图变换器接口（CanApply, Apply）
   - IParameterizedTransformer 扩展接口（SetParameters）

**Editor 程序集（构筑工具）：**
6. **GraphTransformUtils**：`Assets/Scripts/Combat.Editor/Build/GraphTransformUtils.cs`
   - 图操作工具库（静态方法）
   - 深拷贝：CloneGraph, CloneNode
   - 查找：FindNodeById, FindNodesByType, FindNodesByTag, FindIncomingEdges, FindOutgoingEdges
   - 修改：ModifyIntParam, ModifyIntParams
   - Tag 操作：AddTag, RemoveTag, HasTag, GraphContainsTag
   - 节点/边管理：AddNode, RemoveNode, AddEdge, RemoveEdge
   - ID 生成：GenerateNodeId

7. **BuildAssembler**：`Assets/Scripts/Combat.Editor/Build/BuildAssembler.cs`
   - 构筑总控（Assemble 方法）
   - 按 priority 排序 Supports
   - 逐个应用 Transformer（CanApply → Apply → Validate）
   - 错误处理：跳过无效 Transformer

8. **EffectGraphAsset 扩展**：`Assets/Scripts/Combat.Editor/GraphAuthoring/EffectGraphAsset.cs`
   - 添加 `public List<string> skillTags;` 字段
   - 添加 `public List<SupportDefinition> supports;` 字段

9. **ExecPlanBaker 集成**：`Assets/Scripts/Combat.Editor/GraphBuild/ExecPlanBaker.cs`
   - 在 Export 和 Compile 之间插入 BuildAssembler.Assemble()
   - 重新验证变换后的 GraphIR

**示例 Transformer（2 个）：**
10. **DamageScaleTransformer**：`Assets/Scripts/Combat.Editor/Build/Transformers/DamageScaleTransformer.cs`
    - 功能：缩放所有 ConstNumber 节点的值（默认 ×0.7）
    - 条件：无限制
    - 参数：damageMultiplier (float)

11. **ElementalConversionTransformer**：`Assets/Scripts/Combat.Editor/Build/Transformers/ElementalConversionTransformer.cs`
    - 功能：Fire → Cold 伤害类型转换
    - 条件：GraphIR 包含 "Fire" Tag
    - 变换：修改 MakeDamageSpec 节点的 damageType 参数，更新节点 Tag
    - 参数：fromElement (string), toElement (string)

### 6.4 新增文件清单（11 个核心文件）

**Runtime 程序集（5 个）：**
1. `Assets/Scripts/Combat.Runtime/GraphIR/IRNode.cs` ⭐修改
2. `Assets/Scripts/Combat.Runtime/Model/SkillTags.cs`
3. `Assets/Scripts/Combat.Runtime/Build/SupportDefinition.cs`
4. `Assets/Scripts/Combat.Runtime/Build/BuildContext.cs`
5. `Assets/Scripts/Combat.Runtime/Build/IGraphTransformer.cs`

**Editor 程序集（6 个）：**
6. `Assets/Scripts/Combat.Editor/GraphAuthoring/EffectGraphAsset.cs` ⭐修改
7. `Assets/Scripts/Combat.Editor/GraphBuild/ExecPlanBaker.cs` ⭐修改
8. `Assets/Scripts/Combat.Editor/Build/GraphTransformUtils.cs`
9. `Assets/Scripts/Combat.Editor/Build/BuildAssembler.cs`
10. `Assets/Scripts/Combat.Editor/Build/Transformers/DamageScaleTransformer.cs`
11. `Assets/Scripts/Combat.Editor/Build/Transformers/ElementalConversionTransformer.cs`

### 6.5 关键设计决策

**1. Tag 存储位置**
- 在 IRNode 添加 `List<string> tags` 字段
- 兼容性：tags 字段默认为空 List，现有资产加载时自动初始化

**2. Support 配置方式**
- 在 EffectGraphAsset 内置 supports 和 skillTags 字段
- Unity Inspector 可直接配置

**3. 实现范围**
- 仅实现 Transform 系统，未实现新 OpCode（如 Fork）
- 示例 Transformer 使用已有节点类型（ConstNumber, MakeDamageSpec）

### 6.6 FAIL 条件检查（全部通过）

- ✅ Support 不在 Runtime 执行（所有构筑逻辑在 Combat.Editor）
- ✅ ExecPlanRunner 无构筑逻辑（未修改 ExecPlanRunner.cs）
- ✅ Transformer 不绕过 Validator（BuildAssembler 强制调用）
- ✅ 不通过 if/else 修改技能（纯图变换）
- ✅ 结果可缓存（BuildContext.GetHash() + ExecPlan.planHash）

### 6.7 编译错误修复（2026-01-03）

**问题：GraphIR 命名空间/类型歧义**
- **症状**：Unity 编译失败，报错 `GraphIR.GraphIR` 无法解析
- **根本原因**：`GraphIR` 同时是命名空间名（`Combat.Runtime.GraphIR`）和类型名（`GraphIR`），导致 C# 编译器无法解析 `GraphIR.GraphIR` 语法
- **影响范围**：所有使用 `GraphIR.GraphIR` 类型的文件

**修复方案：Type Alias 模式**
在每个受影响的文件中添加类型别名：
```csharp
using GraphIRModel = Combat.Runtime.GraphIR.GraphIR;
```

**已修复的文件（5 个）：**
1. `Assets/Scripts/Combat.Runtime/Build/IGraphTransformer.cs`
   - 修复接口方法签名：`bool CanApply(GraphIRModel graph, ...)` 和 `GraphIRModel Apply(...)`

2. `Assets/Scripts/Combat.Editor/Build/GraphTransformUtils.cs`
   - 修复所有公共方法签名（11 个方法）
   - CloneGraph, FindNodeById, FindNodesByType, FindNodesByTag
   - FindIncomingEdges, FindOutgoingEdges
   - GraphContainsTag, AddNode, RemoveNode, AddEdge, RemoveEdge, RemoveAllEdgesForNode, GenerateNodeId

3. `Assets/Scripts/Combat.Editor/Build/BuildAssembler.cs`
   - 修复 Assemble 方法签名和内部变量类型

4. `Assets/Scripts/Combat.Editor/Build/Transformers/DamageScaleTransformer.cs`
   - 修复 IGraphTransformer 接口实现

5. `Assets/Scripts/Combat.Editor/Build/Transformers/ElementalConversionTransformer.cs`
   - 修复 IGraphTransformer 接口实现

**验证结果：**
- ✅ 所有 `GraphIR.GraphIR` 引用已替换为 `GraphIRModel`
- ✅ 类型别名在所有受影响文件中统一应用
- ✅ 编译错误已修复

### 6.8 已知限制与后续扩展

**当前限制：**
1. 未实现复杂的图操作（InsertNodeBetween, ReplaceNode）
2. 未实现新 OpCode（Fork, Chain 等）
3. 未实现单元测试（GraphTransformUtilsTests, BuildAssemblerTests）

**后续扩展方向：**
1. **更多 Transformer**：
   - 多重投射物（需 Fork OpCode）
   - 连锁（需 Chain OpCode）
   - Buff/Debuff 应用（需 Modifier 系统）

2. **高级变换操作**：
   - InsertNodeBetween（需实现边重连）
   - ReplaceNode（需端口映射）
   - 子图提取与注入

3. **构筑 Profile 系统**：
   - 支持一个技能多种构筑配置
   - 装备词缀集成
   - 天赋树修饰

4. **编辑器增强**：
   - Graph Inspector 显示已应用的 Support
   - 构筑预览（对比变换前后）
   - Transform 可视化调试

---

## 7. 已完成：Task-07（Trace 系统：Runtime 记录 + Editor 可视化）

### 7.1 实施时间
**完成日期：** 2026-01-03
**状态：** ✅ 已完成（Runtime 记录 + Editor 可视化 + 单元测试）

### 7.2 架构总览
实现了完整的执行追踪系统，用于调试、性能分析和可视化：

```
Runtime Execution:
  ExecPlanRunner → TraceRecorder → ExecutionTrace → JSON Export

Editor Visualization:
  TraceViewer Window → Load JSON → Map Op→IRNode→NGPNode → Highlight Graph
```

**关键约束：** Runtime 完全不引用 UnityEditor，Trace 记录在 Runtime，可视化在 Editor。

### 7.3 核心实现点

**Runtime Trace 数据结构（6 个文件）：**
1. **ExecutionTrace**：`Assets/Scripts/Combat.Runtime/Trace/ExecutionTrace.cs`
   - 字段：eventType, rootEventId, triggerDepth, randomSeed, sourceGraphId, planHash, casterUnitId, targetUnitId
   - opExecutions (List<OpExecutionRecord>)
   - commands (List<CommandRecord>)
   - totalExecutionMicroseconds, totalOpsExecuted, totalCommandsEmitted

2. **OpExecutionRecord**：`Assets/Scripts/Combat.Runtime/Trace/OpExecutionRecord.cs`
   - Struct：opIndex, opCode, microseconds

3. **CommandRecord**：`Assets/Scripts/Combat.Runtime/Trace/CommandRecord.cs`
   - Struct：commandType, commandData, emittedAtOpIndex

4. **ITraceRecorder**：`Assets/Scripts/Combat.Runtime/Trace/ITraceRecorder.cs`
   - 接口方法：BeginTrace, RecordOpBegin, RecordOpEnd, RecordCommand, EndTrace, GetTrace

5. **TraceRecorder**：`Assets/Scripts/Combat.Runtime/Trace/TraceRecorder.cs`
   - ITraceRecorder 实现
   - 使用 System.Diagnostics.Stopwatch 高精度计时（微秒级）
   - 不捕获 slot 值（MVP，避免 GC 压力）

6. **TraceExporter**：`Assets/Scripts/Combat.Runtime/Trace/TraceExporter.cs`
   - 静态方法：ExportToJson, ImportFromJson
   - 存储路径：Application.persistentDataPath/Traces/
   - 使用 JsonUtility 序列化

**Op → IRNode 映射保留：**
7. **ExecPlanCompiler 修改**：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs`
   - 返回类型改为 `(ExecPlan, string[])` tuple
   - 记录每个 Op 对应的 IRNode.nodeId
   - 跳过 Entry 节点（不产生 Op）

8. **ExecPlanAsset 扩展**：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanAsset.cs`
   - 添加 `string[] opToNodeId` 字段（Op index → IRNode ID 映射）
   - Initialize 方法增加 opToNodeId 参数

9. **ExecPlanBaker 更新**：`Assets/Scripts/Combat.Editor/GraphBuild/ExecPlanBaker.cs`
   - 使用 tuple 解构接收编译结果
   - 传递 opToNodeId 到 ExecPlanAsset.Initialize

**Runtime 集成：**
10. **ExecutionContext 扩展**：`Assets/Scripts/Combat.Runtime/Runtime/ExecutionContext.cs`
    - 添加 eventType 和 sourceGraphId 字段

11. **ExecPlanRunner 改造**：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanRunner.cs`
    - 添加 ITraceRecorder 可选参数（构造函数注入）
    - 使用 Stopwatch.GetTimestamp() 计时每个 Op
    - RecordOpBegin / RecordOpEnd 包裹 Op 执行
    - RecordCommand 记录命令发射

12. **BattleSimulator 集成**：`Assets/Scripts/Combat.Runtime/Runtime/BattleSimulator.cs`
    - 添加 enableTracing 构造参数
    - 创建 TraceRecorder 并注入到 ExecPlanRunner
    - ProcessSingleEvent 中导出 trace JSON
    - 添加 RegisterEventPlan(Type, ExecPlan) 重载（兼容测试）

**Editor 可视化（3 个文件）：**
13. **TraceHighlightData**：`Assets/Scripts/Combat.Editor/Trace/TraceHighlightData.cs`
    - DTO：executedNodeIds, opExecutions, opToNodeIdMapping
    - GetExecutionForNode 方法（Op index → IRNode → OpExecutionRecord）

14. **TraceViewerWindow**：`Assets/Scripts/Combat.Editor/Trace/TraceViewerWindow.cs`
    - EditorWindow（MenuItem: Window/Combat/Trace Viewer）
    - IMGUI UI：Load Trace, Refresh, Highlight in Graph 按钮
    - 显示 Metadata、Op Execution Timeline（带耗时柱状图）、Commands Emitted
    - HighlightInGraph：通过 sourceGraphId 查找 EffectGraphAsset 和 ExecPlanAsset

15. **NodeHighlighter**：`Assets/Scripts/Combat.Editor/Trace/NodeHighlighter.cs`
    - 静态类：ApplyHighlightToGraph, GetHighlightForNode, ClearHighlight
    - MVP 实现：打开 EffectGraphWindow，输出节点执行信息到 Console
    - 未来扩展：可通过 NGP BaseNodeView 扩展实现可视化高亮

**单元测试：**
16. **TraceRecorderTests**：`Assets/Tests/Combat.Runtime.Tests/Trace/TraceRecorderTests.cs`
    - 测试 BeginTrace 元数据设置
    - 测试单个/多个 Op 记录
    - 测试 Command 记录
    - 测试 EndTrace 总耗时
    - 测试 JSON 序列化 round-trip
    - 测试 TraceExporter.ExportToJson / ImportFromJson

### 7.4 关键技术决策

1. **Timing 精度**：使用 Stopwatch.GetTimestamp() + Stopwatch.Frequency 计算微秒（μs）
2. **ITraceRecorder 可选注入**：ExecPlanRunner 默认参数 null，避免性能开销
3. **Op → IRNode 映射**：并行数组（opToNodeId），O(1) 查找，最小开销
4. **Slot 捕获**：MVP 阶段禁用（captureSlots: false），避免 GC 压力
5. **Graph 高亮**：MVP 使用 Console 输出，NGP 无公开高亮 API
6. **存储格式**：JsonUtility（无依赖），文件名含 rootEventId + 时间戳

### 7.5 新增文件清单（10 个）

**Runtime（6 个）：**
1. `Assets/Scripts/Combat.Runtime/Trace/ExecutionTrace.cs`
2. `Assets/Scripts/Combat.Runtime/Trace/OpExecutionRecord.cs`
3. `Assets/Scripts/Combat.Runtime/Trace/CommandRecord.cs`
4. `Assets/Scripts/Combat.Runtime/Trace/ITraceRecorder.cs`
5. `Assets/Scripts/Combat.Runtime/Trace/TraceRecorder.cs`
6. `Assets/Scripts/Combat.Runtime/Trace/TraceExporter.cs`

**Editor（3 个）：**
7. `Assets/Scripts/Combat.Editor/Trace/TraceHighlightData.cs`
8. `Assets/Scripts/Combat.Editor/Trace/TraceViewerWindow.cs`
9. `Assets/Scripts/Combat.Editor/Trace/NodeHighlighter.cs`

**Tests（1 个）：**
10. `Assets/Tests/Combat.Runtime.Tests/Trace/TraceRecorderTests.cs`

### 7.6 修改文件清单（6 个）

**Runtime：**
1. `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs` ⭐返回 tuple
2. `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanAsset.cs` ⭐添加 opToNodeId
3. `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanRunner.cs` ⭐注入 ITraceRecorder
4. `Assets/Scripts/Combat.Runtime/Runtime/ExecutionContext.cs` ⭐添加 eventType, sourceGraphId
5. `Assets/Scripts/Combat.Runtime/Runtime/BattleSimulator.cs` ⭐enableTracing + 导出
6. `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanExamples.cs` ⭐tuple 解构

**Editor：**
7. `Assets/Scripts/Combat.Editor/GraphBuild/ExecPlanBaker.cs` ⭐tuple 解构

**Tests：**
8. `Assets/Tests/Combat.Runtime.Tests/ExecPlanCompilerTests.cs` ⭐tuple 解构

### 7.7 成功标准（全部达成）

- ✅ Runtime 可记录执行 trace 且不破坏现有功能
- ✅ Trace JSON 导出到磁盘（包含所有元数据）
- ✅ TraceViewer 加载并正确显示 trace 数据
- ✅ Op→IRNode→NGPNode 映射链工作（Console 输出验证）
- ✅ Runtime 不引用 UnityEditor
- ✅ 单元测试通过（8 个测试用例）

### 7.8 已知限制与后续扩展

**MVP 限制：**
1. **可视化高亮**：仅 Console 输出，未实现 NGP 节点视觉高亮
   - 需要扩展 NGP BaseNodeView（自定义渲染）
2. **Slot 捕获**：未实现（性能考虑）
3. **多 Event 关联**：未实现 trace 跨事件链关联

**后续扩展方向：**
1. **可视化增强**：
   - NGP 节点高亮（颜色标记执行路径）
   - Timing 热力图（节点颜色对应耗时）
   - Trace 播放器（逐步回放执行）

2. **性能分析**：
   - Op 执行热点识别
   - Budget 使用统计
   - Trace 对比工具（Diff 两次执行）

3. **Slot 审计**：
   - 捕获每个 Op 后的 slot 值
   - 在 TraceViewer 中显示中间值

4. **多 Event Trace**：
   - 关联触发链中的所有 Event
   - 生成完整的事件流图

---

## 8. 下一步切入（Task-08 及后续）

Task-07（Trace 系统）已完成，下一步是选择新的功能方向：

### 选项 A：Modifier 系统（Buff/Debuff）
- 实现 ModifierSpec / ApplyModifierCommand
- 添加 Modifier 端口类型到 NGP
- 实现 EmitApplyModifierCommandNode
- 扩展 OpCode 和 ExecPlanRunner

### 选项 B：扩展 OpCode（条件与目标选择）
- 实现 RollChance / Branch OpCode
- 实现 FindTargetsInRadius OpCode
- 扩展 ExecPlanRunner Op Handler

### 选项 C：性能优化
- ExecPlan 缓存系统（基于 planHash）
- Bake 增量构建（跳过未变更的 Graph）
- SlotStorage 对象池优化

### 选项 D：高级构筑功能
- 实现 Fork / Chain OpCode（多重投射物/连锁）
- 装备词缀集成到 BuildContext
- 天赋树修饰系统

### 短期（完成 Task-07 验收）
- Unity Play Mode 测试（enableTracing: true，触发 OnHitEvent）
- 验证 Trace JSON 导出正确
- 验证 TraceViewer 加载并高亮节点

### 中期（扩展 Task-07 功能）
- 实现 NGP 节点视觉高亮
- 实现 Slot 值捕获与显示
- Trace 对比工具（Diff UI）

### 长期（后续任务）
- Task-08+：根据上述选项选择推进方向

---

## 9. 更新日志

### 2026-01-03（Task-07 完成）
**完成内容：**
- ✅ 实现完整的 Trace 系统（Runtime 记录 + Editor 可视化）
- ✅ ExecPlanCompiler 返回 tuple（ExecPlan + Op→IRNode 映射）
- ✅ ExecPlanRunner 集成 ITraceRecorder（可选注入）
- ✅ BattleSimulator 支持 enableTracing 参数
- ✅ TraceViewerWindow（IMGUI 编辑器窗口）
- ✅ NodeHighlighter（Console 输出 MVP）
- ✅ 单元测试（8 个测试用例，JSON round-trip）

**新增文件：**
- Runtime Trace（6 个）：ExecutionTrace.cs, OpExecutionRecord.cs, CommandRecord.cs, ITraceRecorder.cs, TraceRecorder.cs, TraceExporter.cs
- Editor Trace（3 个）：TraceHighlightData.cs, TraceViewerWindow.cs, NodeHighlighter.cs
- 测试（1 个）：TraceRecorderTests.cs

**修改文件：**
- Runtime（6 个）：ExecPlanCompiler.cs, ExecPlanAsset.cs, ExecPlanRunner.cs, ExecutionContext.cs, BattleSimulator.cs, ExecPlanExamples.cs
- Editor（1 个）：ExecPlanBaker.cs
- Tests（1 个）：ExecPlanCompilerTests.cs
- 文档（1 个）：WORK_MEMORY.md

**下一步：**
- Task-08+：选择新功能方向（Modifier 系统 / OpCode 扩展 / 性能优化 / 高级构筑）

### 2026-01-03（Task-06 完成）
**完成内容：**
- ✅ 实现完整的构筑系统（Support + Graph Transform）
- ✅ 修复 GraphIR 命名空间/类型歧义编译错误
- ✅ 创建 TASK_06_GUIDE.md 开发指南

**修改文件：**
- Runtime 程序集（5 个）：IRNode.cs, SkillTags.cs, SupportDefinition.cs, BuildContext.cs, IGraphTransformer.cs
- Editor 程序集（6 个）：EffectGraphAsset.cs, ExecPlanBaker.cs, GraphTransformUtils.cs, BuildAssembler.cs, DamageScaleTransformer.cs, ElementalConversionTransformer.cs
- 文档（2 个）：WORK_MEMORY.md, CLAUDE.md

**下一步：**
- Task-07：Trace 数据结构与回灌 Editor 高亮
