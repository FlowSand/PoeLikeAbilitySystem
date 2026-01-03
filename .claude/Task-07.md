你正在一个 Unity 工程中实现 PoE-like 技能系统的“Trace 回灌编辑器”。

在此前 Task-01 ～ Task-06 中，系统已具备：
- 稳定的 Event-driven Runtime（ExecPlanRunner + EventQueue）
- 两阶段 Command 提交
- GraphIR 与 ExecPlan（可缓存）
- NodeGraphProcessor 编辑器（作者态）
- Support Transformer / BuildAssembler（构筑）

【本任务目标】
实现一个完整的 Trace 闭环，使设计者可以：
- 在 PlayMode 运行技能
- 捕获 Runtime Trace
- 回到 Editor
- 在 EffectGraphAsset（NGP 图）中：
    - 高亮哪些节点被执行
    - 显示执行顺序 / 次数 / 耗时
    - 对比不同构筑的执行差异

⚠️ 本任务只负责 **解释与可视化 Trace**，不修改任何 Runtime 行为。

---

## 一、强制架构约束（ABSOLUTE）

1. Trace 的采集发生在 Runtime（已完成）
2. Task-07 不得修改 ExecPlanRunner 的执行逻辑
3. Task-07 不得影响技能执行结果
4. Editor 侧只读取 Trace 数据，不参与执行
5. Trace → Graph 的映射必须基于：
    - planHash
    - nodeId（或 opIndex → nodeId 映射）

---

## 二、Trace 数据模型（Editor 可读）

假设 Runtime 已提供 TraceEntry（如无，可补齐 Reader 层）：

### TraceEntry 至少包含
- planHash
- eventType
- nodeId
- opIndex
- executionIndex（执行顺序）
- durationMs
- generatedCommandCount

⚠️ Task-07 可以包装 /转换 Trace 数据，但不得改变 Runtime Trace 格式。

---

## 三、Editor 侧核心模块

### 1️⃣ TraceRepository（Trace 数据访问层）

职责：
- 从 Runtime 获取 Trace
- 管理多条 Trace（最近 N 次）

接口示例：
- GetAllTraces()
- GetTraceByPlanHash(planHash)

允许的来源：
- Runtime 静态缓存
- JSON 文件
- Debug ScriptableObject

禁止：
- 在 Editor 中重新执行技能

---

### 2️⃣ Trace → Graph 映射器（核心）

实现一个 TraceGraphMapper：

职责：
- 输入：
    - EffectGraphAsset
    - GraphIR
    - ExecPlan
    - TraceEntry[]
- 输出：
    - NodeExecutionInfo（按 nodeId 聚合）

NodeExecutionInfo 至少包含：
- nodeId
- executionCount
- totalDuration
- averageDuration
- executionOrder（List<int>）

⚠️ 映射规则：
- opIndex → ExecPlan → nodeId
- nodeId → EffectGraphAsset Node

---

### 3️⃣ NGP Graph 高亮系统

在 NodeGraphProcessor GraphView 中实现：

#### 必须支持
- 高亮被执行过的 Node
- 颜色强度表示：
    - 执行次数
    - 或总耗时
- Tooltip / Overlay 显示：
    - 执行次数
    - 平均耗时
    - 产生 Command 数量

#### 可选增强
- 执行顺序播放（Step / Timeline）
- 仅显示某 EventType 的 Trace

---

## 四、Editor UI（最小可用）

至少提供一种入口：

### 方案 A（推荐）
- EffectGraphAsset Inspector 中：
    - “Show Trace” 按钮
    - 选择 planHash
    - 打开 GraphView 并高亮

### 方案 B
- 独立 EditorWindow：
    - 左侧 Trace 列表
    - 右侧 GraphView

---

## 五、必须实现的验证场景

### 场景 1：单技能 Trace
- 火球技能
- 执行一次 OnCast
- Editor 中：
    - OnCastEntry → Damage 节点被高亮
    - 执行顺序正确

### 场景 2：多构筑 Trace 对比
- 火球（无 Support）
- 火球 + 多重投射物
- 对比：
    - 执行节点数量不同
    - 某些节点执行次数明显增加

### 场景 3：高频触发
- OnHit 多次触发
- 执行次数在 Node 上正确累计

---

## 六、交付要求（DELIVERABLES）

1. 新增 / 修改的文件路径列表
2. TraceRepository 接口说明
3. Trace → Graph 映射逻辑说明
4. GraphView 高亮实现方式说明
5. 一个操作指南：
    - 如何运行技能
    - 如何在 Editor 查看 Trace

---

## 七、FAIL 条件（任一即失败）

- Editor 中重新执行技能逻辑
- Trace 映射不到 nodeId
- 高亮与实际执行不一致
- 修改了 Runtime 执行结果
- Trace 只存在日志，无法结构化分析
