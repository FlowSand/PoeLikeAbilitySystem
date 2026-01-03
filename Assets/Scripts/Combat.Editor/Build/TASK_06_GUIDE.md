# Task-06 Transformer 开发指南

本文档指导如何为 PoE-like 技能系统创建新的 **Support Transformer**。

---

## 一、Transformer 概念

### 什么是 Transformer？

Transformer 是构筑系统的核心，用于在**构建期**修改 GraphIR，生成不同的技能变体。

- **输入**：原始 GraphIR（技能图）
- **输出**：变换后的 GraphIR
- **时机**：构建期（Bake 时），运行时完全不感知

### Transformer vs 运行时逻辑

| 特性 | Transformer（正确） | 运行时 if/else（错误） |
|------|---------------------|----------------------|
| 执行时机 | 构建期（Bake） | 运行时（Play） |
| 性能影响 | 零（已编译） | 有（每帧判断） |
| 可缓存 | 是（基于 hash） | 否（动态） |
| 可审计 | 是（图结构变化可见） | 否（代码分支隐藏） |

---

## 二、快速开始

### Step 1: 创建 Transformer 类

在 `Assets/Scripts/Combat.Editor/Build/Transformers/` 下创建新文件：

```csharp
using Combat.Runtime.GraphIR;
using Combat.Runtime.Build;
using Combat.Runtime.Model;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Editor.Build.Transformers
{
    /// <summary>
    /// 示例：伤害加成 Transformer
    /// 为所有伤害节点添加固定加成值
    /// </summary>
    public class DamageBonusTransformer : IGraphTransformer, IParameterizedTransformer
    {
        private int bonusDamage = 10;

        // ===== 步骤 1：实现参数设置 =====
        public void SetParameters(List<SupportParam> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].key == "bonusDamage")
                {
                    bonusDamage = parameters[i].intValue;
                }
            }
        }

        // ===== 步骤 2：实现应用条件 =====
        public bool CanApply(GraphIR.GraphIR graph, BuildContext context)
        {
            // 示例：无限制，始终可应用
            return true;

            // 或者：检查 Tag
            // return GraphTransformUtils.GraphContainsTag(graph, SkillTags.Projectile);

            // 或者：检查节点类型
            // return GraphTransformUtils.FindNodesByType(graph, IRNodeType.MakeDamageSpec).Count > 0;
        }

        // ===== 步骤 3：实现图变换逻辑 =====
        public GraphIR.GraphIR Apply(GraphIR.GraphIR sourceGraph, BuildContext context)
        {
            // 1. 克隆图（永远不修改源图）
            GraphIR.GraphIR graph = GraphTransformUtils.CloneGraph(sourceGraph);

            // 2. 查找目标节点
            List<IRNode> damageNodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.MakeDamageSpec);

            // 3. 修改节点（这里需要找到连接到 MakeDamageSpec 的 ConstNumber 节点）
            for (int i = 0; i < damageNodes.Count; i++)
            {
                // 查找输入边
                List<IREdge> inEdges = GraphTransformUtils.FindIncomingEdges(graph, damageNodes[i].nodeId);

                for (int j = 0; j < inEdges.Count; j++)
                {
                    IRNode sourceNode = GraphTransformUtils.FindNodeById(graph, inEdges[j].fromNodeId);

                    if (sourceNode != null && sourceNode.nodeType == IRNodeType.ConstNumber)
                    {
                        // 提取当前值
                        int valueBits = sourceNode.intParams["value"];
                        float currentValue = System.BitConverter.Int32BitsToSingle(valueBits);

                        // 添加加成
                        float newValue = currentValue + bonusDamage;
                        int newBits = System.BitConverter.SingleToInt32Bits(newValue);

                        // 修改参数
                        GraphTransformUtils.ModifyIntParam(sourceNode, "value", newBits);
                    }
                }
            }

            // 4. 返回变换后的图
            return graph;
        }
    }
}
```

### Step 2: 创建 SupportDefinition 资产

1. 在 Unity 编辑器中，右键 `Assets/GraphAssets/Supports/`
2. 选择 `Create → Combat → Support Definition`
3. 命名为 `Support_DamageBonus.asset`
4. 在 Inspector 中配置：
   - **Support Id**: `support_damage_bonus`
   - **Display Name**: `Damage Bonus Support`
   - **Priority**: 50
   - **Transformer Type Name**: `Combat.Editor.Build.Transformers.DamageBonusTransformer`
   - **Parameters**:
     - Key: `bonusDamage`
     - Type: `Int`
     - Int Value: `10`

### Step 3: 应用到技能

1. 打开 EffectGraphAsset
2. 在 Inspector 中找到 `Build Configuration` 部分
3. **Skill Tags** 中添加技能 Tag（如 `Fire`, `Projectile`）
4. **Supports** 中添加 `Support_DamageBonus.asset`
5. 点击 `Assets → Combat → Bake Effect Graph`

