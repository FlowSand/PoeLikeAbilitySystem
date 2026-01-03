# CLAUDE.md
# PoE-like 技能系统（Effect Graph + Event-driven Runtime）
# Unity 工程落地执行规范（方案 B：NGP 仅编辑器使用）

---

## 0. 文档目的（MANDATE）

本文档定义了一个 **类《流放之路》技能系统** 在 Unity 中的**唯一正确实现方式**。

所有 AI Agent 在本仓库中进行的：
- 架构设计
- 编码实现
- 重构调整
- 工具扩展

**必须严格遵循本文档约定**，不得自行改变核心架构决策。

---

## 1. 不可违背的核心架构原则（ABSOLUTE RULES）

### 1.1 总体原则

- **NodeGraphProcessor（NGP）只用于编辑器“作者态”**
- **运行时永远不执行 NGP 图**
- 运行时只执行 **编译后的 ExecPlan（执行计划）**

> 编辑器图 = 设计表达  
> GraphIR = 中间表示（可校验、可迁移）  
> ExecPlan = 运行时契约（唯一执行单位）

---

### 1.2 强制约束（Hard Constraints）

#### 运行时约束
- Runtime 程序集 **禁止引用**：
    - `GraphProcessor`
    - `GraphProcessor.Editor`
    - `UnityEditor`
- Runtime 必须：
    - 事件驱动（Event-driven），禁止 Update 轮询
    - 使用 **两阶段命令提交**
    - 内建 **触发深度限制** 与 **执行预算**

#### 图执行约束
- 技能效果必须：
    - 可复现（同 Context + Seed → 同结果）
    - 可缓存（基于 hash）
    - 可审计（traceable）

---

## 2. 总体架构分层（AUTHORITATIVE）

┌────────────────────────────┐
│ 编辑器层（Editor） │
│ NodeGraphProcessor (NGP) │
│ - EffectGraphAsset │
│ - Node 定义 │
└────────────┬───────────────┘
│ 导出
▼
┌────────────────────────────┐
│ 中间表示层（IR） │
│ GraphIR (JSON / Model) │
│ - Nodes / Ports / Edges │
│ - 静态校验 │
└────────────┬───────────────┘
│ 编译
▼
┌────────────────────────────┐
│ 运行时计划层 │
│ ExecPlanAsset │
│ - Op 序列 │
│ - Slot 分配 │
│ - Hash / Cache Key │
└────────────┬───────────────┘
│ 执行
▼
┌────────────────────────────┐
│ Event-driven Runtime │
│ EventQueue + Runner │
│ CommandBuffer (2-phase) │
│ Budget / Depth / Trace │
└────────────────────────────┘

yaml
复制代码

---

## 3. Unity 工程目录与程序集规范

### 3.1 目录结构（必须遵守）

Assets/
Scripts/
Combat.Runtime/ (asmdef)
Model/
Events/
Commands/
Runtime/
GraphRuntime/
GraphIR/
Build/              # 构筑系统接口（Runtime 可用）
Trace/              # Trace 数据结构
Combat.Editor/ (asmdef, Editor only)
GraphAuthoring/ # NGP 图与节点
GraphBuild/     # 导出 / 编译 / Bake
Build/          # 构筑系统工具（Transformer 实现）
Trace/          # Trace 可视化工具
GraphAssets/
Skills/         # NGP 图资产
Supports/       # Support Definition 资产
Generated/
ExecPlans/      # 编译后的 ExecPlanAsset
Tests/
Combat.Runtime.Tests/ # 单元测试

### 3.2 asmdef 规则

- `Combat.Runtime`
    - ❌ 不允许引用 GraphProcessor / UnityEditor
- `Combat.Editor`
    - ✅ 允许引用 NodeGraphProcessor

---

## 4. Effect Graph（作者态）设计规范

### 4.1 Graph 资产

- 类型：`EffectGraphAsset : BaseGraph`
- 职责：
    - 技能效果的“设计表达”
    - 不承担运行时逻辑

