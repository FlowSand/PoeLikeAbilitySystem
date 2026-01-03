# Task-05 集成测试指南

## 前置检查

1. **打开 Unity 编辑器**
2. **等待编译完成**（检查 Console 是否有编译错误）
3. **验证程序集引用正确**

### 预期编译结果
- ✅ Combat.Runtime 编译成功（无 GraphProcessor 依赖）
- ✅ Combat.Editor 编译成功（引用 Combat.Runtime + GraphProcessor）
- ✅ 无循环引用警告

---

## 测试场景 1：创建 EffectGraphAsset

### 步骤
1. 在 Project 窗口右键 `Assets/GraphAssets/Skills/`
2. 选择 `Create → Combat → Effect Graph`
3. 命名为 `TestFireball`

### 预期结果
- ✅ 创建 `TestFireball.asset` 文件
- ✅ 双击打开 NGP 图编辑器窗口
- ✅ Inspector 显示 `graphVersion = 1`, `entryEventType = "OnHit"`

---

## 测试场景 2：构建技能图

### 步骤：构建最小火球技能链

在 NGP 编辑器中，按以下顺序创建节点并连线：

```
OnHitEntry
  ├─ [Target] → GetTarget → [Target]
  │                            ↓
ConstNumber(10.0)               |
  ↓ [Value]                     |
  └──→ MakeDamageSpec ←─────────┘
           ↓ [DamageSpec]
       EmitApplyDamageCommand
```

### 详细步骤
1. **创建节点**（右键 → Add Node）
   - `Effect/Entry/OnHit` → OnHit Entry
   - `Effect/Number/Const` → Const Number（设置 value = 10）
   - `Effect/Entity/GetTarget` → Get Target
   - `Effect/Damage/MakeDamageSpec` → Make Damage Spec（设置 damageType = Fire）
   - `Effect/Command/EmitApplyDamage` → Emit Apply Damage

2. **连线**
   - OnHitEntry.Target → GetTarget.Target
   - GetTarget.Target → MakeDamageSpec.Caster（注意：这是隐式的，由 ExecutionContext 提供）
   - ConstNumber.Value → MakeDamageSpec.BaseDamage
   - MakeDamageSpec.DamageSpec → EmitApplyDamageCommand.DamageSpec

3. **保存图**（Ctrl+S）

### 预期结果
- ✅ 所有节点可以创建
- ✅ 端口类型颜色区分（Number/EntityId/DamageSpec）
- ✅ 连线类型安全（无法连接不兼容类型）
- ✅ Inspector 可编辑节点参数（value, damageType）

---

## 测试场景 3：Bake ExecPlanAsset

### 步骤
1. 在 Project 窗口选中 `TestFireball.asset`
2. 右键 → `Combat → Bake Effect Graph`
3. 观察 Console 输出

### 预期 Console 输出
```
[Bake] Starting bake for 'TestFireball'...
[Bake] GraphIR validation passed (nodes: 5, edges: 3)
[Bake] Compilation succeeded (hash: XXXXXXXXXXXXXXXX, ops: 3)
[Bake] Created ExecPlan asset at 'Assets/Generated/ExecPlans/TestFireball_ExecPlan.asset' (hash: XXXXXXXXXXXXXXXX)
```

### 预期结果
- ✅ 无错误日志
- ✅ 生成 `Assets/Generated/ExecPlans/TestFireball_ExecPlan.asset`
- ✅ 自动选中并 Ping 生成的资产
- ✅ ExecPlanAsset Inspector 显示：
  - Source Graph Id: `<GUID>`
  - Graph Version: `1`
  - Plan Hash: `<hash>`

---

## 测试场景 4：验证增量构建

### 步骤
1. 选中 `TestFireball.asset`
2. 再次右键 → `Combat → Bake Effect Graph`
3. 观察 Console 输出

### 预期 Console 输出
```
[Bake] Starting bake for 'TestFireball'...
[Bake] GraphIR validation passed (nodes: 5, edges: 3)
[Bake] Compilation succeeded (hash: XXXXXXXXXXXXXXXX, ops: 3)
[Bake] ExecPlan unchanged (hash: XXXXXXXXXXXXXXXX), skipping asset creation
```

### 预期结果
- ✅ Console 显示 "ExecPlan unchanged"
- ✅ 不创建重复文件
- ✅ Hash 值与第一次相同

---

## 测试场景 5：修改图并重新 Bake

### 步骤
1. 打开 `TestFireball.asset` 图编辑器
2. 修改 `ConstNumber.value = 20`
3. 保存图（Ctrl+S）
4. 右键 → `Combat → Bake Effect Graph`
5. 观察 Console 输出

### 预期 Console 输出
```
[Bake] Starting bake for 'TestFireball'...
[Bake] GraphIR validation passed (nodes: 5, edges: 3)
[Bake] Compilation succeeded (hash: YYYYYYYYYYYYYYYY, ops: 3)
[Bake] Updated ExecPlan asset at 'Assets/Generated/ExecPlans/TestFireball_ExecPlan.asset' (hash: YYYYYYYYYYYYYYYY)
```

### 预期结果
- ✅ Hash 值改变（与第一次不同）
- ✅ Console 显示 "Updated ExecPlan asset"
- ✅ ExecPlanAsset 内容已更新

---

## 测试场景 6：GraphIR 校验失败

### 步骤（故意制造错误）
1. 创建新图 `TestInvalid.asset`
2. 只添加一个 `ConstNumber` 节点（无 Entry 节点）
3. 尝试 Bake

### 预期 Console 输出
```
[Bake] Starting bake for 'TestInvalid'...
[Bake] Export failed: No entry node found for event type 'OnHit'. Please add an appropriate entry node to the graph.
```

### 预期结果
- ✅ Bake 被阻止
- ✅ 清晰的错误信息
- ✅ 不生成 ExecPlanAsset

---

## 常见问题排查

### 编译错误：找不到 GraphProcessor 命名空间
**原因**：Combat.Editor.asmdef 未正确引用 GraphProcessor.dll
**解决**：检查 `precompiledReferences` 是否包含 `"GraphProcessor.dll"`

### 编译错误：找不到 Combat.Runtime 类型
**原因**：Combat.Editor.asmdef 的 GUID 引用错误
**解决**：确认 references 中的 GUID 为 `88b404c862cf46bba38ec004acbad6b8`

### 菜单项不显示
**原因**：asmdef 未设置 `includePlatforms: ["Editor"]`
**解决**：检查 Combat.Editor.asmdef 配置

### NGP 窗口打不开
**原因**：NodeGraphProcessor 包未正确安装
**解决**：检查 `Packages/manifest.json` 是否包含 NGP 引用

---

## 成功标准

完成以上所有测试场景后，确认：

- ✅ 能在编辑器中创建/编辑 EffectGraphAsset
- ✅ 能 Bake 生成 ExecPlanAsset
- ✅ 生成的 ExecPlanAsset 包含正确的 planHash
- ✅ GraphIR 校验能捕获错误
- ✅ 增量构建正确工作（hash 检查）
- ✅ Combat.Runtime 无 Editor 依赖（运行时隔离）

---

## 下一步（Phase 8 后续）

测试通过后，可选：

1. **运行时集成测试**（在 Play Mode 中执行 ExecPlanAsset）
2. **创建 Extended 节点集**（Add, Mul, GetCaster, Branch 等）
3. **添加 Editor Tests**（自动化 Export/Bake 测试）
4. **实现错误高亮**（在 Graph View 中显示校验错误）

---

**测试完成后请报告结果！**