---

## 三、GraphTransformUtils 工具库

所有 Transformer **必须**通过 GraphTransformUtils 操作图，禁止直接修改 List/Dictionary。

### 3.1 深拷贝

```csharp
// 克隆整个图（必须在 Apply 开头调用）
GraphIR.GraphIR clone = GraphTransformUtils.CloneGraph(sourceGraph);

// 克隆单个节点
IRNode nodeCopy = GraphTransformUtils.CloneNode(originalNode);
```

### 3.2 查找节点

```csharp
// 通过 ID 查找
IRNode node = GraphTransformUtils.FindNodeById(graph, "node_12345");

// 通过类型查找（返回列表）
List<IRNode> damageNodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.MakeDamageSpec);

// 通过 Tag 查找
List<IRNode> fireNodes = GraphTransformUtils.FindNodesByTag(graph, SkillTags.Fire);
```

### 3.3 查找边

```csharp
// 查找指向某节点的边（输入边）
List<IREdge> inEdges = GraphTransformUtils.FindIncomingEdges(graph, nodeId);

// 查找从某节点出发的边（输出边）
List<IREdge> outEdges = GraphTransformUtils.FindOutgoingEdges(graph, nodeId);
```

### 3.4 修改参数

```csharp
// 修改单个 intParam
GraphTransformUtils.ModifyIntParam(node, "damageType", (int)DamageType.Cold);

// 修改多个 intParam
Dictionary<string, int> newParams = new Dictionary<string, int>();
newParams["value"] = newValueBits;
newParams["damageType"] = (int)DamageType.Fire;
GraphTransformUtils.ModifyIntParams(node, newParams);
```

### 3.5 Tag 操作

```csharp
// 添加 Tag
GraphTransformUtils.AddTag(node, SkillTags.Fire);

// 移除 Tag
GraphTransformUtils.RemoveTag(node, SkillTags.Cold);

// 检查节点是否有 Tag
bool hasFire = GraphTransformUtils.HasTag(node, SkillTags.Fire);

// 检查图中是否存在某 Tag
bool graphHasFire = GraphTransformUtils.GraphContainsTag(graph, SkillTags.Fire);
```

### 3.6 节点/边管理

```csharp
// 添加节点
IRNode newNode = new IRNode { ... };
GraphTransformUtils.AddNode(graph, newNode);

// 移除节点
GraphTransformUtils.RemoveNode(graph, nodeId);

// 添加边
IREdge edge = new IREdge { fromNodeId = "a", fromPort = "out", toNodeId = "b", toPort = "in" };
GraphTransformUtils.AddEdge(graph, edge);

// 移除边
GraphTransformUtils.RemoveEdge(graph, edge);
```

### 3.7 ID 生成

```csharp
// 生成唯一节点 ID
string nodeId = GraphTransformUtils.GenerateNodeId(graph, "bonus");
// 结果示例："bonus_a1b2c3d4"
```

---

## 四、常见模式

### 模式 1：修改节点参数

**场景**：元素转化（Fire → Cold）

```csharp
public GraphIR.GraphIR Apply(GraphIR.GraphIR sourceGraph, BuildContext context)
{
    GraphIR.GraphIR graph = GraphTransformUtils.CloneGraph(sourceGraph);

    // 查找所有 MakeDamageSpec 节点
    List<IRNode> nodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.MakeDamageSpec);

    for (int i = 0; i < nodes.Count; i++)
    {
        IRNode node = nodes[i];

        // 检查当前伤害类型
        if (node.intParams.ContainsKey("damageType") &&
            node.intParams["damageType"] == (int)DamageType.Fire)
        {
            // 修改为 Cold
            GraphTransformUtils.ModifyIntParam(node, "damageType", (int)DamageType.Cold);

            // 更新 Tag
            GraphTransformUtils.RemoveTag(node, SkillTags.Fire);
            GraphTransformUtils.AddTag(node, SkillTags.Cold);
        }
    }

    return graph;
}
```

### 模式 2：缩放数值

**场景**：伤害倍率（×0.7）

```csharp
public GraphIR.GraphIR Apply(GraphIR.GraphIR sourceGraph, BuildContext context)
{
    GraphIR.GraphIR graph = GraphTransformUtils.CloneGraph(sourceGraph);

    // 查找所有 ConstNumber 节点
    List<IRNode> constNodes = GraphTransformUtils.FindNodesByType(graph, IRNodeType.ConstNumber);

    for (int i = 0; i < constNodes.Count; i++)
    {
        IRNode node = constNodes[i];

        if (node.intParams.ContainsKey("value"))
        {
            // 提取 float 值（存储为 int bits）
            int valueBits = node.intParams["value"];
            float value = System.BitConverter.Int32BitsToSingle(valueBits);

            // 缩放
            float scaled = value * 0.7f;

            // 写回
            int scaledBits = System.BitConverter.SingleToInt32Bits(scaled);
            GraphTransformUtils.ModifyIntParam(node, "value", scaledBits);
        }
    }

    return graph;
}
```