必须包含：
- `graphVersion`
- `entryEventType`（OnCast / OnHit / OnKill 等）
- 可选：参数黑板（策划调参）

---

### 4.2 Node 设计原则

- 每个 Node = **一个确定、可编译的算子**
- Node **不得直接修改战斗状态**
- 副作用只能通过 **Emit Command Node** 表达

#### MVP 已实现的节点（13 个）

**入口节点（2 个）**
- ✅ `OnCastEntryNode` (IRNodeType.OnCastEntry)
- ✅ `OnHitEntryNode` (IRNodeType.OnHitEntry)

**数值节点（4 个）**
- ✅ `ConstNumber` (IRNodeType.ConstNumber → OpCode.ConstNumber)
- ✅ `GetStat` (IRNodeType.GetStat → OpCode.GetStat)
- ✅ `Add` (IRNodeType.Add → OpCode.Add)
- ✅ `Mul` (IRNodeType.Mul → OpCode.Mul)

**条件节点（2 个）**
- ⏳ `RollChance` (IRNodeType.RollChance) - *Node 已创建，OpCode 未实现*
- ⏳ `Branch` (IRNodeType.Branch) - *Node 已创建，OpCode 未实现*

**实体节点（3 个）**
- ✅ `GetCasterNode` (IRNodeType.GetCaster → OpCode.GetCaster)
- ✅ `GetTargetNode` (IRNodeType.GetTarget → OpCode.GetTarget)
- ⏳ `FindTargetsInRadius` (IRNodeType.FindTargetsInRadius) - *Node 已创建，OpCode 未实现*

**效果构建（1 个）**
- ✅ `MakeDamageSpec` (IRNodeType.MakeDamageSpec → OpCode.MakeDamage)

**副作用（命令生成）（1 个）**
- ✅ `EmitApplyDamageCommand` (IRNodeType.EmitApplyDamageCommand → OpCode.EmitApplyDamage)
- ⏳ `EmitApplyModifierCommand` - *未实现，需要 Modifier 系统*

**已实现状态**：
- **完全支持**（8 个）：OnCastEntry, OnHitEntry, ConstNumber, GetStat, Add, Mul, GetCaster, GetTarget, MakeDamageSpec, EmitApplyDamageCommand
- **部分支持**（3 个）：RollChance, Branch, FindTargetsInRadius（NGP 节点存在，但编译器未实现 OpCode）
- **未实现**（1 个）：EmitApplyModifierCommand（需要完整 Modifier 系统）

---

## 5. GraphIR（中间表示）规范

### 5.1 GraphIR 的职责

- NGP 图导出的 **稳定结构表示**
- 支持：
    - 静态校验
    - 版本迁移
    - Diff / Review

### 5.2 必须做的静态校验

- 端口类型匹配
- Entry 唯一性
- 禁止非法环（默认 DAG）
- 副作用节点位置合法性

---

## 6. ExecPlan（运行时执行计划）

### 6.1 ExecPlan 定义

ExecPlan 是 **运行时唯一执行单元**，包含：

- `Op[]`：线性指令序列
- `SlotLayout`：端口值槽位映射
- `PlanHash`：用于缓存与复用

**ExecPlanAsset**（ScriptableObject 包装）：
- `sourceGraphId`：源 GraphIR ID
- `graphVersion`：图版本号
- `planHash`：计划哈希（用于缓存）
- `serializedOps`：序列化的 Op 数组
- `slotLayout`：Slot 布局
- `opToNodeId`：Op index → IRNode ID 映射（用于 Trace 回灌）

### 6.2 编译规则

- GraphIR → 拓扑排序（确定性，零入度选择按 nodeId 字典序）
- 分配 slot（按类型池：Number / Bool / Entity / DamageSpec）
- 生成 Op（switch / handler 数组，支持 8 个 OpCode）
- 计算 PlanHash（FNV-1a 64 位，基于图结构）
- **保留 Op → IRNode 映射**（opToNodeId 数组，用于 Trace 可视化）
- 输出 `ExecPlanAsset`（ScriptableObject，存储到 Generated/ExecPlans/）

