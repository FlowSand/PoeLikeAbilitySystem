你正在 Unity 工程中实现 PoE-like 技能系统的编辑器作者态。

【前置条件（已完成）】
- Combat.Runtime：BattleModel / EventBus / CommandBuffer
- GraphIR：中间表示 + 校验器
- ExecPlan：编译器（GraphIR → ExecPlan）
- ExecPlanRunner：运行时执行（EventQueue + Budget + Depth）

【本任务目标】
接入 NodeGraphProcessor（NGP）作为技能图编辑器，仅用于 Editor。
实现：
1) EffectGraphAsset（NGP 图资产）
2) 一组基础节点（MVP 节点集）
3) 导出器：NGP Graph → GraphIR
4) Bake 工具：GraphIR → ExecPlanAsset（生成到 Assets/Generated/ExecPlans/）
5) 基本的错误提示（校验失败能定位到 Node）

【强制架构约束（ABSOLUTE）】
- NGP 相关代码必须放在 Assets/Scripts/Combat.Editor/ 下
- 必须创建 asmdef：Combat.Editor（Editor only）
- Combat.Runtime 不得引用 GraphProcessor/UnityEditor
- Runtime 永远不执行 NGP 图
- NGP 只是作者态，导出 GraphIR 后由既有编译器编译为 ExecPlan

【工程目录规范（必须遵守）】
- Assets/Scripts/Combat.Editor/GraphAuthoring/
- Assets/Scripts/Combat.Editor/GraphBuild/
- Assets/GraphAssets/Skills/               (存放 NGP 图资产)
- Assets/Generated/ExecPlans/              (存放生成的 ExecPlanAsset)

---

## 一、接入 NGP（集成要求）

1) 通过 UPM 或 Packages 导入 NodeGraphProcessor
- 若已有引入则跳过
- 若需新增包，提供清晰说明与版本

2) asmdef 依赖
- Combat.Editor 允许引用 GraphProcessor
- Combat.Runtime 必须不引用

---

## 二、实现作者态图资产：EffectGraphAsset

实现一个图资产类：
- EffectGraphAsset : BaseGraph

必须包含字段：
- string graphId（稳定唯一，可用 GUID）
- int graphVersion（用于迁移）
- EventEntryType entryType（OnCast/OnHit 最小集）

图资产必须能作为 ScriptableObject 存储到：
Assets/GraphAssets/Skills/

---

## 三、实现基础 Node（MVP）

实现以下 Node（BaseNode 派生），要求：
- 端口类型明确（Number/Bool/EntityId/EntityList/DamageSpec）
- Node 内参数可编辑（例如 ConstNumber 的 value）
- Node 不实现运行时逻辑，只用于导出编译信息

节点清单（与 OpCode 对齐）：
1) OnCastEntryNode（输出：CasterId）
2) OnHitEntryNode（输出：CasterId, TargetId）
3) ConstNumberNode（输出：Number）
4) GetStatNode（输入：EntityId，参数：StatType，输出：Number）
5) AddNode（输入：Number, Number，输出：Number）
6) MulNode（输入：Number, Number，输出：Number）
7) MakeDamageSpecNode（输入：CasterId, TargetId, Amount:Number，参数：DamageType，输出：DamageSpec）
8) EmitApplyDamageCommandNode（输入：DamageSpec）

注意：
- 端口命名必须稳定，作为导出依据（例如 "A","B","Out"）
- 端口类型必须与 Combat.Runtime.GraphIR.PortType 一致

---

## 四、导出器：NGP Graph → GraphIR

实现一个 Editor 导出器：
- EffectGraphExporter.Export(EffectGraphAsset graph) → GraphIR

导出内容必须包含：
- graphId / version / entryNodeId
- IRNode 列表（nodeId、nodeType、ports、params）
- IREdge 列表（连线）

### 节点类型映射（必须实现）
NGP Node 类型 → GraphIR.NodeType(enum)
必须给出一个集中注册表，例如：
- NodeTypeRegistry.GetNodeType(BaseNode node)

### 静态校验
导出后必须调用 GraphIRValidator.Validate(graphIR)
- 校验失败：
    - 返回清晰错误信息
    - 尽可能定位到具体 nodeId（可选：在 Editor 里 ping/highlight）

---

## 五、Bake：GraphIR → ExecPlanAsset 生成到指定目录

实现一个 Editor Bake 工具（菜单项或 Inspector 按钮）：
- Bake 当前选中的 EffectGraphAsset
- 输出：
    - GraphIR JSON（可选，便于 diff）
    - ExecPlanAsset（ScriptableObject）
- 输出路径：
    - Assets/Generated/ExecPlans/{graphId}_{hash}.asset

ExecPlanAsset 内容必须包含：
- planHash
- operations
- slotLayout
- graphId / graphVersion（溯源）

要求：
- 若 hash 不变，重复 bake 不应产生重复文件（增量构建）
- 若校验失败，阻止产物生成

---

## 六、必须提供的测试与验证（至少满足其一）

任选其一即可（推荐 1）：

1) Editor 验证流程（必须提供操作说明）
- 创建一个 EffectGraphAsset
- 拉一条最小链：OnHitEntry -> GetStat -> Mul -> MakeDamageSpec -> EmitApplyDamage
- 点击 Bake
- 在 Generated 目录生成 ExecPlanAsset
- Console 输出：planHash、opCount、commandCount(可估)

2) Editor 测试（如能写 EditorTest 更好）
- 构造图资产并连线
- Export → Validate → Compile → 输出产物

---

## 七、交付要求（DELIVERABLES）

1) 新增/修改文件路径列表
2) asmdef 设置说明
3) NGP Node 与 GraphIR NodeType/PortType 的映射表
4) 一个示例图资产（可选，但强烈建议提供）
5) Bake 操作步骤（确保任何人照着能生成 ExecPlanAsset）

【FAIL 条件】
- Combat.Runtime 引用了 GraphProcessor 或 UnityEditor
- 运行时直接执行 NGP 图
- 导出无法稳定映射端口类型
- Bake 生成的 ExecPlanAsset 无法溯源 graphId/version/hash