### 模式 3：条件应用（检查 Tag）

**场景**：仅对投射物技能生效

```csharp
public bool CanApply(GraphIR.GraphIR graph, BuildContext context)
{
    // 检查 BuildContext 中的技能 Tag
    if (context.skillTags != null && context.skillTags.Contains(SkillTags.Projectile))
    {
        return true;
    }

    // 或者检查图中是否有投射物节点 Tag
    return GraphTransformUtils.GraphContainsTag(graph, SkillTags.Projectile);
}
```

---

## 五、最佳实践

### ✅ DO（应该做的事）

1. **始终克隆图**：在 Apply() 第一行调用 `CloneGraph()`
2. **使用工具库**：所有图操作通过 GraphTransformUtils
3. **添加日志**：使用 `Debug.Log()` 记录变换过程
4. **参数化**：通过 IParameterizedTransformer 接受配置
5. **防御性编程**：检查 null，检查参数存在性
6. **语义化命名**：Transformer 类名清晰表达功能

### ❌ DON'T（禁止做的事）

1. **直接修改源图**：永远不要修改 `sourceGraph` 参数
2. **直接操作 List/Dictionary**：禁止 `graph.nodes.Add()` 等操作
3. **运行时逻辑**：Transformer 只能在构建期生效
4. **绕过 Validator**：BuildAssembler 会自动验证，无需手动调用
5. **破坏图结构**：不要创建无效边、孤立节点、循环依赖
6. **依赖外部状态**：Transformer 必须是纯函数（相同输入 → 相同输出）

---

## 六、调试技巧

### 6.1 启用日志

在 EffectGraphAsset 的 BuildContext 中：
```csharp
context.options.logTransforms = true; // 记录每个 Support 的应用
```

### 6.2 查看变换前后

在 Transformer 的 Apply() 中：
```csharp
Debug.Log($"[MyTransformer] Before: {sourceGraph.nodes.Count} nodes");
// ... 变换逻辑 ...
Debug.Log($"[MyTransformer] After: {graph.nodes.Count} nodes");
```

### 6.3 验证失败排查

如果 Bake 后报 "Post-transform validation failed"：
1. 检查是否创建了无效边（端口类型不匹配）
2. 检查是否创建了不存在的节点引用
3. 检查是否引入了循环依赖

---

## 七、示例 Transformers

### 示例 1：DamageScaleTransformer

**文件**：`Assets/Scripts/Combat.Editor/Build/Transformers/DamageScaleTransformer.cs`

**功能**：缩放所有 ConstNumber 节点的值

**适用条件**：无限制

**参数**：
- `damageMultiplier` (float): 伤害倍率，默认 0.7

### 示例 2：ElementalConversionTransformer

**文件**：`Assets/Scripts/Combat.Editor/Build/Transformers/ElementalConversionTransformer.cs`

**功能**：Fire → Cold 伤害类型转换

**适用条件**：GraphIR 包含 "Fire" Tag

**参数**：
- `fromElement` (string): 源元素，默认 "Fire"
- `toElement` (string): 目标元素，默认 "Cold"

---

## 八、FAQ

### Q1：如何插入新节点？

A：当前版本暂未实现 InsertNodeBetween。临时方案：
1. 创建新节点：`IRNode newNode = new IRNode { ... };`
2. 添加到图：`GraphTransformUtils.AddNode(graph, newNode);`
3. 手动创建边：`GraphTransformUtils.AddEdge(graph, new IREdge { ... });`

### Q2：如何处理多个 Support 的优先级？

A：在 SupportDefinition 的 `priority` 字段设置优先级（数字越小越先执行）。

### Q3：如何调试 Transformer 未生效？

A：检查：
1. CanApply() 是否返回 true
2. transformerTypeName 是否正确（完整类型名）
3. SupportDefinition 是否添加到 EffectGraphAsset.supports
4. Bake 日志中是否有错误

### Q4：如何让 Transformer 支持参数配置？

A：实现 `IParameterizedTransformer` 接口，然后在 SupportDefinition.parameters 中添加参数。

---

## 九、参考资料

- **代码示例**：`Assets/Scripts/Combat.Editor/Build/Transformers/`
- **工具库 API**：`Assets/Scripts/Combat.Editor/Build/GraphTransformUtils.cs`
- **架构文档**：`CLAUDE.md` 第 9 节 Task-06
- **工作记忆**：`WORK_MEMORY.md` 第 6 节