### 6.3 已实现的 OpCode（8 个）

| OpCode | 功能 | Op 字段使用 |
|--------|------|------------|
| ConstNumber | 加载常量浮点数 | a=floatAsInt, output=slotId |
| GetStat | 读取 Unit 属性 | a=statType, b=entitySlot, output=numberSlot |
| Add | 两数相加 | a=slotA, b=slotB, output=slotOut |
| Mul | 两数相乘 | a=slotA, b=slotB, output=slotOut |
| MakeDamage | 构建 DamageSpec | a=amountSlot, b=targetSlot, output=damageSpecSlot |
| EmitApplyDamage | 发射伤害命令 | a=damageSpecSlot |
| GetCaster | 加载施法者 UnitId | output=entitySlot |
| GetTarget | 加载目标 UnitId | output=entitySlot |

---

## 7. Event-driven Runtime 执行模型

### 7.1 执行流程（强制）

1. Event 进入 `EventQueue`（FIFO 环形队列）
2. 构建 `ExecutionContext`（携带 casterUnitId, targetUnitId, slots, budget, eventType, sourceGraphId）
3. `BattleSimulator` 查找 Event → ExecPlan 映射
4. `ExecPlanRunner.Execute()` 执行 Op 序列
5. 生成 `CommandBuffer`
6. **两阶段提交**
    - Phase 1：收集命令（Enqueue）
    - Phase 2：统一 Apply，产生新事件（再次 Enqueue）

**核心组件**：
- `EventBus`：类型安全的事件订阅/发布（泛型 + Dictionary，零反射）
- `EventQueue`：FIFO 环形队列（自动扩容）
- `ExecutionContext`：轻量级 struct（slots, budget, eventType, sourceGraphId）
- `SlotStorage`：可池化的数组容器（numbers / entities / damageSpecs）
- `ExecPlanRunner`：虚拟机核心（switch-based dispatch，8 个 Op Handler）
- `BattleSimulator`：主循环（Event → ExecPlan → CommandBuffer → Apply）

### 7.2 触发链安全（必须实现）

- **Trigger Depth Limit**（最大递归深度 = 10）
  - `BattleSimulator.EnqueueEvent()` 检查 `depth < MAX_TRIGGER_DEPTH`
  - 超限事件拒绝入队，输出警告日志
- **Execution Budget**（每个 Event 的资源限制）
  - 最大 Op 数：1000（MAX_OPS_PER_EVENT）
  - 最大 Command 数：100（MAX_COMMANDS_PER_EVENT）
  - `ExecPlanRunner.Execute()` 中检查 `budget.CanExecuteOp()` / `budget.CanEmitCommand()`
- **每帧事件数限制**（防止卡顿）
  - 最大事件处理数：100（MAX_EVENTS_PER_FRAME）
  - `BattleSimulator.ProcessEvents()` 限制单次处理数量
- **同源触发去重 / 内部冷却（ICD）**（未实现，Task-08+ 选项）

**BattleConfig 常量**（`Assets/Scripts/Combat.Runtime/Runtime/BattleConfig.cs`）：
```csharp
public static class BattleConfig {
    public const int MAX_TRIGGER_DEPTH = 10;
    public const int MAX_OPS_PER_EVENT = 1000;
    public const int MAX_COMMANDS_PER_EVENT = 100;
    public const int MAX_EVENTS_PER_FRAME = 100;
}
```

---

## 8. Trace 与可观测性（已实现）

### 8.1 Trace 数据记录（Runtime）

