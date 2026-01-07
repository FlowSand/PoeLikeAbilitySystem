# ExecPlan 架构设计文档
## PoE-like 技能系统 - 运行时执行计划详解

**文档版本：** 1.0
**创建日期：** 2026-01-08
**适用版本：** Task-01 至 Task-08

---

## 目录

1. [概述](#1-概述)
2. [在系统中的位置](#2-在系统中的位置)
3. [数据结构详解](#3-数据结构详解)
4. [编译过程（GraphIR → ExecPlan）](#4-编译过程graphir--execplan)
5. [运行时执行（ExecPlanRunner）](#5-运行时执行execplanrunner)
6. [与事件系统的集成](#6-与事件系统的集成)
7. [性能优化策略](#7-性能优化策略)
8. [实际案例：火球术](#8-实际案例火球术)
9. [与 DamagePipeline 的关系](#9-与-damagepipeline-的关系)
10. [关键设计决策](#10-关键设计决策)
11. [常见问题 FAQ](#11-常见问题-faq)

---

## 1. 概述

### 1.1 什么是 ExecPlan？

**ExecPlan**（Execution Plan，执行计划）是整个技能系统的**运行时唯一执行单元**。它是从 NodeGraphProcessor（NGP）图编译而来的**线性指令序列**，类似于虚拟机的字节码。

### 1.2 核心设计理念

| 设计原则 | 说明 |
|---------|------|
| **编译期做重活** | 拓扑排序、类型推导、slot 分配都在编译时完成 |
| **运行时做轻活** | 只需线性执行指令，读写 slot，生成 Command |
| **解耦与性能兼得** | GraphIR 提供灵活性，ExecPlan 提供性能 |
| **确定性执行** | 相同输入 + 相同 seed → 相同结果 |

### 1.3 类比理解

```
NGP 图（编辑器）  = 高级编程语言（C#, Python）
    ↓ Export
GraphIR（中间表示）= 中间表示（LLVM IR）
    ↓ Compile
ExecPlan（运行时） = 机器码（x86, ARM）
    ↓ Execute
产生副作用（Command）
```

---

## 2. 在系统中的位置

### 2.1 完整工作流

```
┌─────────────────────────────────┐
│ 编辑器层（Editor Only）          │
│ NodeGraphProcessor (NGP)        │
│ - EffectGraphAsset               │
│ - 节点可视化编辑                  │
└─────────────┬───────────────────┘
              │ GraphIRExporter.Export()
              ▼
┌─────────────────────────────────┐
│ 中间表示层（Runtime/Editor 共享）│
│ GraphIR (JSON / Model)          │
│ - Nodes / Ports / Edges         │
│ - 静态校验                       │
└─────────────┬───────────────────┘
              │ ExecPlanCompiler.Compile()
              ▼
┌─────────────────────────────────┐
│ 运行时计划层（Runtime Only）     │
│ ExecPlan                        │
│ - Op 序列（线性指令）            │
│ - Slot 布局                      │
│ - Plan Hash（缓存键）            │
└─────────────┬───────────────────┘
              │ ExecPlanRunner.Execute()
              ▼
┌─────────────────────────────────┐
│ 事件驱动层                       │
│ EventQueue + BattleSimulator    │
│ CommandBuffer (2-phase commit)  │
│ Budget / Depth / Trace          │
└─────────────────────────────────┘
```

### 2.2 为什么需要 ExecPlan？

| 问题 | ExecPlan 的解决方案 |
|------|-------------------|
| NGP 图依赖 Unity Editor | ExecPlan 是纯 Runtime 数据结构 |
| 图遍历性能差（指针跳转） | 线性指令序列，CPU 缓存友好 |
| 难以优化和缓存 | planHash 稳定，可缓存编译结果 |
| 不可复现（图遍历顺序不定） | 确定性拓扑排序 → 确定性执行 |

---

## 3. 数据结构详解

### 3.1 ExecPlan 核心结构

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlan.cs`

```csharp
public struct ExecPlan
{
    /// <summary>
    /// 计划哈希（用于缓存和版本控制）
    /// 相同的图结构 → 相同的 hash
    /// </summary>
    public ulong planHash;

    /// <summary>
    /// 线性指令序列（按拓扑序排列）
    /// </summary>
    public Op[] operations;

    /// <summary>
    /// Slot 布局（告诉 Runner 需要分配多少 slot）
    /// </summary>
    public SlotLayout slotLayout;
}
```

### 3.2 Op（操作指令）

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/Op.cs`

```csharp
public struct Op
{
    /// <summary>
    /// 指令类型（ConstNumber, Add, Mul, GetStat 等）
    /// </summary>
    public OpCode opCode;

    /// <summary>
    /// 参数 A（语义根据 opCode 不同而不同）
    /// 可能是：slot id, statType, floatAsInt 等
    /// </summary>
    public int a;

    /// <summary>
    /// 参数 B
    /// </summary>
    public int b;

    /// <summary>
    /// 输出 slot id
    /// </summary>
    public int output;
}
```

**Op 的设计特点**：
- **固定大小**：3 个 int 参数 + 1 个 OpCode，内存布局紧凑
- **无虚方法**：通过 switch dispatch，避免虚方法调用开销
- **语义灵活**：参数 a/b 根据 opCode 有不同含义，复用字段

### 3.3 OpCode 枚举

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/OpCode.cs`

**当前已实现的 OpCode（8 个）**：

| OpCode | 功能 | Op 字段使用 | 示例 |
|--------|------|------------|------|
| **ConstNumber** | 加载常量浮点数 | a=floatAsInt, output=slotId | `ConstNumber(100) → slot[0]` |
| **GetStat** | 读取 Unit 属性 | a=statType, b=entitySlot, output=numberSlot | `GetStat(Health, entity[0]) → number[1]` |
| **Add** | 两数相加 | a=slotA, b=slotB, output=slotOut | `Add(slot[0], slot[1]) → slot[2]` |
| **Mul** | 两数相乘 | a=slotA, b=slotB, output=slotOut | `Mul(slot[0], slot[1]) → slot[2]` |
| **MakeDamage** | 构建 DamageSpec | a=amountSlot, b=targetSlot, output=damageSpecSlot | `MakeDamage(amount[0], target[1]) → damageSpec[0]` |
| **EmitApplyDamage** | 发射伤害命令 | a=damageSpecSlot | `EmitApplyDamage(damageSpec[0]) → CommandBuffer` |
| **GetCaster** | 加载施法者 UnitId | output=entitySlot | `GetCaster() → entity[0]` |
| **GetTarget** | 加载目标 UnitId | output=entitySlot | `GetTarget() → entity[1]` |

### 3.4 SlotLayout（槽位布局）

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/SlotLayout.cs`

```csharp
public struct SlotLayout
{
    /// <summary>
    /// 浮点数 slot 数量（用于存储 number 类型的中间值）
    /// </summary>
    public int numberSlotCount;

    /// <summary>
    /// 实体 ID slot 数量（用于存储 UnitId）
    /// </summary>
    public int entitySlotCount;

    /// <summary>
    /// 伤害规格 slot 数量（用于存储 DamageSpec）
    /// </summary>
    public int damageSpecSlotCount;
}
```

**Slot 分池设计**：
- 不同类型的值存储在不同的 slot 池中
- 编译时确定每个池的大小（编译器分析端口类型）
- 运行时按需分配数组，避免动态分配

---

## 4. 编译过程（GraphIR → ExecPlan）

### 4.1 编译器职责

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs`

**核心方法**：
```csharp
public static (ExecPlan, string[]) Compile(GraphIR graph)
```

**返回值**：
- `ExecPlan`：编译后的执行计划
- `string[]`：Op → IRNode 映射（用于 Trace 回溯）

### 4.2 编译步骤详解

#### Step 1：静态校验

```csharp
// 复用 GraphIRValidator 先校验图的合法性
ValidationResult validation = GraphIRValidator.Validate(graph);
if (!validation.IsValid) {
    throw new Exception($"GraphIR 校验失败: {validation.ErrorMessage}");
}
```

**校验内容**：
- entryNodeId 存在
- nodeId 唯一
- edge 引用合法（node/port 存在）
- 端口方向正确
- 端口类型匹配
- 默认禁止环（DAG）

#### Step 2：确定性拓扑排序

```csharp
List<IRNode> topoOrder = TopologicalSort(graph);
// 零入度选择策略：按 nodeId 字典序
// 保证相同图 → 相同拓扑序
```

**为什么需要确定性？**

| 场景 | 要求 | 实现方式 |
|------|------|---------|
| **缓存** | 相同图 → 相同 hash | 确定性拓扑序 → 确定性 Op 序列 → 稳定 hash |
| **调试** | 多次运行结果一致 | 相同 Op 序列 → 相同执行路径 |
| **Diff** | 比较两个版本的变化 | Op 序列一致性保证 Diff 有效 |

**拓扑排序算法（Kahn 算法变体）**：

```csharp
// 伪代码
Dictionary<nodeId, inDegree> inDegrees = ComputeInDegrees(graph);
PriorityQueue<IRNode> zeroInDegreeQueue = new PriorityQueue(
    comparer: (a, b) => a.nodeId.CompareTo(b.nodeId)  // 字典序
);

List<IRNode> topoOrder = new List<IRNode>();
while (zeroInDegreeQueue.Count > 0) {
    IRNode node = zeroInDegreeQueue.Dequeue();  // 取字典序最小的
    topoOrder.Add(node);

    foreach (var successor in GetSuccessors(node)) {
        inDegrees[successor]--;
        if (inDegrees[successor] == 0) {
            zeroInDegreeQueue.Enqueue(successor);
        }
    }
}
```

#### Step 3：Slot 分配

```csharp
// 按"拓扑序 + Out 端口名排序"依次分配
// 按端口类型分池（Number 池、Entity 池、DamageSpec 池）
Dictionary<(nodeId, portName), int> slotMap = AllocateSlots(topoOrder);
```

**分配策略**：

1. **Out 端口分配 slot**：
   ```csharp
   foreach (var node in topoOrder) {
       var outPorts = node.ports.Where(p => p.direction == IRPortDirection.Output)
                                 .OrderBy(p => p.portName);  // 按名称排序
       foreach (var port in outPorts) {
           int slotId = AllocateSlotForType(port.portType);
           slotMap[(node.nodeId, port.portName)] = slotId;
       }
   }
   ```

2. **In 端口不分配 slot**：
   - In 端口通过 edge 绑定到 Out 端口的 slot
   - 节省内存，避免冗余拷贝

3. **按类型分池**：
   ```csharp
   int AllocateSlotForType(IRPortType portType) {
       switch (portType) {
           case IRPortType.Number:
               return nextNumberSlot++;
           case IRPortType.EntityId:
               return nextEntitySlot++;
           case IRPortType.DamageSpec:
               return nextDamageSpecSlot++;
           default:
               throw new Exception($"Unsupported port type: {portType}");
       }
   }
   ```

#### Step 4：Edge 绑定

```csharp
Dictionary<(nodeId, portName), int> inputBindings = new Dictionary();

foreach (var edge in graph.edges) {
    // 获取 from 端口的 slot
    int fromSlot = slotMap[(edge.fromNodeId, edge.fromPortName)];

    // 绑定 to 端口到此 slot
    var key = (edge.toNodeId, edge.toPortName);
    if (inputBindings.ContainsKey(key)) {
        throw new Exception($"Input port {key} has multiple incoming edges");
    }
    inputBindings[key] = fromSlot;
}
```

**同一 In 端口多条边 → 编译失败**：
- 保证数据流的确定性
- 避免"最后写入胜出"的不确定行为

#### Step 5：生成 Op 列表

```csharp
List<Op> ops = new List<Op>();
List<string> opToNodeId = new List<string>();

foreach (var node in topoOrder) {
    // 跳过 Entry 节点（不产生 Op）
    if (IsEntryNode(node.nodeType)) {
        continue;
    }

    switch (node.nodeType) {
        case IRNodeType.ConstNumber:
            float value = node.intParams["value"];
            int valueBits = BitConverter.SingleToInt32Bits(value);
            int outputSlot = slotMap[(node.nodeId, GetSingleOutPortName(node))];

            ops.Add(new Op {
                opCode = OpCode.ConstNumber,
                a = valueBits,
                output = outputSlot
            });
            opToNodeId.Add(node.nodeId);
            break;

        case IRNodeType.Add:
            var (inA, inB) = GetTwoInPortsSorted(node);  // 按字母序
            int slotA = inputBindings[(node.nodeId, inA)];
            int slotB = inputBindings[(node.nodeId, inB)];
            int outputAdd = slotMap[(node.nodeId, GetSingleOutPortName(node))];

            ops.Add(new Op {
                opCode = OpCode.Add,
                a = slotA,
                b = slotB,
                output = outputAdd
            });
            opToNodeId.Add(node.nodeId);
            break;

        case IRNodeType.GetStat:
            StatType statType = (StatType)node.intParams["statType"];
            int entitySlot = inputBindings[(node.nodeId, "entity")];
            int statOutput = slotMap[(node.nodeId, "value")];

            ops.Add(new Op {
                opCode = OpCode.GetStat,
                a = (int)statType,
                b = entitySlot,
                output = statOutput
            });
            opToNodeId.Add(node.nodeId);
            break;

        case IRNodeType.EmitApplyDamageCommand:
            int damageSpecSlot = inputBindings[(node.nodeId, "damageSpec")];

            ops.Add(new Op {
                opCode = OpCode.EmitApplyDamage,
                a = damageSpecSlot
            });
            opToNodeId.Add(node.nodeId);
            break;

        // ... 其他 OpCode

        default:
            throw new Exception($"Unsupported node type: {node.nodeType}");
    }
}
```

**关键点**：
- 每个 IRNode 生成 0 或 1 个 Op
- Entry 节点不生成 Op（只是标记入口）
- 保留 Op → IRNode 映射（用于 Trace）

#### Step 6：计算 planHash

```csharp
ulong planHash = StableHash64.Compute(graph);
// FNV-1a 64 位哈希，基于图结构
```

**StableHash64 算法**（`Assets/Scripts/Combat.Runtime/GraphRuntime/StableHash64.cs`）：

```csharp
public static ulong Compute(GraphIR graph) {
    ulong hash = 14695981039346656037UL;  // FNV offset basis

    // 1. 对 nodes 排序（按 nodeId）
    var sortedNodes = graph.nodes.OrderBy(n => n.nodeId).ToList();
    foreach (var node in sortedNodes) {
        hash = HashString(hash, node.nodeId);
        hash = HashInt(hash, (int)node.nodeType);

        // 对 ports 排序（按 portName）
        var sortedPorts = node.ports.OrderBy(p => p.portName).ToList();
        foreach (var port in sortedPorts) {
            hash = HashString(hash, port.portName);
            hash = HashInt(hash, (int)port.portType);
            hash = HashInt(hash, (int)port.direction);
        }

        // 对 intParams 排序（按 key）
        var sortedParams = node.intParams.OrderBy(kv => kv.Key).ToList();
        foreach (var param in sortedParams) {
            hash = HashString(hash, param.Key);
            hash = HashInt(hash, param.Value);
        }
    }

    // 2. 对 edges 排序（按 fromNodeId, toNodeId）
    var sortedEdges = graph.edges.OrderBy(e => e.fromNodeId)
                                  .ThenBy(e => e.toNodeId).ToList();
    foreach (var edge in sortedEdges) {
        hash = HashString(hash, edge.fromNodeId);
        hash = HashString(hash, edge.fromPortName);
        hash = HashString(hash, edge.toNodeId);
        hash = HashString(hash, edge.toPortName);
    }

    return hash;
}

private static ulong HashInt(ulong hash, int value) {
    hash ^= (ulong)value;
    hash *= 1099511628211UL;  // FNV prime
    return hash;
}

private static ulong HashString(ulong hash, string str) {
    foreach (char c in str) {
        hash = HashInt(hash, (int)c);
    }
    return hash;
}
```

**Hash 稳定性保证**：
- 所有集合（nodes, edges, ports, params）先排序再哈希
- 避免依赖 `string.GetHashCode()`（不同平台可能不同）
- 相同图结构 → 相同 hash
- 参数变化 → hash 不同

#### Step 7：构造 ExecPlan 和 ExecPlanAsset

```csharp
ExecPlan plan = new ExecPlan {
    planHash = planHash,
    operations = ops.ToArray(),
    slotLayout = new SlotLayout {
        numberSlotCount = nextNumberSlot,
        entitySlotCount = nextEntitySlot,
        damageSpecSlotCount = nextDamageSpecSlot
    }
};

// 返回 ExecPlan + Op→IRNode 映射
return (plan, opToNodeId.ToArray());
```

**ExecPlanAsset 包装**（`Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanAsset.cs`）：

```csharp
// 在 ExecPlanBaker 中创建
ExecPlanAsset asset = ScriptableObject.CreateInstance<ExecPlanAsset>();
asset.Initialize(
    sourceGraphId: graph.graphId,
    graphVersion: graph.version,
    plan: plan,
    opToNodeId: opToNodeId
);

// 保存到 Assets/Generated/ExecPlans/
AssetDatabase.CreateAsset(asset, outputPath);
```

---

## 5. 运行时执行（ExecPlanRunner）

### 5.1 ExecPlanRunner 职责

**文件位置：** `Assets/Scripts/Combat.Runtime/GraphRuntime/ExecPlanRunner.cs`

**核心方法**：
```csharp
public static void Execute(
    ExecPlan plan,
    ExecutionContext context,
    CommandBuffer commandBuffer,
    BattleContext battleContext,
    ITraceRecorder traceRecorder = null  // 可选 Trace
)
```

### 5.2 ExecutionContext（执行上下文）

**文件位置：** `Assets/Scripts/Combat.Runtime/Runtime/ExecutionContext.cs`

```csharp
public struct ExecutionContext
{
    // 事件元数据
    public int rootEventId;
    public int triggerDepth;
    public uint randomSeed;
    public string eventType;
    public string sourceGraphId;

    // 技能上下文
    public UnitId casterUnitId;
    public UnitId targetUnitId;

    // 执行资源
    public SlotStorage slots;         // Slot 数组
    public ExecutionBudget budget;    // 预算跟踪
}
```

**SlotStorage**（槽位存储）：

```csharp
public class SlotStorage
{
    public float[] numbers;          // Number slots
    public UnitId[] entities;        // Entity ID slots
    public DamageSpec[] damageSpecs; // DamageSpec slots

    public static SlotStorage Rent(SlotLayout layout) {
        return new SlotStorage {
            numbers = new float[layout.numberSlotCount],
            entities = new UnitId[layout.entitySlotCount],
            damageSpecs = new DamageSpec[layout.damageSpecSlotCount]
        };
    }

    public void Clear() {
        Array.Clear(numbers, 0, numbers.Length);
        Array.Clear(entities, 0, entities.Length);
        Array.Clear(damageSpecs, 0, damageSpecs.Length);
    }
}
```

**ExecutionBudget**（预算跟踪）：

```csharp
public class ExecutionBudget
{
    public int maxOpsPerEvent;
    public int maxCommandsPerEvent;

    public int opsExecuted;
    public int commandsEmitted;

    public bool CanExecuteOp() => opsExecuted < maxOpsPerEvent;
    public bool CanEmitCommand() => commandsEmitted < maxCommandsPerEvent;
}
```

### 5.3 执行流程

#### 阶段 1：准备上下文

```csharp
// 在 BattleSimulator 中准备
ExecutionContext context = new ExecutionContext {
    rootEventId = envelope.rootEventId,
    triggerDepth = envelope.triggerDepth,
    randomSeed = envelope.randomSeed,
    eventType = envelope.payload.GetType().Name,
    sourceGraphId = plan.sourceGraphId,
    casterUnitId = GetCasterFromEvent(envelope.payload),
    targetUnitId = GetTargetFromEvent(envelope.payload),
    slots = SlotStorage.Rent(plan.slotLayout),
    budget = new ExecutionBudget(
        BattleConfig.MAX_OPS_PER_EVENT,
        BattleConfig.MAX_COMMANDS_PER_EVENT
    )
};
```

#### 阶段 2：顺序执行 Op

```csharp
public static void Execute(
    ExecPlan plan,
    ExecutionContext context,
    CommandBuffer commandBuffer,
    BattleContext battleContext,
    ITraceRecorder traceRecorder = null)
{
    SlotStorage slots = context.slots;
    ExecutionBudget budget = context.budget;

    for (int i = 0; i < plan.operations.Length; i++) {
        Op op = plan.operations[i];

        // 预算检查
        if (!budget.CanExecuteOp()) {
            Debug.LogWarning($"Op 预算超限，已执行 {budget.opsExecuted} 个 Op");
            break;
        }
        budget.opsExecuted++;

        // Trace 开始（可选）
        traceRecorder?.RecordOpBegin(i, op.opCode);
        long startTicks = Stopwatch.GetTimestamp();

        // Switch-based dispatch
        switch (op.opCode) {
            case OpCode.ConstNumber:
                ExecuteConstNumber(op, slots);
                break;

            case OpCode.Add:
                ExecuteAdd(op, slots);
                break;

            case OpCode.Mul:
                ExecuteMul(op, slots);
                break;

            case OpCode.GetStat:
                ExecuteGetStat(op, slots, battleContext);
                break;

            case OpCode.MakeDamage:
                ExecuteMakeDamage(op, slots, context);
                break;

            case OpCode.EmitApplyDamage:
                ExecuteEmitApplyDamage(op, slots, commandBuffer, budget);
                break;

            case OpCode.GetCaster:
                ExecuteGetCaster(op, slots, context);
                break;

            case OpCode.GetTarget:
                ExecuteGetTarget(op, slots, context);
                break;

            default:
                throw new Exception($"Unknown OpCode: {op.opCode}");
        }

        // Trace 结束（可选）
        long endTicks = Stopwatch.GetTimestamp();
        long microseconds = (endTicks - startTicks) * 1000000 / Stopwatch.Frequency;
        traceRecorder?.RecordOpEnd(i, microseconds);
    }
}
```

#### 阶段 3：Op Handler 实现示例

**ConstNumber**：
```csharp
private static void ExecuteConstNumber(Op op, SlotStorage slots) {
    // 将 int bits 转换回 float
    float value = BitConverter.Int32BitsToSingle(op.a);
    slots.numbers[op.output] = value;
}
```

**Add**：
```csharp
private static void ExecuteAdd(Op op, SlotStorage slots) {
    float a = slots.numbers[op.a];
    float b = slots.numbers[op.b];
    slots.numbers[op.output] = a + b;
}
```

**GetStat**：
```csharp
private static void ExecuteGetStat(Op op, SlotStorage slots, BattleContext battleContext) {
    StatType statType = (StatType)op.a;
    UnitId entityId = slots.entities[op.b];

    if (!battleContext.TryGetUnit(entityId, out Unit unit)) {
        Debug.LogWarning($"Unit not found: {entityId}");
        slots.numbers[op.output] = 0;
        return;
    }

    int statValue = unit.Stats.GetStat(statType);
    slots.numbers[op.output] = statValue;
}
```

**EmitApplyDamage**：
```csharp
private static void ExecuteEmitApplyDamage(
    Op op,
    SlotStorage slots,
    CommandBuffer commandBuffer,
    ExecutionBudget budget)
{
    // 预算检查
    if (!budget.CanEmitCommand()) {
        Debug.LogWarning($"Command 预算超限，已发射 {budget.commandsEmitted} 个 Command");
        return;
    }

    DamageSpec spec = slots.damageSpecs[op.a];
    commandBuffer.Enqueue(new ApplyDamageCommand(spec));
    budget.commandsEmitted++;

    // Trace（可选）
    // traceRecorder?.RecordCommand(command, currentOpIndex);
}
```

### 5.4 性能特性

| 特性 | 实现方式 | 优势 |
|------|---------|------|
| **零虚方法调用** | Switch dispatch | 避免虚方法表查找开销 |
| **缓存友好** | 线性 Op 数组 | CPU 预取和缓存命中率高 |
| **零 GC 压力** | Struct + 数组复用 | 避免堆分配和 GC |
| **预算保护** | CanExecuteOp() 检查 | 防止无限循环和触发链爆炸 |
| **可追踪** | ITraceRecorder 注入 | 零开销（默认 null） |

---

## 6. 与事件系统的集成

### 6.1 BattleSimulator 主循环

**文件位置：** `Assets/Scripts/Combat.Runtime/Runtime/BattleSimulator.cs`

```csharp
public class BattleSimulator
{
    private EventQueue _eventQueue;
    private Dictionary<Type, ExecPlan> _eventToPlan;
    private BattleContext _battleContext;
    private CommandBuffer _commandBuffer;
    private bool _enableTracing;

    public void ProcessEvents(int maxEventsPerFrame = BattleConfig.MAX_EVENTS_PER_FRAME)
    {
        int processedCount = 0;

        while (_eventQueue.TryDequeue(out EventEnvelope envelope) &&
               processedCount < maxEventsPerFrame)
        {
            ProcessSingleEvent(envelope);
            processedCount++;
        }
    }

    private void ProcessSingleEvent(EventEnvelope envelope)
    {
        // 1. 深度限制检查
        if (envelope.triggerDepth >= BattleConfig.MAX_TRIGGER_DEPTH) {
            Debug.LogWarning($"触发链深度超限: {envelope.triggerDepth}");
            return;
        }

        // 2. 查找 Event → ExecPlan 映射
        Type eventType = envelope.payload.GetType();
        if (!_eventToPlan.TryGetValue(eventType, out ExecPlan plan)) {
            Debug.LogWarning($"No ExecPlan registered for event type: {eventType.Name}");
            return;
        }

        // 3. 准备执行上下文
        ExecutionContext context = CreateExecutionContext(envelope, plan);

        // 4. 执行 ExecPlan
        ITraceRecorder traceRecorder = _enableTracing ? new TraceRecorder() : null;
        if (traceRecorder != null) {
            traceRecorder.BeginTrace(
                eventType: context.eventType,
                rootEventId: context.rootEventId,
                triggerDepth: context.triggerDepth,
                seed: context.randomSeed,
                sourceGraphId: context.sourceGraphId,
                planHash: plan.planHash
            );
        }

        ExecPlanRunner.Execute(
            plan,
            context,
            _commandBuffer,
            _battleContext,
            traceRecorder
        );

        if (traceRecorder != null) {
            long totalMicros = /* calculate */;
            traceRecorder.EndTrace(totalMicros);

            // 导出 Trace JSON
            ExecutionTrace trace = traceRecorder.GetTrace();
            TraceExporter.ExportToJson(trace);
        }

        // 5. 两阶段提交：Apply 所有 Commands
        _commandBuffer.ApplyAll(_battleContext);
        _commandBuffer.Clear();

        // 6. 清理 SlotStorage
        context.slots.Clear();
    }
}
```

### 6.2 两阶段提交机制

**Phase 1：收集命令**
```csharp
// ExecPlanRunner.Execute() 中
// EmitApplyDamage → commandBuffer.Enqueue(new ApplyDamageCommand(spec))
// 此时不修改 Unit.HP，只将命令加入队列
```

**Phase 2：统一 Apply**
```csharp
// BattleSimulator.ProcessSingleEvent() 中
_commandBuffer.ApplyAll(_battleContext);

// CommandBuffer.ApplyAll() 实现
public void ApplyAll(BattleContext context) {
    foreach (var command in _commands) {
        command.Apply(context);  // 修改 Unit.HP，可能发布新 Event
    }
    _commands.Clear();
}
```

**为什么需要两阶段？**

| 问题 | 两阶段提交的解决方案 |
|------|-------------------|
| 回调重入 | 所有修改延迟到 Phase 2，避免执行中状态变化 |
| 执行顺序不确定 | Phase 1 确定顺序，Phase 2 统一执行 |
| Trace 完整性 | Phase 1 记录所有 Command，Phase 2 执行可追踪 |
| 触发链管理 | Phase 2 产生的新 Event 统一入队（depth + 1） |

### 6.3 触发链安全机制

**机制 1：深度限制**
```csharp
public void EnqueueEvent(ICombatEvent evt, int rootEventId, int depth, uint seed) {
    if (depth >= BattleConfig.MAX_TRIGGER_DEPTH) {
        Debug.LogWarning($"拒绝入队：触发深度超限 (depth={depth})");
        return;
    }

    _eventQueue.Enqueue(evt, rootEventId, depth, seed);
}
```

**机制 2：执行预算**
```csharp
// ExecPlanRunner.Execute() 中
if (!budget.CanExecuteOp()) {
    Debug.LogWarning($"Op 预算超限，已执行 {budget.opsExecuted} 个 Op");
    break;  // 中断执行
}
```

**机制 3：每帧事件数限制**
```csharp
public void ProcessEvents(int maxEventsPerFrame = BattleConfig.MAX_EVENTS_PER_FRAME) {
    int processedCount = 0;
    while (_eventQueue.TryDequeue(out var envelope) &&
           processedCount < maxEventsPerFrame) {
        ProcessSingleEvent(envelope);
        processedCount++;
    }
    // 剩余事件留到下一帧处理
}
```

**BattleConfig 常量**（`Assets/Scripts/Combat.Runtime/Runtime/BattleConfig.cs`）：
```csharp
public static class BattleConfig
{
    public const int MAX_TRIGGER_DEPTH = 10;        // 最大触发深度
    public const int MAX_OPS_PER_EVENT = 1000;      // 每个 Event 最多执行 1000 个 Op
    public const int MAX_COMMANDS_PER_EVENT = 100;  // 每个 Event 最多发射 100 个 Command
    public const int MAX_EVENTS_PER_FRAME = 100;    // 每帧最多处理 100 个 Event
}
```

---

## 7. 性能优化策略

### 7.1 编译期优化

| 优化项 | 实现方式 | 效果 |
|--------|---------|------|
| **确定性拓扑序** | Kahn 算法 + 字典序选择 | 相同图 → 相同 Op 序列 |
| **Slot 复用** | 编译时确定 slot 数量 | 避免运行时动态分配 |
| **常量折叠** | 识别 ConstNumber 链 | 减少运行时计算（未实现，未来优化） |
| **死代码消除** | 检测未连接的节点 | 减少 Op 数量（未实现，未来优化） |

### 7.2 运行时优化

| 优化项 | 实现方式 | 效果 |
|--------|---------|------|
| **Switch dispatch** | 避免虚方法调用 | 减少间接跳转开销 |
| **线性 Op 数组** | 顺序访问 | CPU 缓存友好 |
| **Struct 设计** | Op 是 struct | 避免堆分配 |
| **数组复用** | SlotStorage 池化 | 零 GC（未实现，未来优化） |
| **预算保护** | CanExecuteOp() 检查 | 防止性能崩溃 |

### 7.3 缓存策略

**ExecPlan 缓存**（未实现，未来优化）：
```csharp
// 伪代码
Dictionary<ulong, ExecPlan> _planCache = new Dictionary();

ExecPlan GetOrCompilePlan(GraphIR graph) {
    ulong hash = StableHash64.Compute(graph);
    if (_planCache.TryGetValue(hash, out ExecPlan cached)) {
        return cached;  // 缓存命中
    }

    // 缓存未命中，编译
    var (plan, _) = ExecPlanCompiler.Compile(graph);
    _planCache[hash] = plan;
    return plan;
}
```

### 7.4 性能基准（理论分析）

**火球术示例（6 个 Op）**：

| 操作 | 耗时（估算） | 说明 |
|------|------------|------|
| Slot 分配 | ~100ns | 3 个数组分配（编译时确定大小） |
| Op[0-5] 执行 | ~50ns × 6 = 300ns | Switch + 数组访问 |
| Command 生成 | ~50ns | 结构体拷贝 |
| Command Apply | ~200ns | Unit.HP 修改 |
| **总计** | **~650ns** | 约 150 万次/秒 |

**对比 NGP 直接执行（理论）**：

| 操作 | NGP 图遍历 | ExecPlan 执行 | 优势 |
|------|-----------|--------------|------|
| 节点访问 | 虚方法调用 + 指针跳转 | Switch + 数组访问 | **3-5x 更快** |
| 缓存命中率 | 低（指针跳转） | 高（线性数组） | **2-3x 更快** |
| GC 压力 | 每次执行分配 | 零分配（Struct） | **避免 GC 卡顿** |

---

## 8. 实际案例：火球术

### 8.1 NGP 图设计

```
[OnHitEntry]
      ↓
[GetCaster] ────────┐
      ↓             │
[ConstNumber: 100]  │
      ↓             │
[GetTarget]         │
      ↓             │
[MakeDamageSpec] ←──┘
      ↓
[EmitApplyDamageCommand]
```

**节点列表**：
1. OnHitEntry（Entry 节点）
2. GetCaster（实体节点）
3. ConstNumber(100)（数值节点）
4. GetTarget（实体节点）
5. MakeDamageSpec（效果节点）
6. EmitApplyDamageCommand（命令节点）

### 8.2 GraphIR 表示

```json
{
  "graphId": "fireball_001",
  "version": 1,
  "entryNodeId": "node_entry",
  "nodes": [
    {
      "nodeId": "node_entry",
      "nodeType": "OnHitEntry",
      "ports": [
        { "portName": "flow", "portType": "Flow", "direction": "Output" }
      ]
    },
    {
      "nodeId": "node_caster",
      "nodeType": "GetCaster",
      "ports": [
        { "portName": "caster", "portType": "EntityId", "direction": "Output" }
      ]
    },
    {
      "nodeId": "node_damage_value",
      "nodeType": "ConstNumber",
      "ports": [
        { "portName": "value", "portType": "Number", "direction": "Output" }
      ],
      "intParams": {
        "value": 1120403456  // 100.0f as int bits
      }
    },
    {
      "nodeId": "node_target",
      "nodeType": "GetTarget",
      "ports": [
        { "portName": "target", "portType": "EntityId", "direction": "Output" }
      ]
    },
    {
      "nodeId": "node_make_damage",
      "nodeType": "MakeDamageSpec",
      "ports": [
        { "portName": "amount", "portType": "Number", "direction": "Input" },
        { "portName": "target", "portType": "EntityId", "direction": "Input" },
        { "portName": "damageSpec", "portType": "DamageSpec", "direction": "Output" }
      ],
      "intParams": {
        "damageType": 0  // DamageType.Physical
      }
    },
    {
      "nodeId": "node_emit",
      "nodeType": "EmitApplyDamageCommand",
      "ports": [
        { "portName": "damageSpec", "portType": "DamageSpec", "direction": "Input" }
      ]
    }
  ],
  "edges": [
    { "fromNodeId": "node_damage_value", "fromPortName": "value",
      "toNodeId": "node_make_damage", "toPortName": "amount" },
    { "fromNodeId": "node_target", "fromPortName": "target",
      "toNodeId": "node_make_damage", "toPortName": "target" },
    { "fromNodeId": "node_make_damage", "fromPortName": "damageSpec",
      "toNodeId": "node_emit", "toPortName": "damageSpec" }
  ]
}
```

### 8.3 编译后的 ExecPlan

**拓扑序**（Entry 节点跳过）：
```
node_caster → node_damage_value → node_target → node_make_damage → node_emit
```

**Slot 分配**：
```
numberSlotCount = 1     (node_damage_value.value)
entitySlotCount = 2     (node_caster.caster, node_target.target)
damageSpecSlotCount = 1 (node_make_damage.damageSpec)

slotMap = {
  ("node_damage_value", "value") → numbers[0],
  ("node_caster", "caster") → entities[0],
  ("node_target", "target") → entities[1],
  ("node_make_damage", "damageSpec") → damageSpecs[0]
}

inputBindings = {
  ("node_make_damage", "amount") → numbers[0],
  ("node_make_damage", "target") → entities[1],
  ("node_emit", "damageSpec") → damageSpecs[0]
}
```

**Op 序列**：
```csharp
Op[0]: { opCode=GetCaster,     a=0,            b=0,            output=0 }  // → entities[0]
Op[1]: { opCode=ConstNumber,   a=1120403456,   b=0,            output=0 }  // → numbers[0]
Op[2]: { opCode=GetTarget,     a=0,            b=0,            output=1 }  // → entities[1]
Op[3]: { opCode=MakeDamage,    a=0,            b=1,            output=0 }  // → damageSpecs[0]
Op[4]: { opCode=EmitApplyDamage, a=0,          b=0,            output=0 }  // → CommandBuffer
```

**planHash**：`0x1A2B3C4D5E6F7890`（示例值）

### 8.4 运行时执行流程

#### Step 1：准备上下文
```csharp
ExecutionContext context = new ExecutionContext {
    casterUnitId = new UnitId(1),
    targetUnitId = new UnitId(2),
    slots = SlotStorage.Rent(new SlotLayout {
        numberSlotCount = 1,
        entitySlotCount = 2,
        damageSpecSlotCount = 1
    }),
    budget = new ExecutionBudget(1000, 100)
};
```

#### Step 2：执行 Op[0] - GetCaster
```csharp
context.slots.entities[0] = context.casterUnitId;  // UnitId(1)
```

#### Step 3：执行 Op[1] - ConstNumber
```csharp
float value = BitConverter.Int32BitsToSingle(1120403456);  // 100.0f
context.slots.numbers[0] = value;
```

#### Step 4：执行 Op[2] - GetTarget
```csharp
context.slots.entities[1] = context.targetUnitId;  // UnitId(2)
```

#### Step 5：执行 Op[3] - MakeDamage
```csharp
float amount = context.slots.numbers[0];          // 100.0f
UnitId target = context.slots.entities[1];        // UnitId(2)
DamageType damageType = DamageType.Physical;      // 从 Op 参数读取

context.slots.damageSpecs[0] = new DamageSpec(
    sourceUnitId: context.casterUnitId,
    targetUnitId: target,
    baseValue: (int)amount,
    damageType: damageType
);
```

#### Step 6：执行 Op[4] - EmitApplyDamage
```csharp
DamageSpec spec = context.slots.damageSpecs[0];
commandBuffer.Enqueue(new ApplyDamageCommand(spec));
budget.commandsEmitted++;
```

#### Step 7：Apply Commands
```csharp
commandBuffer.ApplyAll(battleContext);

// ApplyDamageCommand.Apply() 实现
public void Apply(BattleContext context) {
    context.ApplyDamage(spec.TargetUnitId, spec.BaseValue);

    // 可能发布新 Event（如 OnDamageTaken, OnKill）
    // context.Events.Publish(new OnDamageTakenEvent(...));
}
```

### 8.5 Trace 输出示例

```json
{
  "eventType": "OnHitEvent",
  "rootEventId": 42,
  "triggerDepth": 0,
  "randomSeed": 12345,
  "sourceGraphId": "fireball_001",
  "planHash": "0x1A2B3C4D5E6F7890",
  "casterUnitId": 1,
  "targetUnitId": 2,
  "opExecutions": [
    { "opIndex": 0, "opCode": "GetCaster", "microseconds": 45 },
    { "opIndex": 1, "opCode": "ConstNumber", "microseconds": 38 },
    { "opIndex": 2, "opCode": "GetTarget", "microseconds": 42 },
    { "opIndex": 3, "opCode": "MakeDamage", "microseconds": 67 },
    { "opIndex": 4, "opCode": "EmitApplyDamage", "microseconds": 53 }
  ],
  "commands": [
    { "commandType": "ApplyDamageCommand", "emittedAtOpIndex": 4,
      "commandData": "DamageSpec(source=1, target=2, amount=100, type=Physical)" }
  ],
  "totalExecutionMicroseconds": 245,
  "totalOpsExecuted": 5,
  "totalCommandsEmitted": 1
}
```

---

## 9. 与 DamagePipeline 的关系

### 9.1 职责分层

```
┌─────────────────────────────────┐
│ ExecPlan（图执行层）             │
│ - 负责"图逻辑"                   │
│ - 算术、条件、数据流             │
│ - 生成 Command                  │
└─────────────┬───────────────────┘
              │ EmitApplyDamage Command
              ▼
┌─────────────────────────────────┐
│ Command.Apply()                 │
│ - 构造 HitInstance               │
│ - 调用 DamagePipeline            │
└─────────────┬───────────────────┘
              │ DamagePipeline.Resolve()
              ▼
┌─────────────────────────────────┐
│ DamagePipeline（战斗结算层）     │
│ - 负责"战斗结算"                 │
│ - 暴击、抗性、命中判定           │
│ - 返回 DamageResult              │
└─────────────┬───────────────────┘
              │ DamageResult
              ▼
┌─────────────────────────────────┐
│ HealthComponent.Apply()         │
│ - 修改 Unit.HP                   │
│ - 发布新 Event（OnKill 等）      │
└─────────────────────────────────┘
```

### 9.2 集成示例（未来实现）

**当前实现（Task-01 ~ Task-07）**：
```csharp
// ApplyDamageCommand.Apply()
public void Apply(BattleContext context) {
    context.ApplyDamage(spec.TargetUnitId, spec.BaseValue);
    // 直接修改 HP，无暴击、抗性逻辑
}
```

**未来集成（Task-08 + Task-09）**：
```csharp
// ApplyDamageCommand.Apply()
public void Apply(BattleContext context) {
    // 1. 构造 HitInstance
    HitInstance hit = new HitInstance(
        sourceUnitId: spec.SourceUnitId,
        targetUnitId: spec.TargetUnitId,
        randomSeed: context.NextRandomSeed(),
        flags: HitFlags.IsSpell
    );
    hit.BaseDamage = new DamagePacket(physical: spec.BaseValue);

    // 2. 调用 DamagePipeline
    StandardDamagePipeline pipeline = context.GetDamagePipeline();
    DamageResult result = pipeline.Resolve(hit);

    // 3. 应用最终伤害
    float totalDamage = result.GetTotalDamage();
    context.ApplyDamage(spec.TargetUnitId, (int)totalDamage);

    // 4. 发布事件
    if (result.IsCrit) {
        context.Events.Publish(new OnCritEvent(...));
    }
}
```

### 9.3 为什么分层？

| 层级 | 职责 | 扩展方式 | 示例 |
|------|------|---------|------|
| **ExecPlan** | 图逻辑 | 扩展 OpCode | Add, Mul, Branch, Fork |
| **DamagePipeline** | 战斗结算 | 扩展 Step | RollCrit, ApplyResist, ApplyArmor |

**好处**：
- **职责清晰**：图执行与战斗结算解耦
- **独立扩展**：添加新 Step 不影响 ExecPlan
- **可复用**：同一 DamagePipeline 可用于所有伤害来源

---

## 10. 关键设计决策

### 10.1 为什么不直接执行 NGP 图？

| 问题 | 说明 |
|------|------|
| **依赖性** | NGP 依赖 Unity Editor，Runtime 无法引用 |
| **性能** | 图遍历需要指针跳转，缓存不友好 |
| **可缓存性** | 图结构难以序列化和缓存 |
| **确定性** | 图遍历顺序可能不确定 |

### 10.2 为什么需要 GraphIR？

| 优势 | 说明 |
|------|------|
| **稳定契约** | 不依赖 NGP 版本，可跨平台迁移 |
| **静态校验** | 编译前检查类型匹配、环检测等 |
| **中间优化** | 可以做常量折叠、死代码消除等 |
| **多后端支持** | 未来可生成 C++ / Lua / WASM |

### 10.3 为什么使用 Switch 而非虚方法？

**虚方法方案**（未采用）：
```csharp
interface IOpHandler {
    void Execute(ExecutionContext context);
}

class AddOpHandler : IOpHandler { ... }
class MulOpHandler : IOpHandler { ... }

// 运行时
foreach (var op in plan.operations) {
    IOpHandler handler = GetHandler(op.opCode);
    handler.Execute(context);  // 虚方法调用
}
```

**Switch 方案**（已采用）：
```csharp
foreach (var op in plan.operations) {
    switch (op.opCode) {
        case OpCode.Add: ExecuteAdd(op, context); break;
        case OpCode.Mul: ExecuteMul(op, context); break;
        // ...
    }
}
```

**对比**：

| 指标 | 虚方法 | Switch |
|------|--------|--------|
| **性能** | 虚方法表查找 (~2-5ns) | 分支预测 (~1ns) |
| **缓存友好** | 中等（间接跳转） | 高（线性代码） |
| **代码膨胀** | 多个类文件 | 单个 switch |
| **扩展性** | 添加新 Handler 无需修改 Runner | 需修改 switch |
| **适用场景** | OpCode 数量不确定 | OpCode 数量已知且固定 |

**结论**：由于 OpCode 数量已知且固定（8-20 个），Switch 性能更优。

### 10.4 为什么使用 Slot 数组而非字典？

**字典方案**（未采用）：
```csharp
Dictionary<string, object> values = new Dictionary();
values["node1.output"] = 100.0f;  // Boxing + Hash 查找
```

**Slot 数组方案**（已采用）：
```csharp
float[] numbers = new float[10];
numbers[3] = 100.0f;  // 直接数组访问
```

**对比**：

| 指标 | 字典 | Slot 数组 |
|------|------|----------|
| **访问速度** | O(1) Hash 查找 (~5-10ns) | O(1) 数组访问 (~1ns) |
| **内存开销** | 高（Entry 对象 + Hash 表） | 低（连续数组） |
| **GC 压力** | 高（Boxing + Entry 分配） | 零（Struct + 栈分配） |
| **类型安全** | 低（object，需类型转换） | 高（强类型数组） |
| **缓存友好** | 中等（Hash 跳转） | 高（连续内存） |

**结论**：Slot 数组在性能、内存、GC 方面全面优于字典。

### 10.5 为什么需要两阶段提交？

| 场景 | 单阶段（直接修改） | 两阶段（Command 队列） |
|------|------------------|---------------------|
| **回调重入** | 执行中状态变化，顺序混乱 | 延迟修改，顺序确定 |
| **Trace 完整性** | 难以追踪所有修改 | Command 队列可追踪 |
| **触发链管理** | 递归调用，深度难控制 | 统一入队，深度可控 |
| **事务性** | 无法回滚 | 可实现回滚（未来） |

**示例问题**（单阶段）：
```csharp
// OnHit → ApplyDamage → 发布 OnKill → 触发 OnKill Handler
// OnKill Handler 修改战场状态 → 影响后续 OnHit 的执行
// 顺序不可预测
```

**解决方案**（两阶段）：
```csharp
// Phase 1: OnHit 收集 Commands（不修改状态）
// Phase 2: 统一 Apply Commands → 发布 OnKill → 新 Event 入队（depth + 1）
// Phase 1: OnKill 收集 Commands（不修改状态）
// Phase 2: 统一 Apply Commands
// 顺序可预测
```

---

## 11. 常见问题 FAQ

### Q1：如何扩展新的 OpCode？

**步骤**：
1. 在 `OpCode.cs` 添加新枚举值
2. 在 `IRNodeType.cs` 添加对应的节点类型
3. 在 `ExecPlanCompiler.cs` 的 switch 中添加编译逻辑
4. 在 `ExecPlanRunner.cs` 的 switch 中添加执行逻辑
5. 在 NGP 中创建对应的 Node 类（Editor Only）

**示例：添加 RollChance OpCode**：

```csharp
// 1. OpCode.cs
public enum OpCode : byte {
    // ...
    RollChance = 8,  // 新增
}

// 2. IRNodeType.cs
public enum IRNodeType : byte {
    // ...
    RollChance = 40,  // 新增
}

// 3. ExecPlanCompiler.cs
case IRNodeType.RollChance:
    float chance = BitConverter.Int32BitsToSingle(node.intParams["chance"]);
    int chanceSlot = inputBindings[(node.nodeId, "chance")];
    int resultSlot = slotMap[(node.nodeId, "result")];

    ops.Add(new Op {
        opCode = OpCode.RollChance,
        a = chanceSlot,
        output = resultSlot
    });
    break;

// 4. ExecPlanRunner.cs
case OpCode.RollChance:
    float chance = slots.numbers[op.a];
    bool success = context.Rng.NextFloat() < chance;
    slots.bools[op.output] = success;  // 需要扩展 bool slot 池
    break;
```

### Q2：如何调试 ExecPlan 执行？

**方法 1：启用 Trace**
```csharp
BattleSimulator simulator = new BattleSimulator(
    battleContext,
    eventBus,
    enableTracing: true  // 启用 Trace
);

// 执行后查看 Trace JSON
// Application.persistentDataPath/Traces/trace_42_20260108.json
```

**方法 2：使用断点**
```csharp
// ExecPlanRunner.cs 的 Execute 方法中设置断点
// 单步执行每个 Op，查看 slots 内容
```

**方法 3：日志输出**
```csharp
// ExecPlanRunner.cs
case OpCode.Add:
    float a = slots.numbers[op.a];
    float b = slots.numbers[op.b];
    float result = a + b;
    Debug.Log($"Add: {a} + {b} = {result}");
    slots.numbers[op.output] = result;
    break;
```

### Q3：如何优化 ExecPlan 的性能？

**编译期优化**（未实现）：
- 常量折叠：识别 `ConstNumber → Add` 链，编译时计算
- 死代码消除：移除未连接到 Emit 节点的计算
- 公共子表达式消除：复用相同的计算结果

**运行时优化**（部分实现）：
- Slot 池化：使用 `ArrayPool<T>` 避免分配
- ExecPlan 缓存：基于 `planHash` 缓存编译结果
- SIMD 指令：对批量数值计算使用 Vector 指令

### Q4：ExecPlan 如何支持条件分支？

**当前实现**：不支持条件分支（所有 Op 顺序执行）

**未来扩展**：添加 `Branch` OpCode

```csharp
// Op 扩展：添加 jumpTarget 字段
public struct Op {
    public OpCode opCode;
    public int a, b, output;
    public int jumpTarget;  // 新增：跳转目标 Op 索引
}

// ExecPlanRunner 扩展
case OpCode.Branch:
    bool condition = slots.bools[op.a];
    if (condition) {
        i = op.jumpTarget - 1;  // 跳转到目标 Op（-1 因为 for 循环会 i++）
    }
    break;
```

**挑战**：
- 拓扑排序不再适用（需要支持有环图）
- Slot 生命周期管理复杂（分支后 slot 复用）
- 编译器需要做控制流分析

### Q5：ExecPlan 如何支持循环？

**当前实现**：不支持循环（DAG 约束）

**未来扩展**：添加 `Loop` OpCode + 循环预算

```csharp
// Loop 预算
public class ExecutionBudget {
    public int maxLoopIterations = 1000;
    public int loopIterations = 0;
}

// ExecPlanRunner
case OpCode.LoopBegin:
    context.budget.loopIterations = 0;
    break;

case OpCode.LoopContinue:
    bool condition = slots.bools[op.a];
    if (condition && context.budget.loopIterations < context.budget.maxLoopIterations) {
        context.budget.loopIterations++;
        i = op.jumpTarget - 1;  // 跳回 LoopBegin
    }
    break;
```

**挑战**：
- 循环展开策略（编译期 vs 运行时）
- 死循环检测（预算限制 + 静态分析）
- 性能优化（循环内 slot 复用）

### Q6：如何支持多目标技能（AOE）？

**方案 1：FindTargetsInRadius 节点**（NGP 层）
```
FindTargetsInRadius(center, radius) → EntityList
ForEach(EntityList) → Entity
  └─ MakeDamage → EmitApplyDamage
```

**方案 2：EmitApplyDamageAoE 命令**（Command 层）
```csharp
public class ApplyDamageAoeCommand : ICombatCommand {
    public UnitId SourceUnitId;
    public List<UnitId> TargetUnitIds;  // 多个目标
    public int BaseValue;

    public void Apply(BattleContext context) {
        foreach (var target in TargetUnitIds) {
            context.ApplyDamage(target, BaseValue);
        }
    }
}
```

**推荐**：方案 2，保持 ExecPlan 简单（无循环），由 Command 处理多目标。

---

## 附录 A：文件速查表

| 文件路径 | 职责 | 关键类型 |
|---------|------|---------|
| `Combat.Runtime/GraphRuntime/ExecPlan.cs` | ExecPlan 定义 | ExecPlan, SlotLayout |
| `Combat.Runtime/GraphRuntime/Op.cs` | Op 定义 | Op, OpCode |
| `Combat.Runtime/GraphRuntime/ExecPlanCompiler.cs` | 编译器 | ExecPlanCompiler.Compile() |
| `Combat.Runtime/GraphRuntime/ExecPlanRunner.cs` | 执行器 | ExecPlanRunner.Execute() |
| `Combat.Runtime/GraphRuntime/StableHash64.cs` | Hash 计算 | StableHash64.Compute() |
| `Combat.Runtime/GraphRuntime/ExecPlanAsset.cs` | Unity 资产包装 | ExecPlanAsset |
| `Combat.Runtime/Runtime/ExecutionContext.cs` | 执行上下文 | ExecutionContext, SlotStorage |
| `Combat.Runtime/Runtime/BattleSimulator.cs` | 主循环 | BattleSimulator |
| `Combat.Runtime/Runtime/BattleConfig.cs` | 配置常量 | BattleConfig |
| `Combat.Editor/GraphBuild/ExecPlanBaker.cs` | 烘焙工具 | ExecPlanBaker.Bake() |

---

## 附录 B：术语表

| 术语 | 说明 |
|------|------|
| **ExecPlan** | 执行计划，NGP 图编译后的线性指令序列 |
| **Op** | 操作指令，ExecPlan 的基本执行单元 |
| **OpCode** | 操作码，标识 Op 的类型（Add, Mul 等） |
| **Slot** | 槽位，存储中间计算结果的数组元素 |
| **SlotLayout** | 槽位布局，描述需要分配多少 slot |
| **GraphIR** | 图中间表示，NGP 图的稳定数据结构 |
| **拓扑排序** | 将 DAG 转换为线性序列的算法 |
| **确定性** | 相同输入 → 相同输出 |
| **两阶段提交** | Command 收集 + 统一 Apply |
| **触发链** | Event → ExecPlan → Command → Event → ... |
| **预算** | 限制执行次数，防止无限循环 |
| **Trace** | 执行追踪，记录 Op 执行时间和顺序 |

---

## 附录 C：性能基准测试（TODO）

**待补充内容**：
- 火球术执行耗时（实测）
- ExecPlan vs NGP 图性能对比
- Slot 池化前后 GC 对比
- Switch vs 虚方法性能对比
- 不同 OpCode 的执行耗时分布

**测试计划**：
```csharp
[Test]
public void Benchmark_Fireball_1000Iterations() {
    // 执行 1000 次火球术，测量总耗时
    Stopwatch sw = Stopwatch.StartNew();
    for (int i = 0; i < 1000; i++) {
        simulator.ProcessEvents();
    }
    sw.Stop();

    double avgMicros = sw.Elapsed.TotalMilliseconds * 1000 / 1000;
    Debug.Log($"Average: {avgMicros:F2} μs/iteration");
}
```

---

## 附录 D：更新日志

| 版本 | 日期 | 作者 | 变更内容 |
|------|------|------|---------|
| 1.0 | 2026-01-08 | Claude | 初始版本，覆盖 Task-01 至 Task-08 |

---

**文档结束**

如有疑问或需要补充内容，请联系项目维护者。
