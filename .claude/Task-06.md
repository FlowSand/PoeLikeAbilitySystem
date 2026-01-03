你正在一个 Unity 工程中实现 PoE-like 技能系统的“构筑阶段”。

在此前 Task-01 ～ Task-05 中，系统已具备：
- 稳定的 Combat Runtime（事件驱动 + 两阶段提交）
- GraphIR（技能中间表示）
- ExecPlan（编译执行计划）
- NodeGraphProcessor 编辑器（作者态）
- NGP Graph → GraphIR → ExecPlan 的完整链路

【本任务目标】
实现 PoE-like 的“构筑系统”，使：
- 同一个主技能（Effect Graph）
- 在不同 Support / 构筑条件下
- 通过 **图变换（Graph Transform）**
- 生成不同的 GraphIR / ExecPlan

⚠️ 本任务发生在 **构建期（Editor / Build 阶段）**，不是 Runtime。

---

## 一、强制架构约束（ABSOLUTE）

1. Support 不得在 Runtime 执行
2. Support 不得写技能脚本
3. Support 只能通过 **GraphIR 变换** 生效
4. 构筑结果必须是：
    - 新的 GraphIR
    - 新的 ExecPlan
5. Runtime 不感知 Support 的存在，只执行 ExecPlan
6. 不允许在 ExecPlanRunner 中写任何构筑逻辑

---

## 二、需要实现的核心模块

### 1️⃣ SupportDefinition（Support 定义）

实现 Support 的“声明式定义”。

#### 必须包含字段
- supportId（string）
- displayName（string）
- requiredTags（TagSet）
- priority（int，越小越先执行）
- transformers（List<IGraphTransformer>）

#### 说明
- requiredTags 用于判断技能是否可被该 Support 修改
- priority 决定多个 Support 的应用顺序

---

### 2️⃣ IGraphTransformer（图变换器接口）

这是 Task-06 的技术核心。

#### 接口定义（示例）
- bool CanApply(GraphIR graph)
- GraphIR Apply(GraphIR graph, BuildContext context)

#### Transformer 必须支持的操作
1. 插入节点（InsertNode）
2. 替换节点（ReplaceNode）
3. 修改节点参数（ModifyParam）
4. 重连边（RewireEdge）

⚠️ Transformer：
- 不得破坏 GraphIR 的合法性
- 不得生成非法端口连接
- 不得绕过 GraphIRValidator

---

### 3️⃣ GraphTransformUtils（图操作工具）

实现一组 **低层、可复用的图操作工具**：

- CloneGraph(GraphIR)
- InsertNodeBefore(nodeId)
- InsertNodeAfter(nodeId)
- ReplaceNode(oldNodeId, newNode)
- RewireEdge(from, to)
- FindNodesByType(NodeType)
- FindNodesByTag(Tag)

所有 Transformer 必须通过这些工具操作图，禁止直接手改 List。

---

### 4️⃣ BuildContext（构筑上下文）

构筑期上下文信息。

#### 至少包含
- skillId
- skillTags
- List<SupportDefinition> supports
- （预留）装备词缀 / 天赋信息

BuildContext 只在构筑期使用，不进入 Runtime。

---

### 5️⃣ BuildAssembler（构筑总控）

BuildAssembler 负责：

1. 输入：
    - 原始 GraphIR（主技能）
    - BuildContext（Support 列表）
2. 按 priority 排序 Support
3. 逐个 Support：
    - 判断 CanApply
    - 执行其 transformers
4. 每一步变换后：
    - 调用 GraphIRValidator
5. 输出：
    - 最终 GraphIR

---

## 三、必须实现的示例 Support（至少 2 个）

### 示例 1：多重投射物（Multiple Projectiles）

#### 行为
- 条件：GraphIR 含 Projectile Tag
- 变换：
    - 在 SpawnProjectile / EmitApplyDamage 前
    - 插入 Fork / Split 节点
- 数值副作用：
    - 降低单次伤害（例如 *0.7）

### 示例 2：元素转化（Fire → Cold）

#### 行为
- 查找 MakeDamageSpec 节点
- 修改 DamageType 参数
- （可选）插入 ConvertDamage 节点

---

## 四、构筑流程（必须遵守）

1. 从 EffectGraphAsset 导出基础 GraphIR
2. 构建 BuildContext（技能 + Support）
3. BuildAssembler.Apply(baseGraph, context)
4. 得到 FinalGraphIR
5. FinalGraphIR → ExecPlan 编译
6. 缓存：
    - graphHash
    - planHash

---

## 五、必须提供的测试 / 验证

### 测试 1：无 Support
- 输入：基础 GraphIR
- 输出：GraphIR 不变（hash 相同）

### 测试 2：单 Support
- 输入：火球 + 多重投射物
- 输出：
    - GraphIR 节点数增加
    - ExecPlan 不同于原始版本

### 测试 3：多个 Support 顺序
- 两个 Support（priority 不同）
- 验证：
    - 应用顺序正确
    - 最终 GraphIR 符合预期

---

## 六、交付要求（DELIVERABLES）

1. 新增 / 修改的文件路径列表
2. SupportDefinition 与 IGraphTransformer 接口说明
3. GraphTransformUtils 的 API 列表
4. 一个完整示例：
    - 基础技能 Graph
    - Support 列表
    - 变换前 / 后 GraphIR 差异说明
5. 说明：
    - 为什么构筑必须发生在 Build 阶段
    - 为什么 Runtime 不感知 Support

---

## 七、FAIL 条件（任一即失败）

- Support 在 Runtime 执行
- ExecPlanRunner 中出现构筑逻辑
- Transformer 绕过 GraphIRValidator
- 通过 if/else 修改技能行为
- 构筑结果不可缓存 / 不可复现