**ExecutionTrace 数据结构**（`Assets/Scripts/Combat.Runtime/Trace/ExecutionTrace.cs`）：
- **事件元数据**：eventType, rootEventId, triggerDepth, randomSeed
- **图信息**：sourceGraphId, planHash
- **执行上下文**：casterUnitId, targetUnitId
- **执行记录**：opExecutions（OpExecutionRecord 列表）
- **命令记录**：commands（CommandRecord 列表）
- **性能统计**：totalExecutionMicroseconds, totalOpsExecuted, totalCommandsEmitted

**每次执行必须记录**：
- Event 类型
- 执行的 ExecPlan（sourceGraphId + planHash）
- Op 执行顺序（opIndex, opCode, microseconds）
- 每个 Op 耗时（微秒级，使用 Stopwatch）
- 生成的命令列表（commandType, commandData, emittedAtOpIndex）

### 8.2 Trace 导出与回灌

**导出格式**：JSON（使用 JsonUtility）
- 存储路径：`Application.persistentDataPath/Traces/`
- 文件名格式：`trace_{rootEventId}_{timestamp}.json`

**Trace 回灌到编辑器**：
- ✅ TraceViewerWindow（`Window/Combat/Trace Viewer`）
  - 加载 JSON 文件
  - 显示事件元数据（eventType, rootEventId, depth, seed）
  - 显示 Op 执行时间轴（柱状图）
  - 显示命令列表（commandType, 发射位置）
- ✅ NodeHighlighter（Console 输出 MVP）
  - 通过 sourceGraphId 查找 EffectGraphAsset 和 ExecPlanAsset
  - 使用 opToNodeId 映射（Op index → IRNode ID）
  - 在 Console 输出节点执行信息（nodeId, opIndex, microseconds）
  - **未来扩展**：NGP 节点视觉高亮（颜色标记）

### 8.3 实现细节

**ITraceRecorder 接口**（可选注入）：
- `BeginTrace(eventType, rootEventId, triggerDepth, seed, sourceGraphId, planHash)`
- `RecordOpBegin(opIndex, opCode)`
- `RecordOpEnd(opIndex, microseconds)`
- `RecordCommand(command, emittedAtOpIndex)`
- `EndTrace(totalMicroseconds)`
- `GetTrace()`：返回 ExecutionTrace

**集成到 ExecPlanRunner**：
- 构造函数可选注入 `ITraceRecorder`（默认 null）
- 使用 `Stopwatch.GetTimestamp()` 计时（微秒精度）
- RecordOpBegin / RecordOpEnd 包裹 Op 执行
- RecordCommand 记录命令发射

**集成到 BattleSimulator**：
- 构造参数 `enableTracing: bool`（默认 false）
- 启用时创建 `TraceRecorder` 并注入到 `ExecPlanRunner`
- ProcessSingleEvent 后导出 trace JSON

### 8.4 已知限制（MVP）

- ⏳ **可视化高亮**：仅 Console 输出，未实现 NGP 节点视觉高亮（需扩展 BaseNodeView）
- ⏳ **Slot 捕获**：未实现（性能考虑，避免 GC 压力）
- ⏳ **多 Event 关联**：未实现 trace 跨事件链关联
- ⏳ **Trace 对比工具**：未实现（Diff 两次执行）

---

## 9. Task 执行清单（AI Agent 必须按顺序完成）

### Task-01：战斗模型与命令系统（Runtime）✅ 已完成
- ✅ Unit / Stats / Damage（UnitId, Unit, StatCollection, DamageSpec）
- ✅ EventBus（泛型事件订阅/发布，零反射，零 GC）
- ✅ CommandBuffer（两阶段提交：Enqueue → ApplyAll）
- ✅ BattleContext（Unit 管理 + ApplyDamage）
- ✅ 测试：OnHitDamageSystemTests（一次 OnHit 伤害结算）
- **路径**：`Assets/Scripts/Combat.Runtime/Model/`, `Events/`, `Commands/`, `Runtime/`

