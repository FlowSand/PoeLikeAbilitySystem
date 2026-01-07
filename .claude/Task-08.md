# Task-08 Agent Prompt
## 命中与伤害域模型（Hit / Damage / Resist / Crit / Ailment 基础）

---

## 一、目标（Objective）

建立统一的**战斗域模型**，规范一次“命中（Hit）”从生成到结算的**全链路对象模型与结算管线**。  
后续**所有技能节点与 Support** 必须**仅通过该域模型交互**，禁止在节点内散落任何伤害/暴击/抗性等计算逻辑。

---

## 二、产出物（Deliverables）

### 1. HitInstance
一次命中实例，包含：
- 来源（Source）
- 目标（Target）
- 技能实例（SkillInstance）
- 命中时间（HitTime）
- 随机种子 / 随机源（Rng）
- 标志位（HitFlags）

---

### 2. DamagePacket
可多分量的伤害包，支持以下分量：
- Physical
- Fire
- Cold
- Lightning
- Chaos

要求：
- 使用 `float` 表示
- 支持 Added / Increased / More 结算后的结果表达

---

### 3. DefenseSnapshot
目标防御快照，最小实现包含：
- 元素抗性（Resist）
- 预留 Armor / Evasion（可先占位）

---

### 4. DamageResult
伤害结算输出结果，包含：
- 各分量最终伤害值
- 是否暴击
- 是否命中（占位）
- 是否格挡（占位）
- 异常状态触发列表（Ailment，占位）

---

### 5. DamagePipeline
统一的伤害结算管线（可插拔 Step 链，列表或数组结构）。

最少包含以下步骤：
1. 命中判定（可先恒 true）
2. 暴击判定（RollCrit）
3. 抗性减免（ApplyResist）

---

### 6. Trace
- 每个 Pipeline Step 必须输出 `TracePoint`
- Trace 内容包含：
    - Step 名称
    - 输入摘要
    - 输出摘要

---

## 三、约束（Constraints）

- 技能节点 **不得直接修改 HP**
- 标准流程必须为：  
  `HitInstance → DamagePipeline.Resolve() → DamageResult → HealthComponent.Apply()`
- 所有随机数 **必须来源于 `HitInstance.Rng`**（支持 deterministic seed）
- 结算顺序必须固定，并且可单元测试

---

## 四、任务清单（Task Checklist）

1. 建立 `Combat/Model` 命名空间与目录结构
2. 定义 `DamageType` 枚举
3. 实现 `DamagePacket`（支持 5 种伤害分量）
4. 定义 `HitFlags`（至少包含以下 4 项）：
    - IsSpell
    - IsAttack
    - IsProjectile
    - IsAoE
5. 实现 `DamagePipeline` 抽象与以下 3 个 Step：
    - `RollCritStep`
    - `ApplyResistStep`
    - `FinalizeStep`
6. 扩展 `ApplyHitNode`：
    - 不再直接结算伤害
    - 改为构造 `HitInstance` 并调用 `DamagePipeline`
7. 编写单元测试（至少 6 条）：
    - 暴击倍率验证
    - 抗性 75% 减伤
    - 抗性为负数
    - 零伤害输入
    - 多分量伤害累加
    - 确定性随机（相同 seed 结果一致）

---

## 五、验收标准（Acceptance Criteria）

- 同一随机 seed 下，多次运行得到完全一致的 `DamageResult`
- Trace 数据可完整回放每个 Pipeline Step 的输入与输出（无需 UI）
- Demo 场景中，一次施法可直观看到：
    - 多分量伤害
    - 抗性减免效果

---

## 六、提交要求（Submission Requirements）

- 新增或修改的代码需包含 XML 注释，或在 README 中有说明
- 不引入任何第三方依赖库