### Task-02：GraphIR 数据结构与校验 ✅ 已完成
- ✅ GraphIR Model（graphId, version, nodes, edges, entryNodeId）
- ✅ IRNode（nodeId, nodeType, ports, intParams, tags）
- ✅ 静态校验器（端口类型匹配、Entry 唯一性、DAG 检查）
- ✅ ValidationResult / ValidationError
- ✅ 单元测试：GraphIRValidatorTests
- **路径**：`Assets/Scripts/Combat.Runtime/GraphIR/`

### Task-03：ExecPlan 编译器 ✅ 已完成
- ✅ 拓扑排序（确定性，零入度选择按 nodeId 字典序）
- ✅ Slot 分配（按端口类型分池：Number / Entity / DamageSpec）
- ✅ Op 生成（8 个 OpCode：ConstNumber, GetStat, Add, Mul, MakeDamage, EmitApplyDamage, GetCaster, GetTarget）
- ✅ Hash 缓存（StableHash64，FNV-1a 64 位）
- ✅ Op → IRNode 映射保留（opToNodeId 数组）
- ✅ 单元测试：ExecPlanCompilerTests
- **路径**：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs`

### Task-04：ExecPlanRunner（Runtime）✅ 已完成
- ✅ 执行 Op（switch-based dispatch，8 个 Op Handler）
- ✅ 生成 CommandBuffer
- ✅ 支持 Budget / Depth（BattleConfig 常量）
- ✅ EventQueue（FIFO 环形队列）
- ✅ BattleSimulator（主循环，Event → ExecPlan → CommandBuffer → Apply）
- ✅ ExecutionContext（轻量级 struct，携带 slots / budget / 事件元数据）
- ✅ 单元测试：ExecPlanRunnerTests, BattleSimulatorTests
- **路径**：`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanRunner.cs`, `Runtime/BattleSimulator.cs`

### Task-05：NGP 编辑器接入（Editor）✅ 已完成
- ✅ EffectGraphAsset（BaseGraph 子类，graphVersion, entryEventType, skillTags, supports）
- ✅ 基础 Node 实现（13 个节点：Entry, 数值, 条件, 实体, 效果）
- ✅ Graph → GraphIR 导出（GraphIRExporter，反射提取端口定义）
- ✅ ExecPlanBaker（GraphIR → ExecPlan 编译 → ExecPlanAsset 资产）
- ✅ EffectGraphWindow（BaseGraphWindow 编辑器窗口）
- ✅ NodeTypeRegistry（类型映射注册表）
- ✅ BakeMenuItem（Unity 菜单：Assets/Combat/Bake Effect Graph）
- **路径**：`Assets/Scripts/Combat.Editor/GraphAuthoring/`, `GraphBuild/`

### Task-06：构筑系统（Support + Graph Transform）✅ 已完成
- ✅ IRNode 添加 tags 字段
- ✅ SupportDefinition（ScriptableObject）
- ✅ IGraphTransformer 接口（CanApply, Apply）+ IParameterizedTransformer
- ✅ GraphTransformUtils 工具库（CloneGraph, FindNodesByType, ModifyIntParam, Tag 操作等）
- ✅ BuildAssembler（构筑总控，按 priority 排序 Supports → 逐个 Apply → Validate）
- ✅ EffectGraphAsset 添加 supports 和 skillTags 字段
- ✅ ExecPlanBaker 集成 BuildAssembler（Export → Assemble → Compile）
- ✅ 示例 Transformer：DamageScaleTransformer, ElementalConversionTransformer
- **关键约束**：Support 仅在构建期生效，Runtime 完全不感知
- **文档**：TASK_06_GUIDE.md（Transformer 开发指南）
- **路径**：`Assets/Scripts/Combat.Runtime/Build/`, `Combat.Editor/Build/`

### Task-07：Trace 系统（Runtime 记录 + Editor 可视化）✅ 已完成
- ✅ ExecutionTrace 数据结构（eventType, opExecutions, commands, totalMicroseconds）
- ✅ ITraceRecorder / TraceRecorder（Stopwatch 微秒级计时）
- ✅ TraceExporter（JSON 导出/导入，存储到 persistentDataPath/Traces/）
- ✅ ExecPlanRunner 集成 ITraceRecorder（可选注入）
- ✅ BattleSimulator 支持 enableTracing 参数
- ✅ Op → IRNode 映射保留（ExecPlanCompiler 返回 tuple）
- ✅ TraceViewerWindow（IMGUI 编辑器窗口，加载 trace、显示 timeline、高亮节点）
- ✅ NodeHighlighter（Console 输出 MVP，未来可扩展 NGP 视觉高亮）
- ✅ 单元测试：TraceRecorderTests（8 个测试用例，JSON round-trip）
- **路径**：`Assets/Scripts/Combat.Runtime/Trace/`, `Combat.Editor/Trace/`

### Task-08+：后续扩展方向（待选择）
根据项目需求选择以下方向之一推进：

#### 选项 A：Modifier 系统（Buff/Debuff）
- ModifierSpec / ApplyModifierCommand
- Modifier 端口类型到 NGP
- EmitApplyModifierCommandNode
- 扩展 OpCode 和 ExecPlanRunner

#### 选项 B：扩展 OpCode（条件与目标选择）
- 实现 RollChance / Branch OpCode（已有 Node 未编译）
- 实现 FindTargetsInRadius OpCode（已有 Node 未编译）
- 扩展 ExecPlanRunner Op Handler

#### 选项 C：性能优化
- ExecPlan 缓存系统（基于 planHash）
- Bake 增量构建（跳过未变更的 Graph）
- SlotStorage 对象池优化

#### 选项 D：高级构筑功能
- 实现 Fork / Chain OpCode（多重投射物/连锁）
- 装备词缀集成到 BuildContext
- 天赋树修饰系统

#### 选项 E：Trace 可视化增强
- NGP 节点视觉高亮（颜色标记执行路径）
- Timing 热力图（节点颜色对应耗时）
- Slot 值捕获与显示
- Trace 播放器（逐步回放执行）

---

## 10. 明确禁止事项（DO NOT）

- ❌ 在运行时执行 NGP Graph
- ❌ Node 直接修改战斗状态
- ❌ Lua / Script 绕过 ExecPlan
- ❌ Update 里跑技能逻辑
- ❌ 无预算的触发链

---

## 11. 最终目标

- 技能 = Graph + Support Transformer + Build
- 运行时 = Event → ExecPlan → Commands
- 系统可：
    - 高度组合
    - 可控性能
    - 可回放
    - 可长期演进

---

## 本文档是最终裁决版本。
## 如需修改，必须先更新 CLAUDE.md，再允许任何实现变更。

---

## 附录：当前实现状态总结（2026-01-03）

### 已完成的核心功能（Task-01 到 Task-07）

**Runtime 程序集（Combat.Runtime）**：
- ✅ 战斗模型：Unit, Stats, Damage, BattleContext
- ✅ 事件系统：EventBus（泛型，零反射）, EventQueue（FIFO 环形队列）
- ✅ 命令系统：CommandBuffer（两阶段提交）, ApplyDamageCommand
- ✅ GraphIR：数据结构 + 静态校验器（7 种校验规则）
- ✅ ExecPlan：编译器（拓扑排序 + Slot 分配 + Op 生成 + Hash 缓存）
- ✅ ExecPlanRunner：虚拟机（8 个 Op Handler，Budget/Depth 限制）
- ✅ BattleSimulator：主循环（Event → ExecPlan → CommandBuffer → Apply）
- ✅ 构筑系统：IGraphTransformer, SupportDefinition, BuildContext
- ✅ Trace 系统：ExecutionTrace, TraceRecorder, TraceExporter

**Editor 程序集（Combat.Editor）**：
- ✅ NGP 集成：EffectGraphAsset, EffectGraphWindow, 13 个节点
- ✅ Build Pipeline：GraphIRExporter, ExecPlanBaker, BakeMenuItem
- ✅ 构筑工具：GraphTransformUtils, BuildAssembler, 2 个示例 Transformer
- ✅ Trace 可视化：TraceViewerWindow, NodeHighlighter（Console 输出 MVP）

**测试覆盖**：
- ✅ Combat.Runtime.Tests（19+ 测试用例）
  - OnHitDamageSystemTests
  - GraphIRValidatorTests
  - ExecPlanCompilerTests
  - EventQueueTests
  - ExecPlanRunnerTests
  - BattleSimulatorTests
  - TraceRecorderTests

### 已实现的节点（8/13 完全支持）

| 节点 | 状态 | IRNodeType | OpCode |
|------|------|-----------|--------|
| OnCastEntryNode | ✅ 完全支持 | OnCastEntry | - |
| OnHitEntryNode | ✅ 完全支持 | OnHitEntry | - |
| ConstNumberNode | ✅ 完全支持 | ConstNumber | ConstNumber |
| GetStatNode | ✅ 完全支持 | GetStat | GetStat |
| AddNode | ✅ 完全支持 | Add | Add |
| MulNode | ✅ 完全支持 | Mul | Mul |
| GetCasterNode | ✅ 完全支持 | GetCaster | GetCaster |
| GetTargetNode | ✅ 完全支持 | GetTarget | GetTarget |
| MakeDamageSpecNode | ✅ 完全支持 | MakeDamageSpec | MakeDamage |
| EmitApplyDamageCommandNode | ✅ 完全支持 | EmitApplyDamageCommand | EmitApplyDamage |
| RollChanceNode | ⏳ 部分支持 | RollChance | 未实现 |
| BranchNode | ⏳ 部分支持 | Branch | 未实现 |
| FindTargetsInRadiusNode | ⏳ 部分支持 | FindTargetsInRadius | 未实现 |

### 已验证的端到端工作流

1. **编辑器工作流**：
   - 创建 EffectGraphAsset → 在 EffectGraphWindow 中设计图 → 添加 Support → Bake → 生成 ExecPlanAsset

2. **构筑工作流**：
   - 配置 skillTags + supports → BuildAssembler.Assemble() → 应用 Transformer → 变换 GraphIR → 编译 ExecPlan

3. **运行时工作流**：
   - 加载 ExecPlanAsset → 注册到 BattleSimulator → 发布 Event → 执行 ExecPlan → 生成 Command → Apply 修改状态

4. **Trace 工作流**：
   - enableTracing: true → 执行 ExecPlan → 记录 ExecutionTrace → 导出 JSON → TraceViewerWindow 加载 → NodeHighlighter 高亮

### 未实现的功能（Task-08+ 候选）

- ⏳ OpCode 扩展：RollChance, Branch, FindTargetsInRadius（Node 已创建）
- ⏳ Modifier 系统：ModifierSpec, ApplyModifierCommand, EmitApplyModifierCommandNode
- ⏳ 性能优化：ExecPlan 缓存系统, Bake 增量构建, SlotStorage 对象池
- ⏳ 高级构筑：Fork/Chain OpCode, 装备词缀集成, 天赋树系统
- ⏳ Trace 增强：NGP 节点视觉高亮, Slot 值捕获, Trace 对比工具

### 文档与指南

- ✅ CLAUDE.md（架构规范，本文档）
- ✅ WORK_MEMORY.md（实现交接说明，详细记录）
- ✅ TASK_06_GUIDE.md（Transformer 开发指南）
- ✅ TASK_05_TEST_GUIDE.md（NGP 集成测试指南）
- ✅ TASK_05_COMPLETION.md（Task-05 验收总结）

### 程序集引用验证

- ✅ Combat.Runtime.asmdef：零引用（不引用 GraphProcessor/UnityEditor）
- ✅ Combat.Editor.asmdef：引用 Combat.Runtime + GraphProcessor.dll（Editor only）
- ✅ 程序集隔离严格遵守